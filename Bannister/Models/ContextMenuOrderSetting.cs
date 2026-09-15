using SQLite;

namespace Bannister.Models;

[Table("context_menu_order")]
public class ContextMenuOrderSetting
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    /// <summary>
    /// The option key — a stable identifier for each menu item.
    /// </summary>
    public string OptionKey { get; set; } = "";

    /// <summary>
    /// Display order — lower = higher in menu.
    /// </summary>
    public int SortOrder { get; set; } = 0;
}
