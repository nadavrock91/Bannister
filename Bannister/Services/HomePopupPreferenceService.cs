using Bannister.Models;
using SQLite;

namespace Bannister.Services;

public class HomePopupPreferenceService
{
    private readonly DatabaseService _db;
    private readonly SubActivityService _subActivityService;
    private readonly HashSet<string> _seededUsers = new();

    public static readonly string[] AllPopupKeys =
    {
        "streak_reset",
        "streak_escalation",
        "missed_fragments",
        "quick_input",
        "days_since_escalation",
        "missed_activities",
        "habit_scolding",
        "subactivity",
        "pending_prompts",
        "life_path_checkin"
    };

    public HomePopupPreferenceService(DatabaseService db, SubActivityService subActivityService)
    {
        _db = db;
        _subActivityService = subActivityService;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private async Task EnsureTableAsync()
    {
        if (_db.IsReadOnly) return;

        var conn = await _db.GetConnectionAsync();
        try
        {
            await conn.CreateTableAsync<HomePopupPreference>();
            await conn.CreateTableAsync<PopupSeenToday>();
        }
        catch { }
    }

    public async Task<bool> IsEnabledAsync(string username, string popupKey, string deviceRole)
    {
        deviceRole = NormalizeDeviceRole(deviceRole);

        await EnsureTableAsync();
        await EnsurePreferencesSeededAsync(username);

        try
        {
            var conn = await _db.GetConnectionAsync();
            var pref = await conn.Table<HomePopupPreference>()
                .Where(p => p.Username == username && p.PopupKey == popupKey && p.DeviceRole == deviceRole)
                .FirstOrDefaultAsync();

            return pref?.Enabled ?? deviceRole == "primary";
        }
        catch (SQLiteException)
        {
            return deviceRole == "primary";
        }
    }

    public async Task<Dictionary<string, (bool primary, bool secondary)>> GetAllPreferencesAsync(string username)
    {
        await EnsureTableAsync();
        await EnsurePreferencesSeededAsync(username);

        List<HomePopupPreference> rows;
        try
        {
            var conn = await _db.GetConnectionAsync();
            rows = await conn.Table<HomePopupPreference>()
                .Where(p => p.Username == username)
                .ToListAsync();
        }
        catch (SQLiteException)
        {
            rows = new List<HomePopupPreference>();
        }

        var result = new Dictionary<string, (bool primary, bool secondary)>();
        foreach (var key in AllPopupKeys)
        {
            var primary = rows.FirstOrDefault(r => r.PopupKey == key && r.DeviceRole == "primary")?.Enabled ?? true;
            var secondary = rows.FirstOrDefault(r => r.PopupKey == key && r.DeviceRole == "secondary")?.Enabled ?? false;
            result[key] = (primary, secondary);
        }

        return result;
    }

    public async Task<bool> SetPreferenceAsync(string username, string popupKey, string deviceRole, bool enabled)
    {
        if (_db.IsReadOnly) return false;

        deviceRole = NormalizeDeviceRole(deviceRole);
        await EnsureTableAsync();

        var conn = await _db.GetConnectionAsync();
        var existing = await conn.Table<HomePopupPreference>()
            .Where(p => p.Username == username && p.PopupKey == popupKey && p.DeviceRole == deviceRole)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            existing.Enabled = enabled;
            existing.UpdatedAt = DateTime.UtcNow;
            await conn.UpdateAsync(existing);
        }
        else
        {
            await conn.InsertAsync(new HomePopupPreference
            {
                Username = username,
                PopupKey = popupKey,
                DeviceRole = deviceRole,
                Enabled = enabled,
                UpdatedAt = DateTime.UtcNow
            });
        }

        return true;
    }

    public async Task<bool> IsSeenTodayAsync(
        string username,
        string popupKey)
    {
        if (!HasSeenTodayGate(popupKey))
            return false;
        try
        {
            var conn = await _db.GetConnectionAsync();
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            var row = await conn.Table<PopupSeenToday>()
                .Where(r => r.Username == username &&
                            r.PopupKey == popupKey)
                .FirstOrDefaultAsync();
            return row?.SeenDate == today;
        }
        catch { return false; }
    }

    public async Task<bool> SetSeenTodayAsync(
        string username,
        string popupKey,
        bool seen)
    {
        if (!HasSeenTodayGate(popupKey))
            return false;
        try
        {
            var conn = await _db.GetConnectionAsync();
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            var existing = await conn.Table<PopupSeenToday>()
                .Where(r => r.Username == username &&
                            r.PopupKey == popupKey)
                .FirstOrDefaultAsync();

            if (seen)
            {
                if (existing == null)
                {
                    await conn.InsertAsync(new PopupSeenToday
                    {
                        Username = username,
                        PopupKey = popupKey,
                        SeenDate = today,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.SeenDate = today;
                    existing.UpdatedAt = DateTime.UtcNow;
                    await conn.UpdateAsync(existing);
                }
            }
            else if (existing != null)
            {
                existing.SeenDate = "";
                existing.UpdatedAt = DateTime.UtcNow;
                await conn.UpdateAsync(existing);
            }

            return true;
        }
        catch { return false; }
    }

    public bool HasSeenTodayGate(string popupKey)
    {
        if (popupKey.StartsWith("life_path_checkin_"))
            return true;
        if (popupKey.StartsWith("daily_login_prompts_shown_") ||
            popupKey.StartsWith("allowance_daily_prompt_") ||
            popupKey.StartsWith("deadlines_checkin_") ||
            popupKey.StartsWith("expired_activities_prompt_") ||
            popupKey.StartsWith("daily_habit_allowance_prompt_") ||
            popupKey.StartsWith("weekly_commitment_prompt_") ||
            popupKey.StartsWith("weekly_habit_allowance_prompt_") ||
            popupKey.StartsWith("monthly_habit_allowance_prompt_"))
            return true;
        return popupKey switch
        {
            "habit_scolding" => true,
            "subactivity" => true,
            "pending_prompts" => true,
            "life_path_checkin" => true,
            _ => false
        };
    }

    private async Task EnsurePreferencesSeededAsync(string username)
    {
        if (_db.IsReadOnly) return;
        if (_seededUsers.Contains(username)) return;

        var conn = await _db.GetConnectionAsync();
        var count = await conn.Table<HomePopupPreference>()
            .Where(p => p.Username == username)
            .CountAsync();

        if (count > 0)
        {
            var existing = await conn.Table<HomePopupPreference>()
                .Where(p => p.Username == username)
                .ToListAsync();

            foreach (var key in AllPopupKeys)
            {
                foreach (var role in new[] { "primary", "secondary" })
                {
                    if (existing.Any(e => e.PopupKey == key && e.DeviceRole == role))
                        continue;

                    await conn.InsertAsync(new HomePopupPreference
                    {
                        Username = username,
                        PopupKey = key,
                        DeviceRole = role,
                        Enabled = role == "primary",
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }
        else
        {
            bool streakEnabled = true;
            bool daysSinceEnabled = true;

            try
            {
                var streakRaw = await SecureStorage.GetAsync("streak_notification_enabled");
                if (!string.IsNullOrWhiteSpace(streakRaw) && bool.TryParse(streakRaw, out var parsed))
                    streakEnabled = parsed;
            }
            catch { }

            try
            {
                var daysRaw = await SecureStorage.GetAsync($"days_since_notif_enabled_{username}");
                if (!string.IsNullOrWhiteSpace(daysRaw) && bool.TryParse(daysRaw, out var parsed))
                    daysSinceEnabled = parsed;
            }
            catch { }

            foreach (var key in AllPopupKeys)
            {
                foreach (var role in new[] { "primary", "secondary" })
                {
                    bool defaultEnabled = role == "secondary"
                        ? false
                        : key switch
                        {
                            "streak_reset" => streakEnabled,
                            "streak_escalation" => streakEnabled,
                            "days_since_escalation" => daysSinceEnabled,
                            _ => true
                        };

                    await conn.InsertAsync(new HomePopupPreference
                    {
                        Username = username,
                        PopupKey = key,
                        DeviceRole = role,
                        Enabled = defaultEnabled,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        _seededUsers.Add(username);
    }

    private static string NormalizeDeviceRole(string deviceRole)
    {
        return string.Equals(deviceRole, "secondary", StringComparison.OrdinalIgnoreCase)
            ? "secondary"
            : "primary";
    }

    private static string[] GetPendingPromptDailyKeys(string username) => new[]
    {
        $"daily_login_prompts_shown_{username}",
        $"allowance_daily_prompt_{username}",
        $"deadlines_checkin_{username}",
        $"expired_activities_prompt_{username}",
        $"daily_habit_allowance_prompt_{username}",
        $"weekly_commitment_prompt_{username}"
    };

    private static string[] GetPendingPromptMonthlyKeys(string username) => new[]
    {
        $"weekly_habit_allowance_prompt_{username}",
        $"monthly_habit_allowance_prompt_{username}"
    };
}
