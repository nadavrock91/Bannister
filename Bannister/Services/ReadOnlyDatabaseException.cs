namespace Bannister.Services;

/// <summary>
/// Thrown when a write is attempted in secondary (read-only) mode.
/// The global handler in AppMain catches this and shows a friendly alert
/// instead of a raw SQLite error.
/// </summary>
public class ReadOnlyDatabaseException : InvalidOperationException
{
    public ReadOnlyDatabaseException()
        : base("This device is in read-only mode. Switch to master device to make changes.") { }

    public ReadOnlyDatabaseException(string message) : base(message) { }

    public ReadOnlyDatabaseException(string message, Exception inner) : base(message, inner) { }

    /// <summary>
    /// Inspect a thrown exception and decide whether it's a read-only DB error.
    /// SQLite-net surfaces this as a SQLiteException with Result.ReadOnly (code 8),
    /// but the exact type varies; checking the message is the most reliable.
    /// </summary>
    public static bool IsReadOnlyError(Exception ex)
    {
        if (ex is ReadOnlyDatabaseException) return true;
        var msg = ex.Message?.ToLowerInvariant() ?? "";
        if (msg.Contains("readonly") || msg.Contains("read-only") || msg.Contains("read only"))
            return true;
        // Walk inner exceptions
        return ex.InnerException != null && IsReadOnlyError(ex.InnerException);
    }
}
