using Bannister.Models;

namespace Bannister.Services;

public class PendingActivityIdeaService
{
    private readonly DatabaseService _db;
    private readonly OperationQueueService _queue;

    public PendingActivityIdeaService(DatabaseService db, OperationQueueService queue)
    {
        _db = db;
        _queue = queue;
    }

    public async Task<int> AddAsync(string username, string gameName, string activityName, string activityCategory)
    {
        var createdAt = DateTime.UtcNow;
        var normalizedUsername = username.Trim();
        var normalizedGame = gameName.Trim();
        var normalizedName = activityName.Trim();
        var normalizedCategory = activityCategory.Trim();

        if (_db.IsReadOnly)
        {
            await _queue.EnqueueAsync("pending_activity_idea_added", new
            {
                username = normalizedUsername,
                game = normalizedGame,
                activity_name = normalizedName,
                activity_category = normalizedCategory,
                created_at = createdAt
            });

            return 0;
        }

        var idea = new PendingActivityIdea
        {
            Username = normalizedUsername,
            Game = normalizedGame,
            ActivityName = normalizedName,
            ActivityCategory = normalizedCategory,
            CreatedAt = createdAt,
            Status = 0
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(idea);
        return idea.Id;
    }

    public async Task<List<PendingActivityIdea>> GetPendingForGameAsync(string username, string gameName)
    {
        var conn = await _db.GetConnectionAsync();
        var rows = await conn.Table<PendingActivityIdea>()
            .Where(x => x.Username == username && x.Game == gameName && x.Status == 0)
            .ToListAsync();

        return rows.OrderBy(x => x.CreatedAt).ToList();
    }

    public async Task<int> GetPendingCountForGameAsync(string username, string gameName)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pending_activity_ideas WHERE Username = ? AND Game = ? AND Status = 0",
            username,
            gameName);
    }

    public async Task<int> GetPendingCountForUserAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pending_activity_ideas WHERE Username = ? AND Status = 0",
            username);
    }

    public async Task<List<PendingActivityIdea>> GetPendingForUserAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        var rows = await conn.Table<PendingActivityIdea>()
            .Where(x => x.Username == username && x.Status == 0)
            .ToListAsync();

        return rows.OrderBy(x => x.CreatedAt).ToList();
    }

    public async Task MarkConvertedAsync(int id)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE pending_activity_ideas SET Status = 1, AppliedAt = ? WHERE Id = ?",
            DateTime.UtcNow,
            id);
    }

    public async Task MarkDismissedAsync(int id)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE pending_activity_ideas SET Status = 2, AppliedAt = ? WHERE Id = ?",
            DateTime.UtcNow,
            id);
    }

    public async Task<List<string>> GetCategoriesForGameAsync(string username, string gameName)
    {
        var conn = await _db.GetConnectionAsync();
        var categories = await conn.QueryScalarsAsync<string>(
            "SELECT DISTINCT TRIM(Category) FROM game_activities WHERE Username = ? AND Game = ? AND IsActive = 1 AND Category IS NOT NULL AND TRIM(Category) <> '' ORDER BY TRIM(Category) COLLATE NOCASE",
            username,
            gameName);

        var result = categories
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!result.Contains("Misc", StringComparer.OrdinalIgnoreCase))
            result.Insert(0, "Misc");
        if (!result.Contains("Negative", StringComparer.OrdinalIgnoreCase))
            result.Add("Negative");

        return result;
    }
}
