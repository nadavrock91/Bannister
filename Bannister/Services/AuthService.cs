using System.Security.Cryptography;
using Bannister.Models;

namespace Bannister.Services;

public class AuthService
{
    private readonly DatabaseService _db;
    private User? _currentUser;
    private string _lastRegisterError = "";

    public AuthService(DatabaseService db)
    {
        _db = db;
    }

    public User? CurrentUser => _currentUser;
    public bool IsLoggedIn => _currentUser != null;
    public string CurrentUsername => _currentUser?.Username ?? "";
    public string LastRegisterError => _lastRegisterError;

    /// <summary>
    /// Register a new user with secure password hashing.
    /// On first-ever registration (no DB exists yet), this also sets the DB encryption key.
    /// </summary>
    public async Task<bool> RegisterAsync(string username, string password)
    {
        _lastRegisterError = "";
        username = username?.Trim().ToLowerInvariant() ?? "";
        if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
        {
            _lastRegisterError = "Username must be at least 3 characters.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
        {
            _lastRegisterError = "Password must be at least 4 characters.";
            return false;
        }

        if (_db.HasPassword && _db.GetDbPassword() != password)
            await _db.CloseConnectionAndClearPasswordAsync();

        if (File.Exists(DatabaseService.DatabasePath) &&
            !await _db.TryOpenWithPasswordAsync(password))
        {
            _lastRegisterError =
                "This database already exists and cannot be opened with that password. " +
                "Use the same login password as the existing database, or delete/import a different database first.";
            return false;
        }

        // Set the DB password so we can open/create the database
        _db.SetPassword(password);

        try
        {
            var conn = await _db.GetConnectionAsync();
            var existing = await conn.Table<User>().Where(x => x.Username == username).FirstOrDefaultAsync();
            if (existing != null)
            {
                _lastRegisterError = "Username already exists.";
                return false;
            }

            // Use secure PBKDF2 hashing
            var (hash, salt) = PasswordHasher.HashPassword(password);

            var user = new User
            {
                Username = username,
                PasswordHash = hash,
                PasswordSalt = salt,
                DisplayName = username,
                CreatedAt = DateTime.UtcNow
            };

            await conn.InsertAsync(user);
            return true;
        }
        catch (SQLite.SQLiteException ex) when (
            ex.Message.Contains("file is not a database", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("not an error", StringComparison.OrdinalIgnoreCase))
        {
            _lastRegisterError =
                "Could not open the existing database with that password. " +
                "Use the existing database password, or delete/import a different database first.";
            await _db.CloseConnectionAndClearPasswordAsync();
            return false;
        }
    }

    public async Task<bool> EnsureImportedUserAsync(string username, string password)
    {
        username = username?.Trim().ToLowerInvariant() ?? "";
        if (string.IsNullOrWhiteSpace(username) || username.Length < 3) return false;
        if (string.IsNullOrWhiteSpace(password) || password.Length < 4) return false;

        _db.SetPassword(password);

        var conn = await _db.GetConnectionAsync();
        var existing = await conn.Table<User>().Where(x => x.Username == username).FirstOrDefaultAsync();
        if (existing != null)
        {
            if (existing.PasswordHash?.Length > 0 && existing.PasswordSalt?.Length > 0)
                return PasswordHasher.Verify(password, existing.PasswordSalt, existing.PasswordHash);

            return false;
        }

        var (hash, salt) = PasswordHasher.HashPassword(password);
        var user = new User
        {
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            DisplayName = username,
            CreatedAt = DateTime.UtcNow
        };

        await conn.InsertAsync(user);
        return true;
    }

    /// <summary>
    /// Login with automatic migration from legacy insecure hashing.
    /// Sets the database encryption password on successful login.
    /// </summary>
    public async Task<bool> LoginAsync(string username, string password)
    {
        username = username?.Trim().ToLowerInvariant() ?? "";
        System.Diagnostics.Debug.WriteLine("========== AuthService.LoginAsync START ==========");
        System.Diagnostics.Debug.WriteLine($"[Auth.Login] username='{username}', passwordLength={password?.Length ?? 0}");
        System.Diagnostics.Debug.WriteLine($"[Auth.Login] db.IsReadOnly={_db.IsReadOnly}, db.HasPassword(before)={_db.HasPassword}");
        System.Diagnostics.Debug.WriteLine($"[Auth.Login] DatabasePath='{DatabaseService.DatabasePath}', exists={File.Exists(DatabaseService.DatabasePath)}");
        if (File.Exists(DatabaseService.DatabasePath))
        {
            try
            {
                var info = new FileInfo(DatabaseService.DatabasePath);
                System.Diagnostics.Debug.WriteLine($"[Auth.Login] DatabaseLength={info.Length}, LastWriteUtc={info.LastWriteTimeUtc:O}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Auth.Login] Could not stat database file: {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (_db.HasPassword && _db.GetDbPassword() != password)
        {
            System.Diagnostics.Debug.WriteLine("[Auth.Login] Existing DB password differs from this login attempt; clearing stale state first.");
            await _db.CloseConnectionAndClearPasswordAsync();
            System.Diagnostics.Debug.WriteLine($"[Auth.Login] Cleared stale DB state. db.HasPassword(now)={_db.HasPassword}");
        }
        else if (_db.HasPassword)
        {
            System.Diagnostics.Debug.WriteLine("[Auth.Login] Existing DB password matches this login attempt; keeping current connection state.");
        }

        // First, check if the DB can be opened with this password
        bool canOpen = await _db.TryOpenWithPasswordAsync(password);
        System.Diagnostics.Debug.WriteLine($"[Auth.Login] TryOpenWithPasswordAsync returned {canOpen}");
        if (!canOpen)
        {
            System.Diagnostics.Debug.WriteLine("[Auth.Login] Returning false because database could not be opened with supplied password.");
            System.Diagnostics.Debug.WriteLine("========== AuthService.LoginAsync END ==========");
            return false;
        }

        // Set the password so InitAsync can open the DB
        _db.SetPassword(password);
        System.Diagnostics.Debug.WriteLine($"[Auth.Login] SetPassword complete. db.HasPassword(after)={_db.HasPassword}");

        var conn = await _db.GetConnectionAsync();
        System.Diagnostics.Debug.WriteLine($"[Auth.Login] GetConnectionAsync returned {conn.GetType().FullName}");
        var user = await conn.Table<User>().Where(x => x.Username == username).FirstOrDefaultAsync();
        System.Diagnostics.Debug.WriteLine($"[Auth.Login] User lookup found={(user != null)}");

        if (user == null)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth.Login] Returning false because user '{username}' was not found.");
            System.Diagnostics.Debug.WriteLine("========== AuthService.LoginAsync END ==========");
            return false;
        }

        System.Diagnostics.Debug.WriteLine(
            $"[Auth.Login] User Id={user.Id}, Username='{user.Username}', " +
            $"PasswordHashLength={user.PasswordHash?.Length ?? -1}, PasswordSaltLength={user.PasswordSalt?.Length ?? -1}");

        bool isValid = false;

        // Check if this is a new user with proper hash/salt
        if (user.PasswordHash != null && user.PasswordHash.Length > 0 &&
            user.PasswordSalt != null && user.PasswordSalt.Length > 0)
        {
            // Verify with secure PBKDF2
            isValid = PasswordHasher.Verify(password, user.PasswordSalt, user.PasswordHash);
            System.Diagnostics.Debug.WriteLine($"[Auth.Login] PasswordHasher.Verify returned {isValid}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[Auth.Login] User is missing hash/salt; trying legacy verifier.");
            // LEGACY: User created with old insecure hashing
            // This handles migration from the old SHA256 system
            isValid = VerifyLegacyPassword(password, user);
            System.Diagnostics.Debug.WriteLine($"[Auth.Login] VerifyLegacyPassword returned {isValid}");

            if (isValid)
            {
                // AUTOMATIC MIGRATION: Rehash password with secure method
                var (newHash, newSalt) = PasswordHasher.HashPassword(password);
                user.PasswordHash = newHash;
                user.PasswordSalt = newSalt;
                await conn.UpdateAsync(user);
                
                System.Diagnostics.Debug.WriteLine($"Migrated user {username} to secure password hashing");
            }
        }

        if (!isValid)
        {
            // Password didn't match the user record - clear DB password
            _db.SetPassword(null!);
            System.Diagnostics.Debug.WriteLine("[Auth.Login] Returning false because password verification failed. DB password cleared.");
            System.Diagnostics.Debug.WriteLine("========== AuthService.LoginAsync END ==========");
            return false;
        }

        _currentUser = user;
        await SecureStorage.SetAsync("session_user", username);
        // Save password for session restore
        await SecureStorage.SetAsync("session_password", password);
        System.Diagnostics.Debug.WriteLine($"[Auth.Login] Success. CurrentUser='{_currentUser.Username}'.");
        System.Diagnostics.Debug.WriteLine("========== AuthService.LoginAsync END ==========");
        return true;
    }

    /// <summary>
    /// Verify legacy insecure password (for migration only)
    /// THIS METHOD CAN BE REMOVED AFTER ALL USERS HAVE LOGGED IN ONCE
    /// </summary>
    private bool VerifyLegacyPassword(string password, User user)
    {
        // Check if user has the old PasswordHash string property
        // You may need to add a temporary migration property to User model
        // For now, this returns false - old users will need to reset password
        
        // If you stored the old hash somewhere, verify it here:
        // string legacyHash = HashPasswordLegacy(password);
        // return legacyHash == user.OldPasswordHashField;
        
        return false; // Force password reset for old users
    }

    /// <summary>
    /// Legacy hash method (INSECURE - only for migration verification)
    /// </summary>
    [Obsolete("Only for migration - DO NOT USE for new passwords")]
    private static string HashPasswordLegacy(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password + "bannister_salt"));
        return Convert.ToBase64String(bytes);
    }

    public void Logout()
    {
        _currentUser = null;
        SecureStorage.Remove("session_user");
        SecureStorage.Remove("session_password");
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            var savedUser = await SecureStorage.GetAsync("session_user");
            var savedPassword = await SecureStorage.GetAsync("session_password");
            
            if (string.IsNullOrEmpty(savedUser) || string.IsNullOrEmpty(savedPassword)) 
                return false;

            // Set DB password from saved session
            _db.SetPassword(savedPassword);

            var conn = await _db.GetConnectionAsync();
            var user = await conn.Table<User>().Where(x => x.Username == savedUser).FirstOrDefaultAsync();
            if (user != null)
            {
                _currentUser = user;
                return true;
            }

            System.Diagnostics.Debug.WriteLine($"[Auth.Restore] Saved session user '{savedUser}' not found. Clearing stale session state.");
            await _db.CloseConnectionAndClearPasswordAsync();
            SecureStorage.Remove("session_user");
            SecureStorage.Remove("session_password");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth.Restore] Failed: {ex.GetType().FullName}: {ex.Message}");
            await _db.CloseConnectionAndClearPasswordAsync();
            SecureStorage.Remove("session_user");
            SecureStorage.Remove("session_password");
        }
        return false;
    }

    /// <summary>
    /// Change user password (with proper security).
    /// Also re-encrypts the database with the new password.
    /// </summary>
    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        if (_currentUser == null) return false;
        if (newPassword.Length < 4) return false;

        var conn = await _db.GetConnectionAsync();
        var user = await conn.Table<User>()
            .Where(x => x.Username == _currentUser.Username)
            .FirstOrDefaultAsync();

        if (user == null) return false;

        // Verify current password
        if (!PasswordHasher.Verify(currentPassword, user.PasswordSalt, user.PasswordHash))
            return false;

        // Hash new password
        var (newHash, newSalt) = PasswordHasher.HashPassword(newPassword);
        user.PasswordHash = newHash;
        user.PasswordSalt = newSalt;

        await conn.UpdateAsync(user);
        _currentUser = user;

        // Re-encrypt the database with the new password
        await _db.ChangeDbPasswordAsync(newPassword);
        
        // Update saved session password
        await SecureStorage.SetAsync("session_password", newPassword);

        return true;
    }

    /// <summary>
    /// Reset password (admin function or forgot password flow).
    /// Also re-encrypts the database with the new password.
    /// </summary>
    public async Task<bool> ResetPasswordAsync(string username, string newPassword)
    {
        if (newPassword.Length < 4) return false;

        var conn = await _db.GetConnectionAsync();
        var user = await conn.Table<User>()
            .Where(x => x.Username == username.ToLowerInvariant())
            .FirstOrDefaultAsync();

        if (user == null) return false;

        var (newHash, newSalt) = PasswordHasher.HashPassword(newPassword);
        user.PasswordHash = newHash;
        user.PasswordSalt = newSalt;

        await conn.UpdateAsync(user);

        // Re-encrypt the database with the new password
        await _db.ChangeDbPasswordAsync(newPassword);

        return true;
    }
}
