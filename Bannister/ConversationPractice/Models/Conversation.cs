using SQLite;

namespace ConversationPractice.Models;

/// <summary>
/// A conversation scenario for practice (e.g., job interview, sales call)
/// </summary>
[Table("conversations")]
public class Conversation
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Username (optional - for multi-user support, null for standalone)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Scenario type (e.g., "Job Interview", "Sales Call", "Difficult Customer")
    /// </summary>
    [Indexed]
    public string ScenarioType { get; set; } = "";

    /// <summary>
    /// Specific title (e.g., "Software Engineer Interview at Google")
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Description of the scenario
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// User's role in the conversation (e.g., "Job Candidate", "Sales Rep")
    /// </summary>
    public string UserRole { get; set; } = "User";

    /// <summary>
    /// AI's role (e.g., "Hiring Manager", "Prospect", "Customer")
    /// </summary>
    public string AiRole { get; set; } = "AI";

    /// <summary>
    /// System prompt that defines the AI's personality and behavior
    /// </summary>
    public string SystemPrompt { get; set; } = "";

    /// <summary>
    /// Optional context/background information
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Difficulty level (1-5)
    /// </summary>
    public int DifficultyLevel { get; set; } = 3;

    /// <summary>
    /// Icon/emoji for visual representation
    /// </summary>
    public string Icon { get; set; } = "💬";

    /// <summary>
    /// Number of times this scenario has been practiced
    /// </summary>
    public int TimesCompleted { get; set; } = 0;

    /// <summary>
    /// Total EXP earned from this conversation across all practices
    /// </summary>
    public int TotalExp { get; set; } = 0;

    /// <summary>
    /// Current level for this conversation (1-100)
    /// </summary>
    public int CurrentLevel { get; set; } = 1;

    /// <summary>
    /// Current EXP within the current level
    /// </summary>
    public int CurrentLevelExp { get; set; } = 0;

    /// <summary>
    /// When this scenario was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last time this scenario was practiced
    /// </summary>
    public DateTime? LastPracticedAt { get; set; }

    /// <summary>
    /// Is this a default/template scenario or user-created?
    /// </summary>
    public bool IsTemplate { get; set; } = false;

    /// <summary>
    /// Active/archived
    /// </summary>
    public bool IsActive { get; set; } = true;
}
