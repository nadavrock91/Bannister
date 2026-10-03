using SQLite;

namespace Bannister.Models;

[Table("hook_prefixes")]
public class HookPrefix
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string PrefixText { get; set; } = "";

    public int TotalCropped { get; set; }

    public int TotalExtraordinary { get; set; }

    public int TotalCroppedAsCombined { get; set; }

    public int TotalExtraordinaryAsCombined { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public bool IsArchived { get; set; }
}

[Table("hook_prefix_sessions")]
public class HookPrefixSession
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public string PrefixText { get; set; } = "";

    public int? PrefixId1 { get; set; }

    public int? PrefixId2 { get; set; }

    public string Mode { get; set; } = "";

    public int? TotalCropped { get; set; }

    public int? TotalExtraordinary { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

[Table("hook_cropper_settings")]
public class HookCropperSettings
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    public bool RequirePrefixBeforeCrop { get; set; } = true;

    public string LastPrefixText { get; set; } = "";
}
