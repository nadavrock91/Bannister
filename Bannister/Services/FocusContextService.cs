using Bannister.Models;

namespace Bannister.Services;

public class FocusContextService
{
    private readonly DatabaseService _db;

    public FocusContextService(DatabaseService db)
    {
        _db = db;
    }

    public async Task<List<FocusBulletPoint>> GetPointsAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<FocusBulletPoint>()
            .Where(p => p.Username == username && p.Status == "active")
            .OrderBy(p => p.SortOrder)
            .ToListAsync();
    }

    public async Task<FocusBulletPoint> AddPointAsync(
        string username, string text, int sortOrder)
    {
        var point = new FocusBulletPoint
        {
            Username = username,
            Text = text.Trim(),
            SortOrder = sortOrder
        };
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(point);
        return point;
    }

    public async Task UpdatePointAsync(FocusBulletPoint point)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(point);
    }

    public async Task ArchivePointAsync(int id)
    {
        var conn = await _db.GetConnectionAsync();
        var point = await conn.FindAsync<FocusBulletPoint>(id);
        if (point == null) return;
        point.Status = "archived";
        point.ArchivedAt = DateTime.UtcNow;
        await conn.UpdateAsync(point);
    }

    /// <summary>
    /// Moves a point to a specific 1-based position, shifting
    /// other points to accommodate.
    /// </summary>
    public async Task MoveToPositionAsync(
        string username, int pointId, int targetPosition)
    {
        var points = await GetPointsAsync(username);
        var point = points.FirstOrDefault(p => p.Id == pointId);
        if (point == null) return;

        // Clamp target to valid range
        targetPosition = Math.Clamp(targetPosition, 1, points.Count);

        // Remove from current position
        points.Remove(point);

        // Insert at target (0-based index)
        int insertIdx = Math.Min(targetPosition - 1, points.Count);
        points.Insert(insertIdx, point);

        // Reassign SortOrder
        var conn = await _db.GetConnectionAsync();
        for (int i = 0; i < points.Count; i++)
        {
            points[i].SortOrder = i + 1;
            await conn.UpdateAsync(points[i]);
        }
    }
}
