using SQLite;

namespace Bannister.Models;

/// <summary>
/// Logs individual production task completion times for estimating future work.
/// </summary>
[Table("production_task_logs")]
public class ProductionTaskLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    [Indexed]
    public string Username { get; set; } = "";
    
    /// <summary>
    /// Project ID this task belongs to
    /// </summary>
    [Indexed]
    public int ProjectId { get; set; }
    
    /// <summary>
    /// Line ID within the project
    /// </summary>
    public int LineId { get; set; }
    
    /// <summary>
    /// Shot index if applicable (0 for single-shot visuals)
    /// </summary>
    public int ShotIndex { get; set; } = 0;
    
    /// <summary>
    /// Task type: "visual_complete", "image_generated", "video_generated", "shot_done"
    /// </summary>
    [Indexed]
    public string TaskType { get; set; } = "";
    
    /// <summary>
    /// Minutes spent on this task (null if unknown)
    /// </summary>
    public int? MinutesSpent { get; set; }
    
    /// <summary>
    /// Brief description of what was done
    /// </summary>
    public string Description { get; set; } = "";
    
    /// <summary>
    /// The actual script/narration line text
    /// </summary>
    public string LineText { get; set; } = "";
    
    /// <summary>
    /// The visual description for this line
    /// </summary>
    public string VisualDescription { get; set; } = "";
    
    /// <summary>
    /// Shot description if this is a multi-shot visual
    /// </summary>
    public string ShotDescription { get; set; } = "";
    
    /// <summary>
    /// When this task was completed
    /// </summary>
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}
