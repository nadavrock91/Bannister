using SQLite;

namespace Bannister.Models;

/// <summary>
/// Represents a story point/idea for a production project.
/// Can be categorized as active, partial, pending, possible, or archived.
/// </summary>
[Table("story_points")]
public class StoryPoint
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    [Indexed]
    public int ProjectId { get; set; }
    
    /// <summary>
    /// Version number for snapshots. Higher = newer. Default is 1.
    /// </summary>
    [Indexed]
    public int Version { get; set; } = 1;
    
    /// <summary>
    /// The point/idea text
    /// </summary>
    public string Text { get; set; } = "";
    
    /// <summary>
    /// Category: "active", "partial", "pending", "possible", "archived" (legacy: "irrelevant")
    /// </summary>
    [Indexed]
    public string Category { get; set; } = "active";
    
    /// <summary>
    /// Subcategory within Active: "chronological" (story order) or "misc" (importance order).
    /// Only used when Category = "active". Null/empty for other categories.
    /// </summary>
    public string Subcategory { get; set; } = "chronological";
    
    /// <summary>
    /// If true, this point's subcategory is locked and won't be changed by LLM reordering imports.
    /// The point can still be reordered within its subcategory.
    /// Only applies to Active points (chronological/misc).
    /// </summary>
    public bool IsSubcategoryLocked { get; set; } = false;
    
    /// <summary>
    /// If true, this point's main category is locked and won't be moved between
    /// Active/Partial/Pending/Possible/Archived by any import operations.
    /// </summary>
    public bool IsCategoryLocked { get; set; } = false;
    
    /// <summary>
    /// Display order within category/subcategory (lower = higher in list)
    /// </summary>
    public int DisplayOrder { get; set; } = 0;
    
    /// <summary>
    /// Optional notes about this point
    /// </summary>
    public string Notes { get; set; } = "";
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? ModifiedAt { get; set; }
}

/// <summary>
/// Metadata for story point versions/snapshots
/// </summary>
[Table("story_point_versions")]
public class StoryPointVersion
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    [Indexed]
    public int ProjectId { get; set; }
    
    /// <summary>
    /// Version number (1, 2, 3, ...)
    /// </summary>
    public int Version { get; set; }
    
    /// <summary>
    /// Optional name/label for this version
    /// </summary>
    public string Name { get; set; } = "";
    
    /// <summary>
    /// Whether this is the "latest" working version
    /// </summary>
    public bool IsLatest { get; set; } = false;
    
    /// <summary>
    /// When this version was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
