using Bannister.Data;
using Bannister.Models;

namespace Bannister.Services;

public class ActivitySuggestionService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public ActivitySuggestionService(DatabaseService db) => _db = db;

    public async Task InitAsync()
    {
        if (_initialized) return;
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<ActivitySuggestionLog>();
        _initialized = true;
    }

    public async Task<List<ActivitySuggestion>> GetUnseenSuggestionsAsync(
        string username, string gameId, string gameDisplayName)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var seenIds = (await conn.Table<ActivitySuggestionLog>()
                .Where(l => l.Username == username && l.GameId == gameId)
                .ToListAsync())
            .Where(l => l.WasSeen)
            .Select(l => l.SuggestionId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return ActivitySuggestions.All
            .Where(s => (s.GameTag == null ||
                         gameDisplayName.Contains(
                             s.GameTag,
                             StringComparison.OrdinalIgnoreCase)) &&
                        !seenIds.Contains(s.Id))
            .ToList();
    }

    public async Task MarkSeenAsync(
        string username, string gameId, string suggestionId)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var log = await conn.Table<ActivitySuggestionLog>()
            .Where(l => l.Username == username &&
                        l.GameId == gameId &&
                        l.SuggestionId == suggestionId)
            .FirstOrDefaultAsync();
        if (log == null)
        {
            await conn.InsertAsync(new ActivitySuggestionLog
            {
                Username = username,
                GameId = gameId,
                SuggestionId = suggestionId,
                WasSeen = true
            });
        }
        else
        {
            log.WasSeen = true;
            await conn.UpdateAsync(log);
        }
    }
}
