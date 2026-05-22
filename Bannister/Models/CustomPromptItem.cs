using SQLite;

namespace Bannister.Models;

[Table("custom_prompts")]
public class CustomPromptItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    [Indexed]
    public string Area { get; set; } = "";

    public string Title { get; set; } = "";

    public string Text { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
