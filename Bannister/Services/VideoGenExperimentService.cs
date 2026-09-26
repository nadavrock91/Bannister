using Bannister.Models;

namespace Bannister.Services;

public sealed class ComparisonStats
{
    public double AverageRetention10s { get; set; }
    public double AverageRetention20s { get; set; }
    public double AverageRetention30s { get; set; }
    public double MedianRetention10s { get; set; }
    public double MedianRetention20s { get; set; }
    public double MedianRetention30s { get; set; }
    public double BestRetention10s { get; set; }
    public double BestRetention20s { get; set; }
    public double BestRetention30s { get; set; }
    public double PercentageExceedingThreshold { get; set; }
    public double VideoGenerationsPerPublishedClip { get; set; }
    public int TotalClips { get; set; }
    public int TotalWithRetention { get; set; }
}

public sealed class HypothesisStats
{
    public string Hypothesis { get; set; } = "";
    public double AverageRetention10s { get; set; }
    public double AverageRetention20s { get; set; }
    public double AverageRetention30s { get; set; }
    public int Count { get; set; }
}

public class VideoGenExperimentService
{
    private readonly DatabaseService _db;
    private bool _initialized;
    public VideoGenExperimentService(DatabaseService db) => _db = db;

    public async Task InitAsync()
    {
        if (_initialized) return;
        var c = await _db.GetConnectionAsync();
        await c.CreateTableAsync<WeeklyMethodExperiment>();
        await c.CreateTableAsync<ClipExperiment>();
        _initialized = true;
    }
    public async Task<List<WeeklyMethodExperiment>> GetActiveWeeklyMethodsAsync(string username)
    { await InitAsync(); var c = await _db.GetConnectionAsync(); return await c.Table<WeeklyMethodExperiment>().Where(x => x.Username == username && !x.IsArchived).OrderByDescending(x => x.CreatedDate).ToListAsync(); }
    public async Task<List<WeeklyMethodExperiment>> GetAllWeeklyMethodsAsync(string username)
    { await InitAsync(); var c = await _db.GetConnectionAsync(); return await c.Table<WeeklyMethodExperiment>().Where(x => x.Username == username).OrderByDescending(x => x.CreatedDate).ToListAsync(); }
    public async Task SaveWeeklyMethodAsync(WeeklyMethodExperiment item)
    { await InitAsync(); var c = await _db.GetConnectionAsync(); if (item.Id == 0) await c.InsertAsync(item); else await c.UpdateAsync(item); }
    public async Task ArchiveWeeklyMethodAsync(int id)
    { await InitAsync(); var c = await _db.GetConnectionAsync(); var x = await c.FindAsync<WeeklyMethodExperiment>(id); if (x != null) { x.IsArchived = true; await c.UpdateAsync(x); } }
    public async Task<List<ClipExperiment>> GetClipsAsync(string username, int? weeklyMethodId = null)
    { await InitAsync(); var c = await _db.GetConnectionAsync(); var q = c.Table<ClipExperiment>().Where(x => x.Username == username); if (weeklyMethodId.HasValue) q = q.Where(x => x.WeeklyMethodExperimentId == weeklyMethodId.Value); return await q.OrderByDescending(x => x.CreatedDate).ToListAsync(); }
    public async Task SaveClipAsync(ClipExperiment item)
    { await InitAsync(); var c = await _db.GetConnectionAsync(); if (item.Id == 0) await c.InsertAsync(item); else await c.UpdateAsync(item); }
    public async Task DeleteClipAsync(int id)
    { await InitAsync(); var c = await _db.GetConnectionAsync(); var x = await c.FindAsync<ClipExperiment>(id); if (x != null) await c.DeleteAsync(x); }

    public async Task<ComparisonStats> GetComparisonStatsAsync(string username, int? weeklyMethodId = null, double threshold = 50)
    {
        var clips = await GetClipsAsync(username, weeklyMethodId);
        static List<double> Values(IEnumerable<ClipExperiment> s, Func<ClipExperiment, double?> f) => s.Select(f).Where(v => v.HasValue).Select(v => v!.Value).ToList();
        static double Avg(List<double> x) => x.Count == 0 ? 0 : x.Average();
        static double Med(List<double> x) { if (x.Count == 0) return 0; x.Sort(); return x.Count % 2 == 1 ? x[x.Count / 2] : (x[x.Count / 2 - 1] + x[x.Count / 2]) / 2; }
        var a = Values(clips, x => x.Retention10s); var b = Values(clips, x => x.Retention20s); var d = Values(clips, x => x.Retention30s);
        var methods = await GetActiveWeeklyMethodsAsync(username);
        if (weeklyMethodId.HasValue) methods = methods.Where(x => x.Id == weeklyMethodId.Value).ToList();
        int generations = methods.Sum(x => x.VideoGenerationsUsed ?? 0); int published = clips.Count(x => x.PostedDate.HasValue);
        return new ComparisonStats { AverageRetention10s = Avg(a), AverageRetention20s = Avg(b), AverageRetention30s = Avg(d), MedianRetention10s = Med(a), MedianRetention20s = Med(b), MedianRetention30s = Med(d), BestRetention10s = a.DefaultIfEmpty().Max(), BestRetention20s = b.DefaultIfEmpty().Max(), BestRetention30s = d.DefaultIfEmpty().Max(), PercentageExceedingThreshold = a.Count == 0 ? 0 : 100.0 * a.Count(x => x >= threshold) / a.Count, VideoGenerationsPerPublishedClip = published == 0 ? 0 : (double)generations / published, TotalClips = clips.Count, TotalWithRetention = clips.Count(x => x.Retention10s.HasValue || x.Retention20s.HasValue || x.Retention30s.HasValue) };
    }
    public async Task<List<HypothesisStats>> GetHypothesisStatsAsync(string username)
    { var clips = (await GetClipsAsync(username)).Where(x => !string.IsNullOrWhiteSpace(x.Hypothesis)); return clips.GroupBy(x => x.Hypothesis.Trim(), StringComparer.OrdinalIgnoreCase).Select(g => new HypothesisStats { Hypothesis = g.First().Hypothesis.Trim(), AverageRetention10s = g.Where(x => x.Retention10s.HasValue).Select(x => x.Retention10s!.Value).DefaultIfEmpty().Average(), AverageRetention20s = g.Where(x => x.Retention20s.HasValue).Select(x => x.Retention20s!.Value).DefaultIfEmpty().Average(), AverageRetention30s = g.Where(x => x.Retention30s.HasValue).Select(x => x.Retention30s!.Value).DefaultIfEmpty().Average(), Count = g.Count() }).OrderByDescending(x => x.AverageRetention10s).ToList(); }
}
