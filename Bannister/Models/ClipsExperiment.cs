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
    public string Title { get; set; } = "";
    public string Concept { get; set; } = "";
    public DateTime PostedDate { get; set; } = DateTime.Today;
    public double? Retention10s { get; set; }
    public double? Retention20s { get; set; }
    public double? Retention30s { get; set; }
    public string Notes { get; set; } = "";
}
