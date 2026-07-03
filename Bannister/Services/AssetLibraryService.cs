using Bannister.Models;

namespace Bannister.Services;

public class AssetLibraryService
{
    private readonly DatabaseService _db;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".webm", ".avi", ".mkv"
    };

    public AssetLibraryService(DatabaseService db)
    {
        _db = db;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private async Task EnsureTableAsync()
    {
        if (!_db.IsReadOnly)
        {
            await _db.EnsureTableAsync<AssetLibraryItem>();
            var conn = await _db.GetConnectionAsync();
            try { await conn.ExecuteAsync("ALTER TABLE asset_library_items ADD COLUMN Categories TEXT NOT NULL DEFAULT ''"); }
            catch { }
        }
    }

    public static List<string> ParseCategories(AssetLibraryItem item)
    {
        var raw = string.IsNullOrWhiteSpace(item.Categories) ? item.Category : item.Categories;
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string SerializeCategories(IEnumerable<string> categories)
    {
        var clean = categories
            .Select(c => c?.Trim() ?? "")
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return string.Join(",", clean);
    }

    public async Task<List<AssetLibraryItem>> GetAllAsync(string username)
    {
        await EnsureTableAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<AssetLibraryItem>()
            .Where(i => i.Username == username)
            .OrderBy(i => i.FileName)
            .ToListAsync();
    }

    public async Task<List<AssetLibraryItem>> GetByCategoryAsync(string username, string category)
    {
        var items = await GetAllAsync(username);
        return items
            .Where(i => ParseCategories(i).Contains(category, StringComparer.OrdinalIgnoreCase))
            .OrderBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<string>> GetDistinctCategoriesAsync(string username)
    {
        var items = await GetAllAsync(username);
        return items
            .SelectMany(ParseCategories)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<AssetLibraryItem?> GetByIdAsync(int id)
    {
        await EnsureTableAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<AssetLibraryItem>()
            .Where(i => i.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> SetCategoryAsync(int id, string category)
    {
        await EnsureTableAsync();
        if (_db.IsReadOnly) return false;

        var conn = await _db.GetConnectionAsync();
        var item = await GetByIdAsync(id);
        if (item == null) return false;

        item.Categories = SerializeCategories(new[] { category?.Trim() ?? "" });
        await conn.UpdateAsync(item);
        return true;
    }

    public async Task<bool> SetCategoriesAsync(int id, IEnumerable<string> categories)
    {
        await EnsureTableAsync();
        if (_db.IsReadOnly) return false;

        var conn = await _db.GetConnectionAsync();
        var item = await GetByIdAsync(id);
        if (item == null) return false;

        item.Categories = SerializeCategories(categories);
        await conn.UpdateAsync(item);
        return true;
    }

    public async Task<int> SetCategoryBulkAsync(IEnumerable<int> ids, string category)
    {
        await EnsureTableAsync();
        if (_db.IsReadOnly) return 0;

        var conn = await _db.GetConnectionAsync();
        int count = 0;
        var trimmed = category?.Trim() ?? "";
        foreach (var id in ids.Distinct())
        {
            var item = await conn.Table<AssetLibraryItem>()
                .Where(i => i.Id == id)
                .FirstOrDefaultAsync();
            if (item == null) continue;

            item.Categories = SerializeCategories(new[] { trimmed });
            await conn.UpdateAsync(item);
            count++;
        }

        return count;
    }

    public async Task<int> AddCategoryBulkAsync(IEnumerable<int> ids, string category)
    {
        await EnsureTableAsync();
        if (_db.IsReadOnly) return 0;

        var trimmed = category?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(trimmed))
            return 0;

        var conn = await _db.GetConnectionAsync();
        int count = 0;
        foreach (var id in ids.Distinct())
        {
            var item = await conn.Table<AssetLibraryItem>()
                .Where(i => i.Id == id)
                .FirstOrDefaultAsync();
            if (item == null) continue;

            var categories = ParseCategories(item);
            categories.Add(trimmed);
            item.Categories = SerializeCategories(categories);
            await conn.UpdateAsync(item);
            count++;
        }

        return count;
    }

    public async Task<bool> SetDescriptiveNameAsync(int id, string descriptiveName)
    {
        await EnsureTableAsync();
        if (_db.IsReadOnly) return false;

        var conn = await _db.GetConnectionAsync();
        var item = await GetByIdAsync(id);
        if (item == null) return false;

        item.DescriptiveName = descriptiveName?.Trim() ?? "";
        await conn.UpdateAsync(item);
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await EnsureTableAsync();
        if (_db.IsReadOnly) return false;

        var conn = await _db.GetConnectionAsync();
        var item = await GetByIdAsync(id);
        if (item == null) return false;

        await conn.DeleteAsync(item);
        return true;
    }

    public async Task<AssetLibraryScanResult> RescanAsync(
        string username,
        string rootPath,
        IProgress<AssetLibraryScanProgress>? progress = null)
    {
        await EnsureTableAsync();
        if (_db.IsReadOnly)
            return new AssetLibraryScanResult(0, 0, 0, 0);

        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            return new AssetLibraryScanResult(0, 0, 0, 0);

        return await Task.Run(async () =>
        {
            var conn = await _db.GetConnectionAsync();
            var existing = await conn.Table<AssetLibraryItem>()
                .Where(i => i.Username == username)
                .ToListAsync();
            var existingByPath = existing
                .GroupBy(i => i.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            int filesScanned = 0;
            int newlyIndexed = 0;
            int alreadyIndexed = 0;
            int markedMissing = 0;

            foreach (var path in EnumerateFilesSafe(rootPath))
            {
                filesScanned++;
                var extension = Path.GetExtension(path);
                string fileType;
                if (ImageExtensions.Contains(extension))
                    fileType = "image";
                else if (VideoExtensions.Contains(extension))
                    fileType = "video";
                else
                    continue;

                if (existingByPath.Remove(path, out _))
                {
                    alreadyIndexed++;
                }
                else
                {
                    long sizeBytes = 0;
                    try { sizeBytes = new FileInfo(path).Length; } catch { }

                    await conn.InsertAsync(new AssetLibraryItem
                    {
                        Username = username,
                        FilePath = path,
                        FileName = Path.GetFileName(path),
                        FileType = fileType,
                        FileSizeBytes = sizeBytes,
                        IndexedAt = DateTime.UtcNow
                    });
                    newlyIndexed++;
                }

                if (filesScanned % 100 == 0)
                {
                    progress?.Report(new AssetLibraryScanProgress(
                        filesScanned,
                        newlyIndexed + alreadyIndexed,
                        Path.GetDirectoryName(path) ?? rootPath));
                }
            }

            foreach (var item in existingByPath.Values)
            {
                if (item.MissingSince == null)
                {
                    item.MissingSince = DateTime.UtcNow;
                    await conn.UpdateAsync(item);
                    markedMissing++;
                }
            }

            progress?.Report(new AssetLibraryScanProgress(
                filesScanned,
                newlyIndexed + alreadyIndexed,
                rootPath));

            int totalAfterScan = await conn.Table<AssetLibraryItem>()
                .Where(i => i.Username == username)
                .CountAsync();
            return new AssetLibraryScanResult(newlyIndexed, alreadyIndexed, markedMissing, totalAfterScan);
        });
    }

    private static IEnumerable<string> EnumerateFilesSafe(string rootPath)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(current); }
            catch { files = Array.Empty<string>(); }

            foreach (var file in files)
                yield return file;

            IEnumerable<string> directories;
            try { directories = Directory.EnumerateDirectories(current); }
            catch { directories = Array.Empty<string>(); }

            foreach (var directory in directories)
                pending.Push(directory);
        }
    }
}

public record AssetLibraryScanProgress(int FilesScanned, int FilesIndexed, string CurrentDirectory);

public record AssetLibraryScanResult(int NewlyIndexed, int AlreadyIndexed, int MarkedMissing, int TotalAfterScan);
