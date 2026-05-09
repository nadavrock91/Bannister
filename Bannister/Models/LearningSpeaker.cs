using SQLite;

namespace Bannister.Models;

[Table("learning_speakers")]
public class LearningSpeaker
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Name { get; set; } = "";

    public string Notes { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? LastSearchedAt { get; set; }

    public int SearchCount { get; set; }
}
