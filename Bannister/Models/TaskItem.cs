using SQLite;

namespace Bannister.Models;

[Table("tasks")]
public class TaskItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    [Indexed]
    public string Username { get; set; } = "";
    
    public string Title { get; set; } = "";
    
    public string Category { get; set; } = "General";
    
    public string Notes { get; set; } = "";
    
    public bool IsCompleted { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? CompletedAt { get; set; }
    
    // Priority: 1 = High, 2 = Medium, 3 = Low
    public int Priority { get; set; } = 2;
    
    // Optional due date
    public DateTime? DueDate { get; set; }
    
    [Ignore]
    public bool IsOverdue => DueDate.HasValue && !IsCompleted && DueDate.Value.Date < DateTime.Today;
    
    [Ignore]
    public bool IsDueToday => DueDate.HasValue && !IsCompleted && DueDate.Value.Date == DateTime.Today;
}
