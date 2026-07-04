using SQLite;

namespace Bannister.Models;

[Table("idea_category_classifications")]
public class IdeaCategoryClassification
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    [Indexed]
    public string CategoryName { get; set; } = "";

    // "normal" or "llm"
    public string Classification { get; set; } = "normal";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
