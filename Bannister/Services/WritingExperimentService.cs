using Bannister.Models;

namespace Bannister.Services;

public class WritingExperimentService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public WritingExperimentService(DatabaseService db)
    {
        _db = db;
    }

    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<WritingExperiment>();
        await conn.CreateTableAsync<WritingExperimentEntry>();
        try { await conn.ExecuteAsync("ALTER TABLE WritingExperiment ADD COLUMN ProcessQueueJson TEXT DEFAULT ''"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE WritingExperimentEntry ADD COLUMN Retention10s INTEGER DEFAULT -1"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE WritingExperimentEntry ADD COLUMN Retention30s INTEGER DEFAULT -1"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE WritingExperimentEntry ADD COLUMN Retention1m INTEGER DEFAULT -1"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE WritingExperimentEntry ADD COLUMN Retention2m INTEGER DEFAULT -1"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE WritingExperimentEntry ADD COLUMN Retention3m INTEGER DEFAULT -1"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE WritingExperimentEntry ADD COLUMN StoryTitle TEXT DEFAULT ''"); } catch { }
        _initialized = true;
    }

    public async Task<WritingExperiment?> GetActiveExperimentAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<WritingExperiment>()
            .Where(e => e.Username == username && e.Status == "active")
            .FirstOrDefaultAsync();
    }

    public async Task<WritingExperiment> StartExperimentAsync(string username, string baselineProcess)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();

        var existing = await GetActiveExperimentAsync(username);
        if (existing != null)
        {
            existing.Status = "archived";
            existing.CompletedAt = DateTime.UtcNow;
            await conn.UpdateAsync(existing);
        }

        var experiment = new WritingExperiment
        {
            Username = username,
            BaselineProcessName = baselineProcess,
            Phase = "baseline",
            CurrentWeek = 1
        };
        await conn.InsertAsync(experiment);
        return experiment;
    }

    public async Task<WritingExperimentEntry?> GetEntryForDateAsync(int experimentId, DateTime date)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<WritingExperimentEntry>()
            .Where(e => e.ExperimentId == experimentId && e.Date == date.Date)
            .FirstOrDefaultAsync();
    }

    public async Task<WritingExperimentEntry> CreateEntryAsync(
        int experimentId,
        string assignedProcess,
        bool isBaseline,
        DateTime? date = null)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var entry = new WritingExperimentEntry
        {
            ExperimentId = experimentId,
            Date = (date ?? DateTime.UtcNow).Date,
            AssignedProcess = assignedProcess,
            IsBaseline = isBaseline
        };
        await conn.InsertAsync(entry);
        return entry;
    }

    public async Task CompleteEntryAsync(WritingExperimentEntry entry)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(entry);
    }

    public async Task<List<WritingExperimentEntry>> GetEntriesAsync(int experimentId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<WritingExperimentEntry>()
            .Where(e => e.ExperimentId == experimentId)
            .OrderBy(e => e.Date)
            .ToListAsync();
    }

    public async Task ChangeProcessForRangeAsync(
        int experimentId,
        DateTime fromDate,
        DateTime toDate,
        string processName,
        bool isBaseline)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var entries = await conn.Table<WritingExperimentEntry>()
            .Where(e => e.ExperimentId == experimentId)
            .ToListAsync();

        foreach (var entry in entries)
        {
            if (entry.Date.Date >= fromDate.Date && entry.Date.Date <= toDate.Date)
            {
                entry.AssignedProcess = processName;
                entry.IsBaseline = isBaseline;
                await conn.UpdateAsync(entry);
            }
        }
    }

    public async Task<int> GetBaselineEntryCountAsync(int experimentId)
    {
        var entries = await GetEntriesAsync(experimentId);
        return entries.Count(e => e.IsBaseline && e.IsCompleted);
    }

    public async Task SetChallengerAsync(int experimentId, string challengerProcess)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var experiment = await conn.FindAsync<WritingExperiment>(experimentId);
        if (experiment == null) return;
        experiment.ChallengerProcessName = challengerProcess;
        experiment.Phase = "challenger";
        await conn.UpdateAsync(experiment);
    }

    public async Task UpdateExperimentAsync(WritingExperiment experiment)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(experiment);
    }

    public async Task<(double baselineAvg, double challengerAvg, int baselineCount, int challengerCount)> GetComparisonAsync(int experimentId)
    {
        var entries = await GetEntriesAsync(experimentId);
        var completed = entries.Where(e => e.IsCompleted).ToList();
        var baselineEntries = completed.Where(e => e.IsBaseline).ToList();
        var challengerEntries = completed.Where(e => !e.IsBaseline).ToList();

        double baselineAvg = baselineEntries.Count > 0 ? baselineEntries.Average(GetAverageRetention) : 0;
        double challengerAvg = challengerEntries.Count > 0 ? challengerEntries.Average(GetAverageRetention) : 0;

        return (baselineAvg, challengerAvg, baselineEntries.Count, challengerEntries.Count);
    }

    private static double GetAverageRetention(WritingExperimentEntry entry)
    {
        var values = new[]
        {
            entry.Retention10s,
            entry.Retention30s,
            entry.Retention1m,
            entry.Retention2m,
            entry.Retention3m
        }.Where(value => value >= 0).ToArray();

        return values.Length > 0 ? values.Average() : 0;
    }

    public async Task<List<WritingExperiment>> GetArchivedExperimentsAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<WritingExperiment>()
            .Where(e => e.Username == username && e.Status == "archived")
            .OrderByDescending(e => e.CompletedAt)
            .ToListAsync();
    }
}
