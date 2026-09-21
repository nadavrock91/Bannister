using Bannister.Models;

namespace Bannister.Data;

public static class ActivitySuggestions
{
    public static readonly List<ActivitySuggestion> All = new()
    {
        new ActivitySuggestion
        {
            Id = "universal_days_since_level",
            Name = "Days Since Level Increase",
            Category = "misc",
            GameTag = null,
            Description = "A streak-tracked negative reward activity. Penalizes you for going too long without leveling up.",
            PrefillStreakTracked = true,
            PrefillIsNegative = true,
            PrefillRewardType = "PercentOfLevel",
            PrefillPercent = "-1",
            PrefillStartDateNow = true,
            PrefillEndDateOneYear = true
        },
        new ActivitySuggestion
        {
            Id = "universal_days_since_activity_added",
            Name = "Days Since New Activity Added",
            Category = "misc",
            GameTag = null,
            Description = "A streak-tracked negative reward activity. Penalizes you for not adding new activities.",
            PrefillStreakTracked = true,
            PrefillIsNegative = true,
            PrefillRewardType = "PercentOfLevel",
            PrefillPercent = "-1",
            PrefillStartDateNow = true,
            PrefillEndDateOneYear = true
        },
        new ActivitySuggestion
        {
            Id = "universal_days_since_autoreward",
            Name = "Days Since Activity Made Auto-Reward",
            Category = "misc",
            GameTag = null,
            Description = "A streak-tracked negative reward activity. Penalizes you for not converting activities to auto-reward.",
            PrefillStreakTracked = true,
            PrefillIsNegative = true,
            PrefillRewardType = "PercentOfLevel",
            PrefillPercent = "-1",
            PrefillStartDateNow = true,
            PrefillEndDateOneYear = true
        }
        // Add more here in future updates
    };
}
