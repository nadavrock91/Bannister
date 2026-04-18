using SQLite;

namespace Bannister.Models;

[Table("users")]
public class User
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique]
    public string Username { get; set; } = "";

    /// <summary>
    /// PBKDF2 password hash (32 bytes / 256 bits)
    /// </summary>
    public byte[] PasswordHash { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Cryptographically secure random salt (16 bytes / 128 bits)
    /// </summary>
    public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();

    public string DisplayName { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Migration flag - true if user was migrated from old insecure hash
    /// Can be removed after all users have been migrated
    /// </summary>
    [Ignore]
    public bool IsLegacyUser { get; set; } = false;
}
