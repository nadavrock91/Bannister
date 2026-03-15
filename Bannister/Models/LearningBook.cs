using SQLite;

namespace Bannister.Models;

/// <summary>
/// Represents a book in the learning queue
/// </summary>
public class LearningBook
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    public string Username { get; set; } = "";
    
    /// <summary>
    /// Title of the book
    /// </summary>
    public string Title { get; set; } = "";
    
    /// <summary>
    /// Author of the book
    /// </summary>
    public string Author { get; set; } = "";
    
    /// <summary>
    /// Optional description or notes
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Order in the reading queue (lower = read first)
    /// </summary>
    public int SortOrder { get; set; }
    
    /// <summary>
    /// Status: NotStarted, InProgress, Completed
    /// </summary>
    public string Status { get; set; } = "NotStarted";
    
    /// <summary>
    /// Date when marked as completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Date added to the list
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Optional category/genre
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Optional image path for book cover
    /// </summary>
    public string? ImagePath { get; set; }
}
