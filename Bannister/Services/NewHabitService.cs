using Bannister.Models;

namespace Bannister.Services;

public class NewHabitService
{
    private readonly DatabaseService _db;
    private readonly ActivityService _activities;

    public NewHabitService(DatabaseService db, ActivityService activities)
    {
        _db = db;
        _activities = activities;
    }

    #region Allowance Management

    public async Task<HabitAllowance> GetOrCreateAllowanceAsync(string username, string frequency = "Daily")
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<HabitAllowance>();
        
        var allowance = await conn.Table<HabitAllowance>()
            .Where(a => a.Username == username && a.Frequency == frequency)
            .FirstOrDefaultAsync();

        if (allowance == null)
        {
            allowance = new HabitAllowance
            {
                Username = username,
                Frequency = frequency,
                CurrentAllowance = 1,
                HighestAllowance = 1
            };
            await conn.InsertAsync(allowance);
        }

        return allowance;
    }

    public async Task<int> GetAvailableSlotsAsync(string username, string frequency = "Daily")
    {
        var allowance = await GetOrCreateAllowanceAsync(username, frequency);
        var activeHabits = await GetAllActiveHabitsAsync(username);
        var habitsOfFrequency = activeHabits.Where(h => h.Frequency == frequency).Count();
        return Math.Max(0, allowance.CurrentAllowance - habitsOfFrequency);
    }

    public async Task UpdateAllowanceAsync(HabitAllowance allowance)
    {
        var conn = await _db.GetConnectionAsync();
        if (allowance.CurrentAllowance > allowance.HighestAllowance)
        {
            allowance.HighestAllowance = allowance.CurrentAllowance;
        }
        await conn.UpdateAsync(allowance);
    }

    /// <summary>
    /// Increase allowance by 1. Only call after user confirmation.
    /// Resets the graduation counter to the new allowance value.
    /// </summary>
    public async Task IncreaseAllowanceAsync(string username, string frequency)
    {
        var allowance = await GetOrCreateAllowanceAsync(username, frequency);
        allowance.CurrentAllowance++;
        if (allowance.CurrentAllowance > allowance.HighestAllowance)
        {
            allowance.HighestAllowance = allowance.CurrentAllowance;
        }
        // Reset counter to new allowance value
        allowance.GraduationsUntilIncrease = allowance.CurrentAllowance;
        await UpdateAllowanceAsync(allowance);
        System.Diagnostics.Debug.WriteLine($"[NEW HABIT] {frequency} allowance increased to {allowance.CurrentAllowance}, counter reset to {allowance.GraduationsUntilIncrease}");
    }

    /// <summary>
    /// Check if user is eligible for allowance increase.
    /// Returns true only if GraduationsUntilIncrease has reached 0.
    /// </summary>
    public async Task<bool> IsEligibleForAllowanceIncreaseAsync(string username, string frequency)
    {
        var allowance = await GetOrCreateAllowanceAsync(username, frequency);
        return allowance.GraduationsUntilIncrease <= 0;
    }

    /// <summary>
    /// Get count of graduations needed before allowance can increase.
    /// </summary>
    public async Task<int> GetHabitsUntilAllowanceIncreaseAsync(string username, string frequency)
    {
        var allowance = await GetOrCreateAllowanceAsync(username, frequency);
        return Math.Max(0, allowance.GraduationsUntilIncrease);
    }

    /// <summary>
    /// Reset the graduation counter (e.g., when user declines allowance increase or manually edits).
    /// </summary>
    public async Task ResetGraduationCounterAsync(string username, string frequency)
    {
        var allowance = await GetOrCreateAllowanceAsync(username, frequency);
        allowance.GraduationsUntilIncrease = allowance.CurrentAllowance;
        await UpdateAllowanceAsync(allowance);
        System.Diagnostics.Debug.WriteLine($"[NEW HABIT] {frequency} graduation counter reset to {allowance.GraduationsUntilIncrease}");
    }

    /// <summary>
    /// Get the start of the week (Sunday) for a given date
    /// </summary>
    public static DateTime GetWeekStart(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Sunday)) % 7;
        return date.Date.AddDays(-diff);
    }

    /// <summary>
    /// Check if daily habit slots are filled for the current week
    /// </summary>
    public async Task<bool> IsDailyAllowanceFilledThisWeekAsync(string username)
    {
        var allowance = await GetOrCreateAllowanceAsync(username, "Daily");
        var activeHabits = await GetActiveHabitsByFrequencyAsync(username, "Daily");
        
        // Check if active habits >= allowance
        return activeHabits.Count >= allowance.CurrentAllowance;
    }

    /// <summary>
    /// Get the fill status for daily habits
    /// Returns (activeCount, requiredCount, isFilledThisWeek)
    /// </summary>
    public async Task<(int Active, int Required, bool IsFilled)> GetDailyFillStatusAsync(string username)
    {
        var allowance = await GetOrCreateAllowanceAsync(username, "Daily");
        var activeHabits = await GetActiveHabitsByFrequencyAsync(username, "Daily");
        
        bool isFilled = activeHabits.Count >= allowance.CurrentAllowance;
        return (activeHabits.Count, allowance.CurrentAllowance, isFilled);
    }

    /// <summary>
    /// Process week end for daily habits - reduce allowance if not filled
    /// Call this on Sunday or when checking for missed weeks
    /// </summary>
    public async Task ProcessDailyHabitWeekEndAsync(string username)
    {
        var allowance = await GetOrCreateAllowanceAsync(username, "Daily");
        var currentWeekStart = GetWeekStart(DateTime.Today);
        
        // Check if we already processed this week
        if (allowance.LastFilledWeekStart.HasValue && 
            allowance.LastFilledWeekStart.Value >= currentWeekStart)
        {
            return; // Already processed this week
        }

        // Check last week
        var lastWeekStart = currentWeekStart.AddDays(-7);
        
        // If LastFilledWeekStart is before last week, we missed a week
        if (!allowance.LastFilledWeekStart.HasValue || 
            allowance.LastFilledWeekStart.Value < lastWeekStart)
        {
            // Check if last week was filled (by checking if habits were active)
            var activeHabits = await GetActiveHabitsByFrequencyAsync(username, "Daily");
            
            if (activeHabits.Count < allowance.CurrentAllowance)
            {
                // Not filled - reduce allowance
                allowance.CurrentAllowance = Math.Max(1, allowance.CurrentAllowance - 1);
                System.Diagnostics.Debug.WriteLine($"[NEW HABIT] Daily allowance reduced to {allowance.CurrentAllowance} due to unfilled slots");
            }
        }

        // Update the last checked week
        allowance.LastFilledWeekStart = currentWeekStart;
        await UpdateAllowanceAsync(allowance);
    }

    /// <summary>
    /// Mark daily habits as filled for this week (called when slots are filled)
    /// </summary>
    public async Task MarkDailyAllowanceFilledAsync(string username)
    {
        var allowance = await GetOrCreateAllowanceAsync(username, "Daily");
        var currentWeekStart = GetWeekStart(DateTime.Today);
        
        allowance.LastFilledWeekStart = currentWeekStart;
        allowance.IsFilledThisWeek = true;
        await UpdateAllowanceAsync(allowance);
        System.Diagnostics.Debug.WriteLine($"[NEW HABIT] Daily allowance marked as filled for week starting {currentWeekStart:yyyy-MM-dd}");
    }

    /// <summary>
    /// Check if user needs to fill daily habit slots (for HomePage alert)
    /// Returns true if it's Saturday and slots are not filled
    /// </summary>
    public async Task<(bool NeedsAlert, int Active, int Required)> CheckDailyHabitAlertAsync(string username)
    {
        // Only alert on Saturday (last day before week resets)
        if (DateTime.Today.DayOfWeek != DayOfWeek.Saturday)
        {
            return (false, 0, 0);
        }

        var (active, required, isFilled) = await GetDailyFillStatusAsync(username);
        
        // Need alert if not filled
        return (!isFilled, active, required);
    }

    #endregion

    #region Habit CRUD

    public async Task<List<NewHabit>> GetAllActiveHabitsAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<NewHabit>();
        
        return await conn.Table<NewHabit>()
            .Where(h => h.Username == username && h.Status == "active")
            .ToListAsync();
    }

    public async Task<List<NewHabit>> GetActiveHabitsAsync(string username, string game)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<NewHabit>();
        
        return await conn.Table<NewHabit>()
            .Where(h => h.Username == username && h.Game == game && h.Status == "active")
            .ToListAsync();
    }

    public async Task<List<NewHabit>> GetActiveHabitsByFrequencyAsync(string username, string frequency)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<NewHabit>();
        
        return await conn.Table<NewHabit>()
            .Where(h => h.Username == username && h.Frequency == frequency && h.Status == "active")
            .ToListAsync();
    }

    public async Task<List<NewHabit>> GetAllGraduatedHabitsAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<NewHabit>();
        
        return await conn.Table<NewHabit>()
            .Where(h => h.Username == username && h.Status == "graduated")
            .OrderByDescending(h => h.CompletedAt)
            .ToListAsync();
    }

    public async Task<List<NewHabit>> GetGraduatedHabitsAsync(string username, string game)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<NewHabit>();
        
        return await conn.Table<NewHabit>()
            .Where(h => h.Username == username && h.Game == game && h.Status == "graduated")
            .OrderByDescending(h => h.CompletedAt)
            .ToListAsync();
    }

    public async Task<List<NewHabit>> GetFailedHabitsAsync(string username, string game)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<NewHabit>();
        
        return await conn.Table<NewHabit>()
            .Where(h => h.Username == username && h.Game == game && h.Status == "failed")
            .OrderByDescending(h => h.CompletedAt)
            .ToListAsync();
    }

    public async Task<NewHabit?> GetHabitByIdAsync(int habitId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.FindAsync<NewHabit>(habitId);
    }

    public async Task UpdateHabitAsync(NewHabit habit)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(habit);
    }

    #region Pending Habits

    public async Task<List<NewHabit>> GetAllPendingHabitsAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<NewHabit>();
        
        return await conn.Table<NewHabit>()
            .Where(h => h.Username == username && h.Status == "pending")
            .OrderBy(h => h.PendingOrder)
            .ToListAsync();
    }

    public async Task<NewHabit> CreatePendingHabitAsync(
        string username,
        string game,
        string habitName,
        int positiveActivityId,
        int negativeActivityId)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<NewHabit>();

        // Get max pending order
        var pendingHabits = await GetAllPendingHabitsAsync(username);
        int maxOrder = pendingHabits.Count > 0 ? pendingHabits.Max(h => h.PendingOrder) : 0;

        var newHabit = new NewHabit
        {
            Username = username,
            Game = game,
            HabitName = habitName,
            PositiveActivityId = positiveActivityId,
            NegativeActivityId = negativeActivityId,
            ConsecutiveDays = 0,
            Status = "pending",
            PendingOrder = maxOrder + 1
        };

        await conn.InsertAsync(newHabit);
        return newHabit;
    }

    public async Task ActivatePendingHabitAsync(NewHabit habit)
    {
        var conn = await _db.GetConnectionAsync();
        
        habit.Status = "active";
        habit.StartedAt = DateTime.UtcNow;
        habit.ConsecutiveDays = 0;
        habit.LastAppliedDate = null;
        
        await conn.UpdateAsync(habit);
        
        System.Diagnostics.Debug.WriteLine($"[NEW HABIT] Activated pending habit: {habit.HabitName}");
    }

    public async Task MoveToPendingAsync(NewHabit habit)
    {
        var conn = await _db.GetConnectionAsync();
        
        // Get max pending order
        var pendingHabits = await GetAllPendingHabitsAsync(habit.Username);
        int maxOrder = pendingHabits.Count > 0 ? pendingHabits.Max(h => h.PendingOrder) : 0;
        
        habit.Status = "pending";
        habit.ConsecutiveDays = 0;
        habit.LastAppliedDate = null;
        habit.PendingOrder = maxOrder + 1;
        
        await conn.UpdateAsync(habit);
        
        System.Diagnostics.Debug.WriteLine($"[NEW HABIT] Moved to pending: {habit.HabitName}");
    }

    public async Task DeletePendingHabitAsync(NewHabit habit)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync(habit);
        
        System.Diagnostics.Debug.WriteLine($"[NEW HABIT] Deleted pending habit: {habit.HabitName}");
    }

    public async Task MovePendingHabitUpAsync(NewHabit habit)
    {
        var conn = await _db.GetConnectionAsync();
        var pendingHabits = await GetAllPendingHabitsAsync(habit.Username);
        
        var currentIndex = pendingHabits.FindIndex(h => h.Id == habit.Id);
        if (currentIndex > 0)
        {
            var aboveHabit = pendingHabits[currentIndex - 1];
            int tempOrder = habit.PendingOrder;
            habit.PendingOrder = aboveHabit.PendingOrder;
            aboveHabit.PendingOrder = tempOrder;
            
            await conn.UpdateAsync(habit);
            await conn.UpdateAsync(aboveHabit);
        }
    }

    public async Task MovePendingHabitDownAsync(NewHabit habit)
    {
        var conn = await _db.GetConnectionAsync();
        var pendingHabits = await GetAllPendingHabitsAsync(habit.Username);
        
        var currentIndex = pendingHabits.FindIndex(h => h.Id == habit.Id);
        if (currentIndex < pendingHabits.Count - 1)
        {
            var belowHabit = pendingHabits[currentIndex + 1];
            int tempOrder = habit.PendingOrder;
            habit.PendingOrder = belowHabit.PendingOrder;
            belowHabit.PendingOrder = tempOrder;
            
            await conn.UpdateAsync(habit);
            await conn.UpdateAsync(belowHabit);
        }
    }

    #endregion

    public async Task<NewHabit?> CreateNewHabitAsync(
        string username, 
        string game, 
        string habitName,
        int positiveExp,
        int negativeExp,
        string category = "New Habits",
        string? imagePath = null,
        string frequency = "Daily")
    {
        // Check if there's available allowance for this frequency
        var availableSlots = await GetAvailableSlotsAsync(username, frequency);
        if (availableSlots <= 0)
        {
            System.Diagnostics.Debug.WriteLine($"[NEW HABIT] No available {frequency} slots");
            return null;
        }

        var conn = await _db.GetConnectionAsync();

        // Set days to graduate based on frequency
        int daysToGraduate = frequency switch
        {
            "Daily" => 7,
            "Weekly" => 4,
            "Monthly" => 3,
            _ => 7
        };

        // Create the positive activity
        var positiveActivity = new Activity
        {
            Username = username,
            Game = game,
            Name = habitName,
            ExpGain = positiveExp,
            MeaningfulUntilLevel = positiveExp / 2,
            Category = category,
            ImagePath = imagePath ?? "",
            HabitType = frequency,
            StartDate = DateTime.Now
        };
        await conn.InsertAsync(positiveActivity);

        // Create the negative activity
        var negativeActivity = new Activity
        {
            Username = username,
            Game = game,
            Name = $"{habitName} (Missed)",
            ExpGain = -Math.Abs(negativeExp),
            MeaningfulUntilLevel = 100,
            Category = "Negative",
            ImagePath = imagePath ?? "",
            HabitType = "None",
            StartDate = DateTime.Now
        };
        await conn.InsertAsync(negativeActivity);

        // Create the new habit tracker
        var newHabit = new NewHabit
        {
            Username = username,
            Game = game,
            HabitName = habitName,
            PositiveActivityId = positiveActivity.Id,
            NegativeActivityId = negativeActivity.Id,
            ConsecutiveDays = 0,
            DaysToGraduate = daysToGraduate,
            Frequency = frequency,
            StartedAt = DateTime.UtcNow,
            Status = "active"
        };
        await conn.InsertAsync(newHabit);

        System.Diagnostics.Debug.WriteLine($"[NEW HABIT] Created '{habitName}' ({frequency}) with positive ID {positiveActivity.Id} and negative ID {negativeActivity.Id}");

        return newHabit;
    }

    public async Task DeleteHabitAsync(int habitId)
    {
        var conn = await _db.GetConnectionAsync();
        var habit = await conn.GetAsync<NewHabit>(habitId);
        
        if (habit != null)
        {
            // Also delete/deactivate the associated activities
            await _activities.BlankActivityAsync(habit.PositiveActivityId);
            await _activities.BlankActivityAsync(habit.NegativeActivityId);
            
            await conn.DeleteAsync(habit);
        }
    }

    #endregion

    #region Daily Check & Recording

    /// <summary>
    /// Get habits that are ready to graduate (all circles filled).
    /// These need user confirmation before graduating.
    /// </summary>
    public async Task<List<NewHabit>> GetHabitsReadyToGraduateAsync(string username, string frequency)
    {
        var activeHabits = await GetAllActiveHabitsAsync(username);
        return activeHabits
            .Where(h => h.Frequency == frequency && h.ConsecutiveDays >= h.DaysToGraduate)
            .ToList();
    }

    /// <summary>
    /// Record that the positive activity was done today.
    /// Call this when user clicks the positive activity in Game window.
    /// Only increments streak - does NOT graduate or change allowance.
    /// </summary>
    /// <param name="positiveActivityId">Activity ID</param>
    /// <returns>The updated habit, or null if not found or already recorded today</returns>
    public async Task<NewHabit?> RecordHabitDoneAsync(int positiveActivityId)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<NewHabit>();
        
        var habit = await conn.Table<NewHabit>()
            .Where(h => h.PositiveActivityId == positiveActivityId && h.Status == "active")
            .FirstOrDefaultAsync();

        if (habit == null) return null;

        var today = DateTime.UtcNow.Date;
        var lastDate = habit.LastAppliedDate?.Date;

        if (lastDate == today)
        {
            // Already recorded today
            System.Diagnostics.Debug.WriteLine($"[NEW HABIT] '{habit.HabitName}' already recorded today");
            return null;
        }

        if (lastDate == today.AddDays(-1))
        {
            // Consecutive day!
            habit.ConsecutiveDays++;
            System.Diagnostics.Debug.WriteLine($"[NEW HABIT] '{habit.HabitName}' consecutive day {habit.ConsecutiveDays}");
        }
        else if (lastDate == null)
        {
            // First day
            habit.ConsecutiveDays = 1;
            System.Diagnostics.Debug.WriteLine($"[NEW HABIT] '{habit.HabitName}' first day");
        }
        else
        {
            // Missed days - this shouldn't happen if CheckMissedHabitsAsync runs daily
            // But handle it gracefully by resetting
            habit.ConsecutiveDays = 1;
            System.Diagnostics.Debug.WriteLine($"[NEW HABIT] '{habit.HabitName}' reset after gap");
        }

        habit.LastAppliedDate = today;

        // NOTE: We do NOT graduate here anymore.
        // Graduation only happens in the Habits page with user confirmation.
        
        await conn.UpdateAsync(habit);
        return habit;
    }

    /// <summary>
    /// Check for missed habits and apply penalties.
    /// Call this once per day (e.g., on app start or game load).
    /// </summary>
    public async Task<List<NewHabit>> CheckMissedHabitsAsync(string username, string game, ExpService expService)
    {
        var conn = await _db.GetConnectionAsync();
        var activeHabits = await GetActiveHabitsAsync(username, game);
        var today = DateTime.UtcNow.Date;
        var missedHabits = new List<NewHabit>();

        foreach (var habit in activeHabits)
        {
            if (!habit.LastAppliedDate.HasValue)
            {
                // Never started - check if it's been more than 1 day since created
                if ((today - habit.StartedAt.Date).TotalDays > 1)
                {
                    await FailHabitAsync(habit, expService, game);
                    missedHabits.Add(habit);
                }
                continue;
            }

            var lastDate = habit.LastAppliedDate.Value.Date;
            var daysMissed = (today - lastDate).TotalDays;

            if (daysMissed > 1)
            {
                // Missed yesterday (or more) - FAIL
                await FailHabitAsync(habit, expService, game);
                missedHabits.Add(habit);
            }
        }

        return missedHabits;
    }

    /// <summary>
    /// Graduate a habit WITHOUT changing allowance.
    /// Call this after user confirms graduation.
    /// Decrements GraduationsUntilIncrease counter.
    /// Allowance increase is handled separately via IncreaseAllowanceAsync.
    /// </summary>
    public async Task<NewHabit> GraduateHabitAsync(NewHabit habit)
    {
        var conn = await _db.GetConnectionAsync();
        
        habit.Status = "graduated";
        habit.CompletedAt = DateTime.UtcNow;
        await conn.UpdateAsync(habit);

        // Update graduated count and decrement the counter
        var allowance = await GetOrCreateAllowanceAsync(habit.Username, habit.Frequency);
        allowance.TotalGraduated++;
        allowance.GraduationsUntilIncrease = Math.Max(0, allowance.GraduationsUntilIncrease - 1);
        await conn.UpdateAsync(allowance);

        System.Diagnostics.Debug.WriteLine($"[NEW HABIT] '{habit.HabitName}' GRADUATED! (allowance unchanged until all slots empty)");
        
        return habit;
    }

    /// <summary>
    /// Remove the negative activity associated with a graduated habit
    /// </summary>
    public async Task RemoveNegativeActivityAsync(NewHabit habit)
    {
        await _activities.BlankActivityAsync(habit.NegativeActivityId);
        System.Diagnostics.Debug.WriteLine($"[NEW HABIT] Removed negative activity for '{habit.HabitName}'");
    }

    /// <summary>
    /// Manually fail a habit (called from UI when user chooses to fail)
    /// </summary>
    public async Task FailHabitManualAsync(NewHabit habit, ExpService? expService = null, string? game = null)
    {
        var conn = await _db.GetConnectionAsync();

        // Apply the negative penalty if expService provided
        if (expService != null)
        {
            var negativeActivity = await _activities.GetActivityAsync(habit.NegativeActivityId);
            if (negativeActivity != null)
            {
                string gameToUse = game ?? habit.Game;
                await expService.ApplyExpAsync(habit.Username, gameToUse, negativeActivity.Name, negativeActivity.ExpGain, negativeActivity.Id);
                System.Diagnostics.Debug.WriteLine($"[NEW HABIT] Applied penalty {negativeActivity.ExpGain} for manual fail '{habit.HabitName}'");
            }
        }

        habit.Status = "failed";
        habit.CompletedAt = DateTime.UtcNow;
        await conn.UpdateAsync(habit);

        // Decrease frequency-specific allowance (minimum 1)
        var allowance = await GetOrCreateAllowanceAsync(habit.Username, habit.Frequency);
        allowance.CurrentAllowance = Math.Max(1, allowance.CurrentAllowance - 1);
        allowance.TotalFailed++;
        await UpdateAllowanceAsync(allowance);

        // NOTE: Activities are NOT blanked - they remain usable in the game
        // User can manually delete them if desired

        System.Diagnostics.Debug.WriteLine($"[NEW HABIT] '{habit.HabitName}' FAILED (manual)! {habit.Frequency} allowance now {allowance.CurrentAllowance}");
    }

    private async Task FailHabitAsync(NewHabit habit, ExpService expService, string game)
    {
        var conn = await _db.GetConnectionAsync();

        // Apply the negative penalty
        var negativeActivity = await _activities.GetActivityAsync(habit.NegativeActivityId);
        if (negativeActivity != null)
        {
            await expService.ApplyExpAsync(habit.Username, game, negativeActivity.Name, negativeActivity.ExpGain, negativeActivity.Id);
            System.Diagnostics.Debug.WriteLine($"[NEW HABIT] Applied penalty {negativeActivity.ExpGain} for missing '{habit.HabitName}'");
        }

        habit.Status = "failed";
        habit.CompletedAt = DateTime.UtcNow;
        await conn.UpdateAsync(habit);

        // Decrease frequency-specific allowance (minimum 1)
        var allowance = await GetOrCreateAllowanceAsync(habit.Username, habit.Frequency);
        allowance.CurrentAllowance = Math.Max(1, allowance.CurrentAllowance - 1);
        allowance.TotalFailed++;
        await UpdateAllowanceAsync(allowance);

        // NOTE: Activities are NOT blanked - they remain usable in the game
        // User can manually delete them if desired

        System.Diagnostics.Debug.WriteLine($"[NEW HABIT] '{habit.HabitName}' FAILED! {habit.Frequency} allowance now {allowance.CurrentAllowance}");
    }

    #endregion
}
