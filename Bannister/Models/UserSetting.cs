using SQLite;

namespace Bannister.Models;

[Table("user_settings")]
public class UserSetting
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
