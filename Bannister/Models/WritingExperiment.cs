using SQLite;

namespace Bannister.Models;

public class WritingExperiment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string BaselineProcessName { get; set; } = "";
    public string ChallengerProcessName { get; set; } = "";
    public string Phase { get; set; } = "baseline"; // baseline, challenger, completed
    public int CurrentWeek { get; set; } = 1;
    public string Status { get; set; } = "active"; // active, archived
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public class WritingExperimentEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int ExperimentId { get; set; }
    public DateTime Date { get; set; }
    public string AssignedProcess { get; set; } = "";
    public bool IsBaseline { get; set; } = true;
    public string ExecutionNotes { get; set; } = ""; // how exactly the process was followed
    public int RetentionScore { get; set; } = 0; // 1-10
    public string RetentionNotes { get; set; } = "";
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }
}
