using SQLite;

namespace Bannister.Models;

[Table("sync_counters")]
public class SyncCounter
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Unique = true)]
    public string Username { get; set; } = "";

    public int Counter { get; set; } = 0;

    public DateTime LastIncrementedAt { get; set; }
}
