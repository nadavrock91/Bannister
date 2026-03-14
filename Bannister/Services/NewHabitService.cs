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
    /// Record that the positive activity was done today.
    /// Call this when user clicks the positive activity.
    /// Returns the habit if it graduated, null otherwise.
    /// </summary>
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

        // Check if graduated
        if (habit.ConsecutiveDays >= habit.DaysToGraduate)
        {
            return await GraduateHabitAsync(habit);
        }
        else
        {
            await conn.UpdateAsync(habit);
            return null;
        }
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
    /// Graduate a habit. Returns the habit for UI to ask about removing negative.
    /// </summary>
    public async Task<NewHabit?> GraduateHabitAsync(NewHabit habit)
    {
        var conn = await _db.GetConnectionAsync();
        
        habit.Status = "graduated";
        habit.CompletedAt = DateTime.UtcNow;
        await conn.UpdateAsync(habit);

        // Increase frequency-specific allowance
        var allowance = await GetOrCreateAllowanceAsync(habit.Username, habit.Frequency);
        allowance.CurrentAllowance++;
        allowance.TotalGraduated++;
        await UpdateAllowanceAsync(allowance);

        System.Diagnostics.Debug.WriteLine($"[NEW HABIT] '{habit.HabitName}' GRADUATED! {habit.Frequency} allowance now {allowance.CurrentAllowance}");
        
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

        // Deactivate both activities
        await _activities.BlankActivityAsync(habit.PositiveActivityId);
        await _activities.BlankActivityAsync(habit.NegativeActivityId);

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

        // Deactivate both activities
        await _activities.BlankActivityAsync(habit.PositiveActivityId);
        await _activities.BlankActivityAsync(habit.NegativeActivityId);

        System.Diagnostics.Debug.WriteLine($"[NEW HABIT] '{habit.HabitName}' FAILED! {habit.Frequency} allowance now {allowance.CurrentAllowance}");
    }

    #endregion
}
