using SQLite;

namespace Bannister.Models;

[Table("website_projects")]
public class WebsiteProject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Title { get; set; } = "";

    public string IdeaText { get; set; } = "";

    public int TaskCount { get; set; } = 0;

    public int TaskTarget { get; set; } = 1000;

    public string CodebasePath { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
