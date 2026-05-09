using SQLite;

namespace Bannister.Models;

[Table("lookout_scenarios")]
public class LookoutScenario
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Title { get; set; } = "";

    public string Notes { get; set; } = "";

    public DateTime? DisplayDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

[Table("lookout_scenario_items")]
public class LookoutScenarioItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int LookoutScenarioId { get; set; }

    public string Title { get; set; } = "";

    public string Notes { get; set; } = "";

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
