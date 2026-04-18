using SQLite;

namespace Bannister.Models;

/// <summary>
/// Represents a prompt item that can be randomly selected during generation.
/// Prompts are organized into packs (e.g., Writing, Reflection, Planning)
/// and groups within packs for diversity control.
/// </summary>
[Table("prompt_items")]
public class PromptItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    /// <summary>
    /// The prompt text to display
    /// </summary>
    public string Text { get; set; } = "";
    
    /// <summary>
    /// Pack/category name (e.g., "Writing", "Reflection", "Planning")
    /// </summary>
    [Indexed]
    public string PackName { get; set; } = "";
    
    /// <summary>
    /// Group within the pack for diversity control.
    /// Maximum 2 items from the same group can be selected.
    /// </summary>
    public string GroupName { get; set; } = "";
    
    /// <summary>
    /// Quality/importance rating (1-5, higher = better)
    /// </summary>
    public int Rating { get; set; } = 3;
    
    /// <summary>
    /// Base probability of selection (0.0 to 1.0).
    /// Items with lower probability appear less often.
    /// </summary>
    public double Probability { get; set; } = 1.0;
    
    /// <summary>
    /// Probability that a second item from the same group is allowed (0.0 to 1.0).
    /// If first item from group selected, roll against this to allow second.
    /// </summary>
    public double SecondFromGroupProbability { get; set; } = 0.3;
    
    /// <summary>
    /// Whether this prompt is active and can be selected
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Whether this prompt has been archived (soft deleted).
    /// Archived prompts are kept in "All Prompts" but hidden from packs.
    /// </summary>
    public bool IsArchived { get; set; } = false;
    
    /// <summary>
    /// Hard priority for ordering (1 = highest, shows first).
    /// Items with same priority are randomized.
    /// 0 or null means no priority (randomized with other non-priority items).
    /// </summary>
    public int Priority { get; set; } = 0;
    
    /// <summary>
    /// Optional notes or context about this prompt
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Examples of how this prompt can be used, separated by newlines
    /// </summary>
    public string? Examples { get; set; }
    
    /// <summary>
    /// Date when this prompt was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Date when this prompt was last used in generation
    /// </summary>
    public DateTime? LastUsedAt { get; set; }
    
    /// <summary>
    /// Number of times this prompt has been generated
    /// </summary>
    public int UsageCount { get; set; } = 0;
}
