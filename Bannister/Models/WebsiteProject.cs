using SQLite;

namespace Bannister.Models;

[Table("website_projects")]
public class WebsiteProject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Title { get; set; } = "";

    public string IdeaText { get; set; } = "";

    public int TaskCount { get; set; } = 0;

    public int TaskTarget { get; set; } = 1000;

    public string CodebasePath { get; set; } = "";

    public string ProjectSummary { get; set; } = "";

    public int TasksSinceSummaryUpdate { get; set; } = 0;

    public string VisionRaw { get; set; } = "";

    public string VisionRefined { get; set; } = "";

    public string CompletedTaskTitles { get; set; } = "";

    public int WorkflowState { get; set; } = 0;

    public string PendingTaskTitle { get; set; } = "";

    public string PendingCodexPrompt { get; set; } = "";

    public string PendingCommitMessage { get; set; } = "";

    public int PendingBatchSize { get; set; } = 1;

    public string QueuedTasksJson { get; set; } = "";

    public int QueuedTasksIndex { get; set; } = 0;

    public string? LatestQAReport { get; set; }

    public DateTime? LatestQAReportCapturedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed record WebsiteQueuedTask(string Title, string CodexPrompt);
