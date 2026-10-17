using SQLite;

namespace Bannister.Models;

public class ClipsExperiment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int TotalPosts { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

public class ClipsPost
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int ExperimentId { get; set; }
    public int? ExperimentItemId { get; set; }
    public string Title { get; set; } = "";
    public string Concept { get; set; } = "";
    public DateTime PostedDate { get; set; } = DateTime.Today;
    public double? Retention10s { get; set; }
    public double? Retention20s { get; set; }
    public double? Retention30s { get; set; }
    public string Notes { get; set; } = "";
}

public class ClipsDataPoint
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Username { get; set; } = "";
    [Indexed]
    public int ExperimentId { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

public class ClipsExperimentItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Username { get; set; } = "";
    [Indexed]
    public int ExperimentId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int SortOrder { get; set; }
    public bool IsRun { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
