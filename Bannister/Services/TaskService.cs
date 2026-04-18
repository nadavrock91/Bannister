using Bannister.Models;

namespace Bannister.Services;

public class TaskService
{
    private readonly DatabaseService _db;
    private bool _initialized = false;

    public TaskService(DatabaseService db)
    {
        _db = db;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<TaskItem>();
        _initialized = true;
    }

    // === CRUD Operations ===

    public async Task<List<TaskItem>> GetActiveTasksAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<TaskItem>()
            .Where(t => t.Username == username && !t.IsCompleted)
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.DueDate)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<TaskItem>> GetCompletedTasksAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<TaskItem>()
            .Where(t => t.Username == username && t.IsCompleted)
            .OrderByDescending(t => t.CompletedAt)
            .ToListAsync();
    }

    public async Task<List<TaskItem>> GetTasksByCategoryAsync(string username, string category)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<TaskItem>()
            .Where(t => t.Username == username && t.Category == category && !t.IsCompleted)
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.DueDate)
            .ToListAsync();
    }

    public async Task<List<string>> GetCategoriesAsync(string username)
    {
        var tasks = await GetActiveTasksAsync(username);
        var completedTasks = await GetCompletedTasksAsync(username);
        
        return tasks
            .Concat(completedTasks)
            .Select(t => t.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }

    public async Task<TaskItem> CreateTaskAsync(string username, string title, string category = "General", int priority = 2, DateTime? dueDate = null, string notes = "")
    {
        await EnsureInitializedAsync();
        var task = new TaskItem
        {
            Username = username,
            Title = title,
            Category = category,
            Priority = priority,
            DueDate = dueDate,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(task);
        
        return task;
    }

    public async Task UpdateTaskAsync(TaskItem task)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(task);
    }

    public async Task CompleteTaskAsync(TaskItem task)
    {
        task.IsCompleted = true;
        task.CompletedAt = DateTime.UtcNow;
        await UpdateTaskAsync(task);
    }

    public async Task UncompleteTaskAsync(TaskItem task)
    {
        task.IsCompleted = false;
        task.CompletedAt = null;
        await UpdateTaskAsync(task);
    }

    public async Task DeleteTaskAsync(TaskItem task)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync(task);
    }

    public async Task DeleteCompletedTasksAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var completed = await GetCompletedTasksAsync(username);
        foreach (var task in completed)
        {
            await conn.DeleteAsync(task);
        }
    }

    // === Stats ===

    public async Task<(int active, int overdue, int dueToday, int urgent)> GetStatsAsync(string username)
    {
        var tasks = await GetActiveTasksAsync(username);
        int active = tasks.Count;
        int overdue = tasks.Count(t => t.IsOverdue);
        int dueToday = tasks.Count(t => t.IsDueToday);
        int urgent = tasks.Count(t => t.Priority == 0);
        
        return (active, overdue, dueToday, urgent);
    }
}
