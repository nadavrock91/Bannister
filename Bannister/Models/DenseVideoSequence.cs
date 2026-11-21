using SQLite;

namespace Bannister.Models;

[Table("dense_video_sequences")]
public class DenseVideoSequence
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    public string Name { get; set; } = "";
    public string SequenceType { get; set; } = "short clip";
    public string StoryName { get; set; } = "";
    [Indexed] public int? ExperimentId { get; set; }
    public int? SourceGridImageId { get; set; }
    public string InitialThumbnailPath { get; set; } = "";
    public bool IsDone { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("dense_video_generations")]
public class DenseVideoGeneration
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    [Indexed] public int SequenceId { get; set; }
    public int SequenceOrder { get; set; }
    public string StartingThumbnailPath { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string TransitionType { get; set; } = "";
    public string ResultClipPath { get; set; } = "";
    public string Status { get; set; } = "pending";
    public double? UsableStartSeconds { get; set; }
    public double? UsableEndSeconds { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Ignore]
    public double UsableSeconds => Math.Max(0,
        (UsableEndSeconds ?? 0) - (UsableStartSeconds ?? 0));
}

[Table("dense_sequence_experiments")]
public class DenseSequenceExperiment
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("dense_concept_grids")]
public class DenseConceptGrid
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    [Indexed] public int ExperimentId { get; set; }
    public int GridNumber { get; set; }
    public string Title { get; set; } = "";
}

[Table("dense_concept_grid_images")]
public class DenseConceptGridImage
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    [Indexed] public int ExperimentId { get; set; }
    [Indexed] public int GridId { get; set; }
    public string ImagePath { get; set; } = "";
    public bool IsSelectedStart { get; set; }
    public int? SequenceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DenseSequenceStats
{
    public string Key { get; set; } = "";
    public int Attempts { get; set; }
    public int UsableCount { get; set; }
    public double SuccessRate => Attempts == 0 ? 0 : 100.0 * UsableCount / Attempts;
    public double AverageUsableSeconds { get; set; }
    public double TotalUsableSeconds { get; set; }
    public int FailedAttempts { get; set; }
}
