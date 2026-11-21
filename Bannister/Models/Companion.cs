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
}
