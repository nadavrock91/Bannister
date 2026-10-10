using SQLite;

namespace Bannister.Models;

[Table("shorts_cutter_projects")]
public class ShortsCutterProject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Name { get; set; } = "";

    public string VideoPath { get; set; } = "";

    public string TranscriptJson { get; set; } = "";

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
}

[Table("shorts_cutter_cuts")]
public class ShortsCutterCut
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ProjectId { get; set; }

    public string Title { get; set; } = "";

    public string StartTime { get; set; } = "";

    public string EndTime { get; set; } = "";

    public int HookRating { get; set; }

    public int ContentRating { get; set; }

    public string TranscriptText { get; set; } = "";

    public int SortOrder { get; set; }
}
