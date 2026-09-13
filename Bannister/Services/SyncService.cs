using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bannister.Models;

namespace Bannister.Services;

/// <summary>
/// Handles syncing the SQLCipher-encrypted database with a cloud server (HostGator PHP).
///
/// Master devices UPLOAD their snapshot. Secondary devices DOWNLOAD the latest copy.
/// The .db file is already SQLCipher-encrypted before it leaves the device — the server
/// never sees plaintext.
/// </summary>
public class SyncService
{
    private readonly DatabaseService _db;
    private readonly DeviceModeService _deviceMode;
    private readonly OperationQueueService _queue;
    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    public SyncService(DatabaseService db, DeviceModeService deviceMode, OperationQueueService queue)
    {
        _db = db;
        _deviceMode = deviceMode;
        _queue = queue;
    }

    public class SyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public long? BytesTransferred { get; set; }
        public DateTime? ServerTimestamp { get; set; }
    }

    public class ServerInfo
    {
        public bool Exists { get; set; }
        public long Size { get; set; }
        public DateTime? LastModified { get; set; }
    }

    /// <summary>
    /// Register a sync account on the configured server.
    /// </summary>
    public async Task<SyncResult> RegisterAsync(string username, string password)
    {
        var normalizedUsername = (username ?? "").Trim().ToLowerInvariant();
        if (!Regex.IsMatch(normalizedUsername, "^[a-z0-9_-]{3,30}$"))
        {
            return Fail("Username must be 3-30 characters and contain only lowercase letters, digits, underscore, or hyphen.");
        }

        if (string.IsNullOrWhiteSpace(password))
            return Fail("Password is required.");

        var baseUrl = _deviceMode.ServerUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            return Fail("Sync server is not configured.");

        var url = $"{baseUrl.TrimEnd('/')}?action=register";
        var hash = DeviceModeService.HashForTransport(password);

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                username = normalizedUsername,
                hash
            });

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(url, content);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                string registeredUsername = normalizedUsername;
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("username", out var u))
                        registeredUsername = u.GetString() ?? normalizedUsername;
                }
                catch
                {
                    // A successful registration is enough; use the normalized name.
                }

                _deviceMode.ServerUrl = baseUrl;
                await _deviceMode.SetSyncCredentialsAsync(registeredUsername, password);

                return new SyncResult
                {
                    Success = true,
                    Message = $"Account '{registeredUsername}' registered."
                };
            }

            if ((int)response.StatusCode == 409)
            {
                return Fail(
                    $"Username '{normalizedUsername}' is already taken. Choose a different one or use Save Credentials with the existing password.");
            }

            return Fail(ExtractServerError(body, $"Registration failed ({(int)response.StatusCode})."));
        }
        catch (Exception ex)
        {
            return Fail($"Registration error: {ex.Message}");
        }
    }

    /// <summary>
    /// Upload the current device's database to the server.
    /// Only valid on master devices.
    /// </summary>
    public async Task<SyncResult> UploadAsync()
    {
        if (_deviceMode.IsReadOnly)
            return Fail("Cannot upload from a secondary (read-only) device.");

        var (url, headers) = await BuildRequestAsync();
        if (url == null) return Fail("Sync server is not configured.");

        string? snapshotPath = null;
        try
        {
            // Safe consistent snapshot via VACUUM INTO. Encrypted with the same SQLCipher key.
            snapshotPath = await _db.CreateSnapshotAsync();
            var fileInfo = new FileInfo(snapshotPath);

            using var content = new ByteArrayContent(await File.ReadAllBytesAsync(snapshotPath));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            foreach (var h in headers) request.Headers.Add(h.Key, h.Value);

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return Fail($"Upload failed ({(int)response.StatusCode}): {Trim(body, 200)}");
            }

            _deviceMode.LastSyncUtc = DateTime.UtcNow;
            return new SyncResult
            {
                Success = true,
                Message = $"Uploaded {FormatBytes(fileInfo.Length)} successfully.",
                BytesTransferred = fileInfo.Length,
                ServerTimestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return Fail($"Upload error: {ex.Message}");
        }
        finally
        {
            if (snapshotPath != null && File.Exists(snapshotPath))
            {
                try { File.Delete(snapshotPath); } catch { /* best-effort cleanup */ }
            }
        }
    }

    /// <summary>
    /// Download the latest database from the server and install it locally.
    /// Closes the current connection, replaces the file, and the next GetConnectionAsync
    /// will reopen with the fresh data.
    /// </summary>
    public async Task<SyncResult> DownloadAsync()
    {
        var (url, headers) = await BuildRequestAsync();
        if (url == null) return Fail("Sync server is not configured.");

        string? tempPath = null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            foreach (var h in headers) request.Headers.Add(h.Key, h.Value);

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return Fail($"Download failed ({(int)response.StatusCode}): {Trim(body, 200)}");
            }

            tempPath = Path.Combine(FileSystem.CacheDirectory, $"bannister_download_{DateTime.UtcNow.Ticks}.db");
            using (var fs = File.Create(tempPath))
            {
                await response.Content.CopyToAsync(fs);
            }

            var fileInfo = new FileInfo(tempPath);
            if (fileInfo.Length == 0)
                return Fail("Server returned an empty file.");

            // Hand off to DatabaseService which closes the connection and atomically swaps.
            await _db.ReplaceDatabaseFromAsync(tempPath);

            _deviceMode.LastSyncUtc = DateTime.UtcNow;
            return new SyncResult
            {
                Success = true,
                Message = $"Downloaded {FormatBytes(fileInfo.Length)} successfully.",
                BytesTransferred = fileInfo.Length,
                ServerTimestamp = response.Content.Headers.LastModified?.UtcDateTime
            };
        }
        catch (Exception ex)
        {
            return Fail($"Download error: {ex.Message}");
        }
        finally
        {
            if (tempPath != null && File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best-effort */ }
            }
        }
    }

    /// <summary>
    /// Lightweight check — returns size and last-modified for the server copy without
    /// downloading the full file. Use this to show "newer version available" hints.
    /// </summary>
    public async Task<ServerInfo?> GetServerInfoAsync()
    {
        var (url, headers) = await BuildRequestAsync(action: "info");
        if (url == null) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            foreach (var h in headers) request.Headers.Add(h.Key, h.Value);

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new ServerInfo
            {
                Exists = root.TryGetProperty("exists", out var e) && e.GetBoolean(),
                Size = root.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                LastModified = root.TryGetProperty("last_modified", out var lm)
                    ? DateTimeOffset.FromUnixTimeSeconds(lm.GetInt64()).UtcDateTime
                    : (DateTime?)null
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<SyncResult> UploadQueueAsync()
    {
        var ops = await _queue.GetPendingAsync();
        if (ops.Count == 0)
            return new SyncResult { Success = true, Message = "No operations to upload." };

        var (url, headers) = await BuildRequestAsync(action: "queue_upload");
        if (url == null) return Fail("Sync server is not configured.");

        try
        {
            var password = _db.GetDbPassword();
            if (string.IsNullOrEmpty(password))
                return Fail("Cannot upload queued operations because no user is logged in.");

            foreach (var op in ops)
            {
                try
                {
                    _ = QueuePayloadCrypto.DecryptPayload(op.PayloadJson, password);
                }
                catch
                {
                    return Fail("Cannot upload queued operations because one or more payloads are not encrypted for the current login password.");
                }
            }

            var payload = new JsonObject
            {
                ["device_id"] = DeviceModeService.GetDeviceId(),
                ["operations"] = new JsonArray(ops.Select(op => new JsonObject
                {
                    ["uuid"] = op.Uuid,
                    ["operation_type"] = op.OperationType,
                    ["payload"] = op.PayloadJson,
                    ["created_at"] = op.CreatedAt.ToUniversalTime().ToString("O")
                }).ToArray<JsonNode?>())
            };

            using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            foreach (var h in headers) request.Headers.Add(h.Key, h.Value);

            using var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return Fail($"Queue upload failed ({(int)response.StatusCode}): {ExtractServerError(body, Trim(body, 200))}");

            foreach (var op in ops)
                await _queue.MarkSyncedAsync(op.Uuid);

            int uploadedCount = ops.Count;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("uploaded_count", out var c))
                    uploadedCount = c.GetInt32();
            }
            catch
            {
                // Use local count if server omits count or response shape changes.
            }

            return new SyncResult
            {
                Success = true,
                Message = $"Uploaded {uploadedCount} {(uploadedCount == 1 ? "operation" : "operations")}."
            };
        }
        catch (Exception ex)
        {
            return Fail($"Queue upload error: {ex.Message}");
        }
    }

    public async Task<List<QueuedOperation>> DownloadQueueAsync()
    {
        var (url, headers) = await BuildRequestAsync(action: "queue_download");
        if (url == null) return new List<QueuedOperation>();

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var h in headers) request.Headers.Add(h.Key, h.Value);

        using var response = await _http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Queue download failed ({(int)response.StatusCode}): {ExtractServerError(body, Trim(body, 200))}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("operations", out var operations) ||
            operations.ValueKind != JsonValueKind.Array)
            return new List<QueuedOperation>();

        var result = new List<QueuedOperation>();
        foreach (var item in operations.EnumerateArray())
        {
            var payloadJson = item.TryGetProperty("payload", out var payload)
                ? payload.ValueKind == JsonValueKind.String
                    ? payload.GetString() ?? ""
                    : payload.GetRawText()
                : "";

            DateTime createdAt = DateTime.UtcNow;
            if (item.TryGetProperty("created_at", out var created) &&
                DateTime.TryParse(created.GetString(), null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                createdAt = parsed.ToUniversalTime();
            }

            result.Add(new QueuedOperation
            {
                Uuid = item.TryGetProperty("uuid", out var uuid) ? uuid.GetString() ?? "" : "",
                OperationType = item.TryGetProperty("operation_type", out var type) ? type.GetString() ?? "" : "",
                PayloadJson = payloadJson,
                CreatedAt = createdAt,
                SourceDeviceId = item.TryGetProperty("device_id", out var deviceId) ? deviceId.GetString() : null,
                Status = 0
            });
        }

        return result;
    }

    public async Task<SyncResult> ClearAppliedFromServerAsync(List<string> uuids)
    {
        var (url, headers) = await BuildRequestAsync(action: "queue_clear");
        if (url == null) return Fail("Sync server is not configured.");

        try
        {
            var payload = JsonSerializer.Serialize(new { applied_uuids = uuids ?? new List<string>() });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            foreach (var h in headers) request.Headers.Add(h.Key, h.Value);

            using var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return Fail($"Queue clear failed ({(int)response.StatusCode}): {ExtractServerError(body, Trim(body, 200))}");

            int removedCount = 0;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("removed_count", out var c))
                    removedCount = c.GetInt32();
            }
            catch
            {
                removedCount = uuids?.Count ?? 0;
            }

            return new SyncResult
            {
                Success = true,
                Message = $"Cleared {removedCount} applied {(removedCount == 1 ? "operation" : "operations")} from server."
            };
        }
        catch (Exception ex)
        {
            return Fail($"Queue clear error: {ex.Message}");
        }
    }

    private async Task<(string? url, Dictionary<string, string> headers)> BuildRequestAsync(string? action = null)
    {
        var baseUrl = _deviceMode.ServerUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return (null, new Dictionary<string, string>());

        var (user, hash) = await _deviceMode.GetSyncCredentialsAsync();
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(hash))
            return (null, new Dictionary<string, string>());

        string url = action == null ? baseUrl : $"{baseUrl}?action={action}";

        // X-Auth: username:base64hash — server validates against its credentials store.
        var headers = new Dictionary<string, string>
        {
            ["X-Auth"] = $"{user}:{hash}"
        };

        return (url, headers);
    }

    private static SyncResult Fail(string msg) => new SyncResult { Success = false, Message = msg };

    private static string ExtractServerError(string body, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    var message = error.GetString();
                    if (!string.IsNullOrWhiteSpace(message))
                        return message;
                }
            }
            catch
            {
                // Fall back to trimmed raw body below.
            }

            return Trim(body, 200);
        }

        return fallback;
    }

    private static string Trim(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max) + "...");

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    // ── Shared Activity Sync ─────────────────────────────────────

    public async Task<bool> UploadSharedActivitiesAsync(
        string uploaderName,
        SharedActivityLink link,
        List<Activity> activities,
        string password,
        ExpService expService)
    {
        try
        {
            // Collect EXP history per shared activity.
            var conn = await _db.GetConnectionAsync();
            var activityKeys = activities
                .Select(a => (a.Game, a.Name))
                .ToHashSet();
            var allExpLogs = await conn.Table<ExpLog>()
                .Where(r => r.Username == uploaderName)
                .ToListAsync();
            var expRecords = allExpLogs
                .Where(r => activityKeys.Contains((r.Game, r.ActivityName)))
                .Select(r => new
                {
                    GameId = r.Game,
                    r.ActivityName,
                    ExpGained = r.DeltaExp,
                    Timestamp = r.LoggedAt,
                    Note = ""
                })
                .ToList();

            // Current EXP state per shared game. Level is derived from TotalExp.
            var expStates = new List<object>();
            foreach (var gameId in activities.Select(a => a.Game).Distinct())
            {
                var state = await conn.Table<ExpState>()
                    .Where(s => s.Username == uploaderName && s.Game == gameId)
                    .FirstOrDefaultAsync();
                if (state == null) continue;
                var (level, _, _) = await expService.GetProgressAsync(
                    uploaderName, gameId);
                expStates.Add(new
                {
                    GameId = state.Game,
                    state.TotalExp,
                    Level = level,
                    LastUpdated = state.UpdatedAt
                });
            }

            var payload = new
            {
                UpdatedBy = uploaderName,
                UpdatedAt = DateTime.UtcNow,
                Activities = activities.Select(a => new
                {
                    a.Game,
                    a.Name,
                    a.Category,
                    a.ExpGain,
                    a.ImagePath,
                    a.IsActive,
                    a.DisplayDaysOfWeek,
                    a.DisplayDayOfMonth,
                    a.IsAutoAward,
                    a.AutoAwardFrequency,
                    a.AutoAwardDays,
                    a.LastAutoAwarded,
                    a.HabitStreak,
                    a.TimesCompleted,
                    a.MeaningfulUntilLevel
                }).ToList(),
                ExpRecords = expRecords,
                ExpStates = expStates
            };

            var json = JsonSerializer.Serialize(payload);
            var encrypted = SharedActivityService.Encrypt(
                json, password, link.LinkCode);

            var (url, headers) = await BuildRequestAsync("upload_shared");
            if (url == null) return false;

            using var client = new HttpClient();
            foreach (var h in headers)
                client.DefaultRequestHeaders.Add(h.Key, h.Value);

            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(link.LinkCode), "link_code");
            form.Add(new ByteArrayContent(encrypted), "file",
                $"shared_{link.LinkCode.ToUpperInvariant()}.enc");

            var response = await client.PostAsync(url, form);
            if (response.IsSuccessStatusCode) return true;

            // Log the actual error response for debugging
            var errorBody = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine(
                $"[SHARED MANIFEST] Upload failed. " +
                $"Status: {(int)response.StatusCode} {response.StatusCode}. " +
                $"Body: {errorBody}");

            // Store error in a file so we can read it without VS
            try
            {
                var logPath = System.IO.Path.Combine(
                    FileSystem.AppDataDirectory, "shared_manifest_error.txt");
                await System.IO.File.WriteAllTextAsync(logPath,
                    $"Status: {(int)response.StatusCode}\n" +
                    $"Body: {errorBody}\n" +
                    $"URL: {url}\n" +
                    $"Time: {DateTime.Now}");
            }
            catch { }

            return false;
        }
        catch { return false; }
    }

    /// <summary>
    /// Uploads an encrypted manifest of activity names being shared.
    /// File key: shared_manifest_{LINKCODE}.enc
    /// Downloaded by joiner to see what is being offered before accepting.
    /// </summary>
    public async Task<bool> UploadSharedManifestAsync(
        string creatorName,
        string linkCode,
        string password,
        List<(string GameId, string GameName,
            string ActivityName, int ExpGain)> activities)
    {
        try
        {
            var manifest = new
            {
                CreatedBy = creatorName,
                CreatedAt = DateTime.UtcNow,
                Activities = activities.Select(a => new
                {
                    a.GameId,
                    a.GameName,
                    a.ActivityName,
                    a.ExpGain
                }).ToList()
            };

            var json = JsonSerializer.Serialize(manifest);
            var encrypted = SharedActivityService.Encrypt(
                json, password, linkCode + "_manifest");

            var (url, headers) = await BuildRequestAsync("upload_shared");
            if (url == null) return false;

            using var client = new HttpClient();
            foreach (var h in headers)
                client.DefaultRequestHeaders.Add(h.Key, h.Value);

            var fileName =
                $"shared_manifest_{linkCode.ToUpperInvariant()}.enc";
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(linkCode), "link_code");
            form.Add(new ByteArrayContent(encrypted), "file", fileName);

            var response = await client.PostAsync(url, form);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    /// <summary>
    /// Downloads and decrypts the activity manifest for a link code.
    /// Returns null if not found or decryption fails.
    /// </summary>
    public async Task<(string CreatedBy,
        List<SharedManifestItem> Activities)?>
        DownloadSharedManifestAsync(string linkCode, string password)
    {
        try
        {
            var (url, headers) = await BuildRequestAsync("download_shared");
            if (url == null) return null;

            using var client = new HttpClient();
            foreach (var h in headers)
                client.DefaultRequestHeaders.Add(h.Key, h.Value);

            var fileName =
                $"shared_manifest_{linkCode.ToUpperInvariant()}.enc";
            var requestUrl =
                $"{url}&link_code={Uri.EscapeDataString(fileName)}";
            var response = await client.GetAsync(requestUrl);
            if (!response.IsSuccessStatusCode) return null;

            var encrypted = await response.Content.ReadAsByteArrayAsync();
            if (encrypted.Length < 16) return null;

            var json = SharedActivityService.Decrypt(
                encrypted, password, linkCode + "_manifest");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var createdBy = root.GetProperty("CreatedBy").GetString() ?? "";
            var items = JsonSerializer.Deserialize<List<SharedManifestItem>>(
                root.GetProperty("Activities").GetRawText(),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new();

            return (createdBy, items);
        }
        catch { return null; }
    }

    public async Task<(string UpdatedBy, DateTime UpdatedAt,
        List<SharedActivityDownloadItem> Activities,
        List<SharedExpRecordDto> ExpRecords,
        List<SharedExpStateDto> ExpStates)?>
        DownloadSharedActivitiesAsync(SharedActivityLink link,
            string password)
    {
        try
        {
            var (url, headers) = await BuildRequestAsync("download_shared");
            if (url == null) return null;

            using var client = new HttpClient();
            foreach (var h in headers)
                client.DefaultRequestHeaders.Add(h.Key, h.Value);

            var requestUrl =
                $"{url}&link_code={Uri.EscapeDataString(link.LinkCode)}";
            var response = await client.GetAsync(requestUrl);
            if (!response.IsSuccessStatusCode) return null;

            var encrypted = await response.Content.ReadAsByteArrayAsync();
            if (encrypted.Length < 16) return null;

            var json = SharedActivityService.Decrypt(
                encrypted, password, link.LinkCode);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var updatedBy = root.GetProperty("UpdatedBy").GetString() ?? "";
            var updatedAt = root.GetProperty("UpdatedAt").GetDateTime();
            var acts = JsonSerializer.Deserialize<List<SharedActivityDownloadItem>>(
                root.GetProperty("Activities").GetRawText(),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new();

            var expRecords = new List<SharedExpRecordDto>();
            var expStates = new List<SharedExpStateDto>();
            if (root.TryGetProperty("ExpRecords", out var expRecordsEl))
                expRecords = JsonSerializer.Deserialize<List<SharedExpRecordDto>>(
                    expRecordsEl.GetRawText(),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new();
            if (root.TryGetProperty("ExpStates", out var expStatesEl))
                expStates = JsonSerializer.Deserialize<List<SharedExpStateDto>>(
                    expStatesEl.GetRawText(),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new();

            return (updatedBy, updatedAt, acts, expRecords, expStates);
        }
        catch { return null; }
    }

    public class SharedActivityDownloadItem
    {
        public string Game { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public int ExpGain { get; set; }
        public string ImagePath { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public string DisplayDaysOfWeek { get; set; } = "";
        public int DisplayDayOfMonth { get; set; }
        public bool IsAutoAward { get; set; }
        public string AutoAwardFrequency { get; set; } = "None";
        public string AutoAwardDays { get; set; } = "";
        public DateTime? LastAutoAwarded { get; set; }
        public int HabitStreak { get; set; }
        public int TimesCompleted { get; set; }
        public int MeaningfulUntilLevel { get; set; } = 100;
    }

    public class SharedExpRecordDto
    {
        public string GameId { get; set; } = "";
        public string ActivityName { get; set; } = "";
        public int ExpGained { get; set; }
        public DateTime Timestamp { get; set; }
        public string Note { get; set; } = "";
    }

    public class SharedExpStateDto
    {
        public string GameId { get; set; } = "";
        public int TotalExp { get; set; }
        public int Level { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class SharedManifestItem
    {
        public string GameId { get; set; } = "";
        public string GameName { get; set; } = "";
        public string ActivityName { get; set; } = "";
        public int ExpGain { get; set; }
    }
}
