using System.Text;
using System.Text.Json;
using Bannister.Models;

namespace Bannister.Services;

public class JournalAnalysisService
{
    private readonly DatabaseService _db;
    private readonly JournalService _journalService;
    private readonly LifePathService _lifePathService;
    private readonly GameService _gameService;
    private bool _initialized;

    public JournalAnalysisService(DatabaseService db, JournalService journalService,
        LifePathService lifePathService, GameService gameService)
    { _db = db; _journalService = journalService; _lifePathService = lifePathService; _gameService = gameService; }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return; _initialized = true;
        await _db.EnsureTableAsync<JournalAnalysisCheckpoint>();
        await _db.EnsureTableAsync<LifePathProposal>();
    }
    public async Task<JournalAnalysisCheckpoint> GetCheckpointAsync(string username)
    {
        await EnsureInitializedAsync(); var conn = await _db.GetConnectionAsync();
        return await conn.FindAsync<JournalAnalysisCheckpoint>(username) ?? new() { Username = username };
    }
    private async Task SaveCheckpointAsync(JournalAnalysisCheckpoint cp)
    {
        var conn = await _db.GetConnectionAsync(); var old = await conn.FindAsync<JournalAnalysisCheckpoint>(cp.Username);
        if (old == null) await conn.InsertAsync(cp); else await conn.UpdateAsync(cp);
    }
    public async Task<List<LifePathProposal>> GetPendingProposalsAsync(string username)
    {
        await EnsureInitializedAsync(); var conn = await _db.GetConnectionAsync();
        return await conn.Table<LifePathProposal>().Where(p => p.Username == username && p.Status == (int)ProposalStatus.Pending).ToListAsync();
    }
    public async Task<List<LifePathProposal>> GetProposalsForRunAsync(string username, string runId)
    {
        await EnsureInitializedAsync(); var conn = await _db.GetConnectionAsync();
        return await conn.Table<LifePathProposal>().Where(p => p.Username == username && p.RunId == runId).ToListAsync();
    }

    public async Task<(string RunId, List<LifePathProposal> Proposals)> RunAnalysisAsync(
        string username, IJournalAnalysisProvider provider, DateTime? fromDate = null,
        DateTime? toDate = null, bool fullReanalysis = false, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(); var cp = await GetCheckpointAsync(username);
        DateTime? from = fromDate;
        if (!fullReanalysis && from == null && cp.LastAnalyzedAt.HasValue) from = cp.LastAnalyzedAt;
        var entries = await _journalService.GetEntriesInRangeAsync(username, from, toDate);
        if (entries.Count == 0) return ("", new());
        var games = await _gameService.GetGamesAsync(username);
        var blocks = await _lifePathService.GetAllBlocksAsync(username);
        var response = await provider.AnalyzeAsync(BuildPrompt(entries, games, blocks, from, toDate), ct);
        var runId = Guid.NewGuid().ToString("N")[..12];
        var proposals = ParseProposals(response, username, runId);
        var conn = await _db.GetConnectionAsync(); foreach (var proposal in proposals) await conn.InsertAsync(proposal);
        cp.LastAnalyzedAt = entries.Max(e => e.CreatedAt); cp.LastRunAt = DateTime.UtcNow; cp.TotalRunCount++; await SaveCheckpointAsync(cp);
        await _journalService.MarkEntriesAnalyzedAsync(username, entries.Select(e => e.Id).ToList());
        return (runId, proposals);
    }

    public async Task AcceptProposalAsync(LifePathProposal proposal, string username)
    {
        await EnsureInitializedAsync(); var conn = await _db.GetConnectionAsync(); var type = (ProposalType)proposal.ProposalType;
        switch (type)
        {
            case ProposalType.CreateBlock:
            case ProposalType.AddSubBlock:
                var parent = type == ProposalType.AddSubBlock && proposal.ParentBlockId > 0 ? proposal.ParentBlockId : (int?)null;
                var created = await _lifePathService.CreateAsync(username, proposal.GameId, proposal.ProposedLabel,
                    proposal.ProposedStartDate ?? DateTime.Today, proposal.ProposedEndDate, proposal.ProposedReason, parent, parent.HasValue ? 2 : 1);
                created.EvidenceEntryIds = proposal.EvidenceEntryIds; created.AnalysisNote = proposal.Evidence; await _lifePathService.UpdateAsync(created); break;
            case ProposalType.UpdateBlockDates:
            case ProposalType.CorrectLabel:
            case ProposalType.NoteReturn:
            case ProposalType.EndBlock:
                var blocks = await _lifePathService.GetAllBlocksAsync(username); var target = blocks.FirstOrDefault(b => b.Id == proposal.TargetBlockId);
                if (target != null)
                {
                    if (type == ProposalType.EndBlock) target.EndDate = proposal.ProposedEndDate ?? DateTime.Today;
                    else { if (!string.IsNullOrWhiteSpace(proposal.ProposedLabel)) target.Label = proposal.ProposedLabel; if (proposal.ProposedStartDate.HasValue) target.StartDate = proposal.ProposedStartDate.Value; if (proposal.ProposedEndDate.HasValue) target.EndDate = proposal.ProposedEndDate; }
                    if (!string.IsNullOrWhiteSpace(proposal.ProposedReason)) target.Reason = proposal.ProposedReason;
                    target.EvidenceEntryIds = MergeEvidenceIds(target.EvidenceEntryIds, proposal.EvidenceEntryIds); target.AnalysisNote = proposal.Evidence; await _lifePathService.UpdateAsync(target);
                }
                break;
        }
        proposal.Status = (int)ProposalStatus.Accepted; proposal.ResolvedAt = DateTime.UtcNow; await conn.UpdateAsync(proposal);
    }
    public async Task RejectProposalAsync(LifePathProposal proposal)
    { await EnsureInitializedAsync(); var conn = await _db.GetConnectionAsync(); proposal.Status = (int)ProposalStatus.Rejected; proposal.ResolvedAt = DateTime.UtcNow; await conn.UpdateAsync(proposal); }
    public async Task UpdateProposalAsync(LifePathProposal proposal)
    { await EnsureInitializedAsync(); await (await _db.GetConnectionAsync()).UpdateAsync(proposal); }

    private static string BuildPrompt(List<JournalEntry> entries, List<Game> games, List<LifePathBlock> blocks, DateTime? from, DateTime? to)
    {
        var sb = new StringBuilder(); sb.AppendLine("Analyze these journal entries and propose only evidence-supported Life Path changes."); sb.AppendLine("Reference entry IDs. Return proposal[N].type, gameId, label, startDate, endDate, reason, evidence, evidenceEntryIds fields."); sb.AppendLine("EXISTING GAMES:");
        foreach (var game in games) sb.AppendLine($"GameId: {game.GameId} | Name: {game.DisplayName} | Created: {game.CreatedAt:dd MMM yyyy}");
        sb.AppendLine("EXISTING BLOCKS:"); foreach (var block in blocks) sb.AppendLine($"BlockId: {block.Id} | Game: {block.GameId} | Label: {block.Label} | Level: {block.Level}");
        sb.AppendLine($"JOURNAL ENTRIES ({entries.Count}):"); foreach (var entry in entries) sb.AppendLine($"[EntryId:{entry.Id}] {entry.CreatedAt:dd MMM yyyy HH:mm}\n{entry.Text}\n---"); return sb.ToString();
    }

    private static List<LifePathProposal> ParseProposals(string response, string username, string runId)
    {
        var result = new List<LifePathProposal>(); for (int i = 1; ; i++)
        {
            var prefix = $"proposal[{i}]."; if (!response.Contains(prefix, StringComparison.OrdinalIgnoreCase)) break;
            var type = GetVal(response, prefix + "type"); if (type == null) continue;
            var proposal = new LifePathProposal { Username = username, RunId = runId, GameId = GetVal(response, prefix + "gameId") ?? "", ProposedLabel = GetVal(response, prefix + "label") ?? "", ProposedReason = GetVal(response, prefix + "reason") ?? "", Evidence = GetVal(response, prefix + "evidence") ?? "", EvidenceEntryIds = GetVal(response, prefix + "evidenceEntryIds") ?? "[]" };
            proposal.ProposalType = type.Trim('"',' ').ToLowerInvariant() switch { "addsubblock" => (int)ProposalType.AddSubBlock, "updateblockdates" => (int)ProposalType.UpdateBlockDates, "endblock" => (int)ProposalType.EndBlock, "correctlabel" => (int)ProposalType.CorrectLabel, "notereturn" => (int)ProposalType.NoteReturn, _ => (int)ProposalType.CreateBlock };
            if (DateTime.TryParse(GetVal(response, prefix + "startDate"), out var start)) proposal.ProposedStartDate = start;
            if (DateTime.TryParse(GetVal(response, prefix + "endDate"), out var end)) proposal.ProposedEndDate = end;
            if (int.TryParse(GetVal(response, prefix + "targetBlockId"), out var target)) proposal.TargetBlockId = target;
            if (int.TryParse(GetVal(response, prefix + "parentBlockId"), out var parent)) proposal.ParentBlockId = parent;
            result.Add(proposal);
        } return result;
    }
    private static string? GetVal(string text, string key)
    { var idx = text.IndexOf(key, StringComparison.OrdinalIgnoreCase); if (idx < 0) return null; var eq = text.IndexOf('=', idx); if (eq < 0) return null; var semi = text.IndexOf(';', eq); var raw = (semi >= 0 ? text[(eq+1)..semi] : text[(eq+1)..]).Trim().Trim('"'); return raw; }
    private static string MergeEvidenceIds(string existing, string incoming)
    { try { var a = JsonSerializer.Deserialize<List<int>>(existing) ?? new(); var b = JsonSerializer.Deserialize<List<int>>(incoming) ?? new(); return JsonSerializer.Serialize(a.Union(b).Distinct()); } catch { return existing; } }
}
