using SQLite;

namespace Bannister.Models;

[Table("feats")]
public class Feat
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Title { get; set; } = "";

    public int SortOrder { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public bool IsArchived { get; set; }
}

[Table("daily_feats")]
public class DailyFeat
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    [Indexed]
    public int FeatId { get; set; }

    public string CustomTitle { get; set; } = "";

    [Indexed]
    public string RecordedDate { get; set; } = "";

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
