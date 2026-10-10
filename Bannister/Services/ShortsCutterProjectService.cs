using Bannister.Models;

namespace Bannister.Services;

public class ShortsCutterProjectService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public ShortsCutterProjectService(DatabaseService db)
    {
        _db = db;
    }

    public async Task InitAsync()
    {
        if (_initialized) return;
        if (!_db.IsReadOnly)
        {
            await _db.EnsureTableAsync<ShortsCutterProject>();
            await _db.EnsureTableAsync<ShortsCutterCut>();
        }
        _initialized = true;
    }

    public async Task<List<ShortsCutterProject>> GetProjectsAsync(string username)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<ShortsCutterProject>()
            .Where(p => p.Username == username)
            .OrderByDescending(p => p.UpdatedDate)
            .ToListAsync();
    }

    public async Task<ShortsCutterProject?> GetProjectAsync(int id)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<ShortsCutterProject>()
            .Where(p => p.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task SaveProjectAsync(ShortsCutterProject project)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        var now = DateTime.UtcNow;
        if (project.CreatedDate == default)
            project.CreatedDate = now;
        project.UpdatedDate = now;

        var conn = await _db.GetConnectionAsync();
        if (project.Id == 0)
            await conn.InsertAsync(project);
        else
            await conn.UpdateAsync(project);
    }

    public async Task DeleteProjectAsync(int id)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        var conn = await _db.GetConnectionAsync();
        var cuts = await GetCutsAsync(id);
        foreach (var cut in cuts)
            await conn.DeleteAsync(cut);
        await conn.DeleteAsync<ShortsCutterProject>(id);
    }

    public async Task<List<ShortsCutterCut>> GetCutsAsync(int projectId)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<ShortsCutterCut>()
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();
    }

    public async Task SaveCutAsync(ShortsCutterCut cut)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        var conn = await _db.GetConnectionAsync();
        if (cut.Id == 0)
            await conn.InsertAsync(cut);
        else
            await conn.UpdateAsync(cut);
    }

    public async Task DeleteCutAsync(int id)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<ShortsCutterCut>(id);
    }

    public async Task AddCutsToProjectAsync(int projectId, List<ShortsCutterCut> newCuts)
    {
        await InitAsync();
        if (_db.IsReadOnly || newCuts.Count == 0) return;

        var existing = await GetCutsAsync(projectId);
        int sortOrder = existing.Count == 0 ? 0 : existing.Max(c => c.SortOrder) + 1;

        foreach (var cut in newCuts)
        {
            cut.ProjectId = projectId;
            cut.SortOrder = sortOrder++;
            await SaveCutAsync(cut);
        }
    }
}
