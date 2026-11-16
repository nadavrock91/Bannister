using Bannister.Models;

namespace Bannister.Services;

/// <summary>
/// Shared login logic reused by both Bannister and BannisterTools.
/// Validates username + password against the encrypted database, sets the DB password,
/// and returns the authenticated User or null on failure.
/// </summary>
public class LoginMaster
{
    private readonly DatabaseService _db;

    public LoginMaster(DatabaseService db)
    {
        _db = db;
    }

    /// <summary>
    /// Attempts to open the database with the given password, then verifies the
    /// username/password against the stored PBKDF2 hash (with legacy fallback).
    /// On success: DB password is set, returns the User record.
    /// On failure: DB password is cleared, returns null.
    /// </summary>
    public async Task<User?> LoginAsync(string username, string password)
    {
        username = username?.Trim().ToLowerInvariant() ?? "";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return null;

        // Clear stale connection if a different password is already loaded
        if (_db.HasPassword && _db.GetDbPassword() != password)
            await _db.CloseConnectionAndClearPasswordAsync();

        bool canOpen = await _db.TryOpenWithPasswordAsync(password);
        if (!canOpen)
            return null;

        _db.SetPassword(password);

        var conn = await _db.GetConnectionAsync();
        var user = await conn.Table<User>()
            .Where(x => x.Username == username)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            _db.SetPassword(null!);
            return null;
        }

        bool isValid = false;

        if (user.PasswordHash != null && user.PasswordHash.Length > 0 &&
            user.PasswordSalt != null && user.PasswordSalt.Length > 0)
        {
            isValid = PasswordHasher.Verify(password, user.PasswordSalt, user.PasswordHash);
        }
        else
        {
            // Match AuthService.VerifyLegacyPassword exactly: legacy users must reset
            // their password because no verifiable legacy hash is retained.
            isValid = false;
        }

        if (!isValid)
        {
            _db.SetPassword(null!);
            return null;
        }

        return user;
    }
}
