using SQLite;

namespace Bannister.Models;

[Table("reset_enforcers")]
public class ResetEnforcer
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Name { get; set; } = "";

    public string ImagePath { get; set; } = "";

    /// <summary>
    /// Stored as int: 0=AspectFit, 1=AspectFill, 2=Fill
    /// Default 0 (AspectFit) so full image is always visible.
    /// </summary>
    public int ImageAspect { get; set; } = 0;

    public int TotalResets { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
