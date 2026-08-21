using SQLite;

namespace Bannister.Models;

public class StatTracker
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string Status { get; set; } = "active"; // active, archived
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class StatEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int TrackerId { get; set; }
    public string EntryType { get; set; } = ""; // success, failure, increment, decrement, note
    public string Label { get; set; } = "";  // e.g. "blue suit", "casual hoodie"
    public string Notes { get; set; } = "";
    public int Value { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
