using System.Security.Cryptography;
using System.Text;
using Bannister.Models;

namespace Bannister.Services;

public class OpeningClipImageSessionService
{
    private const string StorageFolder = "OpeningClipSessions";
    private readonly DatabaseService _db;
    private bool _initialized;

    public OpeningClipImageSessionService(DatabaseService db) => _db = db;

    private async Task InitAsync()
    {
        if (_initialized) return;
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<OpeningClipImageSessionItem>();
        _initialized = true;
    }

    public async Task<List<OpeningClipImageSessionItem>> GetItemsAsync(string username)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<OpeningClipImageSessionItem>()
            .Where(item => item.Username == username)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .ToListAsync();
    }

    public async Task<OpeningClipImageSessionItem> AddImageAsync(
        string username, FileResult file)
    {
        await InitAsync();
        var items = await GetItemsAsync(username);
        string extension = Path.GetExtension(file.FileName);
        string relativePath = Path.Combine(
            StorageFolder, $"{Guid.NewGuid():N}{extension}")
            .Replace(Path.DirectorySeparatorChar, '/');
        string fullPath = ResolvePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using (var source = await file.OpenReadAsync())
        await using (var destination = File.Create(fullPath))
            await source.CopyToAsync(destination);

        var conn = await _db.GetConnectionAsync();
        foreach (var existing in items.Where(item => item.IsActive))
        {
            existing.IsActive = false;
            await conn.UpdateAsync(existing);
        }

        var item = new OpeningClipImageSessionItem
        {
            Username = username,
            ImagePath = relativePath,
            SortOrder = items.Count == 0 ? 0 : items.Max(x => x.SortOrder) + 1,
            IsActive = true
        };
        await conn.InsertAsync(item);
        return item;
    }

    public async Task SetActiveAsync(string username, int itemId)
    {
        await InitAsync();
        var items = await GetItemsAsync(username);
        var conn = await _db.GetConnectionAsync();
        foreach (var item in items)
        {
            bool active = item.Id == itemId;
            if (item.IsActive == active) continue;
            item.IsActive = active;
            await conn.UpdateAsync(item);
        }
    }

    public async Task<bool> RegisterPromptCopyAsync(
        OpeningClipImageSessionItem item, string prompt)
    {
        await InitAsync();
        string key = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(prompt)));
        var keys = item.CopiedPromptKeys.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!keys.Add(key)) return false;

        item.CopiedPromptKeys = string.Join(',', keys);
        item.CopyCount = keys.Count;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(item);
        return true;
    }

    public async Task ResetAsync(string username)
    {
        await InitAsync();
        var items = await GetItemsAsync(username);
        string storageRoot = Path.GetFullPath(Path.Combine(
            FileSystem.AppDataDirectory, StorageFolder));
        foreach (var item in items)
        {
            string path = ResolvePath(item.ImagePath);
            string fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase)
                && File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        var conn = await _db.GetConnectionAsync();
        foreach (var item in items)
            await conn.DeleteAsync(item);
    }

    public static string ResolvePath(string relativePath) =>
        Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(FileSystem.AppDataDirectory,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
}
