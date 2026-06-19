using Bannister.Models;

namespace Bannister.Services;

public class WebsiteProjectService
{
    private readonly DatabaseService _db;

    public WebsiteProjectService(DatabaseService db)
    {
        _db = db;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private void EnsureWritable()
    {
        if (_db.IsReadOnly)
            throw new ReadOnlyDatabaseException("Website projects are read-only on secondary devices.");
    }

    public async Task<List<WebsiteProject>> GetAllForUserAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        var projects = await conn.Table<WebsiteProject>()
            .Where(project => project.Username == username)
            .ToListAsync();

        return projects
            .OrderByDescending(project => project.UpdatedAt)
            .ThenBy(project => project.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<int> SaveAsync(WebsiteProject project)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        project.UpdatedAt = DateTime.UtcNow;

        if (project.Id == 0)
        {
            project.CreatedAt = DateTime.UtcNow;
            await conn.InsertAsync(project);
        }
        else
        {
            await conn.UpdateAsync(project);
        }

        return project.Id;
    }

    public async Task<WebsiteProject?> GetByIdAsync(int id)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<WebsiteProject>().FirstOrDefaultAsync(project => project.Id == id);
    }

    public async Task DeleteAsync(int id)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        var project = await GetByIdAsync(id);
        if (project != null)
            await conn.DeleteAsync(project);
    }
}
