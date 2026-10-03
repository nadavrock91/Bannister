using Bannister.Models;

namespace Bannister.Services;

public class PromptTechniqueService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public PromptTechniqueService(DatabaseService db)
    {
        _db = db;
    }

    public async Task InitAsync()
    {
        if (_initialized) return;
        await _db.EnsureTableAsync<PromptTechnique>();
        await _db.EnsureTableAsync<TechniqueResult>();
        _initialized = true;
    }

    public async Task<List<PromptTechnique>> GetTechniquesAsync(string username)
    {
        await InitAsync();
        username = NormalizeUsername(username);
        var conn = await _db.GetConnectionAsync();
        var list = await conn.Table<PromptTechnique>()
            .Where(x => x.Username == username)
            .ToListAsync();

        return list
            .OrderBy(x => StatusRank(x.Status))
            .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task SaveTechniqueAsync(PromptTechnique technique)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        technique.Username = NormalizeUsername(technique.Username);
        technique.Title = technique.Title?.Trim() ?? "";
        technique.Description = technique.Description?.Trim() ?? "";
        technique.Status = string.IsNullOrWhiteSpace(technique.Status)
            ? "Testing"
            : technique.Status.Trim();
        if (technique.CreatedDate == default)
            technique.CreatedDate = DateTime.UtcNow;

        var conn = await _db.GetConnectionAsync();
        if (technique.Id == 0)
            await conn.InsertAsync(technique);
        else
            await conn.UpdateAsync(technique);
    }

    public async Task DeleteTechniqueAsync(int id)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        var conn = await _db.GetConnectionAsync();
        var technique = await conn.FindAsync<PromptTechnique>(id);
        if (technique != null)
            await conn.DeleteAsync(technique);
    }

    public async Task<List<TechniqueResult>> GetResultsAsync(
        string username,
        int? techniqueId = null)
    {
        await InitAsync();
        username = NormalizeUsername(username);
        var conn = await _db.GetConnectionAsync();
        var list = await conn.Table<TechniqueResult>()
            .Where(x => x.Username == username)
            .ToListAsync();

        if (techniqueId.HasValue)
            list = list.Where(x => x.TechniqueId == techniqueId.Value).ToList();

        return list
            .OrderByDescending(x => x.CreatedDate)
            .ToList();
    }

    public async Task SaveResultAsync(TechniqueResult result)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        result.Username = NormalizeUsername(result.Username);
        result.Notes = result.Notes?.Trim() ?? "";
        result.FailureModesPresent = result.FailureModesPresent?.Trim() ?? "";
        if (result.CreatedDate == default)
            result.CreatedDate = DateTime.UtcNow;

        var conn = await _db.GetConnectionAsync();
        if (result.Id == 0)
            await conn.InsertAsync(result);
        else
            await conn.UpdateAsync(result);
    }

    public async Task DeleteResultAsync(int id)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;

        var conn = await _db.GetConnectionAsync();
        var result = await conn.FindAsync<TechniqueResult>(id);
        if (result != null)
            await conn.DeleteAsync(result);
    }

    public async Task<List<TechniqueStats>> GetStatsAsync(string username)
    {
        var techniques = await GetTechniquesAsync(username);
        var results = await GetResultsAsync(username);
        var failureLookup = FailureMode.All.ToDictionary(x => x.Id, x => x.Name);

        return techniques.Select(technique =>
        {
            var techniqueResults = results
                .Where(x => x.TechniqueId == technique.Id)
                .ToList();
            var failures = techniqueResults
                .SelectMany(x => ParseFailureIds(x.FailureModesPresent))
                .ToList();
            var mostCommonFailure = failures
                .GroupBy(x => x)
                .OrderByDescending(x => x.Count())
                .Select(x => failureLookup.TryGetValue(x.Key, out var name) ? name : "")
                .FirstOrDefault() ?? "";

            return new TechniqueStats
            {
                TechniqueId = technique.Id,
                Title = technique.Title,
                Status = technique.Status,
                AverageQuality = techniqueResults.Count == 0
                    ? 0
                    : techniqueResults.Average(x => x.QualityRating),
                ResultCount = techniqueResults.Count,
                MostCommonFailure = string.IsNullOrWhiteSpace(mostCommonFailure)
                    ? "None"
                    : mostCommonFailure,
                FailureEliminationRate = techniqueResults.Count == 0
                    ? 0
                    : 100.0 * techniqueResults.Count(x => !ParseFailureIds(x.FailureModesPresent).Any()) / techniqueResults.Count
            };
        }).ToList();
    }

    private static IEnumerable<int> ParseFailureIds(string value) =>
        (value ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0)
            .Where(x => x > 0);

    private static int StatusRank(string status) => status switch
    {
        "Confirmed" => 0,
        "Promising" => 1,
        "Testing" => 2,
        "Abandoned" => 3,
        _ => 4
    };

    private static string NormalizeUsername(string username) =>
        (username ?? "").Trim().ToLowerInvariant();
}
