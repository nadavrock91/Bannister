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
        try { await conn.ExecuteAsync("ALTER TABLE ClipsPost ADD COLUMN ExperimentItemId INTEGER"); } catch { }
        await conn.CreateTableAsync<ClipsDataPoint>();
        await conn.CreateTableAsync<ClipsExperimentItem>();
        await conn.CreateTableAsync<ClipsPostPlatform>();
        try { await conn.ExecuteAsync("ALTER TABLE ClipsExperimentItem ADD COLUMN StartDate TEXT"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE ClipsExperimentItem ADD COLUMN EndDate TEXT"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE ClipsExperimentItem ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0"); } catch { }
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

    public async Task<List<ClipsPost>> GetPostsByExperimentAsync(int experimentItemId)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<ClipsPost>()
            .Where(post => post.ExperimentItemId == experimentItemId)
            .OrderByDescending(post => post.PostedDate)
            .ToListAsync();
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
            .OrderBy(item => item.SortOrder)
            .ThenByDescending(item => item.StartDate)
            .ToListAsync();
    }

    public async Task<ClipsExperimentItem?> GetActiveExperimentItemAsync(string username, int experimentId)
    {
        var items = await GetExperimentItemsAsync(username, experimentId);
        var today = DateTime.Today;
        return items.FirstOrDefault(item =>
            item.StartDate.HasValue &&
            item.EndDate.HasValue &&
            item.StartDate.Value.Date <= today &&
            item.EndDate.Value.Date >= today);
    }

    public async Task<List<ClipsExperimentItem>> GetUnrunExperimentItemsAsync(string username, int experimentId)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<ClipsExperimentItem>()
            .Where(item => item.Username == username && item.ExperimentId == experimentId && !item.IsRun)
            .OrderBy(item => item.SortOrder)
            .ThenByDescending(item => item.StartDate)
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

    public async Task UnmarkExperimentItemRunAsync(int id)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var experimentItem = await conn.FindAsync<ClipsExperimentItem>(id);
        if (experimentItem == null) return;
        experimentItem.IsRun = false;
        await conn.UpdateAsync(experimentItem);
    }

    public async Task<List<ClipsPostPlatform>> GetPlatformsForPostAsync(int postId)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<ClipsPostPlatform>()
            .Where(p => p.PostId == postId)
            .OrderBy(p => p.PlatformName)
            .ToListAsync();
    }

    public async Task<List<ClipsPostPlatform>> GetAllPlatformsForExperimentAsync(int experimentId)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var posts = await GetPostsAsync(experimentId);
        var postIds = posts.Select(p => p.Id).ToHashSet();
        var all = await conn.Table<ClipsPostPlatform>().ToListAsync();
        return all.Where(p => postIds.Contains(p.PostId)).ToList();
    }

    public async Task SavePostPlatformAsync(ClipsPostPlatform platform)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        if (platform.Id == 0)
            await conn.InsertAsync(platform);
        else
            await conn.UpdateAsync(platform);
    }

    public async Task DeletePostPlatformAsync(int id)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var platform = await conn.FindAsync<ClipsPostPlatform>(id);
        if (platform != null)
            await conn.DeleteAsync(platform);
    }

    public async Task<Dictionary<string, Dictionary<string, (double avg10, double avg20, double avg30, int totalViews, int count)>>>
        GetConceptAveragesByPlatformAsync(int experimentId)
    {
        var posts = await GetPostsAsync(experimentId);
        var platforms = await GetAllPlatformsForExperimentAsync(experimentId);
        var platformsByPost = platforms.GroupBy(p => p.PostId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var raw = new Dictionary<string, Dictionary<string, List<ClipsPostPlatform>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var post in posts)
        {
            var concept = post.Concept.Trim();
            if (!raw.ContainsKey(concept))
                raw[concept] = new Dictionary<string, List<ClipsPostPlatform>>(StringComparer.OrdinalIgnoreCase);
            if (!platformsByPost.TryGetValue(post.Id, out var postPlatforms)) continue;
            foreach (var platform in postPlatforms)
            {
                var name = string.IsNullOrWhiteSpace(platform.PlatformName) ? "YouTube" : platform.PlatformName.Trim();
                if (!raw[concept].ContainsKey(name))
                    raw[concept][name] = new List<ClipsPostPlatform>();
                raw[concept][name].Add(platform);
            }
        }

        var averages = new Dictionary<string, Dictionary<string, (double avg10, double avg20, double avg30, int totalViews, int count)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (concept, byPlatform) in raw)
        {
            averages[concept] = new Dictionary<string, (double, double, double, int, int)>(StringComparer.OrdinalIgnoreCase);
            foreach (var (platformName, entries) in byPlatform)
            {
                averages[concept][platformName] = (
                    entries.Where(p => p.Retention10s.HasValue).Select(p => p.Retention10s!.Value).DefaultIfEmpty().Average(),
                    entries.Where(p => p.Retention20s.HasValue).Select(p => p.Retention20s!.Value).DefaultIfEmpty().Average(),
                    entries.Where(p => p.Retention30s.HasValue).Select(p => p.Retention30s!.Value).DefaultIfEmpty().Average(),
                    entries.Where(p => p.TotalViews.HasValue).Sum(p => p.TotalViews!.Value),
                    entries.Count);
            }
        }

        return averages;
    }
}
