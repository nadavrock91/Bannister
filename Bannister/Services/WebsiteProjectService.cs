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

    public async Task<bool> LogAndIncrementTaskAsync(int projectId, string taskTitle)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null)
            return false;

        if (!string.IsNullOrWhiteSpace(taskTitle))
        {
            var entry = $"{DateTime.Today:yyyy-MM-dd}: {taskTitle.Trim()}";
            project.CompletedTaskTitles = string.IsNullOrWhiteSpace(project.CompletedTaskTitles)
                ? entry
                : $"{entry}\n{project.CompletedTaskTitles}";
        }

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

    public async Task<bool> SetVisionRawAsync(int projectId, string vision)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null)
            return false;

        project.VisionRaw = vision;
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> SetVisionRefinedAsync(int projectId, string vision)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null)
            return false;

        project.VisionRefined = vision;
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> SetCompletedTaskTitlesAsync(int projectId, string titles)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null)
            return false;

        project.CompletedTaskTitles = titles;
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> PrependTaskTitleAsync(int projectId, string title)
    {
        EnsureWritable();
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var project = await GetByIdAsync(projectId);
        if (project == null)
            return false;

        var entry = $"{DateTime.Today:yyyy-MM-dd}: {title.Trim()}";
        project.CompletedTaskTitles = string.IsNullOrWhiteSpace(project.CompletedTaskTitles)
            ? entry
            : $"{entry}\n{project.CompletedTaskTitles}";
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> SetWorkflowStateAsync(int projectId, int state)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null)
            return false;

        project.WorkflowState = state;
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> SetPendingTaskTitleAsync(int projectId, string title)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null)
            return false;

        project.PendingTaskTitle = title;
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> SetPendingCodexPromptAsync(int projectId, string prompt)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null)
            return false;

        project.PendingCodexPrompt = prompt;
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> SetPendingCommitMessageAsync(int projectId, string message)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null)
            return false;

        project.PendingCommitMessage = message;
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> SetLatestQAReportAsync(int projectId, string report)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null)
            return false;

        var trimmedReport = report.Trim();
        project.LatestQAReport = trimmedReport;
        project.LatestQAReportCapturedAt = string.IsNullOrWhiteSpace(trimmedReport)
            ? null
            : DateTime.UtcNow;
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> ClearLatestQAReportAsync(int projectId)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null)
            return false;

        project.LatestQAReport = "";
        project.LatestQAReportCapturedAt = null;
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> AdvanceToWaitingForLLMAsync(int projectId)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null || project.WorkflowState != 0)
            return false;

        project.WorkflowState = 1;
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> AdvanceToReadyToExecuteAsync(int projectId, string taskTitle, string codexPrompt)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null || project.WorkflowState != 1)
            return false;

        project.PendingTaskTitle = taskTitle;
        project.PendingCodexPrompt = codexPrompt;
        project.PendingCommitMessage = "";
        project.WorkflowState = 2;
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> AdvanceToReadyToCommitAsync(int projectId, string commitMessage)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null || project.WorkflowState != 2)
            return false;

        project.PendingCommitMessage = commitMessage;
        project.WorkflowState = 3;
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> ResetWorkflowAsync(int projectId)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null)
            return false;

        project.PendingTaskTitle = "";
        project.PendingCodexPrompt = "";
        project.PendingCommitMessage = "";
        project.WorkflowState = 0;
        await SaveAsync(project);
        return true;
    }

    public async Task<bool> CompleteWorkflowAsync(int projectId, string taskTitle)
    {
        EnsureWritable();
        var project = await GetByIdAsync(projectId);
        if (project == null || project.WorkflowState != 3)
            return false;

        if (!string.IsNullOrWhiteSpace(taskTitle))
        {
            var entry = $"{DateTime.Today:yyyy-MM-dd}: {taskTitle.Trim()}";
            project.CompletedTaskTitles = string.IsNullOrWhiteSpace(project.CompletedTaskTitles)
                ? entry
                : $"{entry}\n{project.CompletedTaskTitles}";
        }

        project.TaskCount++;
        project.TasksSinceSummaryUpdate++;
        project.PendingTaskTitle = "";
        project.PendingCodexPrompt = "";
        project.PendingCommitMessage = "";
        project.LatestQAReport = "";
        project.LatestQAReportCapturedAt = null;
        project.WorkflowState = 0;
        await SaveAsync(project);
        return true;
    }
}
