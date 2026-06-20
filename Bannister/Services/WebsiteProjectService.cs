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

    public async Task<bool> IncrementTaskCountAsync(int projectId)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null)
            return false;

        project.TaskCount++;
        project.TasksSinceSummaryUpdate++;
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> DecrementTaskCountAsync(int projectId)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null || project.TaskCount <= 0)
            return false;

        project.TaskCount = Math.Max(0, project.TaskCount - 1);
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> SetTaskCountAsync(int projectId, int newCount)
    {
        EnsureWritable();
        if (newCount < 0)
            return false;

        var project = await GetByIdAsync(projectId);
        if (project == null)
            return false;

        project.TaskCount = newCount;
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> SetTaskTargetAsync(int projectId, int newTarget)
    {
        EnsureWritable();
        if (newTarget <= 0)
            return false;

        var project = await GetByIdAsync(projectId);
        if (project == null)
            return false;

        project.TaskTarget = newTarget;
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> SetCodebasePathAsync(int projectId, string path)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null)
            return false;

        project.CodebasePath = path;
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> SetProjectSummaryAsync(int projectId, string summary)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null)
            return false;

        project.ProjectSummary = summary;
        project.TasksSinceSummaryUpdate = 0;
        await SaveAsync(project);
        return true;
    }
}
