using SQLite;

namespace Bannister.Models;

[Table("companions")]
public class Companion
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string Scope { get; set; } = "custom";
    public string? GameId { get; set; }
    public string Topic { get; set; } = "";
    public string PersonaDescription { get; set; } = "";
    public string InterviewStance { get; set; } = "get to know the user";
    public int DefaultInterviewMinutes { get; set; } = 20;
    public int DefaultQuestionCount { get; set; } = 10;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? AvatarPath { get; set; }
    public byte[]? AvatarImage { get; set; }
}

[Table("companion_level_images")]
public class CompanionLevelImage
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    [Indexed("IX_companion_level_images_persona_level", 1, Unique = true)]
    public int CompanionId { get; set; }
    [Indexed("IX_companion_level_images_persona_level", 2, Unique = true)]
    public int Level { get; set; }
    public byte[] ImageBytes { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("companion_data_points")]
public class CompanionDataPoint
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    [Indexed] public int CompanionId { get; set; }
    public string Text { get; set; } = "";
    public int Priority { get; set; } = 3;
    public string Status { get; set; } = "active";
    public string Source { get; set; } = "manual";
    public int? InterviewId { get; set; }
    public int? IdeaId { get; set; }
    public int? ReplacedByPointId { get; set; }
    public int? ReplacesPointId { get; set; }
    public string? GameId { get; set; }
    public string? Category { get; set; }
    /// <summary>0 = Private, 1 = Public, 2 = Both; null follows the game.</summary>
    public int? VisibilityOverride { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("companion_interviews")]
public class CompanionInterview
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    [Indexed] public int CompanionId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public int Minutes { get; set; }
    public int QuestionCount { get; set; } = 10;
    public int? IdeaId { get; set; }
    public string Transcript { get; set; } = "";
    public string Notes { get; set; } = "";
    public int PointsAdded { get; set; }
    public int PointsReprioritized { get; set; }
    public int PointsArchived { get; set; }
    public bool ExpAwarded { get; set; }
}
