using SQLite;

namespace Bannister.Models;

[Table("custom_game_buttons")]
public class CustomGameButton
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int GameId { get; set; }

    public string Label { get; set; } = "";

    public int PointValue { get; set; }

    public int SortOrder { get; set; }

    public string Color { get; set; } = "";
}
