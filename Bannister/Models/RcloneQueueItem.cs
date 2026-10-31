using SQLite;

namespace Bannister.Models;

[Table("rclone_queue")]
public class RcloneQueueItem
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    public string Title { get; set; } = "";
    public string Command { get; set; } = "";
    public bool IsRunning { get; set; } = false;
    public bool IsDone { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
