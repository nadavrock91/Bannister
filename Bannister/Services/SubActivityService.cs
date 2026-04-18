using Bannister.Models;
using System.Text.Json;

namespace Bannister.Services;

public class SubActivityService
{
    private readonly DatabaseService _db;

    public SubActivityService(DatabaseService db)
    {
        _db = db;
    }

    private async Task EnsureTableAsync()
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<SubActivity>();
    }

    public async Task<List<SubActivity>> GetActiveAsync(string username)
    {
        await EnsureTableAsync();
        var conn = await _db.GetConnectionAsync();
        var items = await conn.Table<SubActivity>()
            .Where(s => s.Username == username && !s.IsArchived)
            .OrderBy(s => s.Name)
            .ToListAsync();

        // Check for daily resets
        foreach (var item in items)
        {
            if (item.ResetMode == "daily" && NeedsReset(item))
            {
                await ResetStepsAsync(item);
            }
        }

        return items;
    }

    public async Task<List<SubActivity>> GetArchivedAsync(string username)
    {
        await EnsureTableAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<SubActivity>()
            .Where(s => s.Username == username && s.IsArchived)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<SubActivity?> GetByIdAsync(int id)
    {
        await EnsureTableAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<SubActivity>()
            .Where(s => s.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<SubActivity> CreateAsync(string username, string name, string resetMode = "manual", string additionMode = "unlimited", int requiredCompletions = 3)
    {
        await EnsureTableAsync();
        var conn = await _db.GetConnectionAsync();

        var item = new SubActivity
        {
            Username = username,
            Name = name,
            ResetMode = resetMode,
            AdditionMode = additionMode,
            RequiredCompletionsToUnlock = requiredCompletions,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        await conn.InsertAsync(item);
        return item;
    }

    public async Task UpdateAsync(SubActivity item)
    {
        var conn = await _db.GetConnectionAsync();
        item.ModifiedAt = DateTime.UtcNow;
        await conn.UpdateAsync(item);
    }

    public async Task DeleteAsync(int id)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<SubActivity>(id);
    }

    public async Task ArchiveAsync(int id)
    {
        var item = await GetByIdAsync(id);
        if (item != null)
        {
            item.IsArchived = true;
            await UpdateAsync(item);
        }
    }

    public async Task RestoreAsync(int id)
    {
        var item = await GetByIdAsync(id);
        if (item != null)
        {
            item.IsArchived = false;
            await UpdateAsync(item);
        }
    }

    #region Steps Management

    public List<SubActivityStep> GetSteps(SubActivity item)
    {
        if (string.IsNullOrEmpty(item.StepsJson)) return new List<SubActivityStep>();
        try
        {
            return JsonSerializer.Deserialize<List<SubActivityStep>>(item.StepsJson) ?? new List<SubActivityStep>();
        }
        catch
        {
            return new List<SubActivityStep>();
        }
    }

    public List<SubActivityStep> GetPendingSteps(SubActivity item)
    {
        if (string.IsNullOrEmpty(item.PendingStepsJson)) return new List<SubActivityStep>();
        try
        {
            return JsonSerializer.Deserialize<List<SubActivityStep>>(item.PendingStepsJson) ?? new List<SubActivityStep>();
        }
        catch
        {
            return new List<SubActivityStep>();
        }
    }

    public async Task SaveStepsAsync(SubActivity item, List<SubActivityStep> steps)
    {
        item.StepsJson = JsonSerializer.Serialize(steps);
        await UpdateAsync(item);
    }

    public async Task SavePendingStepsAsync(SubActivity item, List<SubActivityStep> pendingSteps)
    {
        item.PendingStepsJson = JsonSerializer.Serialize(pendingSteps);
        await UpdateAsync(item);
    }

    public async Task AddStepAsync(SubActivity item, string stepName)
    {
        var steps = GetSteps(item);
        steps.Add(new SubActivityStep { Name = stepName, Done = false });
        await SaveStepsAsync(item, steps);
    }

    public async Task AddPendingStepAsync(SubActivity item, string stepName)
    {
        var pending = GetPendingSteps(item);
        pending.Add(new SubActivityStep { Name = stepName, Done = false });
        await SavePendingStepsAsync(item, pending);
    }

    public async Task ToggleStepAsync(SubActivity item, int stepIndex)
    {
        var steps = GetSteps(item);
        if (stepIndex >= 0 && stepIndex < steps.Count)
        {
            steps[stepIndex].Done = !steps[stepIndex].Done;
            await SaveStepsAsync(item, steps);

            // Check if all done
            if (steps.All(s => s.Done))
            {
                await MarkCompletedAsync(item);
            }
        }
    }

    public async Task RemoveStepAsync(SubActivity item, int stepIndex)
    {
        var steps = GetSteps(item);
        if (stepIndex >= 0 && stepIndex < steps.Count)
        {
            steps.RemoveAt(stepIndex);
            await SaveStepsAsync(item, steps);
        }
    }

    public async Task RemovePendingStepAsync(SubActivity item, int stepIndex)
    {
        var pending = GetPendingSteps(item);
        if (stepIndex >= 0 && stepIndex < pending.Count)
        {
            pending.RemoveAt(stepIndex);
            await SavePendingStepsAsync(item, pending);
        }
    }

    public async Task ActivatePendingStepAsync(SubActivity item, int pendingIndex)
    {
        var pending = GetPendingSteps(item);
        if (pendingIndex >= 0 && pendingIndex < pending.Count)
        {
            var step = pending[pendingIndex];
            pending.RemoveAt(pendingIndex);
            await SavePendingStepsAsync(item, pending);

            var steps = GetSteps(item);
            steps.Add(new SubActivityStep { Name = step.Name, Done = false });
            await SaveStepsAsync(item, steps);

            // Reset completion counter when adding step
            item.CompletionsSinceLastAddition = 0;
            await UpdateAsync(item);
        }
    }

    public async Task MoveStepToPendingAsync(SubActivity item, int stepIndex)
    {
        var steps = GetSteps(item);
        if (stepIndex >= 0 && stepIndex < steps.Count)
        {
            var step = steps[stepIndex];
            steps.RemoveAt(stepIndex);
            await SaveStepsAsync(item, steps);

            var pending = GetPendingSteps(item);
            pending.Add(new SubActivityStep { Name = step.Name, Done = false });
            await SavePendingStepsAsync(item, pending);
        }
    }

    public async Task ReorderStepAsync(SubActivity item, int fromIndex, int toIndex)
    {
        var steps = GetSteps(item);
        if (fromIndex >= 0 && fromIndex < steps.Count && toIndex >= 0 && toIndex < steps.Count)
        {
            var step = steps[fromIndex];
            steps.RemoveAt(fromIndex);
            steps.Insert(toIndex, step);
            await SaveStepsAsync(item, steps);
        }
    }

    #endregion

    #region Completion & Reset

    public bool CanAddStep(SubActivity item)
    {
        if (item.AdditionMode == "unlimited") return true;
        
        // Always allow adding if there are no steps yet
        var steps = GetSteps(item);
        if (steps.Count == 0) return true;
        
        return item.CompletionsSinceLastAddition >= item.RequiredCompletionsToUnlock;
    }

    public int CompletionsNeededToAdd(SubActivity item)
    {
        if (item.AdditionMode == "unlimited") return 0;
        return Math.Max(0, item.RequiredCompletionsToUnlock - item.CompletionsSinceLastAddition);
    }

    private bool NeedsReset(SubActivity item)
    {
        if (item.LastResetDate == null) return true;
        return item.LastResetDate.Value.Date < DateTime.UtcNow.Date;
    }

    public async Task ResetStepsAsync(SubActivity item)
    {
        var steps = GetSteps(item);
        foreach (var step in steps)
        {
            step.Done = false;
        }
        item.StepsJson = JsonSerializer.Serialize(steps);
        item.LastResetDate = DateTime.UtcNow;
        await UpdateAsync(item);
    }

    public async Task MarkCompletedAsync(SubActivity item)
    {
        item.TotalCompletions++;
        item.CompletionsSinceLastAddition++;
        await UpdateAsync(item);
    }

    #endregion
}
