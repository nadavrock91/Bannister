using SQLite;

namespace Bannister.Models;

[Table("custom_game_instances")]
public class CustomGameInstance
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int GameId { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EndedAt { get; set; }

    public int FinalScore { get; set; }

    public bool InProgress { get; set; } = true;
}
