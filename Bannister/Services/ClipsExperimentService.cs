using Bannister.Models;

namespace Bannister.Services;

public class ClipsExperimentService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public ClipsExperimentService(DatabaseService db) => _db = db;

    public async Task InitAsync()
    {
        if (_initialized) return;
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<ClipsExperiment>();
        await conn.CreateTableAsync<ClipsPost>();
        await conn.CreateTableAsync<ClipsDataPoint>();
        await conn.CreateTableAsync<ClipsExperimentItem>();
        _initialized = true;
    }

    public async Task<ClipsExperiment?> GetActiveExperimentAsync()
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<ClipsExperiment>()
            .Where(e => !e.IsArchived)
            .OrderByDescending(e => e.CreatedDate)
            .FirstOrDefaultAsync();
    }

    public async Task<List<ClipsExperiment>> GetAllExperimentsAsync()
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<ClipsExperiment>()
            .OrderByDescending(e => e.CreatedDate)
            .ToListAsync();
    }

    public async Task SaveExperimentAsync(ClipsExperiment experiment)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        if (experiment.Id == 0)
            await conn.InsertAsync(experiment);
        else
            await conn.UpdateAsync(experiment);
    }

    public async Task<List<ClipsPost>> GetPostsAsync(int experimentId)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<ClipsPost>()
            .Where(p => p.ExperimentId == experimentId)
            .OrderByDescending(p => p.PostedDate)
            .ToListAsync();
    }

    public async Task SavePostAsync(ClipsPost post)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        if (post.Id == 0)
            await conn.InsertAsync(post);
        else
            await conn.UpdateAsync(post);
    }

    public async Task DeletePostAsync(int id)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var post = await conn.FindAsync<ClipsPost>(id);
        if (post != null)
            await conn.DeleteAsync(post);
    }

    public async Task<List<ClipsDataPoint>> GetDataPointsAsync(string username, int experimentId)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<ClipsDataPoint>()
            .Where(item => item.Username == username && item.ExperimentId == experimentId)
            .OrderByDescending(item => item.CreatedDate)
            .ToListAsync();
    }

    public async Task SaveDataPointAsync(ClipsDataPoint dataPoint)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        if (dataPoint.Id == 0)
            await conn.InsertAsync(dataPoint);
        else
            await conn.UpdateAsync(dataPoint);
    }

    public async Task DeleteDataPointAsync(int id)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var dataPoint = await conn.FindAsync<ClipsDataPoint>(id);
        if (dataPoint != null)
            await conn.DeleteAsync(dataPoint);
    }

    public async Task<List<ClipsExperimentItem>> GetExperimentItemsAsync(string username, int experimentId)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<ClipsExperimentItem>()
            .Where(item => item.Username == username && item.ExperimentId == experimentId)
            .OrderByDescending(item => item.CreatedDate)
            .ToListAsync();
    }

    public async Task<List<ClipsExperimentItem>> GetUnrunExperimentItemsAsync(string username, int experimentId)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<ClipsExperimentItem>()
            .Where(item => item.Username == username && item.ExperimentId == experimentId && !item.IsRun)
            .OrderByDescending(item => item.CreatedDate)
            .ToListAsync();
    }

    public async Task SaveExperimentItemAsync(ClipsExperimentItem experimentItem)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        if (experimentItem.Id == 0)
            await conn.InsertAsync(experimentItem);
        else
            await conn.UpdateAsync(experimentItem);
    }

    public async Task DeleteExperimentItemAsync(int id)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var experimentItem = await conn.FindAsync<ClipsExperimentItem>(id);
        if (experimentItem != null)
            await conn.DeleteAsync(experimentItem);
    }

    public async Task MarkExperimentItemRunAsync(int id)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var experimentItem = await conn.FindAsync<ClipsExperimentItem>(id);
        if (experimentItem == null) return;
        experimentItem.IsRun = true;
        await conn.UpdateAsync(experimentItem);
    }

    public async Task<Dictionary<string, (double avg10, double avg20, double avg30, int count)>>
        GetConceptAveragesAsync(int experimentId)
    {
        var posts = await GetPostsAsync(experimentId);
        return posts.GroupBy(p => p.Concept.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.First().Concept.Trim(),
                g => (
                    g.Where(p => p.Retention10s.HasValue).Select(p => p.Retention10s!.Value).DefaultIfEmpty().Average(),
                    g.Where(p => p.Retention20s.HasValue).Select(p => p.Retention20s!.Value).DefaultIfEmpty().Average(),
                    g.Where(p => p.Retention30s.HasValue).Select(p => p.Retention30s!.Value).DefaultIfEmpty().Average(),
                    g.Count()));
    }
}
