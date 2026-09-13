using SQLite;

namespace Bannister.Models;

[Table("shared_activity_links")]
public class SharedActivityLink
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string LinkCode { get; set; } = "";
    public string PartnerName { get; set; } = "";

    /// <summary>SHA-256 hash of the shared password. Never stored plain.</summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>JSON array of {GameId, ActivityName} pairs.</summary>
    public string SharedActivitiesJson { get; set; } = "[]";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUploadedAt { get; set; }
    public DateTime? LastDownloadedAt { get; set; }
    public string LastUpdatedBy { get; set; } = "";
    public DateTime? LastUpdatedAt { get; set; }
}
