using SQLite;

namespace Bannister.Models;

[Table("prompt_techniques")]
public class PromptTechnique
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public string PromptSuffix { get; set; } = "";

    public string Status { get; set; } = "Testing";

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

public class FailureMode
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public static readonly IReadOnlyList<FailureMode> All = new List<FailureMode>
    {
        new() { Id = 1, Name = "Too Slow" },
        new() { Id = 2, Name = "Insufficient Change" },
        new() { Id = 3, Name = "Missing Action" },
        new() { Id = 4, Name = "Incorrect Timing" },
        new() { Id = 5, Name = "Weak Escalation" },
        new() { Id = 6, Name = "Unwanted Camera" },
        new() { Id = 7, Name = "Poor Extension" },
        new() { Id = 8, Name = "Grok Missing Details" }
    };
}

[Table("technique_results")]
public class TechniqueResult
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    [Indexed]
    public int TechniqueId { get; set; }

    public int QualityRating { get; set; }

    public string FailureModesPresent { get; set; } = "";

    public string Notes { get; set; } = "";

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

public class TechniqueStats
{
    public int TechniqueId { get; set; }

    public string Title { get; set; } = "";

    public string Status { get; set; } = "";

    public int TotalGenerations { get; set; }

    public int TotalTerrible { get; set; }

    public int TotalUsable { get; set; }

    public int TotalExtraordinary { get; set; }
}
