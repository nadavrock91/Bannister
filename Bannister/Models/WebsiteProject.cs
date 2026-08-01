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

    public string DeploymentUrl { get; set; } = "";

    public string DeployCommand { get; set; } = "";

    public string ProjectSummary { get; set; } = "";

    public int TasksSinceSummaryUpdate { get; set; } = 0;

    public string VisionRaw { get; set; } = "";

    public string VisionRefined { get; set; } = "";

    public string CompletedTaskTitles { get; set; } = "";

    public string CommitStatements { get; set; } = "";

    public string StuckAnalysisJson { get; set; } = "";

    public int WorkflowState { get; set; } = 0;

    public string PendingTaskTitle { get; set; } = "";

    public string PendingCodexPrompt { get; set; } = "";

    public string PendingCommitMessage { get; set; } = "";

    public int PendingBatchSize { get; set; } = 1;

    public string QueuedTasksJson { get; set; } = "";

    public int QueuedTasksIndex { get; set; } = 0;

    // Stores JSON of the picked QA items for the current batch: [{Category, Title, Body}]
    public string PendingPickedItemsJson { get; set; } = "";

    // Stores JSON array of past batch verification results
    public string BatchVerificationHistoryJson { get; set; } = "";

    // JSON array of blocked QA item titles that should be skipped by PickFive
    public string BlockedQAItemsJson { get; set; } = "";

    // === ADD MISSING FOCUS MODE ===

    // The selected MISSING item title being actively worked on
    public string ActiveMissingTitle { get; set; } = "";

    // Detailed description of the MISSING item
    public string ActiveMissingDetail { get; set; } = "";

    // Number of task cycles completed toward this missing item
    public int ActiveMissingTaskCount { get; set; } = 0;

    // QA report specific to progress on the active missing item
    public string ActiveMissingQAReport { get; set; } = "";

    public string? LatestQAReport { get; set; }

    public DateTime? LatestQAReportCapturedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed record WebsiteQueuedTask(string Title, string CodexPrompt);
