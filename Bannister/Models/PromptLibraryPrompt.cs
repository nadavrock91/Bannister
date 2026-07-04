using SQLite;

namespace Bannister.Models;

[Table("prompt_library_prompts")]
public class PromptLibraryPrompt
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    [Indexed]
    public int CategoryId { get; set; }

    public string Title { get; set; } = "";

    public string Body { get; set; } = "";

    public int SortOrder { get; set; }

    public int SuccessCount { get; set; }

    public int FailureCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
