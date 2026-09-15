using Bannister.Models;

namespace Bannister.Services;

public class ContextMenuOrderService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public static readonly List<(string Key, string Label)>
        DefaultItems = new()
    {
        ("edit_activity",       "✏️ Edit Activity"),
        ("edit_category",       "Edit Category"),
        ("set_multiplier",      "Set Multiplier"),
        ("applied_x_times",     "Applied X Times (one-time)"),
        ("update_streak",       "Update Streak Values"),
        ("times_completed",     "Times Completed"),
        ("notes",               " Add/Edit Notes"),
        ("duplicate_negative",  "Duplicate as Negative"),
        ("manual_priority",     "Set Manual Priority"),
        ("auto_award",          "Set Auto-Award"),
        ("move_game",           " Move to Another Game"),
        ("assign_grouping",     " Assign to Grouping"),
        ("disable",             "⏸️ Disable Activity"),
        ("remove",              "️ Remove Activity"),
        ("public_toggle",       " Mark as Public"),
    };

    public ContextMenuOrderService(DatabaseService db)
    {
        _db = db;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await _db.EnsureTableAsync<ContextMenuOrderSetting>();
    }

    public async Task<List<string>> GetOrderedKeysAsync(
        string username)
    {
        await EnsureInitializedAsync();
        try
        {
            var conn = await _db.GetConnectionAsync();
            var rows = await conn
                .Table<ContextMenuOrderSetting>()
                .Where(r => r.Username == username)
                .ToListAsync();

            if (rows.Count == 0)
            {
                await SeedDefaultsAsync(username, conn);
                rows = await conn
                    .Table<ContextMenuOrderSetting>()
                    .Where(r => r.Username == username)
                    .ToListAsync();
            }

            var existingKeys = rows
                .Select(r => r.OptionKey)
                .ToHashSet();
            int maxOrder = rows.Count > 0
                ? rows.Max(r => r.SortOrder) : 0;
            foreach (var (key, _) in DefaultItems)
            {
                if (!existingKeys.Contains(key))
                {
                    maxOrder++;
                    await conn.InsertAsync(
                        new ContextMenuOrderSetting
                        {
                            Username = username,
                            OptionKey = key,
                            SortOrder = maxOrder
                        });
                    rows.Add(new ContextMenuOrderSetting
                    {
                        Username = username,
                        OptionKey = key,
                        SortOrder = maxOrder
                    });
                }
            }

            return rows
                .OrderBy(r => r.SortOrder)
                .Select(r => r.OptionKey)
                .ToList();
        }
        catch
        {
            return DefaultItems.Select(i => i.Key).ToList();
        }
    }

    public async Task SaveOrderAsync(
        string username, List<string> orderedKeys)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        for (int i = 0; i < orderedKeys.Count; i++)
        {
            var existing = await conn
                .Table<ContextMenuOrderSetting>()
                .Where(r => r.Username == username
                    && r.OptionKey == orderedKeys[i])
                .FirstOrDefaultAsync();
            if (existing != null)
            {
                existing.SortOrder = i;
                await conn.UpdateAsync(existing);
            }
            else
            {
                await conn.InsertAsync(
                    new ContextMenuOrderSetting
                    {
                        Username = username,
                        OptionKey = orderedKeys[i],
                        SortOrder = i
                    });
            }
        }
    }

    private async Task SeedDefaultsAsync(
        string username,
        SQLite.ISQLiteAsyncConnection conn)
    {
        for (int i = 0; i < DefaultItems.Count; i++)
        {
            await conn.InsertAsync(new ContextMenuOrderSetting
            {
                Username = username,
                OptionKey = DefaultItems[i].Key,
                SortOrder = i
            });
        }
    }

    public static string KeyToLabel(string key,
        string? dynamicLabel = null)
    {
        if (dynamicLabel != null) return dynamicLabel;
        var item = DefaultItems.FirstOrDefault(i => i.Key == key);
        return item.Label ?? key;
    }
}
