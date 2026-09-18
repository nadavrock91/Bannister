using SQLite;

namespace Bannister.Models;

[Table("journal_analysis_checkpoints")]
public class JournalAnalysisCheckpoint
{
    [PrimaryKey]
    public string Username { get; set; } = "";
    public DateTime? LastAnalyzedAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public int TotalRunCount { get; set; } = 0;
}
