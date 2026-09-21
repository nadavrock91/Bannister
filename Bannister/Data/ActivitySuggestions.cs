using Bannister.Models;

namespace Bannister.Data;

public static class ActivitySuggestions
{
    public static readonly List<ActivitySuggestion> All = new()
    {
        new ActivitySuggestion
        {
            Id = "universal_stagnation",
            Name = "Stagnation Penalty",
            Category = null,
            GameTag = null,
            Description = "A negative-reward activity that penalizes inactivity. Increases in cost the longer you go without leveling up or adding new activities."
        }
        // Add more here in future updates
    };
}
