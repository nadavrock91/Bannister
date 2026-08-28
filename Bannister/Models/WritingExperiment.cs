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
    public string ProcessQueueJson { get; set; } = ""; // JSON array of {WeekNumber, ProcessName} for future weeks
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
    public string StoryTitle { get; set; } = "";
    public bool IsBaseline { get; set; } = true;
    public string ExecutionNotes { get; set; } = ""; // how exactly the process was followed
    public int Retention10s { get; set; } = -1; // -1 = not recorded, 0-100 percent
    public int Retention30s { get; set; } = -1;
    public int Retention1m { get; set; } = -1;
    public int Retention2m { get; set; } = -1;
    public int Retention3m { get; set; } = -1;
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }
}
