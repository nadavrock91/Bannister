using Bannister.Models;
using SQLite;

namespace Bannister.Services;

public class DeadlineService
{
    public const int BucketDaily = 0;
    public const int BucketWeekly = 1;
    public const int BucketMonthly = 2;

    public const int StateActive = 0;
    public const int StatePossible = 1;
    public const int StateFailed = 2;
    public const int StateArchived = 3;

    private readonly DatabaseService _db;
    private readonly AuthService _auth;
    private bool _initialized;

    public DeadlineService(DatabaseService db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private void EnsureWritable()
    {
        if (_db.IsReadOnly)
            throw new ReadOnlyDatabaseException("Deadlines are read-only on secondary devices.");
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly)
            await conn.CreateTableAsync<Deadline>();

        _initialized = true;
    }

    public async Task<List<Deadline>> GetDeadlinesAsync(string username, int bucket)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        try
        {
            return await conn.Table<Deadline>()
                .Where(d => d.Username == username && d.Bucket == bucket)
                .OrderBy(d => d.State)
                .ThenBy(d => d.Title)
                .ToListAsync();
        }
        catch (SQLiteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            return new List<Deadline>();
        }
    }

    public async Task<List<Deadline>> GetDeadlinesByStateAsync(string username, int bucket, int state)
    {
        var deadlines = await GetDeadlinesAsync(username, bucket);
        return deadlines.Where(d => d.State == state).OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<int> GetActiveCountAsync(string username, int bucket)
    {
        return (await GetDeadlinesByStateAsync(username, bucket, StateActive)).Count;
    }

    public async Task<List<Deadline>> GetOverdueActiveDeadlinesAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        try
        {
            var active = await conn.Table<Deadline>()
                .Where(d => d.Username == username && d.State == StateActive)
                .ToListAsync();
            var now = DateTime.Now;
            return active
                .Where(d => now > ComputePeriodEnd(d.Bucket, d.CreatedAt))
                .OrderBy(d => d.Bucket)
                .ThenBy(d => ComputePeriodEnd(d.Bucket, d.CreatedAt))
                .ThenBy(d => d.Title)
                .ToList();
        }
        catch (SQLiteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            return new List<Deadline>();
        }
    }

    public async Task<Deadline> AddDeadlineAsync(string username, string title, int bucket, int state)
    {
        EnsureWritable();
        await EnsureInitializedAsync();

        var now = DateTime.UtcNow;
        var deadline = new Deadline
        {
            Username = username,
            Title = title.Trim(),
            Bucket = NormalizeBucket(bucket),
            State = NormalizeState(state),
            CreatedAt = now,
            StateChangedAt = now
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(deadline);
        return deadline;
    }

    public async Task SetStateAsync(int deadlineId, int newState)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var deadline = await conn.FindAsync<Deadline>(deadlineId);
        if (deadline == null) return;

        deadline.State = NormalizeState(newState);
        deadline.StateChangedAt = DateTime.UtcNow;
        await conn.UpdateAsync(deadline);
    }

    public async Task MoveBucketAsync(int deadlineId, int newBucket)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var deadline = await conn.FindAsync<Deadline>(deadlineId);
        if (deadline == null) return;

        deadline.Bucket = NormalizeBucket(newBucket);
        deadline.CreatedAt = DateTime.UtcNow;
        deadline.StateChangedAt = DateTime.UtcNow;
        await conn.UpdateAsync(deadline);
    }

    public async Task UpdateTitleAsync(int deadlineId, string title)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var deadline = await conn.FindAsync<Deadline>(deadlineId);
        if (deadline == null) return;

        deadline.Title = title.Trim();
        deadline.StateChangedAt = DateTime.UtcNow;
        await conn.UpdateAsync(deadline);
    }

    public async Task DeleteDeadlineAsync(int deadlineId)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<Deadline>(deadlineId);
    }

    public int GetAllowance(string username, int bucket)
    {
        return Preferences.Default.Get(GetAllowanceKey(username, bucket), 1);
    }

    public void SetAllowance(string username, int bucket, int allowance)
    {
        Preferences.Default.Set(GetAllowanceKey(username, bucket), Math.Max(1, allowance));
    }

    public static DateTime ComputePeriodEnd(int bucket, DateTime createdAtUtc)
    {
        var localDate = createdAtUtc.ToLocalTime().Date;
        return NormalizeBucket(bucket) switch
        {
            BucketWeekly => EndOfDay(localDate.AddDays((6 - (int)localDate.DayOfWeek + 7) % 7)),
            BucketMonthly => EndOfDay(new DateTime(localDate.Year, localDate.Month, DateTime.DaysInMonth(localDate.Year, localDate.Month))),
            _ => EndOfDay(localDate)
        };
    }

    public static string BucketName(int bucket) => NormalizeBucket(bucket) switch
    {
        BucketWeekly => "Weekly",
        BucketMonthly => "Monthly",
        _ => "Daily"
    };

    public static string StateName(int state) => NormalizeState(state) switch
    {
        StatePossible => "Possible",
        StateFailed => "Failed",
        StateArchived => "Archived",
        _ => "Active"
    };

    private static DateTime EndOfDay(DateTime date)
    {
        return date.Date.AddDays(1).AddTicks(-1);
    }

    private static int NormalizeBucket(int bucket)
    {
        return bucket is >= BucketDaily and <= BucketMonthly ? bucket : BucketDaily;
    }

    private static int NormalizeState(int state)
    {
        return state is >= StateActive and <= StateArchived ? state : StatePossible;
    }

    private static string GetAllowanceKey(string username, int bucket)
    {
        string suffix = NormalizeBucket(bucket) switch
        {
            BucketWeekly => "weekly",
            BucketMonthly => "monthly",
            _ => "daily"
        };
        return $"deadlines_allowance_{suffix}_{username}";
    }
}
