using Bannister.Models;

namespace Bannister.Services;

/// <summary>
/// Service for managing image production projects.
/// </summary>
public class ImageProductionService
{
    private readonly DatabaseService _db;
    private bool _initialized = false;

    public ImageProductionService(DatabaseService db)
    {
        _db = db;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<ImageProject>();
        _initialized = true;
    }

    public async Task<List<ImageProject>> GetActiveProjectsAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var all = await conn.Table<ImageProject>()
            .Where(p => p.Username == username && p.Status == 0)
            .ToListAsync();
        return all.OrderByDescending(p => p.ModifiedAt ?? p.CreatedAt).ToList();
    }

    public async Task<ImageProject> CreateProjectAsync(string username, string name)
    {
        await EnsureInitializedAsync();
        var project = new ImageProject
        {
            Username = username,
            Name = name.Trim(),
            CreatedAt = DateTime.Now
        };
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(project);
        return project;
    }

    public async Task UpdateProjectAsync(ImageProject project)
    {
        await EnsureInitializedAsync();
        project.ModifiedAt = DateTime.Now;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(project);
    }

    public async Task DeleteProjectAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<ImageProject>(id);
    }
}
