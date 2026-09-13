using SQLite;

namespace Bannister.Models;

[Table("home_button_visibility")]
public class HomeButtonVisibilitySetting
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    [Indexed]
    public string ButtonId { get; set; } = "";

    public bool IsEnabled { get; set; } = false;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
