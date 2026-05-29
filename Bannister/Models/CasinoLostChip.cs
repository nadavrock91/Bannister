using SQLite;

namespace Bannister.Models;

public class CasinoLostChip
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string ChipName { get; set; } = "";
    public string CommandText { get; set; } = "";
    public int DeadlineSeconds { get; set; }
    public DateTime LostAt { get; set; } = DateTime.UtcNow;
}
