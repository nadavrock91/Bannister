using SQLite;

namespace Bannister.Models;

[Table("user_lists")]
public class UserList
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Name { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

[Table("user_list_items")]
public class UserListItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ListId { get; set; }

    public int SortOrder { get; set; }

    public string Text { get; set; } = "";

    public string Notes { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Priority: 1=High 2=Medium 3=Low 0=None
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// True when this item has been executed/done.
    /// </summary>
    public bool IsExecuted { get; set; } = false;
}
