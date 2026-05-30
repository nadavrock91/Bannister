using Bannister.Models;
using SQLite;

namespace Bannister.Services;

public class RoutineService
{
    private readonly DatabaseService _db;
    private readonly AuthService _auth;
    private bool _initialized;

    public RoutineService(DatabaseService db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private void EnsureWritable()
    {
        if (_db.IsReadOnly)
            throw new ReadOnlyDatabaseException("Routines are read-only on secondary devices.");
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly)
        {
            await conn.CreateTableAsync<Routine>();
            await conn.CreateTableAsync<TaskItem>();
            try { await conn.ExecuteAsync("ALTER TABLE tasks ADD COLUMN RoutineId INTEGER"); } catch { }
            try { await conn.ExecuteAsync("ALTER TABLE routines ADD COLUMN FrequencyType INTEGER DEFAULT 0"); } catch { }
            try { await conn.ExecuteAsync("ALTER TABLE routines ADD COLUMN DayOfMonth INTEGER DEFAULT 0"); } catch { }
            try { await conn.ExecuteAsync("ALTER TABLE routines ADD COLUMN WeekOrdinal INTEGER DEFAULT 0"); } catch { }
            try { await conn.ExecuteAsync("ALTER TABLE routines ADD COLUMN DayOfWeek INTEGER DEFAULT 0"); } catch { }
        }

        _initialized = true;
    }

    public async Task<List<Routine>> GetRoutinesAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        try
        {
            return await conn.Table<Routine>()
                .Where(r => r.Username == username)
                .OrderBy(r => r.Name)
                .ToListAsync();
        }
        catch (SQLiteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            return new List<Routine>();
        }
    }

    public async Task<List<Routine>> GetActiveRoutinesAsync(string username)
    {
        var routines = await GetRoutinesAsync(username);
        return routines.Where(r => r.IsActive).OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<Routine> AddRoutineAsync(
        string username,
        string name,
        int frequencyDays,
        DateTime startDate,
        int frequencyType = 0,
        int dayOfMonth = 0,
        int weekOrdinal = 0,
        int dayOfWeek = 0)
    {
        EnsureWritable();
        await EnsureInitializedAsync();

        var conn = await _db.GetConnectionAsync();
        var routine = new Routine
        {
            Username = username,
            Name = name.Trim(),
            FrequencyDays = frequencyType == 0 ? Math.Max(1, frequencyDays) : 0,
            FrequencyType = frequencyType,
            DayOfMonth = dayOfMonth,
            WeekOrdinal = weekOrdinal,
            DayOfWeek = dayOfWeek,
            StartDate = startDate.Date,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        NormalizeRoutineFrequency(routine);

        await conn.InsertAsync(routine);
        await CreateRoutineTaskAsync(conn, routine, routine.StartDate);
        return routine;
    }

    public async Task UpdateRoutineAsync(Routine routine)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        routine.Name = routine.Name.Trim();
        NormalizeRoutineFrequency(routine);
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(routine);
    }

    public async Task SetRoutineActiveAsync(int routineId, bool active, DateTime? resumeStartDate = null)
    {
        EnsureWritable();
        await EnsureInitializedAsync();

        var conn = await _db.GetConnectionAsync();
        var routine = await conn.FindAsync<Routine>(routineId);
        if (routine == null) return;

        routine.IsActive = active;
        await conn.UpdateAsync(routine);

        if (!active)
        {
            var openInstances = await conn.Table<TaskItem>()
                .Where(t => t.RoutineId == routineId && !t.IsCompleted)
                .ToListAsync();
            foreach (var task in openInstances)
                await conn.DeleteAsync(task);
        }
        else if (resumeStartDate.HasValue)
        {
            await CreateRoutineTaskAsync(conn, routine, resumeStartDate.Value.Date);
        }
    }

    public async Task DeleteRoutineAsync(int routineId)
    {
        EnsureWritable();
        await EnsureInitializedAsync();

        var conn = await _db.GetConnectionAsync();
        var instances = await conn.Table<TaskItem>()
            .Where(t => t.RoutineId == routineId)
            .ToListAsync();
        foreach (var task in instances)
            await conn.DeleteAsync(task);

        await conn.DeleteAsync<Routine>(routineId);
    }

    public async Task OnTaskCompletedAsync(int taskId)
    {
        EnsureWritable();
        await EnsureInitializedAsync();

        var conn = await _db.GetConnectionAsync();
        var task = await conn.FindAsync<TaskItem>(taskId);
        if (task?.RoutineId == null)
            return;

        var routine = await conn.FindAsync<Routine>(task.RoutineId.Value);
        if (routine == null || !routine.IsActive)
            return;

        var existingOpen = await conn.Table<TaskItem>()
            .Where(t => t.RoutineId == routine.Id && !t.IsCompleted)
            .FirstOrDefaultAsync();
        if (existingOpen != null)
            return;

        var completionDate = (task.CompletedAt?.ToLocalTime().Date ?? DateTime.Today);
        await CreateRoutineTaskAsync(conn, routine, ComputeNextInstanceDate(routine, completionDate));
    }

    public async Task PostponeRoutineInstanceAsync(int taskId)
    {
        EnsureWritable();
        await EnsureInitializedAsync();

        var conn = await _db.GetConnectionAsync();
        var task = await conn.FindAsync<TaskItem>(taskId);
        if (task?.RoutineId == null)
            return;

        var routine = await conn.FindAsync<Routine>(task.RoutineId.Value);
        if (routine == null)
            return;

        task.DueDate = routine.FrequencyType == 0
            ? (task.DueDate?.Date ?? DateTime.Today).AddDays(Math.Max(1, routine.FrequencyDays))
            : ComputeNextInstanceDate(routine, DateTime.Today);
        await conn.UpdateAsync(task);
    }

    public async Task<Routine?> GetRoutineAsync(int routineId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        try { return await conn.FindAsync<Routine>(routineId); }
        catch (SQLiteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase)) { return null; }
    }

    private static async Task CreateRoutineTaskAsync(ISQLiteAsyncConnection conn, Routine routine, DateTime dueDate)
    {
        var task = new TaskItem
        {
            Username = routine.Username,
            Title = routine.Name,
            Category = "Routines",
            Priority = 2,
            DueDate = dueDate.Date,
            Notes = $"Routine: {FormatRoutineFrequency(routine)}",
            RoutineId = routine.Id,
            CreatedAt = DateTime.UtcNow
        };

        await conn.InsertAsync(task);
    }

    public static DateTime ComputeNextInstanceDate(Routine routine, DateTime fromDate)
    {
        var anchor = fromDate.Date;
        return routine.FrequencyType switch
        {
            1 => ComputeNextDayOfMonth(routine.DayOfMonth, anchor),
            2 => ComputeNextNthWeekday(routine.WeekOrdinal, routine.DayOfWeek, anchor),
            _ => anchor.AddDays(Math.Max(1, routine.FrequencyDays))
        };
    }

    public static string FormatRoutineFrequency(Routine routine)
    {
        return routine.FrequencyType switch
        {
            1 => $"{OrdinalNumber(Math.Clamp(routine.DayOfMonth, 1, 31))} of each month{(routine.DayOfMonth == 31 ? " (or last day)" : "")}",
            2 => $"{OrdinalWord(Math.Clamp(routine.WeekOrdinal, 1, 5))} {WeekdayName(routine.DayOfWeek)} of each month",
            _ => $"every {Math.Max(1, routine.FrequencyDays)} days"
        };
    }

    public static string FormatPostponeTooltip(Routine routine)
    {
        return routine.FrequencyType == 0
            ? $"Postpone (+{Math.Max(1, routine.FrequencyDays)}d)"
            : "Postpone (next month)";
    }

    private static DateTime ComputeNextDayOfMonth(int dayOfMonth, DateTime fromDate)
    {
        var nextMonth = new DateTime(fromDate.Year, fromDate.Month, 1).AddMonths(1);
        int day = Math.Min(Math.Clamp(dayOfMonth, 1, 31), DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
        return new DateTime(nextMonth.Year, nextMonth.Month, day);
    }

    private static DateTime ComputeNextNthWeekday(int weekOrdinal, int dayOfWeek, DateTime fromDate)
    {
        var month = new DateTime(fromDate.Year, fromDate.Month, 1).AddMonths(1);
        var target = (System.DayOfWeek)Math.Clamp(dayOfWeek, 0, 6);
        int ordinal = Math.Clamp(weekOrdinal, 1, 5);

        if (ordinal == 5)
        {
            var date = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
            while (date.DayOfWeek != target)
                date = date.AddDays(-1);
            return date;
        }

        int count = 0;
        for (var date = month; date.Month == month.Month; date = date.AddDays(1))
        {
            if (date.DayOfWeek != target)
                continue;

            count++;
            if (count == ordinal)
                return date;
        }

        return ComputeNextNthWeekday(5, dayOfWeek, fromDate);
    }

    private static void NormalizeRoutineFrequency(Routine routine)
    {
        routine.FrequencyType = routine.FrequencyType is >= 0 and <= 2 ? routine.FrequencyType : 0;

        if (routine.FrequencyType == 1)
        {
            routine.FrequencyDays = 0;
            routine.DayOfMonth = Math.Clamp(routine.DayOfMonth, 1, 31);
            routine.WeekOrdinal = 0;
            routine.DayOfWeek = 0;
        }
        else if (routine.FrequencyType == 2)
        {
            routine.FrequencyDays = 0;
            routine.DayOfMonth = 0;
            routine.WeekOrdinal = Math.Clamp(routine.WeekOrdinal, 1, 5);
            routine.DayOfWeek = Math.Clamp(routine.DayOfWeek, 0, 6);
        }
        else
        {
            routine.FrequencyType = 0;
            routine.FrequencyDays = Math.Max(1, routine.FrequencyDays);
            routine.DayOfMonth = 0;
            routine.WeekOrdinal = 0;
            routine.DayOfWeek = 0;
        }
    }

    private static string OrdinalWord(int ordinal) => ordinal switch
    {
        1 => "first",
        2 => "second",
        3 => "third",
        4 => "fourth",
        _ => "last"
    };

    private static string WeekdayName(int dayOfWeek)
    {
        return ((System.DayOfWeek)Math.Clamp(dayOfWeek, 0, 6)).ToString();
    }

    private static string OrdinalNumber(int number)
    {
        int rem100 = number % 100;
        if (rem100 is >= 11 and <= 13)
            return $"{number}th";

        return (number % 10) switch
        {
            1 => $"{number}st",
            2 => $"{number}nd",
            3 => $"{number}rd",
            _ => $"{number}th"
        };
    }
}
