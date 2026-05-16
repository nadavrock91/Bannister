using SQLite;

namespace Bannister.Models;

[Table("applied_operations")]
public class AppliedOperation
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Unique = true)]
    public string Uuid { get; set; } = "";

    [Indexed]
    public string OperationType { get; set; } = "";

    [Indexed]
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    public string? SourceDeviceId { get; set; }
}
