using Bannister.ViewModels;

namespace Bannister.Helpers;

/// <summary>
/// Helper class for filtering and sorting activities
/// </summary>
public static class ActivityFilterHelper
{
    public static List<ActivityGameViewModel> ApplyMetaFilter(
        List<ActivityGameViewModel> activities, 
        string metaFilter)
    {
        switch (metaFilter)
        {
            case "Has Multiplier":
                return activities.Where(a => a.Multiplier > 1).ToList();

            case "No Multiplier":
                return activities.Where(a => a.Multiplier == 1).ToList();

            case "Active Now":
                var now = DateTime.Now;

                return activities.Where(a =>
                {
                    var activity = a.Activity;
                    
                    // Exclude possible activities from Active Now
                    if (activity.IsPossible) return false;

                    // StartDate and EndDate now include time
                    bool afterStart = !activity.StartDate.HasValue || now >= activity.StartDate.Value;
                    bool beforeEnd = !activity.EndDate.HasValue || now <= activity.EndDate.Value;

                    return afterStart && beforeEnd;
                }).ToList();

            case "Possible":
                return activities.Where(a => a.Activity.IsPossible).ToList();

            case "Expired":
                // Show expired activities, sorted by EndDate descending (most recently expired first)
                return activities
                    .Where(a => a.Activity.Category == "Expired")
                    .OrderByDescending(a => a.Activity.EndDate)
                    .ToList();

            case "Stale":
                // Show stale activities (never clicked for 2+ months), sorted by StartDate descending
                return activities
                    .Where(a => a.Activity.Category == "Stale")
                    .OrderByDescending(a => a.Activity.StartDate)
                    .ToList();

            case "Missing Image":
                // Show activities without an image
                return activities
                    .Where(a => string.IsNullOrEmpty(a.Activity.ImagePath))
                    .OrderBy(a => a.Name)
                    .ToList();

            case "All Activities":
            default:
                // Exclude possible, expired, and stale activities from All Activities by default
                return activities.Where(a => !a.Activity.IsPossible 
                    && a.Activity.Category != "Expired"
                    && a.Activity.Category != "Stale").ToList();
        }
    }

    public static List<ActivityGameViewModel> ApplySorting(
        List<ActivityGameViewModel> activities, 
        string sortOrder)
    {
        // Clear all section headers first
        foreach (var activity in activities)
        {
            activity.SectionHeader = "";
        }

        switch (sortOrder)
        {
            case "Last Used (Recent First)":
                return ApplyLastUsedSorting(activities);

            case "Alphabetical (A-Z)":
                return activities.OrderBy(a => a.Name).ToList();

            case "Alphabetical (Z-A)":
                return activities.OrderByDescending(a => a.Name).ToList();

            case "EXP (High to Low)":
                return activities.OrderByDescending(a => a.ExpGain).ToList();

            case "EXP (Low to High)":
                return activities.OrderBy(a => a.ExpGain).ToList();

            default:
                return activities;
        }
    }

    private static List<ActivityGameViewModel> ApplyLastUsedSorting(List<ActivityGameViewModel> activities)
    {
        var now = DateTime.Now;
        var sorted = new List<ActivityGameViewModel>();

        System.Diagnostics.Debug.WriteLine($"\n=== SORTING BY LAST USED ===");
        System.Diagnostics.Debug.WriteLine($"Total activities to sort: {activities.Count}");

        // ========== HABITS SECTION (TOP PRIORITY) ==========
        // Daily Habits (7+ consecutive days)
        var dailyHabits = activities
            .Where(a => a.Activity.HabitType == "Daily" && a.Activity.IsHabit)
            .OrderByDescending(a => a.Activity.HabitStreak)
            .ToList();
        if (dailyHabits.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"  DAILY HABITS: {dailyHabits.Count} activities");
            dailyHabits[0].SectionHeader = "🌅 DAILY HABITS";
            sorted.AddRange(dailyHabits);
        }

        // Weekly Habits (4+ consecutive weeks)
        var weeklyHabits = activities
            .Where(a => a.Activity.HabitType == "Weekly" && a.Activity.IsHabit)
            .OrderByDescending(a => a.Activity.HabitStreak)
            .ToList();
        if (weeklyHabits.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"  WEEKLY HABITS: {weeklyHabits.Count} activities");
            weeklyHabits[0].SectionHeader = "📆 WEEKLY HABITS";
            sorted.AddRange(weeklyHabits);
        }

        // Monthly Habits (3+ consecutive months)
        var monthlyHabits = activities
            .Where(a => a.Activity.HabitType == "Monthly" && a.Activity.IsHabit)
            .OrderByDescending(a => a.Activity.HabitStreak)
            .ToList();
        if (monthlyHabits.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"  MONTHLY HABITS: {monthlyHabits.Count} activities");
            monthlyHabits[0].SectionHeader = "📅 MONTHLY HABITS";
            sorted.AddRange(monthlyHabits);
        }

        // Building Habits (in progress, not yet achieved habit status)
        var buildingHabits = activities
            .Where(a => a.Activity.HabitType != "None" && !a.Activity.IsHabit && a.Activity.HabitStreak > 0)
            .OrderByDescending(a => a.Activity.HabitProgress)
            .ToList();
        if (buildingHabits.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"  BUILDING HABITS: {buildingHabits.Count} activities");
            buildingHabits[0].SectionHeader = "🔨 BUILDING HABITS";
            sorted.AddRange(buildingHabits);
        }

        // Get IDs of activities already added to habits sections
        var habitActivityIds = sorted.Select(a => a.Id).ToHashSet();

        // ========== REGULAR SECTIONS (exclude habits already shown) ==========
        
        // NEWLY ADDED - Never clicked AND created within 7 days
        var newlyAdded = activities
            .Where(a => !habitActivityIds.Contains(a.Id) &&
                        !a.LastUsedDate.HasValue && 
                        a.StartDate.HasValue && 
                        (now - a.StartDate.Value).TotalDays < 8)
            .OrderByDescending(a => a.StartDate)
            .ToList();
            
        if (newlyAdded.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"  NEWLY ADDED: {newlyAdded.Count} activities");
            newlyAdded[0].SectionHeader = "✨ NEWLY ADDED";
            sorted.AddRange(newlyAdded);
        }

        // Within 7 days - Recent
        var last7Days = activities
            .Where(a => !habitActivityIds.Contains(a.Id) &&
                       a.LastUsedDate.HasValue && (now - a.LastUsedDate.Value).TotalDays <= 7)
            .OrderByDescending(a => a.LastUsedDate)
            .ToList();
        if (last7Days.Count > 0)
        {
            last7Days[0].SectionHeader = "📅 CLICKED THIS WEEK";
            sorted.AddRange(last7Days);
        }

        // 7-30 days - Clicked a week ago
        var last30Days = activities
            .Where(a => !habitActivityIds.Contains(a.Id) &&
                       a.LastUsedDate.HasValue && 
                       (now - a.LastUsedDate.Value).TotalDays > 7 && 
                       (now - a.LastUsedDate.Value).TotalDays <= 30)
            .OrderByDescending(a => a.LastUsedDate)
            .ToList();
        if (last30Days.Count > 0)
        {
            last30Days[0].SectionHeader = "📆 CLICKED A WEEK AGO";
            sorted.AddRange(last30Days);
        }

        // 30-60 days - Clicked a month ago
        var last60Days = activities
            .Where(a => !habitActivityIds.Contains(a.Id) &&
                       a.LastUsedDate.HasValue && 
                       (now - a.LastUsedDate.Value).TotalDays > 30 && 
                       (now - a.LastUsedDate.Value).TotalDays <= 60)
            .OrderByDescending(a => a.LastUsedDate)
            .ToList();
        if (last60Days.Count > 0)
        {
            last60Days[0].SectionHeader = "📅 CLICKED A MONTH AGO";
            sorted.AddRange(last60Days);
        }

        // 60+ days - Over 2 months ago
        var over60Days = activities
            .Where(a => !habitActivityIds.Contains(a.Id) &&
                       a.LastUsedDate.HasValue && (now - a.LastUsedDate.Value).TotalDays > 60)
            .OrderByDescending(a => a.LastUsedDate)
            .ToList();
        if (over60Days.Count > 0)
        {
            over60Days[0].SectionHeader = "⏰ OVER 2 MONTHS AGO";
            sorted.AddRange(over60Days);
        }

        // Never clicked (excluding newly added ones already shown above)
        var neverClicked = activities
            .Where(a => !habitActivityIds.Contains(a.Id) &&
                       !a.LastUsedDate.HasValue && 
                       (!a.StartDate.HasValue || (now - a.StartDate.Value).TotalDays > 7))
            .OrderBy(a => a.Name)
            .ToList();
        if (neverClicked.Count > 0)
        {
            neverClicked[0].SectionHeader = "❓ NEVER CLICKED";
            sorted.AddRange(neverClicked);
        }

        System.Diagnostics.Debug.WriteLine($"=== SORTING COMPLETE: {sorted.Count} total ===");
        return sorted;
    }
}
