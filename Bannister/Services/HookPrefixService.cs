using Bannister.Models;

namespace Bannister.Services;

public class HookPrefixService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public HookPrefixService(DatabaseService db)
    {
        _db = db;
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
        if (_db.IsReadOnly) return;

        session.Username = NormalizeUsername(session.Username);
        session.PrefixText = session.PrefixText?.Trim() ?? "";
        session.Mode = session.Mode?.Trim() ?? "";
        if (session.CreatedDate == default)
            session.CreatedDate = DateTime.UtcNow;

        var conn = await _db.GetConnectionAsync();
        if (session.Id == 0)
            await conn.InsertAsync(session);
        else
            await conn.UpdateAsync(session);
    }

    public async Task UpdateSessionCropResultAsync(
        int sessionId,
        int cropped,
        int extraordinary)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        var conn = await _db.GetConnectionAsync();
        var session = await conn.FindAsync<HookPrefixSession>(sessionId);
        if (session == null) return;

        var oldCropped = session.TotalCropped ?? 0;
        var oldExtraordinary = session.TotalExtraordinary ?? 0;
        var croppedDelta = cropped - oldCropped;
        var extraordinaryDelta = extraordinary - oldExtraordinary;

        session.TotalCropped = cropped;
        session.TotalExtraordinary = extraordinary;
        await conn.UpdateAsync(session);

        if (session.PrefixId1.HasValue)
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

    public async Task<List<HookPrefix>> GetPrefixStatsAsync(string username)
    {
        await InitAsync();
        username = NormalizeUsername(username);
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<HookPrefix>()
            .Where(x => x.Username == username)
            .OrderByDescending(x =>
                x.TotalCropped + x.TotalCroppedAsCombined)
            .ToListAsync();
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

    private static string NormalizeUsername(string username) =>
        (username ?? "").Trim().ToLowerInvariant();
}
