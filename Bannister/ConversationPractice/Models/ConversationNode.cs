using SQLite;

namespace ConversationPractice.Models;

/// <summary>
/// A single node in a conversation tree
/// Represents one thing the other person might say
/// </summary>
[Table("conversation_nodes")]
public class ConversationNode
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// FK to the conversation scenario this belongs to
    /// </summary>
    [Indexed]
    public int ConversationId { get; set; }

    /// <summary>
    /// Parent node ID (null for root nodes that start the conversation)
    /// </summary>
    [Indexed]
    public int? ParentNodeId { get; set; }

    /// <summary>
    /// What the other person says at this node
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Optional recommended responses the user might say (semicolon-separated)
    /// Example: "I'm very interested;Tell me more;That sounds great"
    /// </summary>
    public string? RecommendedResponses { get; set; }

    /// <summary>
    /// Optional notes about this response (tone, context, etc.)
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// How many times this node has been reached during practice
    /// </summary>
    public int TimesReached { get; set; } = 0;

    /// <summary>
    /// Is this a terminal node (conversation ends here)?
    /// </summary>
    public bool IsTerminal { get; set; } = false;

    /// <summary>
    /// Sort order among sibling nodes
    /// </summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// When this node was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
