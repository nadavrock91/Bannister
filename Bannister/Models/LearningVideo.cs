using SQLite;

namespace Bannister.Models;

/// <summary>
/// Represents a video in the learning queue
/// </summary>
public class LearningVideo
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    public string Username { get; set; } = "";
    
    /// <summary>
    /// Title of the video
    /// </summary>
    public string Title { get; set; } = "";
    
    /// <summary>
    /// URL to the video (YouTube, Vimeo, etc.)
    /// </summary>
    public string Url { get; set; } = "";
    
    /// <summary>
    /// Optional channel/creator name
    /// </summary>
    public string? Creator { get; set; }
    
    /// <summary>
    /// Optional description or notes
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Optional duration in minutes
    /// </summary>
    public int? DurationMinutes { get; set; }
    
    /// <summary>
    /// Order in the watch queue (lower = watch first)
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
    /// Optional category/topic
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Optional thumbnail path
    /// </summary>
    public string? ThumbnailPath { get; set; }
}
