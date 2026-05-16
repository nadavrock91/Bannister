using SQLite;

namespace Bannister.Models;

[Table("queued_operations")]
public class QueuedOperation
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Unique = true)]
    public string Uuid { get; set; } = "";

    [Indexed]
    public string OperationType { get; set; } = "";

    public string PayloadJson { get; set; } = "";

    [Indexed]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SyncedAt { get; set; }

    public DateTime? AppliedAt { get; set; }

    [Indexed]
    public int Status { get; set; } = 0;

    public string? FailureReason { get; set; }

    [Ignore]
    public string? SourceDeviceId { get; set; }
}
