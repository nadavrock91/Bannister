using System.Security.Cryptography;

namespace Bannister.Services;

/// <summary>
/// Secure password hashing using PBKDF2 with SHA256
/// </summary>
public static class PasswordHasher
{
    // Security parameters - good defaults for 2024+
    private const int SaltSize = 16;      // 128-bit salt
    private const int KeySize = 32;       // 256-bit hash
    private const int Iterations = 120_000; // OWASP recommendation for 2024

    /// <summary>
    /// Hash a password with a randomly generated salt
    /// </summary>
    /// <param name="password">Plain text password</param>
    /// <returns>Tuple of (hash, salt) as byte arrays</returns>
    public static (byte[] hash, byte[] salt) HashPassword(string password)
    {
        // Generate a cryptographically secure random salt
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        
        // Derive the hash using PBKDF2
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password, 
            salt, 
            Iterations, 
            HashAlgorithmName.SHA256);
        
        byte[] hash = pbkdf2.GetBytes(KeySize);
        
        return (hash, salt);
    }

    /// <summary>
    /// Verify a password against a stored hash and salt
    /// </summary>
    /// <param name="password">Plain text password to verify</param>
    /// <param name="salt">Stored salt from database</param>
    /// <param name="expectedHash">Stored hash from database</param>
    /// <returns>True if password matches, false otherwise</returns>
    public static bool Verify(string password, byte[] salt, byte[] expectedHash)
    {
        // Derive hash from provided password and stored salt
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password, 
            salt, 
            Iterations, 
            HashAlgorithmName.SHA256);
        
        byte[] actualHash = pbkdf2.GetBytes(KeySize);
        
        // Use constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    /// <summary>
    /// Verify password against base64-encoded hash and salt (for migration)
    /// </summary>
    public static bool VerifyBase64(string password, string saltBase64, string hashBase64)
    {
        try
        {
            byte[] salt = Convert.FromBase64String(saltBase64);
            byte[] hash = Convert.FromBase64String(hashBase64);
            return Verify(password, salt, hash);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Convert hash and salt to base64 strings for storage (if not using byte arrays)
    /// </summary>
    public static (string hashBase64, string saltBase64) ToBase64(byte[] hash, byte[] salt)
    {
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }
}
