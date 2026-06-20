using Bannister.Models;

namespace Bannister.Services;

public class WebsiteIdeaService
{
    private readonly DatabaseService _db;

    public WebsiteIdeaService(DatabaseService db)
    {
        _db = db;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private void EnsureWritable()
    {
        if (_db.IsReadOnly)
            throw new ReadOnlyDatabaseException("Website ideas are read-only on secondary devices.");
    }

    public async Task<List<WebsiteIdea>> GetAllForUserAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        var ideas = await conn.Table<WebsiteIdea>()
            .Where(idea => idea.Username == username)
            .ToListAsync();

        return ideas
            .OrderByDescending(idea => idea.UpdatedAt)
            .ThenBy(idea => idea.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<int> SaveAsync(WebsiteIdea idea)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        idea.UpdatedAt = DateTime.UtcNow;

        if (idea.Id == 0)
        {
            idea.CreatedAt = DateTime.UtcNow;
            await conn.InsertAsync(idea);
        }
        else
        {
            await conn.UpdateAsync(idea);
        }

        return idea.Id;
    }

    public async Task<WebsiteIdea?> GetByIdAsync(int id)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<WebsiteIdea>().FirstOrDefaultAsync(idea => idea.Id == id);
    }

    public async Task DeleteAsync(int id)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        var idea = await GetByIdAsync(id);
        if (idea != null)
            await conn.DeleteAsync(idea);
    }
}
