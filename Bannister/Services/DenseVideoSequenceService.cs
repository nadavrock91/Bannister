using Bannister.Models;

namespace Bannister.Services;

public class DenseVideoSequenceService
{
    private readonly DatabaseService _db;
    private const string StorageFolder = "DenseVideoSequences";

    public DenseVideoSequenceService(DatabaseService db) => _db = db;

    public string ResolvePath(string? relativePath) =>
        string.IsNullOrWhiteSpace(relativePath) ? "" :
        Path.IsPathRooted(relativePath) ? relativePath :
        Path.Combine(FileSystem.AppDataDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar));

    public async Task<string> ImportFileAsync(string sourcePath, string category)
    {
        string extension = Path.GetExtension(sourcePath);
        string relative = Path.Combine(StorageFolder, category,
            $"{Guid.NewGuid():N}{extension}");
        string destination = ResolvePath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(sourcePath, destination, true);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    public async Task<string> ImportFileAsync(FileResult file, string category)
    {
        string extension = Path.GetExtension(file.FileName);
        string relative = Path.Combine(StorageFolder, category,
            $"{Guid.NewGuid():N}{extension}");
        string destination = ResolvePath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var input = await file.OpenReadAsync();
        using var output = File.Create(destination);
        await input.CopyToAsync(output);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    public async Task<List<DenseVideoSequence>> GetSequencesAsync(string username, int? experimentId = null)
    {
        var conn = await _db.GetConnectionAsync();
        var rows = await conn.Table<DenseVideoSequence>()
            .Where(x => x.Username == username).ToListAsync();
        return rows.Where(x => x.ExperimentId == experimentId)
            .OrderByDescending(x => x.UpdatedAt).ToList();
    }

    public async Task SaveSequenceAsync(DenseVideoSequence sequence)
    {
        var conn = await _db.GetConnectionAsync();
        sequence.UpdatedAt = DateTime.UtcNow;
        if (sequence.Id == 0) await conn.InsertAsync(sequence);
        else await conn.UpdateAsync(sequence);
    }

    public async Task<List<DenseVideoGeneration>> GetGenerationsAsync(int sequenceId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<DenseVideoGeneration>()
            .Where(x => x.SequenceId == sequenceId)
            .OrderBy(x => x.SequenceOrder).ThenBy(x => x.Id).ToListAsync();
    }

    public async Task SaveGenerationAsync(DenseVideoGeneration generation)
    {
        var conn = await _db.GetConnectionAsync();
        generation.UpdatedAt = DateTime.UtcNow;
        if (generation.Id == 0) await conn.InsertAsync(generation);
        else await conn.UpdateAsync(generation);
    }

    public async Task DeleteGenerationAsync(int id)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<DenseVideoGeneration>(id);
        // Files are intentionally retained: another generation may reference them.
    }

    public async Task<List<string>> GetTransitionSuggestionsAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        var rows = await conn.Table<DenseVideoGeneration>()
            .Where(x => x.Username == username).ToListAsync();
        return rows.Select(x => x.TransitionType.Trim())
            .Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x).ToList();
    }

    public async Task<List<DenseSequenceStats>> GetStatsAsync(string username, bool byPrompt)
    {
        var conn = await _db.GetConnectionAsync();
        var rows = await conn.Table<DenseVideoGeneration>()
            .Where(x => x.Username == username).ToListAsync();
        return rows.GroupBy(x => byPrompt ? x.Prompt.Trim() : x.TransitionType.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Key.Length > 0)
            .Select(g => new DenseSequenceStats
            {
                Key = g.Key,
                Attempts = g.Count(),
                UsableCount = g.Count(x => x.Status == "reviewed" && x.UsableSeconds > 0),
                FailedAttempts = g.Count(x => x.Status == "failed"),
                TotalUsableSeconds = g.Sum(x => x.UsableSeconds),
                AverageUsableSeconds = g.Where(x => x.UsableSeconds > 0)
                    .Select(x => x.UsableSeconds).DefaultIfEmpty().Average()
            }).OrderByDescending(x => x.Attempts).ToList();
    }

    public async Task<List<DenseSequenceExperiment>> GetExperimentsAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<DenseSequenceExperiment>()
            .Where(x => x.Username == username).OrderByDescending(x => x.Id).ToListAsync();
    }

    public async Task SaveExperimentAsync(DenseSequenceExperiment experiment)
    {
        var conn = await _db.GetConnectionAsync();
        if (experiment.Id == 0)
        {
            await conn.InsertAsync(experiment);
            for (int i = 1; i <= 20; i++)
                await conn.InsertAsync(new DenseConceptGrid
                {
                    Username = experiment.Username, ExperimentId = experiment.Id,
                    GridNumber = i, Title = $"Concept Grid {i}"
                });
        }
        else await conn.UpdateAsync(experiment);
    }

    public async Task<List<DenseConceptGrid>> GetGridsAsync(int experimentId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<DenseConceptGrid>().Where(x => x.ExperimentId == experimentId)
            .OrderBy(x => x.GridNumber).ToListAsync();
    }

    public async Task<List<DenseConceptGridImage>> GetGridImagesAsync(int experimentId)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<DenseConceptGridImage>()
            .Where(x => x.ExperimentId == experimentId).OrderBy(x => x.Id).ToListAsync();
    }

    public async Task SaveGridImageAsync(DenseConceptGridImage image)
    {
        var conn = await _db.GetConnectionAsync();
        if (image.Id == 0) await conn.InsertAsync(image);
        else await conn.UpdateAsync(image);
    }

    public async Task EnsureSelectedSequencesAsync(
        DenseSequenceExperiment experiment, IEnumerable<DenseConceptGridImage> selected)
    {
        foreach (var image in selected)
        {
            if (image.SequenceId.HasValue) continue;
            var sequence = new DenseVideoSequence
            {
                Username = experiment.Username,
                Name = $"Start {image.Id}",
                SequenceType = "dense action scene",
                ExperimentId = experiment.Id,
                SourceGridImageId = image.Id,
                InitialThumbnailPath = image.ImagePath
            };
            await SaveSequenceAsync(sequence);
            image.SequenceId = sequence.Id;
            await SaveGridImageAsync(image);
        }
    }
}
