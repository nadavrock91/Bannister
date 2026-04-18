using SQLite;

namespace ConversationPractice.Models;

/// <summary>
/// A single practice session for a conversation scenario
/// </summary>
[Table("practice_sessions")]
public class PracticeSession
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// FK to the conversation scenario
    /// </summary>
    [Indexed]
    public int ConversationId { get; set; }

    /// <summary>
    /// Username (optional - for multi-user support, null for standalone)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// When the session started
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the session ended
    /// </summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>
    /// How many messages were exchanged
    /// </summary>
    public int MessageCount { get; set; } = 0;

    /// <summary>
    /// Duration in seconds
    /// </summary>
    public int DurationSeconds { get; set; } = 0;

    /// <summary>
    /// User's self-rating (1-5 stars)
    /// </summary>
    public int? Rating { get; set; }

    /// <summary>
    /// User's notes about the session
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Was the session completed or abandoned?
    /// </summary>
    public bool Completed { get; set; } = false;
}
