using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Bannister.Models;

namespace Bannister.Services;

public sealed record CompanionAddProposal(int Index, string Text, int Priority);
public sealed record CompanionPriorityProposal(int PointId, int Priority);
public sealed record CompanionArchiveProposal(int PointId, string Reason);
public sealed record CompanionUpdateProposal(int PointId, string Text);
public sealed record CompanionApplyResult(int Added, int Reprioritized, int Archived, int Updated, int IdeaLoggingFailures);

public sealed class CompanionExtraction
{
    public List<CompanionAddProposal> Adds { get; } = new();
    public List<CompanionPriorityProposal> Priorities { get; } = new();
    public List<CompanionArchiveProposal> Archives { get; } = new();
    public List<CompanionUpdateProposal> Updates { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> SkippedLines { get; } = new();
}

public class CompanionService
{
    private readonly DatabaseService _db;
    private readonly IdeaLoggerService _ideaLogger;
    public CompanionService(DatabaseService db, IdeaLoggerService ideaLogger)
    {
        _db = db;
        _ideaLogger = ideaLogger;
    }

    private async Task<SQLite.ISQLiteAsyncConnection> ConnectionAsync() =>
        await _db.GetConnectionAsync();

    public async Task<List<Companion>> GetCompanionsAsync(string username)
    {
        var conn = await ConnectionAsync();
        return await conn.Table<Companion>().Where(x => x.Username == username)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync();
    }

    public async Task<Companion> EnsureMetaAsync(string username)
    {
        var items = await GetCompanionsAsync(username);
        var meta = items.FirstOrDefault(x => x.Scope == "meta");
        if (meta != null) return meta;
        meta = new Companion
        {
            Username = username, Nickname = "Meta", Scope = "meta",
            InterviewStance = "get to know the user", SortOrder = items.Count,
            DefaultQuestionCount = 10
        };
        await SaveCompanionAsync(meta);
        return meta;
    }

    public async Task SaveCompanionAsync(Companion item)
    {
        var conn = await ConnectionAsync();
        if (item.Scope == "meta")
        {
            var existing = await conn.Table<Companion>()
                .Where(x => x.Username == item.Username && x.Scope == "meta")
                .FirstOrDefaultAsync();
            if (existing != null && existing.Id != item.Id)
                throw new InvalidOperationException("Only one meta companion is allowed.");
        }
        if (item.Id == 0) await conn.InsertAsync(item); else await conn.UpdateAsync(item);
    }

    public async Task DeleteCompanionAsync(Companion item)
    {
        var conn = await ConnectionAsync();
        foreach (var point in await conn.Table<CompanionDataPoint>().Where(x => x.CompanionId == item.Id).ToListAsync())
            await conn.DeleteAsync(point);
        foreach (var interview in await conn.Table<CompanionInterview>().Where(x => x.CompanionId == item.Id).ToListAsync())
            await conn.DeleteAsync(interview);
        await conn.DeleteAsync(item);
    }

    public async Task<List<CompanionDataPoint>> GetPointsAsync(string username, int companionId, bool includeArchived = false)
    {
        var conn = await ConnectionAsync();
        var items = await conn.Table<CompanionDataPoint>()
            .Where(x => x.Username == username && x.CompanionId == companionId).ToListAsync();
        return items.Where(x => includeArchived || x.Status == "active")
            .OrderByDescending(x => x.Priority).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task SavePointAsync(CompanionDataPoint point)
    {
        point.Priority = Math.Clamp(point.Priority, 1, 5);
        point.UpdatedAt = DateTime.UtcNow;
        var conn = await ConnectionAsync();
        if (point.Id == 0) await conn.InsertAsync(point); else await conn.UpdateAsync(point);
    }

    public async Task<int> SaveManualPointAsync(
        string username,
        Companion companion,
        string scopeLabel,
        string text,
        int priority)
    {
        var point = new CompanionDataPoint
        {
            Username = username,
            CompanionId = companion.Id,
            Text = text.Trim(),
            Priority = priority,
            Source = "manual"
        };
        await SavePointAsync(point);
        try
        {
            return await LogNewContextPointsAsync(username, companion, scopeLabel, new[] { point }, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[COMPANIONS] Failed to log manual context point {point.Id}: {ex}");
            return 1;
        }
    }

    public async Task DeletePointAsync(CompanionDataPoint point) =>
        await (await ConnectionAsync()).DeleteAsync(point);

    public async Task<List<CompanionInterview>> GetInterviewsAsync(string username, int companionId) =>
        await (await ConnectionAsync()).Table<CompanionInterview>()
            .Where(x => x.Username == username && x.CompanionId == companionId)
            .OrderByDescending(x => x.StartedAt).ToListAsync();

    public async Task SaveInterviewAsync(CompanionInterview interview)
    {
        var conn = await ConnectionAsync();
        if (interview.Id == 0) await conn.InsertAsync(interview); else await conn.UpdateAsync(interview);
    }

    public async Task DeleteInterviewAsync(int interviewId, bool deletePoints)
    {
        var conn = await ConnectionAsync();
        var points = await conn.Table<CompanionDataPoint>()
            .Where(x => x.InterviewId == interviewId)
            .ToListAsync();

        await conn.RunInTransactionAsync(db =>
        {
            foreach (var point in points)
            {
                if (deletePoints)
                {
                    point.IdeaId = null;
                    db.Update(point);
                    db.Delete(point);
                }
                else
                {
                    point.InterviewId = null;
                    db.Update(point);
                }
            }

            db.Delete<CompanionInterview>(interviewId);
        });
    }

    public async Task<string> SaveInterviewIdeaAsync(string username, Companion companion, CompanionInterview interview, string scopeLabel)
    {
        const string category = IdeasService.CompanionInterviewCategory;
        var startedAt = interview.StartedAt.ToLocalTime();
        string title = $"{companion.Nickname} interview · {startedAt:yyyy-MM-dd}";
        string body = BuildInterviewIdeaBody(companion, interview, scopeLabel, startedAt);

        var request = new IdeaLogRequest
        {
            Username = username,
            Title = title,
            Body = body,
            Category = category,
            CreatedAt = startedAt,
            IsLlmSource = true
        };

        int ideaId;
        if (interview.IdeaId is int existingIdeaId)
            ideaId = await _ideaLogger.UpdateIdeaAsync(existingIdeaId, request);
        else
        {
            ideaId = await _ideaLogger.LogIdeaAsync(request, IdeaLogMode.Silent)
                ?? throw new InvalidOperationException("Silent idea logging did not return an idea Id.");
            interview.IdeaId = ideaId;
            await SaveInterviewAsync(interview);
        }

        return category;
    }

    private static string BuildInterviewIdeaBody(Companion companion, CompanionInterview interview, string scopeLabel, DateTime startedAt)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Companion: {companion.Nickname}");
        sb.AppendLine($"Scope: {scopeLabel}");
        sb.AppendLine($"Questions: {interview.QuestionCount}");
        sb.AppendLine($"Date: {startedAt:yyyy-MM-dd HH:mm}");
        sb.AppendLine();
        sb.AppendLine("Transcript:");
        sb.AppendLine(interview.Transcript);
        if (!string.IsNullOrWhiteSpace(interview.Notes))
        {
            sb.AppendLine();
            sb.AppendLine($"Extraction applied: {interview.PointsAdded} added, {interview.PointsReprioritized} reprioritized, {interview.PointsArchived} archived");
        }
        return sb.ToString().TrimEnd();
    }

    public async Task<string> BuildInterviewPromptAsync(string username, Companion companion, int questionCount, string scopeLabel)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are {companion.Nickname}.");
        if (!string.IsNullOrWhiteSpace(companion.PersonaDescription))
            sb.AppendLine($"Persona: {companion.PersonaDescription.Trim()}");
        sb.AppendLine($"Purpose: {companion.InterviewStance.Trim()}");
        if (!string.IsNullOrWhiteSpace(scopeLabel)) sb.AppendLine($"Context: {scopeLabel}");
        sb.AppendLine();
        sb.AppendLine($"This interview has exactly {questionCount} questions. Follow-up and clarifying questions count toward the {questionCount}.");
        sb.AppendLine($"Before asking anything, write your plan: a numbered list of {questionCount} short question topics (topics only, not full questions), chosen to cover what you don't already know about the user. You may adjust later topics based on my answers, but the total stays {questionCount}.");
        sb.AppendLine($"Then ask one question at a time. Start every question with \"Question X of {questionCount} ({{R}} remaining)\".");
        sb.AppendLine("Go deeper on important answers, do not repeat what you already know, and stay in character.");
        sb.AppendLine($"After I answer Question {questionCount}, do not ask anything else. End the interview in character with a short summary of what you learned about me, and the line \"Interview complete.\"");
        sb.AppendLine("If I say \"stop\" at any point, end the same way immediately.");
        sb.AppendLine();
        sb.AppendLine("WHAT YOU ALREADY KNOW ABOUT THE USER:");
        if (companion.Scope == "meta")
        {
            var companions = await GetCompanionsAsync(username);
            bool any = false;
            foreach (var source in companions)
            {
                var points = await GetPointsAsync(username, source.Id);
                if (points.Count == 0) continue;
                any = true;
                sb.AppendLine($"[{source.Nickname}]");
                foreach (var point in points) sb.AppendLine($"- (Priority {point.Priority}) {point.Text}");
            }
            if (!any) sb.AppendLine("- Nothing yet.");
        }
        else
        {
            var points = await GetPointsAsync(username, companion.Id);
            if (points.Count == 0) sb.AppendLine("- Nothing yet.");
            foreach (var point in points) sb.AppendLine($"- (Priority {point.Priority}) {point.Text}");
        }
        sb.AppendLine();
        sb.AppendLine("Begin the interview now with your first question.");
        return sb.ToString();
    }

    public static string BuildExtractionPrompt(Companion companion, CompanionInterview interview, IReadOnlyList<CompanionDataPoint> points)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are extracting durable knowledge for the companion {companion.Nickname} from an interview transcript.");
        sb.AppendLine("Extract only useful facts, preferences, motivations, constraints, patterns, goals, or corrections. Avoid duplicates and temporary conversational details.");
        sb.AppendLine();
        sb.AppendLine("CURRENT DATA POINTS:");
        if (points.Count == 0) sb.AppendLine("- None.");
        foreach (var point in points) sb.AppendLine($"[ID:{point.Id}] [Priority:{point.Priority}] {point.Text}");
        sb.AppendLine();
        sb.AppendLine("INTERVIEW TRANSCRIPT:");
        sb.AppendLine(interview.Transcript);
        sb.AppendLine();
        sb.AppendLine("Return ONLY applicable lines in exactly these formats:");
        sb.AppendLine("addPoint[1] = \"text\"; addPriority[1] = 4;");
        sb.AppendLine("setPriority[<existing id>] = 2;");
        sb.AppendLine("archivePoint[<existing id>] = \"reason\";");
        sb.AppendLine("updatePoint[<existing id>] = \"corrected text\";");
        sb.AppendLine("Use priorities 1-5, where 5 is most important. Number added points consecutively from 1. Do not include explanations or Markdown.");
        return sb.ToString();
    }

    public static CompanionExtraction ParseExtraction(string response)
    {
        var result = new CompanionExtraction();
        var normalized = response.Replace("\"\"", "\"");
        var recognizedSpans = new List<(int Start, int Length)>();
        var addRegex = new Regex("addPoint\\[(\\d+)\\]\\s*=\\s*\"((?:\\\\.|[^\"\\\\])*)\"\\s*;", RegexOptions.IgnoreCase);
        var addPriorityRegex = new Regex("addPriority\\[(\\d+)\\]\\s*=\\s*([1-5])\\s*;", RegexOptions.IgnoreCase);
        var priorityRegex = new Regex("setPriority\\[(\\d+)\\]\\s*=\\s*([1-5])\\s*;", RegexOptions.IgnoreCase);
        var archiveRegex = new Regex("archivePoint\\[(\\d+)\\]\\s*=\\s*\"((?:\\\\.|[^\"\\\\])*)\"\\s*;", RegexOptions.IgnoreCase);
        var updateRegex = new Regex("updatePoint\\[(\\d+)\\]\\s*=\\s*\"((?:\\\\.|[^\"\\\\])*)\"\\s*;", RegexOptions.IgnoreCase);

        var addPoints = new List<(int Index, string Text)>();
        var addIndexes = new HashSet<int>();
        foreach (Match match in addRegex.Matches(normalized))
        {
            recognizedSpans.Add((match.Index, match.Length));
            int index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (!addIndexes.Add(index))
            {
                result.Warnings.Add($"Duplicate addPoint[{index}]; using the first value");
                continue;
            }
            addPoints.Add((index, DecodeLlmValue(match.Groups[2].Value)));
        }

        var addPriorities = new Dictionary<int, int>();
        foreach (Match match in addPriorityRegex.Matches(normalized))
        {
            recognizedSpans.Add((match.Index, match.Length));
            int index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            int priority = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            if (!addPriorities.TryAdd(index, priority))
                result.Warnings.Add($"Duplicate addPriority[{index}]; using the first value {addPriorities[index]}");
        }

        foreach (var point in addPoints)
        {
            int priority = 3;
            if (!addPriorities.TryGetValue(point.Index, out priority))
            {
                priority = 3;
                result.Warnings.Add($"Point {point.Index} has no addPriority[{point.Index}]; using priority 3");
            }
            result.Adds.Add(new(point.Index, point.Text, priority));
        }

        foreach (var priority in addPriorities)
            if (!addIndexes.Contains(priority.Key))
                result.Warnings.Add($"addPriority[{priority.Key}] has no matching addPoint[{priority.Key}]");

        foreach (Match match in priorityRegex.Matches(normalized))
        {
            recognizedSpans.Add((match.Index, match.Length));
            result.Priorities.Add(new(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)));
        }
        foreach (Match match in archiveRegex.Matches(normalized))
        {
            recognizedSpans.Add((match.Index, match.Length));
            result.Archives.Add(new(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), DecodeLlmValue(match.Groups[2].Value)));
        }
        foreach (Match match in updateRegex.Matches(normalized))
        {
            recognizedSpans.Add((match.Index, match.Length));
            result.Updates.Add(new(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), DecodeLlmValue(match.Groups[2].Value)));
        }

        var unrecognized = normalized.ToCharArray();
        foreach (var span in recognizedSpans)
            Array.Fill(unrecognized, ' ', span.Start, span.Length);
        foreach (string rawLine in new string(unrecognized).Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim().Trim(';').Trim();
            if (!string.IsNullOrWhiteSpace(line)) result.SkippedLines.Add(line);
        }
        return result;
    }

    public async Task<CompanionApplyResult> ApplyExtractionAsync(string username, Companion companion, CompanionInterview interview,
        string scopeLabel,
        IEnumerable<CompanionAddProposal> adds, IEnumerable<CompanionPriorityProposal> priorities,
        IEnumerable<CompanionArchiveProposal> archives, IEnumerable<CompanionUpdateProposal> updates)
    {
        var existing = await GetPointsAsync(username, companion.Id, true);
        int added = 0, reprioritized = 0, archived = 0, updated = 0;
        var addedPoints = new List<CompanionDataPoint>();
        var changedLinkedPoints = new Dictionary<int, CompanionDataPoint>();
        foreach (var proposal in adds)
        {
            if (string.IsNullOrWhiteSpace(proposal.Text)) continue;
            var point = new CompanionDataPoint { Username = username, CompanionId = companion.Id, Text = proposal.Text.Trim(), Priority = proposal.Priority, Source = "interview", InterviewId = interview.Id };
            await SavePointAsync(point);
            addedPoints.Add(point);
            added++;
        }
        foreach (var proposal in priorities)
        {
            var point = existing.FirstOrDefault(x => x.Id == proposal.PointId); if (point == null) continue;
            point.Priority = proposal.Priority; await SavePointAsync(point); reprioritized++;
            if (point.IdeaId.HasValue) changedLinkedPoints[point.Id] = point;
        }
        foreach (var proposal in archives)
        {
            var point = existing.FirstOrDefault(x => x.Id == proposal.PointId); if (point == null) continue;
            point.Status = "archived"; await SavePointAsync(point); archived++;
        }
        foreach (var proposal in updates)
        {
            var point = existing.FirstOrDefault(x => x.Id == proposal.PointId); if (point == null || string.IsNullOrWhiteSpace(proposal.Text)) continue;
            point.Text = proposal.Text.Trim(); await SavePointAsync(point); updated++;
            if (point.IdeaId.HasValue) changedLinkedPoints[point.Id] = point;
        }
        interview.PointsAdded += added;
        interview.PointsReprioritized += reprioritized;
        interview.PointsArchived += archived;
        interview.Notes = $"Extraction applied {DateTime.Now:yyyy-MM-dd HH:mm}";
        await SaveInterviewAsync(interview);

        int ideaLoggingFailures = 0;
        try
        {
            ideaLoggingFailures += await LogNewContextPointsAsync(username, companion, scopeLabel, addedPoints, interview);
        }
        catch (Exception ex)
        {
            ideaLoggingFailures += addedPoints.Count;
            System.Diagnostics.Debug.WriteLine($"[COMPANIONS] Failed to batch-log context points: {ex}");
        }

        foreach (var point in changedLinkedPoints.Values)
        {
            try
            {
                string sourceLine = await GetContextPointSourceLineAsync(point, interview);
                await _ideaLogger.UpdateIdeaAsync(
                    point.IdeaId!.Value,
                    BuildContextPointIdeaRequest(username, companion, scopeLabel, point, sourceLine, includeCreatedAt: false));
            }
            catch (Exception ex)
            {
                ideaLoggingFailures++;
                System.Diagnostics.Debug.WriteLine($"[COMPANIONS] Failed to update context-point idea for point {point.Id}: {ex}");
            }
        }

        return new CompanionApplyResult(added, reprioritized, archived, updated, ideaLoggingFailures);
    }

    private async Task<int> LogNewContextPointsAsync(
        string username,
        Companion companion,
        string scopeLabel,
        IReadOnlyList<CompanionDataPoint> points,
        CompanionInterview? interview)
    {
        if (points.Count == 0) return 0;

        string sourceLine = interview == null
            ? "From: manual"
            : $"From interview: {interview.StartedAt.ToLocalTime():yyyy-MM-dd}";
        var entries = points.Select(point => new
        {
            Point = point,
            Request = BuildContextPointIdeaRequest(username, companion, scopeLabel, point, sourceLine, includeCreatedAt: true)
        }).ToList();

        var batch = await _ideaLogger.LogBatchSilentAsync(entries.Select(x => x.Request));
        int failures = batch.Failures.Count;
        foreach (var success in batch.Successes)
        {
            if (!success.IdeaId.HasValue) continue;
            var entry = entries.First(x => ReferenceEquals(x.Request, success.Request));
            try
            {
                entry.Point.IdeaId = success.IdeaId.Value;
                await SavePointAsync(entry.Point);
            }
            catch (Exception ex)
            {
                failures++;
                System.Diagnostics.Debug.WriteLine($"[COMPANIONS] Failed to link context point {entry.Point.Id} to idea {success.IdeaId.Value}: {ex}");
            }
        }
        return failures;
    }

    private async Task<string> GetContextPointSourceLineAsync(CompanionDataPoint point, CompanionInterview currentInterview)
    {
        if (string.Equals(point.Source, "manual", StringComparison.OrdinalIgnoreCase) || !point.InterviewId.HasValue)
            return "From: manual";

        CompanionInterview? sourceInterview = point.InterviewId.Value == currentInterview.Id
            ? currentInterview
            : await (await ConnectionAsync()).Table<CompanionInterview>()
                .Where(x => x.Id == point.InterviewId.Value)
                .FirstOrDefaultAsync();
        sourceInterview ??= currentInterview;
        return $"From interview: {sourceInterview.StartedAt.ToLocalTime():yyyy-MM-dd}";
    }

    private static IdeaLogRequest BuildContextPointIdeaRequest(
        string username,
        Companion companion,
        string scopeLabel,
        CompanionDataPoint point,
        string sourceLine,
        bool includeCreatedAt)
    {
        string text = point.Text.Trim();
        string title = text.Length <= 80 ? text : text[..79].TrimEnd() + "…";
        string body = $"{text}\n\nPriority: {point.Priority}\nCompanion: {companion.Nickname} ({scopeLabel})\n{sourceLine}";
        return new IdeaLogRequest
        {
            Username = username,
            Title = title,
            Body = body,
            Category = IdeasService.CompanionContextPointCategory,
            Subcategory = companion.Nickname,
            Rating = point.Priority * 20,
            CreatedAt = includeCreatedAt ? point.CreatedAt.ToLocalTime() : null,
            IsLlmSource = true
        };
    }

    public static string DecodeLlmValue(string value) =>
        value.Replace("\\\"", "\"").Replace("\\\\", "\\").Trim();
}
