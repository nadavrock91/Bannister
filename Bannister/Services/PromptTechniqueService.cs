using Bannister.Models;

namespace Bannister.Services;

public class PromptTechniqueService
{
    private readonly DatabaseService _db;
    private readonly OperationQueueService _queue;
    private bool _initialized;

    public PromptTechniqueService(DatabaseService db, OperationQueueService queue)
    {
        _db = db;
        _queue = queue;
    }

    public async Task InitAsync()
    {
        if (_initialized) return;
        await _db.EnsureTableAsync<PromptTechnique>();
        await _db.EnsureTableAsync<TechniqueResult>();
        if (!_db.IsReadOnly)
        {
            var conn = await _db.GetConnectionAsync();
            try { await conn.ExecuteAsync("ALTER TABLE prompt_techniques ADD COLUMN PromptSuffix TEXT DEFAULT ''"); } catch { }
        }
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
        var isNew = technique.Id == 0;
        technique.Username = NormalizeUsername(technique.Username);
        technique.Title = technique.Title?.Trim() ?? "";
        technique.Description = technique.Description?.Trim() ?? "";
        technique.PromptSuffix = technique.PromptSuffix?.Trim() ?? "";
        technique.Status = string.IsNullOrWhiteSpace(technique.Status)
            ? "Testing"
            : technique.Status.Trim();
        if (technique.CreatedDate == default)
            technique.CreatedDate = DateTime.UtcNow;

        if (_db.IsReadOnly)
        {
            if (isNew)
            {
                await _queue.EnqueueAsync("save_technique", new
                {
                    operation_type = "save_technique",
                    client_uuid = Guid.NewGuid().ToString("D"),
                    title = technique.Title,
                    description = technique.Description,
                    prompt_suffix = technique.PromptSuffix,
                    status = technique.Status,
                    created_date = technique.CreatedDate.ToUniversalTime().ToString("O")
                });
            }
            return;
        }

        var conn = await _db.GetConnectionAsync();
        if (isNew)
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
        var isNew = result.Id == 0;
        result.Username = NormalizeUsername(result.Username);
        result.Notes = result.Notes?.Trim() ?? "";
        result.FailureModesPresent = result.FailureModesPresent?.Trim() ?? "";
        if (result.CreatedDate == default)
            result.CreatedDate = DateTime.UtcNow;

        var conn = await _db.GetConnectionAsync();
        if (_db.IsReadOnly)
        {
            if (isNew)
            {
                var technique = await conn.FindAsync<PromptTechnique>(result.TechniqueId);
                if (technique != null)
                {
                    await _queue.EnqueueAsync("log_technique_result", new
                    {
                        operation_type = "log_technique_result",
                        client_uuid = Guid.NewGuid().ToString("D"),
                        technique_title = technique.Title,
                        quality_rating = result.QualityRating,
                        failure_modes_present = result.FailureModesPresent,
                        notes = result.Notes,
                        created_date = result.CreatedDate.ToUniversalTime().ToString("O")
                    });
                }
            }
            return;
        }

        if (isNew)
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

        return techniques.Select(technique =>
        {
            var techniqueResults = results
                .Where(x => x.TechniqueId == technique.Id)
                .ToList();

            return new TechniqueStats
            {
                TechniqueId = technique.Id,
                Title = technique.Title,
                Status = technique.Status,
                TotalGenerations = techniqueResults.Count,
                TotalTerrible = techniqueResults.Count(x => x.QualityRating == 1),
                TotalUsable = techniqueResults.Count(x => x.QualityRating == 5),
                TotalExtraordinary = techniqueResults.Count(x => x.QualityRating == 10)
            };
        }).ToList();
    }

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
