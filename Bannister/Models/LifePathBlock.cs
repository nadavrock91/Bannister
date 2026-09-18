using SQLite;

namespace Bannister.Models;

[Table("life_path_blocks")]
public class LifePathBlock
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    [Indexed]
    public string Username { get; set; } = "";
    [Indexed]
    public string GameId { get; set; } = "";
    public int? ParentBlockId { get; set; }
    public string Label { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Level { get; set; } = 1;
    public string ColorHex { get; set; } = "";
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string EvidenceEntryIds { get; set; } = "[]";
    public string AnalysisNote { get; set; } = "";
}
