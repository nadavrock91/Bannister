using SQLite;

namespace Bannister.Models;

[Table("weekly_method_experiments")]
public class WeeklyMethodExperiment
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Username { get; set; } = "";
    public string WeekLabel { get; set; } = "";
    public string MethodDescription { get; set; } = "";
    public int? StartingImagesGenerated { get; set; }
    public int? VideoGenerationsUsed { get; set; }
    public int? ClipsPosted { get; set; }
    public double? GenerationCost { get; set; }
    public string Notes { get; set; } = "";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public bool IsArchived { get; set; }
}

[Table("clip_experiments")]
public class ClipExperiment
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Username { get; set; } = "";
    public int? WeeklyMethodExperimentId { get; set; }
    public string Hypothesis { get; set; } = "";
    public string VariableTested { get; set; } = "";
    public string ControlConstant { get; set; } = "";
    public string GenerationMethodVersion { get; set; } = "";
    public string Platform { get; set; } = "";
    public DateTime? PostedDate { get; set; }
    public double? Retention10s { get; set; }
    public double? Retention20s { get; set; }
    public double? Retention30s { get; set; }
    public int? PredictedRetentionRating { get; set; }
    public int? ActualRetentionRating { get; set; }
    public string Notes { get; set; } = "";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
