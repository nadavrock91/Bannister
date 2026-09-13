using Bannister.Models;

namespace Bannister.Services;

public class HomeButtonVisibilityService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public static readonly HashSet<string> DefaultEnabled =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Calendar",
            "Games",
            "Settings"
        };

    public HomeButtonVisibilityService(DatabaseService db)
    {
        _db = db;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        _initialized = true;
        if (!_db.IsReadOnly)
            await _db.EnsureTableAsync<HomeButtonVisibilitySetting>();
    }

    public async Task<HashSet<string>> GetEnabledButtonsAsync(
        string username, IEnumerable<string> allButtonIds)
    {
        await EnsureInitializedAsync();
        try
        {
            var conn = await _db.GetConnectionAsync();
            var rows = await conn
                .Table<HomeButtonVisibilitySetting>()
                .Where(r => r.Username == username)
                .ToListAsync();

            if (rows.Count == 0)
            {
                await SeedDefaultsAsync(username, allButtonIds, conn);
                rows = await conn
                    .Table<HomeButtonVisibilitySetting>()
                    .Where(r => r.Username == username)
                    .ToListAsync();
            }

            var existingIds = rows
                .Select(r => r.ButtonId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var id in allButtonIds)
            {
                if (!existingIds.Contains(id))
                {
                    var newRow = new HomeButtonVisibilitySetting
                    {
                        Username = username,
                        ButtonId = id,
                        IsEnabled = DefaultEnabled.Contains(id),
                        UpdatedAt = DateTime.UtcNow
                    };
                    await conn.InsertAsync(newRow);
                    rows.Add(newRow);
                }
            }

            return rows
                .Where(r => r.IsEnabled)
                .Select(r => r.ButtonId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(
                DefaultEnabled, StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task<List<HomeButtonVisibilitySetting>>
        GetAllSettingsAsync(
            string username,
            IEnumerable<string> allButtonIds)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var rows = await conn
            .Table<HomeButtonVisibilitySetting>()
            .Where(r => r.Username == username)
            .ToListAsync();

        if (rows.Count == 0)
        {
            await SeedDefaultsAsync(username, allButtonIds, conn);
            rows = await conn
                .Table<HomeButtonVisibilitySetting>()
                .Where(r => r.Username == username)
                .ToListAsync();
        }

        var existingIds = rows
            .Select(r => r.ButtonId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var id in allButtonIds)
        {
            if (!existingIds.Contains(id))
            {
                var newRow = new HomeButtonVisibilitySetting
                {
                    Username = username,
                    ButtonId = id,
                    IsEnabled = DefaultEnabled.Contains(id),
                    UpdatedAt = DateTime.UtcNow
                };
                await conn.InsertAsync(newRow);
                rows.Add(newRow);
            }
        }

        rows = await conn
            .Table<HomeButtonVisibilitySetting>()
            .Where(r => r.Username == username)
            .ToListAsync();

        return rows
            .OrderBy(r => r.ButtonId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task SetButtonEnabledAsync(
        string username, string buttonId, bool enabled)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var existing = await conn
            .Table<HomeButtonVisibilitySetting>()
            .Where(r => r.Username == username
                && r.ButtonId == buttonId)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            existing.IsEnabled = enabled;
            existing.UpdatedAt = DateTime.UtcNow;
            await conn.UpdateAsync(existing);
        }
        else
        {
            await conn.InsertAsync(new HomeButtonVisibilitySetting
            {
                Username = username,
                ButtonId = buttonId,
                IsEnabled = enabled,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }

    private async Task SeedDefaultsAsync(
        string username,
        IEnumerable<string> allButtonIds,
        SQLite.ISQLiteAsyncConnection conn)
    {
        foreach (var id in allButtonIds)
        {
            await conn.InsertAsync(new HomeButtonVisibilitySetting
            {
                Username = username,
                ButtonId = id,
                IsEnabled = DefaultEnabled.Contains(id),
                UpdatedAt = DateTime.UtcNow
            });
        }
    }
}
