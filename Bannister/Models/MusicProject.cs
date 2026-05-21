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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ModifiedAt { get; set; }
}
