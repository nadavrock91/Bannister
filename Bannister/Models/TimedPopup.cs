using SQLite;

namespace Bannister.Models;

public class TimedPopup
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Username { get; set; } = "";
    public string Label { get; set; } = "";
    public string TimeMode { get; set; } = "absolute";
    public string AbsoluteTime { get; set; } = "00:00";
    public int OffsetMinutes { get; set; }
}
