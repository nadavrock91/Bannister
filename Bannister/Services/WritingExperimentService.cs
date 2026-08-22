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

    public async Task<WritingExperimentEntry?> GetTodayEntryAsync(int experimentId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var today = DateTime.UtcNow.Date;
        return await conn.Table<WritingExperimentEntry>()
            .Where(e => e.ExperimentId == experimentId && e.Date == today)
            .FirstOrDefaultAsync();
    }

    public async Task<WritingExperimentEntry> CreateTodayEntryAsync(int experimentId, string assignedProcess, bool isBaseline)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var entry = new WritingExperimentEntry
        {
            ExperimentId = experimentId,
            Date = DateTime.UtcNow.Date,
            AssignedProcess = assignedProcess,
            IsBaseline = isBaseline
        };
        await conn.InsertAsync(entry);
        return entry;
    }

    public async Task CompleteEntryAsync(int entryId, string executionNotes, int retentionScore, string retentionNotes)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var entry = await conn.FindAsync<WritingExperimentEntry>(entryId);
        if (entry == null) return;
        entry.ExecutionNotes = executionNotes;
        entry.RetentionScore = retentionScore;
        entry.RetentionNotes = retentionNotes;
        entry.IsCompleted = true;
        entry.CompletedAt = DateTime.UtcNow;
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

        double baselineAvg = baselineEntries.Count > 0 ? baselineEntries.Average(e => e.RetentionScore) : 0;
        double challengerAvg = challengerEntries.Count > 0 ? challengerEntries.Average(e => e.RetentionScore) : 0;

        return (baselineAvg, challengerAvg, baselineEntries.Count, challengerEntries.Count);
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
