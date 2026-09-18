using SQLite;

namespace Bannister.Models;

public enum ProposalType { CreateBlock = 0, UpdateBlockDates = 1, EndBlock = 2, AddSubBlock = 3, NoteReturn = 4, CorrectLabel = 5 }
public enum ProposalStatus { Pending = 0, Accepted = 1, Rejected = 2 }

[Table("life_path_proposals")]
public class LifePathProposal
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    public int ProposalType { get; set; } = (int)Bannister.Models.ProposalType.CreateBlock;
    public string GameId { get; set; } = "";
    public int TargetBlockId { get; set; } = 0;
    public int ParentBlockId { get; set; } = 0;
    public string ProposedLabel { get; set; } = "";
    public DateTime? ProposedStartDate { get; set; }
    public DateTime? ProposedEndDate { get; set; }
    public string ProposedReason { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string EvidenceEntryIds { get; set; } = "[]";
    public int Status { get; set; } = (int)ProposalStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string RunId { get; set; } = "";
}
