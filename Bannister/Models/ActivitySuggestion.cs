using SQLite;

namespace Bannister.Models;

public class ActivitySuggestion
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    public string? GameTag { get; set; }
    public string Description { get; set; } = "";
    public bool PrefillStreakTracked { get; set; }
    public bool PrefillIsNegative { get; set; }
    public string? PrefillLevel { get; set; }
    public string? PrefillRewardType { get; set; }
    public bool PrefillStartDateNow { get; set; }
    public bool PrefillEndDateOneYear { get; set; }
}

[Table("activity_suggestion_logs")]
public class ActivitySuggestionLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Username { get; set; } = "";
    public string GameId { get; set; } = "";
    public string SuggestionId { get; set; } = "";
    public bool WasSeen { get; set; }
}
