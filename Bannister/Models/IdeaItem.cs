using SQLite;

namespace Bannister.Models;

/// <summary>
/// Represents an idea that can be categorized and tracked.
/// Ideas can have notes, tags, and status tracking.
/// </summary>
[Table("ideas")]
public class IdeaItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    /// <summary>
    /// Username who owns this idea
    /// </summary>
    [Indexed]
    public string Username { get; set; } = "";
    
    /// <summary>
    /// The main idea text/title
    /// </summary>
    public string Title { get; set; } = "";
    
    /// <summary>
    /// Category for organizing ideas (e.g., "Video Ideas", "App Features", "Business")
    /// </summary>
    [Indexed]
    public string Category { get; set; } = "General";

    /// <summary>
    /// Optional subcategory within the category
    /// </summary>
    public string? Subcategory { get; set; }
    
    /// <summary>
    /// Detailed notes or description
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Comma-separated tags for filtering
    /// </summary>
    public string? Tags { get; set; }
    
    /// <summary>
    /// Status: 0=New, 1=InProgress, 2=Done, 3=Archived
    /// </summary>
    public int Status { get; set; } = 0;
    
    /// <summary>
    /// Priority: 0=None, 1=Low, 2=Medium, 3=High
    /// </summary>
    public int Priority { get; set; } = 0;
    
    /// <summary>
    /// Rating/quality score (0-100)
    /// </summary>
    public int Rating { get; set; } = 50;
    
    /// <summary>
    /// Whether this idea is starred/favorited
    /// </summary>
    public bool IsStarred { get; set; } = false;
    
    /// <summary>
    /// Date when this idea was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Date when this idea was last modified
    /// </summary>
    public DateTime? ModifiedAt { get; set; }
    
    /// <summary>
    /// Date when this idea was completed (if Status == Done)
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    // Computed properties
    [Ignore]
    public string StatusText => Status switch
    {
        0 => "New",
        1 => "In Progress",
        2 => "Done",
        3 => "Archived",
        _ => "Unknown"
    };
    
    [Ignore]
    public string PriorityText => Priority switch
    {
        1 => "Low",
        2 => "Medium",
        3 => "High",
        _ => ""
    };
    
    [Ignore]
    public List<string> TagList => string.IsNullOrWhiteSpace(Tags) 
        ? new List<string>() 
        : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
}
