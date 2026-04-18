using SQLite;

namespace Bannister.Models;

/// <summary>
/// Defines settings for a prompt group within a pack.
/// Controls generation behavior like max items per generation.
/// </summary>
[Table("prompt_groups")]
public class PromptGroup
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    /// <summary>
    /// Pack this group belongs to
    /// </summary>
    [Indexed]
    public string PackName { get; set; } = "";
    
    /// <summary>
    /// Group name (e.g., "ending", "Hook", "Character")
    /// </summary>
    [Indexed]
    public string GroupName { get; set; } = "";
    
    /// <summary>
    /// Maximum items from this group per generation.
    /// Default is 2 (matching current behavior).
    /// Set to 1 for groups like "ending" or "Format" where only one makes sense.
    /// </summary>
    public int MaxPerGeneration { get; set; } = 2;
    
    /// <summary>
    /// Optional description of what this group is for
    /// </summary>
    public string? Description { get; set; }
}
