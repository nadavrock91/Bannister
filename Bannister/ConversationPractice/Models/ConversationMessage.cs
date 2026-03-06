using SQLite;

namespace ConversationPractice.Models;

/// <summary>
/// A single message in a conversation practice session
/// </summary>
[Table("conversation_messages")]
public class ConversationMessage
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// FK to the practice session
    /// </summary>
    [Indexed]
    public int SessionId { get; set; }

    /// <summary>
    /// Who sent this message ("user" or "assistant")
    /// </summary>
    public string Role { get; set; } = "user";

    /// <summary>
    /// Message content
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// When this message was sent
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
