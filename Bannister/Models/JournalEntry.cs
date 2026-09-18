using SQLite;

namespace Bannister.Models;

[Table("journal_entries")]
public class JournalEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Text { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UpdatedAt { get; set; }

    [Ignore]
    public string TimeDisplay =>
        CreatedAt.ToLocalTime().ToString("HH:mm");

    [Ignore]
    public string DateDisplay =>
        CreatedAt.ToLocalTime().ToString("dd MMM yyyy");

    [Ignore]
    public string Preview =>
        Text.Length > 80
            ? Text[..77] + "..."
            : Text;
}
