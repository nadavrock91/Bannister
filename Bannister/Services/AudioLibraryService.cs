using Bannister.Models;

namespace Bannister.Services;

/// <summary>
/// Service for managing audio library items (quotes, anecdotes, lessons).
/// Handles CRUD and audio file management.
/// </summary>
public class AudioLibraryService
{
    private readonly DatabaseService _db;
    private bool _initialized = false;

    public AudioLibraryService(DatabaseService db)
    {
        _db = db;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<AudioItem>();
        _initialized = true;
    }

    /// <summary>
    /// Audio files folder
    /// </summary>
    public static string AudioFolder
    {
        get
        {
            var folder = Path.Combine(FileSystem.AppDataDirectory, "AudioLibrary");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    #region CRUD

    public async Task<List<AudioItem>> GetAllAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<AudioItem>()
            .Where(w => w.Username == username)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<AudioItem>> GetByCategoryAsync(string username, string category)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<AudioItem>()
            .Where(w => w.Username == username && w.Category == category)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<string>> GetCategoriesAsync(string username)
    {
        var items = await GetAllAsync(username);
        return items
            .Select(w => w.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }

    public async Task<AudioItem> CreateAsync(string username, string text, string category,
        string source = "", string notes = "")
    {
        await EnsureInitializedAsync();
        var item = new AudioItem
        {
            Username = username,
            Text = text.Trim(),
            Category = category.Trim(),
            Source = source.Trim(),
            Notes = notes.Trim(),
            CreatedAt = DateTime.Now
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(item);
        return item;
    }

    public async Task UpdateAsync(AudioItem item)
    {
        await EnsureInitializedAsync();
        item.ModifiedAt = DateTime.Now;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(item);
    }

    public async Task DeleteAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var item = await conn.GetAsync<AudioItem>(id);

        // Delete audio file if exists
        if (item != null && !string.IsNullOrEmpty(item.AudioPath))
        {
            var fullPath = Path.Combine(AudioFolder, item.AudioPath);
            if (File.Exists(fullPath))
            {
                try { File.Delete(fullPath); } catch { }
            }
        }

        await conn.DeleteAsync<AudioItem>(id);
    }

    public async Task ToggleFavoriteAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var item = await conn.GetAsync<AudioItem>(id);
        if (item != null)
        {
            item.IsFavorite = !item.IsFavorite;
            item.ModifiedAt = DateTime.Now;
            await conn.UpdateAsync(item);
        }
    }

    #endregion

    #region Audio Management

    /// <summary>
    /// Generate a filename from the first several words of the text.
    /// If file exists, adds more words up to 10, then increments suffix.
    /// </summary>
    private string GenerateAudioFileName(string text, string ext)
    {
        // Clean text: remove non-alphanumeric, split into words
        var cleanText = new string(text.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());
        var words = cleanText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
            return $"audio_{DateTime.Now.Ticks}{ext}";

        // Try progressively more words (3 to 10)
        for (int wordCount = 3; wordCount <= Math.Min(10, words.Length); wordCount++)
        {
            string candidate = string.Join("_", words.Take(wordCount)).ToLowerInvariant();
            if (candidate.Length > 80) candidate = candidate.Substring(0, 80);
            string filename = $"{candidate}{ext}";
            string fullPath = Path.Combine(AudioFolder, filename);

            if (!File.Exists(fullPath))
                return filename;
        }

        // All word combinations taken — add incrementing suffix
        string baseName = string.Join("_", words.Take(Math.Min(5, words.Length))).ToLowerInvariant();
        if (baseName.Length > 60) baseName = baseName.Substring(0, 60);

        int suffix = 2;
        while (true)
        {
            string filename = $"{baseName}_{suffix}{ext}";
            string fullPath = Path.Combine(AudioFolder, filename);
            if (!File.Exists(fullPath))
                return filename;
            suffix++;
        }
    }

    /// <summary>
    /// Attach an audio file by copying it to the AudioLibrary folder.
    /// Filename is based on the item's text content.
    /// </summary>
    public async Task<string> AttachAudioFileAsync(AudioItem item, string sourceFilePath)
    {
        string ext = Path.GetExtension(sourceFilePath);
        string filename = GenerateAudioFileName(item.Text, ext);
        string destPath = Path.Combine(AudioFolder, filename);

        // Delete old audio if exists
        if (!string.IsNullOrEmpty(item.AudioPath))
        {
            var oldPath = Path.Combine(AudioFolder, item.AudioPath);
            if (File.Exists(oldPath))
            {
                try { File.Delete(oldPath); } catch { }
            }
        }

        // Copy new file
        File.Copy(sourceFilePath, destPath, overwrite: true);

        item.AudioPath = filename;
        item.AudioSource = "browsed";
        item.ModifiedAt = DateTime.Now;
        await UpdateAsync(item);

        return filename;
    }

    /// <summary>
    /// Attach audio from a stream (e.g., from FilePicker).
    /// </summary>
    public async Task<string> AttachAudioFromStreamAsync(AudioItem item, Stream stream, string originalFileName)
    {
        string ext = Path.GetExtension(originalFileName);
        string filename = GenerateAudioFileName(item.Text, ext);
        string destPath = Path.Combine(AudioFolder, filename);

        // Delete old audio if exists
        if (!string.IsNullOrEmpty(item.AudioPath))
        {
            var oldPath = Path.Combine(AudioFolder, item.AudioPath);
            if (File.Exists(oldPath))
            {
                try { File.Delete(oldPath); } catch { }
            }
        }

        using (var destStream = File.Create(destPath))
        {
            await stream.CopyToAsync(destStream);
        }

        item.AudioPath = filename;
        item.AudioSource = "browsed";
        item.ModifiedAt = DateTime.Now;
        await UpdateAsync(item);

        return filename;
    }

    /// <summary>
    /// Mark audio as generated from text (the actual generation is done externally,
    /// this just saves the path).
    /// </summary>
    public async Task MarkAudioAsGeneratedAsync(AudioItem item, string audioFilePath)
    {
        string ext = Path.GetExtension(audioFilePath);
        string filename = GenerateAudioFileName(item.Text, ext);
        string destPath = Path.Combine(AudioFolder, filename);

        // Delete old audio if exists
        if (!string.IsNullOrEmpty(item.AudioPath))
        {
            var oldPath = Path.Combine(AudioFolder, item.AudioPath);
            if (File.Exists(oldPath))
            {
                try { File.Delete(oldPath); } catch { }
            }
        }

        File.Copy(audioFilePath, destPath, overwrite: true);

        item.AudioPath = filename;
        item.AudioSource = "generated";
        item.ModifiedAt = DateTime.Now;
        await UpdateAsync(item);
    }

    /// <summary>
    /// Remove audio from an item.
    /// </summary>
    public async Task RemoveAudioAsync(AudioItem item)
    {
        if (!string.IsNullOrEmpty(item.AudioPath))
        {
            var fullPath = Path.Combine(AudioFolder, item.AudioPath);
            if (File.Exists(fullPath))
            {
                try { File.Delete(fullPath); } catch { }
            }
        }

        item.AudioPath = "";
        item.AudioSource = "";
        item.AudioDurationSeconds = 0;
        item.ModifiedAt = DateTime.Now;
        await UpdateAsync(item);
    }

    /// <summary>
    /// Get the full path to an item's audio file.
    /// </summary>
    public string? GetAudioFullPath(AudioItem item)
    {
        if (string.IsNullOrEmpty(item.AudioPath)) return null;
        var path = Path.Combine(AudioFolder, item.AudioPath);
        return File.Exists(path) ? path : null;
    }

    #endregion

    #region Stats

    public async Task<(int total, int withAudio, int favorites, int categories)> GetStatsAsync(string username)
    {
        var items = await GetAllAsync(username);
        return (
            items.Count,
            items.Count(i => i.HasAudio),
            items.Count(i => i.IsFavorite),
            items.Select(i => i.Category).Distinct().Count()
        );
    }

    #endregion
}
