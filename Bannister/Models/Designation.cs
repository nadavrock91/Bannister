using SQLite;

namespace Bannister.Models;

[Table("designations")]
public class Designation
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    [Indexed]
    public int SystemId { get; set; }

    public string Description { get; set; } = "";

    public string Status { get; set; } = "Pending";

    public DateTime SpecifiedDate { get; set; } = DateTime.Today;

    public DateTime StartDate { get; set; } = DateTime.Today;

    public DateTime EndDate { get; set; } = DateTime.Today;

    public string StartHour { get; set; } = "";

    public string EndHour { get; set; } = "";

    public string Notes { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
