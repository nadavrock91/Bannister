using SQLite;

namespace Bannister.Models;

[Table("music_projects")]
public class MusicProject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public string MusicConversationLog { get; set; } = "";

    public string GeneralMusicDescription { get; set; } = "";

    public string TimestampedNarration { get; set; } = "";

    public string ProjectCategory { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public string Status { get; set; } = "active";

    public int? ParentProjectId { get; set; }

    public int DraftVersion { get; set; } = 1;

    public string DraftSource { get; set; } = "manual";

    public bool IsLatest { get; set; }

    public int? CompareToProjectId { get; set; }

    public bool IsPublished { get; set; }

    public DateTime? PublishedAt { get; set; }

    public int ProjectedClipCount { get; set; }

    public int ProjectedDays { get; set; }

    public int FinalClipCount { get; set; }

    [Ignore]
    public bool IsActive => Status == "active";

    [Ignore]
    public bool IsCompleted => Status == "completed";

    [Ignore]
    public bool IsDraft => ParentProjectId != null;

    [Ignore]
    public int ActualDays => IsPublished && PublishedAt.HasValue
        ? (int)(PublishedAt.Value.Date - CreatedAt.Date).TotalDays + 1
        : (int)(DateTime.UtcNow.Date - CreatedAt.Date).TotalDays + 1;

    [Ignore]
    public string DisplayName => DraftVersion > 1 ? $"{Name} (Draft v{DraftVersion})" : Name;
}

[Table("music_lines")]
public class MusicLine
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ProjectId { get; set; }

    public int LineOrder { get; set; }

    public string Music { get; set; } = "";

    public string Script { get; set; } = "";

    public string Visuals { get; set; } = "";

    public string ProductionNotes { get; set; } = "";

    public string TargetEmotion { get; set; } = "";

    public string RhythmIntent { get; set; } = "";

    public string LayerNotes { get; set; } = "";

    public string SectionDecision { get; set; } = "";

    public int? AssignedCueId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ModifiedAt { get; set; }
}

[Table("music_cues")]
public class MusicCue
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ProjectId { get; set; }

    public string Label { get; set; } = "";

    public bool IsPrimaryDNA { get; set; }

    public int? ParentCueId { get; set; }

    public string VariationType { get; set; } = "Original";

    public string Mood { get; set; } = "";

    public string Pulse { get; set; } = "";

    public string Motif { get; set; } = "";

    public string EnergyLevel { get; set; } = "Medium";

    public bool MustLoop { get; set; }

    public bool MustSitUnderNarration { get; set; }

    public string GeneratedPrompt { get; set; } = "";

    public string Status { get; set; } = "NotGenerated";

    public string ReuseFlag { get; set; } = "Reusable";

    public string Notes { get; set; } = "";

    public int DurationSeconds { get; set; } = 30;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("music_prompt_templates")]
public class MusicPromptTemplate
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Name { get; set; } = "";

    public string TemplateText { get; set; } = "";

    public bool IsTimestamped { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
