using Bannister.Models;

namespace Bannister.Services;

public enum ExpAwardMode
{
    Normal,
    StreakCard,
    AutoAward,
    CatchUp
}

public sealed record ExpAwardResult(
    int ExpAwarded,
    int BaseExpRequested,
    int BonusExpRequested,
    bool FirstZeroCountCompletion,
    string BonusDetails,
    string? FailureReason = null,
    bool AlreadyAwarded = false)
{
    public bool Succeeded => FailureReason == null;
}

public sealed record ExpAwardedEvent(
    string Username,
    string GameId,
    int TotalExpBefore,
    int TotalExpAfter,
    int LevelBefore,
    int LevelAfter);

public sealed class ExpAwardBatch : IAsyncDisposable
{
    private readonly ExpAwarder _owner;
    private readonly object _context;
    private bool _disposed;

    internal ExpAwardBatch(ExpAwarder owner, object context)
    {
        _owner = owner;
        _context = context;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        return _owner.EndBatchAsync(_context);
    }
}

public sealed class ExpAwarder(
    DatabaseService db,
    ExpService exp,
    ActivityService activities,
    StreakService streaks,
    NewHabitService newHabits,
    DragonService dragons,
    AttemptService attempts)
{
    private sealed class BatchContext(BatchContext? parent)
    {
        public BatchContext? Parent { get; } = parent;
        public Dictionary<string, ExpAwardedEvent> Changes { get; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly DatabaseService _db = db;
    private readonly SemaphoreSlim _awardLock = new(1);
    private readonly AsyncLocal<BatchContext?> _currentBatch = new();

    public event Func<ExpAwardedEvent, Task>? AwardSucceeded;

    public static ExpAwarder Current =>
        Application.Current?.Handler?.MauiContext?.Services.GetRequiredService<ExpAwarder>()
        ?? throw new InvalidOperationException("ExpAwarder is unavailable.");

    public ExpAwardBatch BeginBatch()
    {
        var context = new BatchContext(_currentBatch.Value);
        _currentBatch.Value = context;
        return new ExpAwardBatch(this, context);
    }

    internal ValueTask EndBatchAsync(object contextObject)
    {
        if (contextObject is not BatchContext context || !ReferenceEquals(_currentBatch.Value, context))
            throw new InvalidOperationException("EXP batch scopes must be disposed in reverse order.");

        _currentBatch.Value = context.Parent;
        if (context.Parent != null)
        {
            foreach (var change in context.Changes.Values)
                AddBatchChange(context.Parent, change);
            return ValueTask.CompletedTask;
        }

        return new ValueTask(RaiseBatchSucceededAsync(context.Changes.Values.ToList()));
    }

    public static int GetBaseExp(Activity activity, int level, ExpAwardMode mode = ExpAwardMode.Normal)
    {
        if (activity.RewardType != "PercentOfLevel")
            return activity.ExpGain;

        return mode == ExpAwardMode.StreakCard
            ? ExpEngine.ExpForPercentOfLevel(Math.Min(level, activity.PercentCutoffLevel), activity.PercentOfLevel)
            : ExpEngine.ExpForPercentOfLevel(level, activity.PercentOfLevel, activity.PercentCutoffLevel);
    }

    public async Task<ExpAwardResult> AwardAsync(
        string username,
        Activity activity,
        ExpAwardMode mode = ExpAwardMode.Normal,
        string? gameId = null,
        int? displayedLevel = null,
        int? effectiveMultiplier = null,
        string? logDescription = null,
        DateTime? loggedAt = null,
        Func<Activity, Task>? onZeroCountCompleted = null,
        bool checkDragon = true,
        Func<SQLite.SQLiteConnection, bool>? markAward = null)
    {
        await _awardLock.WaitAsync();
        int awarded = 0, baseRequested = 0, bonusRequested = 0;
        int totalExpBefore = 0;
        int levelBefore = 0;
        string? resolvedGameId = null;
        bool capturedBefore = false;
        bool firstZeroCount = false;
        string bonusDetails = "";
        try
        {
            if (_db.IsReadOnly)
            {
                ReadOnlyModeNotifier.ShowBlockedWriteMessage();
                return new(0, 0, 0, false, "", "EXP cannot be awarded on a read-only device.");
            }

            gameId ??= activity.Game;
            resolvedGameId = gameId;
            totalExpBefore = await exp.GetTotalExpAsync(username, gameId);
            levelBefore = ExpEngine.GetProgress(totalExpBefore).level;
            capturedBefore = true;
            int level = displayedLevel ?? (await exp.GetProgressAsync(username, gameId)).level;
            int baseExp = GetBaseExp(activity, level, mode);
            bool backdatedMode = mode is ExpAwardMode.AutoAward or ExpAwardMode.CatchUp;
            int multiplier = effectiveMultiplier ?? (backdatedMode ? Math.Max(1, activity.Multiplier) : activity.Multiplier);
            firstZeroCount = mode != ExpAwardMode.CatchUp && activity.IsZeroCount && activity.TimesCompleted == 0;
            baseRequested = baseExp * multiplier * (firstZeroCount ? 10 : 1);
            bool combinedEntry = firstZeroCount || mode is ExpAwardMode.StreakCard or ExpAwardMode.AutoAward;
            string displayName = activity.IsImageOnly ? "" : activity.Name;
            int entries = combinedEntry ? 1 : multiplier;
            if (entries <= 0 && markAward != null)
                return new(0, 0, 0, false, "", "The activity must have a positive multiplier for a one-time award.");
            for (int i = 0; i < entries; i++)
            {
                string description = mode switch
                {
                    ExpAwardMode.AutoAward => $"{activity.Name} (Auto Award)",
                    ExpAwardMode.CatchUp when multiplier > 1 => $"{activity.Name} (Catch-Up {i + 1}/{multiplier})",
                    ExpAwardMode.CatchUp => $"{activity.Name} (Catch-Up)",
                    _ => logDescription ?? displayName
                };
                var applied = await exp.ApplyExpAsync(username, gameId, description,
                    combinedEntry ? baseRequested : baseExp, activity.Id, loggedAt, i == 0 ? markAward : null);
                if (applied.AlreadyAwarded)
                    return new(0, 0, 0, false, "", AlreadyAwarded: true);
                awarded += applied.Awarded;
            }

            if (activity.IsStreakTracked)
                await streaks.RecordActivityUsageAsync(username, gameId, activity.Id, activity.Name, activity, loggedAt);
            if (activity.HabitType != "None")
                await activities.RecordHabitCompletionAsync(activity, loggedAt);
            await activities.RecordDisplayDayStreakAsync(activity, loggedAt);

            activity.TimesCompleted++;
            if (firstZeroCount)
            {
                activity.IsZeroCount = false;
                activity.ZeroCountCompletedAt = DateTime.Now;
                activity.Category = "Misc";
            }
            await activities.UpdateActivityAsync(activity);
            if (firstZeroCount && onZeroCountCompleted != null)
                await onZeroCountCompleted(activity);

            if (backdatedMode)
                await newHabits.RecordHabitDoneAsync(activity.Id, loggedAt);
            else
                await RecordDailyNewHabitProgressAsync(username, activity.Id);

            if (activity.ExpGain > 0)
            {
                int bonus = ActivityService.CalculateStreakBonus(activity.DisplayDayStreak);
                if (bonus > 0)
                {
                    string suffix = mode switch
                    {
                        ExpAwardMode.AutoAward => "Auto Award Streak Bonus",
                        ExpAwardMode.CatchUp => "Catch-Up Streak Bonus",
                        _ => "Streak Bonus"
                    };
                    var applied = await exp.ApplyExpAsync(username, gameId, $"{displayName} ({suffix})",
                        bonus, activity.Id, loggedAt);
                    awarded += applied.Awarded;
                    bonusRequested = bonus;
                    string prefix = mode == ExpAwardMode.AutoAward ? "??" : "";
                    bonusDetails = $"{prefix} {displayName} streak bonus ({activity.DisplayDayStreak} days): +{bonus}";
                }
            }

            if (checkDragon)
            {
                var progress = await exp.GetProgressAsync(username, gameId);
                await CheckDragonVictoryAsync(username, gameId, progress.level);
            }

            int totalExpAfter = await exp.GetTotalExpAsync(username, gameId);
            int levelAfter = ExpEngine.GetProgress(totalExpAfter).level;
            await PublishAwardSucceededAsync(new ExpAwardedEvent(
                username, gameId, totalExpBefore, totalExpAfter, levelBefore, levelAfter));

            return new(awarded, baseRequested, bonusRequested, firstZeroCount, bonusDetails);
        }
        catch (Exception ex)
        {
            if (_currentBatch.Value != null && capturedBefore && resolvedGameId != null)
            {
                try
                {
                    int totalExpAfter = await exp.GetTotalExpAsync(username, resolvedGameId);
                    if (totalExpAfter != totalExpBefore)
                    {
                        AddBatchChange(_currentBatch.Value, new ExpAwardedEvent(
                            username, resolvedGameId, totalExpBefore, totalExpAfter, levelBefore,
                            ExpEngine.GetProgress(totalExpAfter).level));
                    }
                }
                catch (Exception batchEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to capture partial EXP batch change: {batchEx.Message}");
                }
            }
            return new(awarded, baseRequested, bonusRequested, firstZeroCount, bonusDetails, ex.Message);
        }
        finally
        {
            _awardLock.Release();
        }
    }

    private Task PublishAwardSucceededAsync(ExpAwardedEvent change)
    {
        if (_currentBatch.Value == null)
            return RaiseAwardSucceededAsync(change);

        AddBatchChange(_currentBatch.Value, change);
        return Task.CompletedTask;
    }

    private static void AddBatchChange(BatchContext context, ExpAwardedEvent change)
    {
        string key = $"{change.Username}\0{change.GameId}";
        if (context.Changes.TryGetValue(key, out var existing))
        {
            context.Changes[key] = existing with
            {
                TotalExpAfter = change.TotalExpAfter,
                LevelAfter = change.LevelAfter
            };
        }
        else
        {
            context.Changes.Add(key, change);
        }
    }

    private async Task RaiseBatchSucceededAsync(IReadOnlyList<ExpAwardedEvent> changes)
    {
        foreach (var change in changes)
            await RaiseAwardSucceededAsync(change);
    }

    private async Task RaiseAwardSucceededAsync(ExpAwardedEvent change)
    {
        var handlers = AwardSucceeded?.GetInvocationList()
            .Cast<Func<ExpAwardedEvent, Task>>()
            .ToList();
        if (handlers == null || handlers.Count == 0) return;

        await Task.WhenAll(handlers.Select(async handler =>
        {
            try
            {
                await handler(change);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EXP award listener failed: {ex.Message}");
            }
        }));
    }

    public async Task<bool> CheckDragonVictoryAsync(string username, string gameId, int level)
    {
        if (level < 100 || await dragons.IsDragonSlainAsync(username, gameId))
            return false;

        await dragons.SlayDragonAsync(username, gameId);
        var dragon = await dragons.GetDragonAsync(username, gameId);
        if (dragon != null)
            await attempts.MarkAttemptSuccessfulAsync(username, gameId, dragon.Title);
        return true;
    }

    public async Task<int> ApplyEscalationPenaltyAsync(string username, string gameId)
    {
        var (level, expInto, _) = await exp.GetProgressAsync(username, gameId);
        int loss = level >= 2
            ? ExpEngine.TotalExpAtLevelStart(level) - ExpEngine.TotalExpAtLevelStart(level - 2) + expInto
            : level == 1 ? expInto : 0;
        if (loss > 0)
            await ApplyAdjustmentAsync(username, gameId, "? Meaningful Escalation Penalty", -loss);
        return loss;
    }

    public Task ApplyAdjustmentAsync(string username, string gameId, string description, int amount, int activityId = 0) =>
        exp.ApplyExpAsync(username, gameId, description, amount, activityId);

    public Task<(bool leveledDown, int fromLevel, int toLevel, int expLost)> ApplyStreakBreakLevelDownAsync(
        string username, string gameId, Activity activity) =>
        exp.HandleStreakBreakLevelDownAsync(username, gameId, activity);

    public async Task<int> ImportSharedExpAsync(string username,
        IEnumerable<SyncService.SharedExpRecordDto> expRecords, IEnumerable<SyncService.SharedExpStateDto> expStates)
    {
        // Insert EXP records not already present in the actual ExpLog table.
        var conn = await _db.GetConnectionAsync();
        int expInserted = 0;
        foreach (var rec in expRecords)
        {
            var existing = await conn.Table<ExpLog>()
                .Where(r => r.Username == username &&
                    r.Game == rec.GameId &&
                    r.ActivityName == rec.ActivityName &&
                    r.LoggedAt == rec.Timestamp)
                .FirstOrDefaultAsync();
            if (existing != null) continue;

            var state = await conn.Table<ExpState>()
                .Where(s => s.Username == username &&
                    s.Game == rec.GameId)
                .FirstOrDefaultAsync();
            int totalBefore = state?.TotalExp ?? 0;
            var (levelBefore, _, _) = ExpEngine.GetProgress(totalBefore);
            var (levelAfter, _, _) = ExpEngine.GetProgress(
                totalBefore + rec.ExpGained);
            await conn.InsertAsync(new ExpLog
            {
                Username = username,
                Game = rec.GameId,
                ActivityName = rec.ActivityName,
                DeltaExp = rec.ExpGained,
                TotalExp = totalBefore + rec.ExpGained,
                LevelBefore = levelBefore,
                LevelAfter = levelAfter,
                LoggedAt = rec.Timestamp
            });
            expInserted++;
        }

        // ExpState stores TotalExp; game level is derived by ExpEngine.
        foreach (var state in expStates)
        {
            var localState = await conn.Table<ExpState>()
                .Where(s => s.Username == username &&
                    s.Game == state.GameId)
                .FirstOrDefaultAsync();
            if (localState == null)
            {
                await conn.InsertAsync(new ExpState
                {
                    Username = username,
                    Game = state.GameId,
                    TotalExp = state.TotalExp,
                    UpdatedAt = state.LastUpdated
                });
            }
            else if (state.TotalExp > localState.TotalExp)
            {
                localState.TotalExp = state.TotalExp;
                localState.UpdatedAt = state.LastUpdated;
                await conn.UpdateAsync(localState);
            }
        }

        return expInserted;
    }

    public async Task<bool> DeleteLogEntryAsync(string username, string gameId, int id)
    {
        var conn = await _db.GetConnectionAsync();
        bool deleted = false;
        await conn.RunInTransactionAsync(db =>
        {
            var log = db.Table<ExpLog>().FirstOrDefault(row => row.Id == id && row.Username == username && row.Game == gameId);
            if (log == null) return;

            var state = db.Table<ExpState>().FirstOrDefault(row => row.Username == username && row.Game == gameId);
            if (state != null)
            {
                state.TotalExp -= log.DeltaExp;
                state.UpdatedAt = DateTime.UtcNow;
                db.Update(state);
            }

            db.Delete(log);
            deleted = true;
        });
        return deleted;
    }

    /// <summary>
    /// Record that a habit's positive activity was completed.
    /// ONLY increments the streak counter - does NOT graduate or change allowance.
    /// Graduation happens only when entering the Habits page.
    /// </summary>
    private async Task RecordDailyNewHabitProgressAsync(string username, int activityId)
    {
        try
        {
            var conn = await _db.GetConnectionAsync();
            
            var habit = await conn.Table<NewHabit>()
                .Where(h => h.Username == username 
                    && h.PositiveActivityId == activityId 
                    && h.Status == "active")
                .FirstOrDefaultAsync();

            if (habit == null) return;

            var today = DateTime.UtcNow.Date;

            if (habit.LastAppliedDate.HasValue && habit.LastAppliedDate.Value.Date == today)
            {
                System.Diagnostics.Debug.WriteLine($"[NEWHABIT] Already recorded today for {habit.HabitName}");
                return;
            }

            bool isConsecutive = habit.LastAppliedDate.HasValue 
                && (today - habit.LastAppliedDate.Value.Date).TotalDays == 1;

            if (isConsecutive || habit.ConsecutiveDays == 0)
            {
                habit.ConsecutiveDays++;
            }
            else
            {
                habit.ConsecutiveDays = 1;
            }

            habit.LastAppliedDate = today;

            // NOTE: We do NOT graduate here anymore.
            // Graduation only happens in the Habits page with user confirmation.
            // This method ONLY updates the streak counter.

            await conn.UpdateAsync(habit);
            System.Diagnostics.Debug.WriteLine($"[NEWHABIT] Recorded day {habit.ConsecutiveDays}/{habit.DaysToGraduate} for {habit.HabitName}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NEWHABIT] Error recording progress: {ex.Message}");
        }
    }

}
