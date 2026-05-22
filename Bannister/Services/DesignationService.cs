using Bannister.Models;
using SQLite;
using System.Globalization;

namespace Bannister.Services;

public class DesignationService
{
    public static readonly string[] ValidStatuses = { "Pending", "Completed", "Archived", "Failed" };

    private readonly DatabaseService _db;
    private bool _initialized;

    public DesignationService(DatabaseService db)
    {
        _db = db;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private void EnsureWritable()
    {
        if (_db.IsReadOnly)
            throw new ReadOnlyDatabaseException("Designations are read-only on secondary devices.");
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly)
        {
            await conn.CreateTableAsync<DesignationSystem>();
            await conn.CreateTableAsync<Designation>();
        }

        _initialized = true;
    }

    public async Task<List<DesignationSystem>> GetSystemsAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        try
        {
            return await conn.Table<DesignationSystem>()
                .Where(s => s.Username == username)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }
        catch (SQLiteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            return new List<DesignationSystem>();
        }
    }

    public async Task<DesignationSystem> AddSystemAsync(string username, string name)
    {
        EnsureWritable();
        await EnsureInitializedAsync();

        var item = new DesignationSystem
        {
            Username = username,
            Name = name.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(item);
        return item;
    }

    public async Task DeleteSystemAsync(int systemId)
    {
        EnsureWritable();
        await EnsureInitializedAsync();

        var conn = await _db.GetConnectionAsync();
        var designations = await conn.Table<Designation>()
            .Where(d => d.SystemId == systemId)
            .ToListAsync();

        foreach (var designation in designations)
            await conn.DeleteAsync(designation);

        await conn.DeleteAsync<DesignationSystem>(systemId);
    }

    public async Task<List<Designation>> GetDesignationsAsync(string username, int systemId, string? statusFilter = null)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        List<Designation> rows;
        try
        {
            rows = await conn.Table<Designation>()
                .Where(d => d.Username == username && d.SystemId == systemId)
                .ToListAsync();
        }
        catch (SQLiteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            return new List<Designation>();
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            !string.Equals(statusFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows
                .Where(d => string.Equals(d.Status, NormalizeStatus(statusFilter), StringComparison.Ordinal))
                .ToList();
        }

        return rows
            .OrderBy(d => d.SpecifiedDate)
            .ThenBy(d => d.StartHour)
            .ThenBy(d => d.Description)
            .ToList();
    }

    public async Task<Designation> AddDesignationAsync(
        string username,
        int systemId,
        string description,
        DateTime specifiedDate,
        DateTime startDate,
        DateTime endDate,
        string startHour,
        string endHour,
        string notes)
    {
        EnsureWritable();
        await EnsureInitializedAsync();

        var item = new Designation
        {
            Username = username,
            SystemId = systemId,
            Description = description.Trim(),
            Status = "Pending",
            SpecifiedDate = specifiedDate.Date,
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            StartHour = NormalizeHour(startHour),
            EndHour = NormalizeHour(endHour),
            Notes = notes.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(item);
        return item;
    }

    public async Task UpdateDesignationAsync(Designation item)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        item.Description = item.Description.Trim();
        item.Status = NormalizeStatus(item.Status);
        item.SpecifiedDate = item.SpecifiedDate.Date;
        item.StartDate = item.StartDate.Date;
        item.EndDate = item.EndDate.Date;
        item.StartHour = NormalizeHour(item.StartHour);
        item.EndHour = NormalizeHour(item.EndHour);
        item.Notes = item.Notes?.Trim() ?? "";

        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(item);
    }

    public async Task DeleteDesignationAsync(int id)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<Designation>(id);
    }

    public async Task<bool> UpdateDesignationCellAsync(string username, string idValue, string columnName, string newValue)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        if (!int.TryParse(idValue, out int id))
            return false;

        var conn = await _db.GetConnectionAsync();
        var item = await conn.FindAsync<Designation>(id);
        if (item == null || !string.Equals(item.Username, username, StringComparison.Ordinal))
            return false;

        switch (columnName)
        {
            case "Description":
                if (string.IsNullOrWhiteSpace(newValue)) return false;
                item.Description = newValue.Trim();
                break;
            case "Status":
                if (!TryNormalizeStatus(newValue, out string status)) return false;
                item.Status = status;
                break;
            case "SpecifiedDate":
                if (!TryParseDate(newValue, out DateTime specifiedDate)) return false;
                item.SpecifiedDate = specifiedDate.Date;
                break;
            case "StartDate":
                if (!TryParseDate(newValue, out DateTime startDate)) return false;
                item.StartDate = startDate.Date;
                break;
            case "EndDate":
                if (!TryParseDate(newValue, out DateTime endDate)) return false;
                item.EndDate = endDate.Date;
                break;
            case "StartHour":
                if (!TryNormalizeHour(newValue, out string startHour)) return false;
                item.StartHour = startHour;
                break;
            case "EndHour":
                if (!TryNormalizeHour(newValue, out string endHour)) return false;
                item.EndHour = endHour;
                break;
            case "Notes":
                item.Notes = newValue.Trim();
                break;
            default:
                return false;
        }

        await conn.UpdateAsync(item);
        return true;
    }

    public static bool IsValidStatus(string value)
    {
        return ValidStatuses.Any(s => string.Equals(s, value.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeStatus(string value)
    {
        return ValidStatuses.FirstOrDefault(s => string.Equals(s, value.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Pending";
    }

    public static bool TryNormalizeStatus(string value, out string status)
    {
        status = NormalizeStatus(value);
        return IsValidStatus(value);
    }

    public static bool TryParseDate(string? value, out DateTime date)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            date = default;
            return false;
        }

        return DateTime.TryParse(value.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out date)
            || DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date);
    }

    public static bool TryNormalizeHour(string value, out string hour)
    {
        value = value.Trim();
        if (string.IsNullOrEmpty(value))
        {
            hour = "";
            return true;
        }

        if (TimeSpan.TryParse(value, CultureInfo.CurrentCulture, out TimeSpan parsed) ||
            TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out parsed))
        {
            hour = parsed.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            return true;
        }

        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault, out DateTime parsedDate) ||
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault, out parsedDate))
        {
            hour = parsedDate.TimeOfDay.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            return true;
        }

        hour = "";
        return false;
    }

    private static string NormalizeHour(string value)
    {
        return TryNormalizeHour(value, out string hour) ? hour : "";
    }
}
