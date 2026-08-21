using Bannister.Models;

namespace Bannister.Services;

public class StatTrackerService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public StatTrackerService(DatabaseService db)
    {
        _db = db;
    }

    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<StatTracker>();
        await conn.CreateTableAsync<StatEntry>();
        _initialized = true;
    }

    // Trackers
    public async Task<List<StatTracker>> GetActiveTrackersAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<StatTracker>()
            .Where(t => t.Username == username && t.Status == "active")
            .OrderBy(t => t.Title)
            .ToListAsync();
    }

    public async Task<List<StatTracker>> GetArchivedTrackersAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<StatTracker>()
            .Where(t => t.Username == username && t.Status == "archived")
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<StatTracker> CreateTrackerAsync(string username, string title, string description = "", string category = "")
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var tracker = new StatTracker
        {
            Username = username,
            Title = title,
            Description = description,
            Category = category
        };
        await conn.InsertAsync(tracker);
        return tracker;
    }

    public async Task UpdateTrackerAsync(StatTracker tracker)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(tracker);
    }

    public async Task ArchiveTrackerAsync(int trackerId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var tracker = await conn.FindAsync<StatTracker>(trackerId);
        if (tracker == null) return;
        tracker.Status = "archived";
        await conn.UpdateAsync(tracker);
    }

    public async Task RestoreTrackerAsync(int trackerId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var tracker = await conn.FindAsync<StatTracker>(trackerId);
        if (tracker == null) return;
        tracker.Status = "active";
        await conn.UpdateAsync(tracker);
    }

    public async Task DeleteTrackerAsync(int trackerId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<StatTracker>(trackerId);
        // Delete all entries
        await conn.ExecuteAsync("DELETE FROM StatEntry WHERE TrackerId = ?", trackerId);
    }

    // Entries
    public async Task<List<StatEntry>> GetEntriesAsync(int trackerId, int limit = 100)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<StatEntry>()
            .Where(e => e.TrackerId == trackerId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<StatEntry> AddEntryAsync(int trackerId, string entryType, string label = "", string notes = "", int value = 1)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var entry = new StatEntry
        {
            TrackerId = trackerId,
            EntryType = entryType,
            Label = label,
            Notes = notes,
            Value = value
        };
        await conn.InsertAsync(entry);
        return entry;
    }

    public async Task DeleteEntryAsync(int entryId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<StatEntry>(entryId);
    }

    // Stats
    public async Task<Dictionary<string, int>> GetSummaryAsync(int trackerId)
    {
        await EnsureInitializedAsync();
        var entries = await GetEntriesAsync(trackerId, 10000);
        var summary = new Dictionary<string, int>();

        foreach (var entry in entries)
        {
            if (!summary.ContainsKey(entry.EntryType))
                summary[entry.EntryType] = 0;
            summary[entry.EntryType] += entry.Value;
        }

        return summary;
    }

    public async Task<Dictionary<string, Dictionary<string, int>>> GetLabelBreakdownAsync(int trackerId)
    {
        await EnsureInitializedAsync();
        var entries = await GetEntriesAsync(trackerId, 10000);
        var breakdown = new Dictionary<string, Dictionary<string, int>>();

        foreach (var entry in entries)
        {
            string label = string.IsNullOrWhiteSpace(entry.Label) ? "(no label)" : entry.Label;
            if (!breakdown.ContainsKey(label))
                breakdown[label] = new Dictionary<string, int>();
            if (!breakdown[label].ContainsKey(entry.EntryType))
                breakdown[label][entry.EntryType] = 0;
            breakdown[label][entry.EntryType] += entry.Value;
        }

        return breakdown;
    }
}
