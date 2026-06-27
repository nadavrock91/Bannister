using Bannister.Models;
using System.Text.Json;
using SQLite;

namespace Bannister.Services;

public class SubActivityService
{
    private readonly DatabaseService _db;

    public SubActivityService(DatabaseService db)
    {
        _db = db;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private async Task EnsureTableAsync()
    {
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly)
        {
            await conn.CreateTableAsync<SubActivity>();
            try { await conn.ExecuteAsync("ALTER TABLE sub_activities ADD COLUMN PromptDailyOnHome INTEGER DEFAULT 0"); } catch { }
            try { await conn.ExecuteAsync("ALTER TABLE sub_activities ADD COLUMN Allowance INTEGER DEFAULT 1"); } catch { }
            try { await conn.ExecuteAsync("ALTER TABLE sub_activities ADD COLUMN ConsecutiveAllDoneDays INTEGER DEFAULT 0"); } catch { }
            try { await conn.ExecuteAsync("ALTER TABLE sub_activities ADD COLUMN LastSubmissionDate TEXT"); } catch { }
            await BackfillAllowancesAsync(conn);
        }
    }

    private async Task BackfillAllowancesAsync(ISQLiteAsyncConnection conn)
    {
        var items = await conn.Table<SubActivity>().ToListAsync();
        foreach (var item in items.Where(i => i.Allowance <= 1))
        {
            int stepCount = GetSteps(item).Count;
            if (stepCount > item.Allowance)
            {
                item.Allowance = stepCount;
                await conn.UpdateAsync(item);
            }
        }
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
            if (!_db.IsReadOnly && item.ResetMode == "daily" && NeedsReset(item))
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

    public async Task<List<SubActivity>> GetDailyPromptProcessesAsync(string username)
    {
        var items = await GetActiveAsync(username);
        return items
            .Where(item => item.PromptDailyOnHome)
            .Where(item => item.LastSubmissionDate?.Date != DateTime.Today)
            .Where(item =>
            {
                var steps = GetSteps(item);
                return steps.Count > 0;
            })
            .OrderBy(item => item.Name)
            .ToList();
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
            Allowance = 1,
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
        if (steps.Count >= item.Allowance)
            return;

        steps.Add(new SubActivityStep
        {
            Name = stepName,
            Done = false,
            LastSubmissionState = (int)SubActivityStepSubmissionState.NotDone
        });
        await SaveStepsAsync(item, steps);
    }

    public async Task<bool> TryAddStepAsync(int processId, string stepName)
    {
        if (_db.IsReadOnly)
            return false;

        var item = await GetByIdAsync(processId);
        if (item == null || string.IsNullOrWhiteSpace(stepName))
            return false;

        var steps = GetSteps(item);
        if (steps.Count >= item.Allowance)
            return false;

        steps.Add(new SubActivityStep
        {
            Name = stepName.Trim(),
            Done = false,
            LastSubmissionState = (int)SubActivityStepSubmissionState.NotDone
        });
        item.StepsJson = JsonSerializer.Serialize(steps);
        await UpdateAsync(item);
        return true;
    }

    public async Task AddPendingStepAsync(SubActivity item, string stepName)
    {
        var pending = GetPendingSteps(item);
        pending.Add(new SubActivityStep { Name = stepName, Done = false });
        await SavePendingStepsAsync(item, pending);
    }

    public async Task<SubActivityCompletionResult?> ToggleStepAsync(SubActivity item, int stepIndex)
    {
        var steps = GetSteps(item);
        if (stepIndex >= 0 && stepIndex < steps.Count)
        {
            steps[stepIndex].Done = !steps[stepIndex].Done;
            await SaveStepsAsync(item, steps);

            // Check if all done
            if (steps.All(s => s.Done))
            {
                return await MarkCompletedAsync(item);
            }
        }

        return null;
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
            var steps = GetSteps(item);
            if (steps.Count >= item.Allowance)
                return;

            var step = pending[pendingIndex];
            pending.RemoveAt(pendingIndex);
            await SavePendingStepsAsync(item, pending);

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
        var steps = GetSteps(item);
        if (steps.Count >= item.Allowance) return false;
        if (item.AdditionMode == "unlimited") return true;
        
        // Always allow adding if there are no steps yet
        if (steps.Count == 0) return true;
        
        return item.CompletionsSinceLastAddition >= item.RequiredCompletionsToUnlock;
    }

    public int CompletionsNeededToAdd(SubActivity item)
    {
        if (GetSteps(item).Count >= item.Allowance) return 0;
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
            step.LastSubmissionState = (int)SubActivityStepSubmissionState.NotDone;
        }
        item.StepsJson = JsonSerializer.Serialize(steps);
        item.LastResetDate = DateTime.UtcNow;
        await UpdateAsync(item);
    }

    private async Task<bool> ApplyAllDoneStreakAsync(SubActivity item, DateTime today)
    {
        today = today.Date;
        int previousStreak = item.ConsecutiveAllDoneDays;
        bool incrementedToday = false;

        if (item.LastSubmissionDate == null)
        {
            item.ConsecutiveAllDoneDays = 1;
            incrementedToday = true;
        }
        else
        {
            var lastDate = item.LastSubmissionDate.Value.Date;
            if (lastDate < today.AddDays(-1))
            {
                item.ConsecutiveAllDoneDays = 1;
                incrementedToday = true;
            }
            else if (lastDate == today.AddDays(-1))
            {
                item.ConsecutiveAllDoneDays++;
                incrementedToday = true;
            }
            else
            {
                // Same-day re-completion, future date, or any other unusual state:
                // record today but do not increment the streak again.
            }
        }

        item.LastSubmissionDate = today;
        return incrementedToday && previousStreak < 3 && item.ConsecutiveAllDoneDays >= 3;
    }

    public async Task<SubActivityCompletionResult> MarkCompletedAsync(SubActivity item)
    {
        item.TotalCompletions++;
        item.CompletionsSinceLastAddition++;
        DateTime today = DateTime.UtcNow.Date;
        bool milestoneReached = await ApplyAllDoneStreakAsync(item, today);
        await UpdateAsync(item);
        return new SubActivityCompletionResult(item, milestoneReached);
    }

    public async Task<SubActivityDailySubmissionResult> SubmitDailySubAsync(int processId, Dictionary<int, int> stepStates)
    {
        if (_db.IsReadOnly)
            return new SubActivityDailySubmissionResult(false, false, null);

        var item = await GetByIdAsync(processId);
        if (item == null)
            return new SubActivityDailySubmissionResult(false, false, null);

        var today = DateTime.Today;
        var steps = GetSteps(item);
        if (steps.Count == 0)
            return new SubActivityDailySubmissionResult(false, false, item);

        for (int i = 0; i < steps.Count; i++)
        {
            int state = stepStates.TryGetValue(i, out var submittedState)
                ? submittedState
                : (int)SubActivityStepSubmissionState.NotDone;
            if (!Enum.IsDefined(typeof(SubActivityStepSubmissionState), state))
                state = (int)SubActivityStepSubmissionState.NotDone;

            steps[i].LastSubmissionState = state;
            steps[i].LastSubmissionDate = today;
            steps[i].Done = state == (int)SubActivityStepSubmissionState.Done ||
                state == (int)SubActivityStepSubmissionState.NotRelevant;
        }

        bool allDone = steps.All(step => step.LastSubmissionState == (int)SubActivityStepSubmissionState.Done ||
            step.LastSubmissionState == (int)SubActivityStepSubmissionState.NotRelevant);

        item.StepsJson = JsonSerializer.Serialize(steps);

        if (allDone)
        {
            item.TotalCompletions++;
            item.CompletionsSinceLastAddition++;
        }
        else
        {
            item.ConsecutiveAllDoneDays = 0;
            item.LastSubmissionDate = today;
        }

        bool milestoneReached = allDone && await ApplyAllDoneStreakAsync(item, today);
        if (milestoneReached)
        {
            item.Allowance++;
        }

        await UpdateAsync(item);
        return new SubActivityDailySubmissionResult(true, milestoneReached, item);
    }

    public async Task IncreaseAllowanceAsync(int processId)
    {
        if (_db.IsReadOnly) return;
        var item = await GetByIdAsync(processId);
        if (item == null) return;
        item.Allowance++;
        await UpdateAsync(item);
    }

    public async Task RevertAllowanceAsync(int processId)
    {
        if (_db.IsReadOnly) return;
        var item = await GetByIdAsync(processId);
        if (item == null) return;
        item.Allowance = Math.Max(GetSteps(item).Count, item.Allowance - 1);
        item.ConsecutiveAllDoneDays = 0;
        await UpdateAsync(item);
    }

    public async Task ResetConsecutiveAllDoneDaysAsync(int processId)
    {
        if (_db.IsReadOnly) return;
        var item = await GetByIdAsync(processId);
        if (item == null) return;
        item.ConsecutiveAllDoneDays = 0;
        await UpdateAsync(item);
    }

    public async Task SetAllowanceAsync(SubActivity item, int allowance)
    {
        if (_db.IsReadOnly) return;
        int stepCount = GetSteps(item).Count;
        item.Allowance = Math.Max(stepCount, allowance);
        await UpdateAsync(item);
    }

    public async Task<SubActivityCompletionResult?> CompleteAllStepsAsync(SubActivity item)
    {
        if (_db.IsReadOnly) return null;

        var steps = GetSteps(item);
        if (steps.Count == 0 || steps.All(s => s.Done)) return null;

        foreach (var step in steps)
        {
            step.Done = true;
        }

        item.StepsJson = JsonSerializer.Serialize(steps);
        return await MarkCompletedAsync(item);
    }

    public async Task<SubActivityCompletionResult?> MarkStepsDoneAsync(SubActivity item, IEnumerable<int> stepIndexes)
    {
        if (_db.IsReadOnly) return null;

        var selectedIndexes = stepIndexes
            .Distinct()
            .Where(index => index >= 0)
            .ToHashSet();
        if (selectedIndexes.Count == 0) return null;

        var steps = GetSteps(item);
        if (steps.Count == 0) return null;

        bool wasComplete = steps.All(s => s.Done);
        bool changed = false;

        for (int i = 0; i < steps.Count; i++)
        {
            if (!selectedIndexes.Contains(i) || steps[i].Done) continue;
            steps[i].Done = true;
            changed = true;
        }

        if (!changed) return null;

        item.StepsJson = JsonSerializer.Serialize(steps);

        if (!wasComplete && steps.All(s => s.Done))
        {
            return await MarkCompletedAsync(item);
        }
        else
        {
            await UpdateAsync(item);
            return null;
        }
    }

    #endregion
}

public record SubActivityDailySubmissionResult(
    bool Submitted,
    bool MilestoneReached,
    SubActivity? Process);

public record SubActivityCompletionResult(SubActivity Process, bool MilestoneReached);
