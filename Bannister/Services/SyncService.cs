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
}
