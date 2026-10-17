using Bannister.Models;

namespace Bannister.Services;

public class HookPrefixService
{
    private readonly DatabaseService _db;
    private readonly OperationQueueService _queue;
    private bool _initialized;

    public HookPrefixService(DatabaseService db, OperationQueueService queue)
    {
        _db = db;
        _queue = queue;
    }

    public async Task InitAsync()
    {
        if (_initialized) return;
        await _db.EnsureTableAsync<HookPrefix>();
        await _db.EnsureTableAsync<HookPrefixSession>();
        await _db.EnsureTableAsync<HookCropperSettings>();
        _initialized = true;
    }

    public async Task<List<HookPrefix>> GetPrefixesAsync(string username)
    {
        await InitAsync();
        username = NormalizeUsername(username);
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<HookPrefix>()
            .Where(x => x.Username == username && !x.IsArchived)
            .OrderBy(x => x.PrefixText)
            .ToListAsync();
    }

    public async Task SavePrefixAsync(HookPrefix prefix)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        prefix.Username = NormalizeUsername(prefix.Username);
        prefix.PrefixText = prefix.PrefixText?.Trim() ?? "";
        if (prefix.CreatedDate == default)
            prefix.CreatedDate = DateTime.UtcNow;

        var conn = await _db.GetConnectionAsync();
        if (prefix.Id == 0)
            await conn.InsertAsync(prefix);
        else
            await conn.UpdateAsync(prefix);
    }

    public async Task ArchivePrefixAsync(int id)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        var conn = await _db.GetConnectionAsync();
        var prefix = await conn.FindAsync<HookPrefix>(id);
        if (prefix == null) return;

        prefix.IsArchived = true;
        await conn.UpdateAsync(prefix);
    }

    public async Task<HookCropperSettings> GetSettingsAsync(string username)
    {
        await InitAsync();
        username = NormalizeUsername(username);
        var conn = await _db.GetConnectionAsync();
        var settings = await conn.Table<HookCropperSettings>()
            .Where(x => x.Username == username)
            .FirstOrDefaultAsync();

        if (settings != null)
            return settings;

        settings = new HookCropperSettings
        {
            Username = username,
            RequirePrefixBeforeCrop = true,
            LastPrefixText = ""
        };

        if (!_db.IsReadOnly)
            await conn.InsertAsync(settings);

        return settings;
    }

    public async Task SaveSettingsAsync(HookCropperSettings settings)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        settings.Username = NormalizeUsername(settings.Username);
        settings.LastPrefixText = settings.LastPrefixText?.Trim() ?? "";

        var conn = await _db.GetConnectionAsync();
        if (settings.Id == 0)
            await conn.InsertAsync(settings);
        else
            await conn.UpdateAsync(settings);
    }

    public async Task<HookPrefixSession?> GetActiveSessionAsync(string username)
    {
        await InitAsync();
        username = NormalizeUsername(username);
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<HookPrefixSession>()
            .Where(x => x.Username == username && x.TotalCropped == null)
            .OrderByDescending(x => x.CreatedDate)
            .FirstOrDefaultAsync();
    }

    public async Task SaveSessionAsync(HookPrefixSession session)
    {
        await InitAsync();
        session.Username = NormalizeUsername(session.Username);
        session.PrefixText = session.PrefixText?.Trim() ?? "";
        session.Mode = session.Mode?.Trim() ?? "";
        if (session.CreatedDate == default)
            session.CreatedDate = DateTime.UtcNow;

        if (_db.IsReadOnly)
        {
            if (session.Id == 0)
            {
                await _queue.EnqueueAsync("start_hook_session", new
                {
                    operation_type = "start_hook_session",
                    client_uuid = Guid.NewGuid().ToString("D"),
                    prefix_text = session.PrefixText,
                    prefix_id_1 = session.PrefixId1,
                    prefix_id_2 = session.PrefixId2,
                    mode = session.Mode,
                    created_date = session.CreatedDate.ToUniversalTime().ToString("O")
                });
            }

            return;
        }

        var conn = await _db.GetConnectionAsync();
        if (session.Id == 0)
        {
            await conn.InsertAsync(session);
            if (session.PrefixId1.HasValue)
                await IncrementGenerationAsync(conn, session.PrefixId1.Value);
        }
        else
        {
            await conn.UpdateAsync(session);
        }
    }

    public async Task UpdateSessionCropResultAsync(
        int sessionId,
        int cropped,
        int extraordinary)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var session = await conn.FindAsync<HookPrefixSession>(sessionId);
        if (session == null) return;

        if (_db.IsReadOnly)
        {
            var queuedCroppedDelta = cropped - (session.TotalCropped ?? 0);
            var queuedExtraordinaryDelta = extraordinary - (session.TotalExtraordinary ?? 0);
            await _queue.EnqueueAsync("update_hook_session_stats", new
            {
                operation_type = "update_hook_session_stats",
                client_uuid = Guid.NewGuid().ToString("D"),
                prefix_text = session.PrefixText,
                cropped_delta = queuedCroppedDelta,
                extraordinary_delta = queuedExtraordinaryDelta,
                session_created_date = session.CreatedDate.ToUniversalTime().ToString("O")
            });
            return;
        }

        var oldCropped = session.TotalCropped ?? 0;
        var oldExtraordinary = session.TotalExtraordinary ?? 0;
        var croppedDelta = cropped - oldCropped;
        var extraordinaryDelta = extraordinary - oldExtraordinary;

        session.TotalCropped = cropped;
        session.TotalExtraordinary = extraordinary;
        await conn.UpdateAsync(session);

        var appliedManualPrefixStats = false;
        if (!session.PrefixId1.HasValue &&
            !string.IsNullOrWhiteSpace(session.PrefixText))
        {
            var prefix = await ResolvePrefixFromTextAsync(conn, session);
            if (prefix != null)
            {
                session.PrefixId1 = prefix.Id;
                await conn.UpdateAsync(session);
                await ApplyStatsDeltaAsync(
                    conn,
                    prefix.Id,
                    croppedDelta,
                    extraordinaryDelta,
                    false);
                appliedManualPrefixStats = true;
            }
        }

        if (session.PrefixId1.HasValue && !appliedManualPrefixStats)
            await ApplyStatsDeltaAsync(
                conn,
                session.PrefixId1.Value,
                croppedDelta,
                extraordinaryDelta,
                session.Mode == "combined");

        if (session.PrefixId2.HasValue &&
            session.PrefixId2.Value != session.PrefixId1)
        {
            await ApplyStatsDeltaAsync(
                conn,
                session.PrefixId2.Value,
                croppedDelta,
                extraordinaryDelta,
                true);
        }
    }

    public async Task ApplyQueuedSessionStatsAsync(
        string username,
        string prefixText,
        DateTime sessionCreatedDate,
        int croppedDelta,
        int extraordinaryDelta)
    {
        await InitAsync();
        username = NormalizeUsername(username);
        prefixText = prefixText?.Trim() ?? "";

        var conn = await _db.GetConnectionAsync();
        var sessions = await conn.Table<HookPrefixSession>()
            .Where(x => x.Username == username && x.PrefixText == prefixText)
            .ToListAsync();
        var targetCreatedDate = sessionCreatedDate.ToUniversalTime();
        var session = sessions.FirstOrDefault(x =>
            x.CreatedDate.ToUniversalTime() == targetCreatedDate);
        if (session == null)
            throw new InvalidOperationException("Matching hook prefix session was not found.");

        session.TotalCropped = (session.TotalCropped ?? 0) + croppedDelta;
        session.TotalExtraordinary =
            (session.TotalExtraordinary ?? 0) + extraordinaryDelta;
        await conn.UpdateAsync(session);

        var appliedManualPrefixStats = false;
        if (!session.PrefixId1.HasValue &&
            !string.IsNullOrWhiteSpace(session.PrefixText))
        {
            var prefix = await ResolvePrefixFromTextAsync(conn, session);
            if (prefix != null)
            {
                session.PrefixId1 = prefix.Id;
                await conn.UpdateAsync(session);
                await ApplyStatsDeltaAsync(
                    conn,
                    prefix.Id,
                    croppedDelta,
                    extraordinaryDelta,
                    false);
                appliedManualPrefixStats = true;
            }
        }

        if (session.PrefixId1.HasValue && !appliedManualPrefixStats)
        {
            await ApplyStatsDeltaAsync(
                conn,
                session.PrefixId1.Value,
                croppedDelta,
                extraordinaryDelta,
                session.Mode == "combined");
        }

        if (session.PrefixId2.HasValue &&
            session.PrefixId2.Value != session.PrefixId1)
        {
            await ApplyStatsDeltaAsync(
                conn,
                session.PrefixId2.Value,
                croppedDelta,
                extraordinaryDelta,
                true);
        }
    }

    public async Task ApplyQueuedStartSessionAsync(
        string username,
        string prefixText,
        int? prefixId1,
        int? prefixId2,
        string mode,
        DateTime createdDate)
    {
        await InitAsync();
        username = NormalizeUsername(username);
        prefixText = prefixText?.Trim() ?? "";
        mode = mode?.Trim() ?? "";

        var conn = await _db.GetConnectionAsync();
        var sessions = await conn.Table<HookPrefixSession>()
            .Where(x => x.Username == username && x.PrefixText == prefixText)
            .ToListAsync();
        var targetCreatedDate = createdDate.ToUniversalTime();
        var existing = sessions.FirstOrDefault(x =>
            x.CreatedDate.ToUniversalTime() == targetCreatedDate);
        if (existing != null)
            return;

        _ = prefixId1;
        _ = prefixId2;

        var session = new HookPrefixSession
        {
            Username = username,
            PrefixText = prefixText,
            Mode = mode,
            CreatedDate = targetCreatedDate
        };

        if (!string.IsNullOrWhiteSpace(prefixText))
        {
            var prefix = await ResolvePrefixFromTextAsync(conn, session);
            session.PrefixId1 = prefix?.Id;
        }

        await conn.InsertAsync(session);
    }

    public async Task<List<HookPrefix>> GetPrefixStatsAsync(string username)
    {
        await InitAsync();
        username = NormalizeUsername(username);
        var conn = await _db.GetConnectionAsync();
        var list = await conn.Table<HookPrefix>()
            .Where(p => p.Username == username && !p.IsArchived)
            .ToListAsync();
        return list
            .OrderByDescending(x => x.TotalCropped + x.TotalCroppedAsCombined)
            .ToList();
    }

    private static async Task ApplyStatsDeltaAsync(
        SQLite.ISQLiteAsyncConnection conn,
        int prefixId,
        int croppedDelta,
        int extraordinaryDelta,
        bool asCombined)
    {
        var prefix = await conn.FindAsync<HookPrefix>(prefixId);
        if (prefix == null) return;

        if (asCombined)
        {
            prefix.TotalCroppedAsCombined =
                Math.Max(0, prefix.TotalCroppedAsCombined + croppedDelta);
            prefix.TotalExtraordinaryAsCombined =
                Math.Max(0, prefix.TotalExtraordinaryAsCombined + extraordinaryDelta);
        }
        else
        {
            prefix.TotalCropped = Math.Max(0, prefix.TotalCropped + croppedDelta);
            prefix.TotalExtraordinary =
                Math.Max(0, prefix.TotalExtraordinary + extraordinaryDelta);
        }

        await conn.UpdateAsync(prefix);
    }

    private static async Task<HookPrefix?> ResolvePrefixFromTextAsync(
        SQLite.ISQLiteAsyncConnection conn,
        HookPrefixSession session)
    {
        var prefixText = session.PrefixText.Trim();
        var prefixes = await conn.Table<HookPrefix>()
            .Where(p => p.Username == session.Username)
            .ToListAsync();
        var prefix = prefixes.FirstOrDefault(p =>
            string.Equals(
                p.PrefixText?.Trim(),
                prefixText,
                StringComparison.OrdinalIgnoreCase));

        if (prefix == null)
        {
            prefix = new HookPrefix
            {
                Username = session.Username,
                PrefixText = prefixText,
                TotalGenerations = 1,
                CreatedDate = DateTime.UtcNow
            };
            await conn.InsertAsync(prefix);
            return prefix;
        }

        prefix.TotalGenerations++;
        await conn.UpdateAsync(prefix);
        return prefix;
    }

    private static async Task IncrementGenerationAsync(
        SQLite.ISQLiteAsyncConnection conn,
        int prefixId)
    {
        var prefix = await conn.FindAsync<HookPrefix>(prefixId);
        if (prefix == null) return;

        prefix.TotalGenerations++;
        await conn.UpdateAsync(prefix);
    }

    private static string NormalizeUsername(string username) =>
        (username ?? "").Trim().ToLowerInvariant();
}
