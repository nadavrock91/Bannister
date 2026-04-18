using Bannister.Models;

namespace Bannister.Services;

/// <summary>
/// Service for managing ideas - CRUD operations and organization.
/// </summary>
public class IdeasService
{
    private readonly DatabaseService _db;
    private bool _initialized = false;

    public IdeasService(DatabaseService db)
    {
        _db = db;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<IdeaItem>();
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
        var all = await conn.Table<IdeaItem>()
            .Where(i => i.Username == username && i.Category == category)
            .ToListAsync();
        
        return all
            .Where(i => i.Status != 3)
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
        var all = await conn.Table<IdeaItem>()
            .Where(i => i.Username == username)
            .ToListAsync();
        
        return all
            .Where(i => i.Status == 3)
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
        var all = await conn.Table<IdeaItem>()
            .Where(i => i.Username == username && i.IsStarred)
            .ToListAsync();
        
        return all
            .Where(i => i.Status != 3)
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
        var ideas = await conn.Table<IdeaItem>()
            .Where(i => i.Username == username)
            .ToListAsync();
        
        return ideas
            .Select(i => i.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }

    /// <summary>
    /// Create a new idea
    /// </summary>
    public async Task<IdeaItem> CreateIdeaAsync(string username, string title, string category, string? notes = null)
    {
        await EnsureInitializedAsync();
        
        var idea = new IdeaItem
        {
            Username = username,
            Title = title.Trim(),
            Category = category.Trim(),
            Notes = notes?.Trim(),
            CreatedAt = DateTime.Now
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
        var ideas = await conn.Table<IdeaItem>()
            .Where(i => i.Username == username)
            .ToListAsync();
        
        var active = ideas.Where(i => i.Status != 3).ToList();
        
        return (
            active.Count,
            active.Count(i => i.IsStarred),
            active.Count(i => i.Status == 1),
            active.Count(i => i.Status == 2)
        );
    }

    #endregion
}
