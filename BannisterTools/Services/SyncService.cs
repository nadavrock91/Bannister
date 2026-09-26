using Bannister.Services;

namespace Bannister.Services;

/// <summary>
/// Minimal database downloader used by BannisterTools on Android.
/// The Windows tool reads the shared local database directly.
/// </summary>
public class SyncService
{
    private readonly DatabaseService _db;
    private readonly DeviceModeService _deviceMode;
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    public SyncService(DatabaseService db, DeviceModeService deviceMode)
    {
        _db = db;
        _deviceMode = deviceMode;
    }

    public sealed class SyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }

    public async Task<SyncResult> DownloadAsync()
    {
        var (username, passwordHash) =
            await _deviceMode.GetSyncCredentialsAsync();
        var baseUrl = _deviceMode.ServerUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(passwordHash))
            return Fail("Sync server credentials are not configured.");

        var url = baseUrl.TrimEnd('/');
        using var request = new HttpRequestMessage(
            HttpMethod.Get, url);
        request.Headers.Add("X-Auth", $"{username}:{passwordHash}");

        try
        {
            using var response = await Http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                return Fail($"Download failed ({(int)response.StatusCode}).");

            string tempPath = Path.Combine(
                FileSystem.CacheDirectory,
                $"bannister_tools_{DateTime.UtcNow.Ticks}.db");
            await using (var source =
                         await response.Content.ReadAsStreamAsync())
            await using (var target = File.Create(tempPath))
            {
                await source.CopyToAsync(target);
            }

            try
            {
                await _db.ReplaceDatabaseFromAsync(tempPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }

            _deviceMode.LastSyncUtc = DateTime.UtcNow;
            return new SyncResult
            {
                Success = true,
                Message = "Database downloaded successfully."
            };
        }
        catch (Exception ex)
        {
            return Fail($"Download error: {ex.Message}");
        }
    }

    private static SyncResult Fail(string message) =>
        new() { Success = false, Message = message };
}
