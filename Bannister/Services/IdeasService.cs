using Bannister.Models;

namespace Bannister.Services;

/// <summary>
/// Service for managing ideas - CRUD operations and organization.
/// </summary>
public class IdeasService
{
    private readonly DatabaseService _db;
    private readonly OperationQueueService _queue;
    private bool _initialized = false;

    public IdeasService(DatabaseService db, OperationQueueService queue)
    {
        _db = db;
        _queue = queue;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly)
        {
            await conn.CreateTableAsync<IdeaItem>();
            try { await conn.ExecuteAsync("ALTER TABLE ideas ADD COLUMN FullIdea TEXT DEFAULT ''"); } catch { }
        }
        _initialized = true;
    }

    #region CRUD Operations

    /// <summary>
    /// Get all active ideas (not archived) for a user
    /// </summary>
    public async Task<List<IdeaItem>> GetIdeasAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var all = await conn.Table<IdeaItem>()
            .Where(i => i.Username == username)
            .ToListAsync();
        
        return all
            .Where(i => i.Status != 3) // Exclude archived
            .OrderByDescending(i => i.IsStarred)
            .ThenByDescending(i => i.Priority)
            .ThenByDescending(i => i.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Get ideas by category
    /// </summary>
    public async Task<List<IdeaItem>> GetIdeasByCategoryAsync(string username, string category)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var ideas = await conn.QueryAsync<IdeaItem>(
            "SELECT * FROM ideas WHERE Username = ? AND Category = ? COLLATE NOCASE AND Status != 3",
            username,
            category);

        return ideas
            .OrderByDescending(i => i.IsStarred)
            .ThenByDescending(i => i.Priority)
            .ThenByDescending(i => i.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Get archived ideas
    /// </summary>
    public async Task<List<IdeaItem>> GetArchivedIdeasAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var ideas = await conn.QueryAsync<IdeaItem>(
            "SELECT * FROM ideas WHERE Username = ? AND Status = 3",
            username);

        return ideas
            .OrderByDescending(i => i.ModifiedAt ?? i.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Get starred ideas
    /// </summary>
    public async Task<List<IdeaItem>> GetStarredIdeasAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var ideas = await conn.QueryAsync<IdeaItem>(
            "SELECT * FROM ideas WHERE Username = ? AND IsStarred = 1 AND Status != 3",
            username);

        return ideas
            .OrderByDescending(i => i.Priority)
            .ThenByDescending(i => i.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Get all categories for a user
    /// </summary>
    public async Task<List<string>> GetCategoriesAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var categories = await conn.QueryScalarsAsync<string>(
            "SELECT DISTINCT TRIM(Category) FROM ideas WHERE Username = ? AND Category IS NOT NULL AND TRIM(Category) <> '' ORDER BY TRIM(Category) COLLATE NOCASE",
            username);

        return categories
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();
    }

    /// <summary>
    /// Create a new idea
    /// </summary>
    public async Task<IdeaItem> CreateIdeaAsync(
        string username,
        string title,
        string category,
        string? notes = null,
        string? subcategory = null,
        int rating = 50,
        bool isStarred = false,
        int status = 0,
        DateTime? createdAt = null,
        string? fullIdea = null)
    {
        if (_db.IsReadOnly)
        {
            var createdAtUtc = createdAt ?? DateTime.UtcNow;
            var queuedIdea = new IdeaItem
            {
                Username = username,
                Title = title.Trim(),
                Category = category.Trim(),
                FullIdea = fullIdea ?? "",
                Notes = notes?.Trim(),
                Subcategory = string.IsNullOrWhiteSpace(subcategory) ? null : subcategory.Trim(),
                Rating = Math.Clamp(rating, 0, 100),
                IsStarred = isStarred,
                Status = status,
                CreatedAt = createdAtUtc
            };

            await _queue.EnqueueAsync("idea_logged", new
            {
                username = queuedIdea.Username,
                title = queuedIdea.Title,
                category = queuedIdea.Category,
                full_idea = queuedIdea.FullIdea,
                subcategory = queuedIdea.Subcategory,
                rating = queuedIdea.Rating,
                notes = queuedIdea.Notes,
                is_starred = queuedIdea.IsStarred,
                status = queuedIdea.Status,
                created_at = queuedIdea.CreatedAt
            });

            return queuedIdea;
        }

        await EnsureInitializedAsync();
        
        var idea = new IdeaItem
        {
            Username = username,
            Title = title.Trim(),
            Category = category.Trim(),
            FullIdea = fullIdea ?? "",
            Subcategory = string.IsNullOrWhiteSpace(subcategory) ? null : subcategory.Trim(),
            Notes = notes?.Trim(),
            Rating = Math.Clamp(rating, 0, 100),
            IsStarred = isStarred,
            Status = status,
            CreatedAt = createdAt ?? DateTime.Now
        };
        
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(idea);
        return idea;
    }

    /// <summary>
    /// Update an idea
    /// </summary>
    public async Task UpdateIdeaAsync(IdeaItem idea)
    {
        await EnsureInitializedAsync();
        idea.ModifiedAt = DateTime.Now;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(idea);
    }

    /// <summary>
    /// Delete an idea permanently
    /// </summary>
    public async Task DeleteIdeaAsync(int ideaId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<IdeaItem>(ideaId);
    }

    /// <summary>
    /// Archive an idea (soft delete)
    /// </summary>
    public async Task ArchiveIdeaAsync(int ideaId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var idea = await conn.GetAsync<IdeaItem>(ideaId);
        if (idea != null)
        {
            idea.Status = 3;
            idea.ModifiedAt = DateTime.Now;
            await conn.UpdateAsync(idea);
        }
    }

    /// <summary>
    /// Restore an archived idea
    /// </summary>
    public async Task RestoreIdeaAsync(int ideaId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var idea = await conn.GetAsync<IdeaItem>(ideaId);
        if (idea != null)
        {
            idea.Status = 0;
            idea.ModifiedAt = DateTime.Now;
            await conn.UpdateAsync(idea);
        }
    }

    /// <summary>
    /// Toggle starred status
    /// </summary>
    public async Task ToggleStarAsync(int ideaId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var idea = await conn.GetAsync<IdeaItem>(ideaId);
        if (idea != null)
        {
            idea.IsStarred = !idea.IsStarred;
            idea.ModifiedAt = DateTime.Now;
            await conn.UpdateAsync(idea);
        }
    }

    /// <summary>
    /// Mark idea as done
    /// </summary>
    public async Task MarkDoneAsync(int ideaId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var idea = await conn.GetAsync<IdeaItem>(ideaId);
        if (idea != null)
        {
            idea.Status = 2;
            idea.CompletedAt = DateTime.Now;
            idea.ModifiedAt = DateTime.Now;
            await conn.UpdateAsync(idea);
        }
    }

    /// <summary>
    /// Get stats for display
    /// </summary>
    public async Task<(int total, int starred, int inProgress, int done)> GetStatsAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();

        return (
            await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ideas WHERE Username = ? AND Status != 3", username),
            await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ideas WHERE Username = ? AND Status != 3 AND IsStarred = 1", username),
            await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ideas WHERE Username = ? AND Status = 1", username),
            await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ideas WHERE Username = ? AND Status = 2", username)
        );
    }

    #endregion
}
