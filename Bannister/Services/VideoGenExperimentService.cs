using Bannister.Models;
using System.Text.Json;

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
        await c.CreateTableAsync<ProductionMethod>();
        await c.CreateTableAsync<ProductionMethodStage>();
        await c.CreateTableAsync<WeeklyMethodSnapshot>();
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
    public async Task<List<ProductionMethod>> GetMethodsAsync(string username)
    { await InitAsync(); var c = await _db.GetConnectionAsync(); return await c.Table<ProductionMethod>().Where(x => x.Username == username && !x.IsArchived).OrderByDescending(x => x.CreatedDate).ToListAsync(); }
    public async Task ArchiveMethodAsync(int id)
    { await InitAsync(); var c = await _db.GetConnectionAsync(); var method = await c.FindAsync<ProductionMethod>(id); if (method != null) { method.IsArchived = true; await c.UpdateAsync(method); } }
    public async Task<ProductionMethod> SaveMethodAsync(ProductionMethod method)
    {
        await InitAsync(); var c = await _db.GetConnectionAsync();
        if (method.Id != 0)
        {
            var used = await c.Table<WeeklyMethodExperiment>().Where(x => x.MethodId == method.Id).CountAsync() > 0;
            if (used)
            {
                var next = new ProductionMethod { Username = method.Username, Name = method.Name, Description = method.Description, Version = method.Version + 1, CreatedDate = DateTime.UtcNow, IsArchived = false };
                await c.InsertAsync(next);
                var stages = await c.Table<ProductionMethodStage>().Where(x => x.MethodId == method.Id).ToListAsync();
                foreach (var stage in stages)
                    await c.InsertAsync(new ProductionMethodStage { MethodId = next.Id, Position = stage.Position, StageName = stage.StageName, Instructions = stage.Instructions, InputCount = stage.InputCount, OutputCount = stage.OutputCount, GenerationAllowance = stage.GenerationAllowance });
                return next;
            }
            await c.UpdateAsync(method); return method;
        }
        if (method.Version < 1) method.Version = 1;
        await c.InsertAsync(method); return method;
    }
    public async Task<List<ProductionMethodStage>> GetStagesAsync(int methodId)
    { await InitAsync(); var c = await _db.GetConnectionAsync(); return await c.Table<ProductionMethodStage>().Where(x => x.MethodId == methodId).OrderBy(x => x.Position).ToListAsync(); }
    public async Task SaveStageAsync(ProductionMethodStage stage)
    { await InitAsync(); var c = await _db.GetConnectionAsync(); if (stage.Id == 0) await c.InsertAsync(stage); else await c.UpdateAsync(stage); }
    public async Task DeleteStageAsync(int id)
    { await InitAsync(); var c = await _db.GetConnectionAsync(); var stage = await c.FindAsync<ProductionMethodStage>(id); if (stage != null) await c.DeleteAsync(stage); }
    public async Task ReorderStagesAsync(List<ProductionMethodStage> stages)
    { await InitAsync(); var c = await _db.GetConnectionAsync(); for (var i = 0; i < stages.Count; i++) { stages[i].Position = i; await c.UpdateAsync(stages[i]); } }
    public async Task<WeeklyMethodSnapshot> CreateSnapshotAsync(int weeklyMethodExperimentId, int methodId)
    {
        await InitAsync(); var c = await _db.GetConnectionAsync(); var method = await c.FindAsync<ProductionMethod>(methodId) ?? throw new InvalidOperationException("Method not found."); var stages = await GetStagesAsync(methodId);
        var json = JsonSerializer.Serialize(new MethodSnapshotPayload { Method = method, Stages = stages });
        var snapshot = new WeeklyMethodSnapshot { WeeklyMethodExperimentId = weeklyMethodExperimentId, MethodId = method.Id, MethodVersion = method.Version, SnapshotJson = json };
        await c.InsertAsync(snapshot); var week = await c.FindAsync<WeeklyMethodExperiment>(weeklyMethodExperimentId); if (week != null) { week.MethodId = method.Id; week.MethodSnapshotId = snapshot.Id; await c.UpdateAsync(week); } return snapshot;
    }
    public async Task<(ProductionMethod Method, List<ProductionMethodStage> Stages)?> GetSnapshotAsync(int weeklyMethodExperimentId)
    { await InitAsync(); var c = await _db.GetConnectionAsync(); var s = await c.Table<WeeklyMethodSnapshot>().Where(x => x.WeeklyMethodExperimentId == weeklyMethodExperimentId).OrderByDescending(x => x.Id).FirstOrDefaultAsync(); if (s == null) return null; var payload = JsonSerializer.Deserialize<MethodSnapshotPayload>(s.SnapshotJson); return payload == null ? null : (payload.Method, payload.Stages ?? new List<ProductionMethodStage>()); }
    private sealed class MethodSnapshotPayload { public ProductionMethod Method { get; set; } = new(); public List<ProductionMethodStage> Stages { get; set; } = new(); }
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
