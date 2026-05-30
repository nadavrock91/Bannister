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

    public async Task<Routine> AddRoutineAsync(string username, string name, int frequencyDays, DateTime startDate)
    {
        EnsureWritable();
        await EnsureInitializedAsync();

        var conn = await _db.GetConnectionAsync();
        var routine = new Routine
        {
            Username = username,
            Name = name.Trim(),
            FrequencyDays = Math.Max(1, frequencyDays),
            StartDate = startDate.Date,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await conn.InsertAsync(routine);
        await CreateRoutineTaskAsync(conn, routine, routine.StartDate);
        return routine;
    }

    public async Task UpdateRoutineAsync(Routine routine)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        routine.Name = routine.Name.Trim();
        routine.FrequencyDays = Math.Max(1, routine.FrequencyDays);
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
        await CreateRoutineTaskAsync(conn, routine, completionDate.AddDays(routine.FrequencyDays));
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

        task.DueDate = (task.DueDate?.Date ?? DateTime.Today).AddDays(routine.FrequencyDays);
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
            Notes = $"Routine: every {routine.FrequencyDays} day(s)",
            RoutineId = routine.Id,
            CreatedAt = DateTime.UtcNow
        };

        await conn.InsertAsync(task);
    }
}
