using SQLite;

namespace Bannister.Models;

[Table("game_activities")]
public class Activity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Username { get; set; } = "";

    public string Game { get; set; } = "";

    public string Name { get; set; } = "";

    public int ExpGain { get; set; } = 10;

    public int MeaningfulUntilLevel { get; set; } = 100;

    public string ImagePath { get; set; } = "";

    public string Category { get; set; } = "Misc";

    // Start/End dates now include time in the DateTime value
    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public int Multiplier { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    // Possible activity - idea for future, not yet active
    public bool IsPossible { get; set; } = false;

    // Reward type: "Fixed" (based on MeaningfulUntilLevel) or "PercentOfLevel" (dynamic)
    public string RewardType { get; set; } = "Fixed";
    
    // For PercentOfLevel type: percentage of EXP needed for current level (default 1%)
    public double PercentOfLevel { get; set; } = 1.0;
    
    // For PercentOfLevel type: level at which percent scaling stops (uses this level's value from then on)
    public int PercentCutoffLevel { get; set; } = 100;

    // Streak tracking
    public bool IsStreakTracked { get; set; } = false;
    
    // If true, streak auto-increments daily without needing to click the activity
    public bool IsStreakAutoIncrement { get; set; } = false;

    // Auto-award settings
    [Indexed]
    public bool IsAutoAward { get; set; } = false;
    
    public string AutoAwardFrequency { get; set; } = "None";  // "Daily", "Weekly", "Monthly", "None"
    
    public string AutoAwardDays { get; set; } = "";  // Comma-separated: "Monday,Wednesday,Friday"
    
    public DateTime? LastAutoAwarded { get; set; }  // Last time it was auto-awarded

    // Habit Tracking (for building habits through repetition)
    public string HabitType { get; set; } = "None";  // "None", "Daily", "Weekly", "Monthly"
    
    public int HabitStreak { get; set; } = 0;  // Current streak count
    
    public DateTime? LastHabitDate { get; set; }  // Last date habit was recorded
    
    /// <summary>
    /// Which attempt number this streak is on (increments when streak resets)
    /// </summary>
    public int StreakAttemptNumber { get; set; } = 1;
    
    /// <summary>
    /// When the current streak started
    /// </summary>
    public DateTime? StreakStartDate { get; set; }

    // Aliases for ViewModel compatibility
    [Ignore]
    public int StreakCount
    {
        get => HabitStreak;
        set => HabitStreak = value;
    }

    /// <summary>
    /// How many times this activity has been completed (increments each time EXP is gained)
    /// </summary>
    public int TimesCompleted { get; set; } = 0;

    /// <summary>
    /// Whether to show the TimesCompleted badge on the activity card
    /// </summary>
    public bool ShowTimesCompletedBadge { get; set; } = false;
    
    /// <summary>
    /// Returns true if this activity has achieved habit status based on its type
    /// </summary>
    [Ignore]
    public bool IsHabit
    {
        get
        {
            return HabitType switch
            {
                "Daily" => HabitStreak >= 7,      // 7 consecutive days
                "Weekly" => HabitStreak >= 4,     // 4 consecutive weeks
                "Monthly" => HabitStreak >= 3,    // 3 consecutive months
                _ => false
            };
        }
    }
    
    /// <summary>
    /// Returns progress percentage towards habit status (0-100)
    /// </summary>
    [Ignore]
    public int HabitProgress
    {
        get
        {
            int target = HabitType switch
            {
                "Daily" => 7,
                "Weekly" => 4,
                "Monthly" => 3,
                _ => 1
            };
            return Math.Min(100, (int)(HabitStreak * 100.0 / target));
        }
    }

    // Habit Target Date - by when user wants to turn this activity into a habit
    public DateTime? HabitTargetDate { get; set; }
    
    // If true, user explicitly doesn't want this activity as a habit
    public bool NoHabitTarget { get; set; } = false;
    
    // When the habit target was first set (to track total days)
    public DateTime? HabitTargetFirstSet { get; set; }
    
    // How many times the target has been postponed/reset
    public int HabitTargetPostponeCount { get; set; } = 0;
    
    /// <summary>
    /// Returns true if this activity needs a habit decision (neither target date nor opted out)
    /// </summary>
    [Ignore]
    public bool NeedsHabitDecision => !NoHabitTarget && !HabitTargetDate.HasValue && ExpGain > 0 && Category != "Negative";
    
    /// <summary>
    /// Returns true if the habit target date has passed
    /// </summary>
    [Ignore]
    public bool IsHabitTargetExpired => HabitTargetDate.HasValue && HabitTargetDate.Value.Date < DateTime.Now.Date;
    
    /// <summary>
    /// Days since habit target was first set
    /// </summary>
    [Ignore]
    public int DaysSinceHabitTargetSet => HabitTargetFirstSet.HasValue 
        ? (int)(DateTime.Now.Date - HabitTargetFirstSet.Value.Date).TotalDays 
        : 0;

    // ============ DISPLAY DAY SETTINGS ============
    
    /// <summary>
    /// Comma-separated days of week when activity should display.
    /// Values: "Sun,Mon,Tue,Wed,Thu,Fri,Sat" or empty for all days.
    /// </summary>
    public string DisplayDaysOfWeek { get; set; } = "";  // Empty = all days
    
    /// <summary>
    /// Specific day of month (1-31) when activity should display.
    /// 0 or null means not restricted to a specific day of month.
    /// </summary>
    public int DisplayDayOfMonth { get; set; } = 0;  // 0 = not restricted
    
    // ============ DISPLAY DAY STREAK TRACKING ============
    
    /// <summary>
    /// Current streak of consecutive scheduled days where activity was used.
    /// Resets if a scheduled day is missed.
    /// </summary>
    public int DisplayDayStreak { get; set; } = 0;
    
    /// <summary>
    /// Last date (scheduled display day) when activity was used.
    /// Used to determine if streak continues or breaks.
    /// </summary>
    public DateTime? LastDisplayDayUsed { get; set; }
    
    /// <summary>
    /// If true, don't track consecutive day streaks for this activity.
    /// </summary>
    public bool OptOutDisplayDayStreak { get; set; } = false;
    
    /// <summary>
    /// Number of times this activity has appeared in the missing images popup.
    /// After 3 times, a black image is auto-assigned.
    /// </summary>
    public int MissingImagePromptCount { get; set; } = 0;
    
    /// <summary>
    /// Returns true if this activity should be displayed today based on day restrictions
    /// </summary>
    [Ignore]
    public bool ShouldDisplayToday
    {
        get
        {
            var today = DateTime.Now;
            
            // Check day of month restriction first
            if (DisplayDayOfMonth > 0)
            {
                return today.Day == DisplayDayOfMonth;
            }
            
            // Check day of week restriction
            if (!string.IsNullOrEmpty(DisplayDaysOfWeek))
            {
                var allowedDays = DisplayDaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var todayAbbrev = today.DayOfWeek switch
                {
                    DayOfWeek.Sunday => "Sun",
                    DayOfWeek.Monday => "Mon",
                    DayOfWeek.Tuesday => "Tue",
                    DayOfWeek.Wednesday => "Wed",
                    DayOfWeek.Thursday => "Thu",
                    DayOfWeek.Friday => "Fri",
                    DayOfWeek.Saturday => "Sat",
                    _ => ""
                };
                return allowedDays.Contains(todayAbbrev);
            }
            
            // No restrictions - show every day
            return true;
        }
    }
    
    /// <summary>
    /// Returns the display day streak as a formatted string (e.g., "🔥5")
    /// </summary>
    [Ignore]
    public string DisplayDayStreakDisplay
    {
        get
        {
            if (OptOutDisplayDayStreak || DisplayDayStreak == 0)
                return "";
            return $"🔥{DisplayDayStreak}";
        }
    }
    
    /// <summary>
    /// Check if a given date is a scheduled display day for this activity
    /// </summary>
    public bool IsScheduledDisplayDay(DateTime date)
    {
        // Check day of month restriction first
        if (DisplayDayOfMonth > 0)
        {
            return date.Day == DisplayDayOfMonth;
        }
        
        // Check day of week restriction
        if (!string.IsNullOrEmpty(DisplayDaysOfWeek))
        {
            var allowedDays = DisplayDaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var dayAbbrev = date.DayOfWeek switch
            {
                DayOfWeek.Sunday => "Sun",
                DayOfWeek.Monday => "Mon",
                DayOfWeek.Tuesday => "Tue",
                DayOfWeek.Wednesday => "Wed",
                DayOfWeek.Thursday => "Thu",
                DayOfWeek.Friday => "Fri",
                DayOfWeek.Saturday => "Sat",
                _ => ""
            };
            return allowedDays.Contains(dayAbbrev);
        }
        
        // No restrictions - every day is scheduled
        return true;
    }
    
    /// <summary>
    /// Get the previous scheduled display day before the given date
    /// </summary>
    public DateTime? GetPreviousScheduledDay(DateTime fromDate)
    {
        // Go back up to 31 days to find previous scheduled day
        for (int i = 1; i <= 31; i++)
        {
            var checkDate = fromDate.AddDays(-i);
            if (IsScheduledDisplayDay(checkDate))
                return checkDate.Date;
        }
        return null;
    }
}
