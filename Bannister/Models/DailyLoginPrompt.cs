using SQLite;

using Bannister.Services;

namespace Bannister.Models;

[Table("daily_login_prompts")]
public class DailyLoginPrompt
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string Text { get; set; } = "";

    public string FontColor { get; set; } = AppColors.Hex(ThemeKey.OnColorText);

    public string BackgroundColor { get; set; } = AppColors.Hex(ThemeKey.SecondaryButtonBackground);

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
