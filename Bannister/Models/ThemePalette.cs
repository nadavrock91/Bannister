using SQLite;

namespace Bannister.Models;

[Table("theme_palette_history")]
public sealed class ThemePaletteHistory
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    [Indexed] public string Mode { get; set; } = "All";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = "";
    public string PaletteJson { get; set; } = "{}";
    public string? Note { get; set; }
}

[Table("theme_templates")]
public sealed class ThemeTemplate
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string PaletteJson { get; set; } = "{}";
    public string? Note { get; set; }
    [Ignore] public bool IsBuiltIn => Id < 0;
    [Ignore] public string DisplayName => IsBuiltIn ? $"{Name} ({Note})" : Name;
}
