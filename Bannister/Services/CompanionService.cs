using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Bannister.Models;

namespace Bannister.Services;

public sealed record CompanionAddProposal(int Index, string Text, int Priority);
public sealed record CompanionPriorityProposal(int PointId, int Priority);
public sealed record CompanionArchiveProposal(int PointId, string Reason);
public sealed record CompanionUpdateProposal(int PointId, string Text);

public sealed class CompanionExtraction
{
    public List<CompanionAddProposal> Adds { get; } = new();
    public List<CompanionPriorityProposal> Priorities { get; } = new();
    public List<CompanionArchiveProposal> Archives { get; } = new();
    public List<CompanionUpdateProposal> Updates { get; } = new();
    public List<string> SkippedLines { get; } = new();
}

public class CompanionService
{
    private readonly DatabaseService _db;
    private readonly IdeasService _ideas;
    public CompanionService(DatabaseService db, IdeasService ideas)
    {
        _db = db;
        _ideas = ideas;
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

    public async Task<string> SaveInterviewIdeaAsync(string username, Companion companion, CompanionInterview interview, string scopeLabel)
    {
        var categories = await _ideas.GetCategoriesAsync(username);
        string category = categories.FirstOrDefault(IsAiConversationCategory) ?? "AI Conversations";
        var startedAt = interview.StartedAt.ToLocalTime();
        string title = $"{companion.Nickname} interview · {startedAt:yyyy-MM-dd}";
        string body = BuildInterviewIdeaBody(companion, interview, scopeLabel, startedAt);

        IdeaItem? idea = null;
        if (interview.IdeaId is int ideaId)
        {
            idea = (await _ideas.GetIdeasAsync(username)).FirstOrDefault(x => x.Id == ideaId);
            idea ??= (await _ideas.GetArchivedIdeasAsync(username)).FirstOrDefault(x => x.Id == ideaId);
        }

        if (idea == null)
        {
            idea = await _ideas.CreateIdeaAsync(
                username,
                title,
                category,
                createdAt: startedAt,
                fullIdea: body,
                isLlmSource: true);
            if (idea.Id > 0)
            {
                interview.IdeaId = idea.Id;
                await SaveInterviewAsync(interview);
            }
        }
        else
        {
            idea.Title = title;
            idea.Category = category;
            idea.FullIdea = body;
            await _ideas.UpdateIdeaAsync(idea);
        }

        return category;
    }

    private static bool IsAiConversationCategory(string category)
    {
        string name = category.Trim();
        if (name.Contains("conversation", StringComparison.OrdinalIgnoreCase) &&
            name.Contains("ai", StringComparison.OrdinalIgnoreCase))
            return true;

        return name.Equals("AI Conversations", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Conversations with AI", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("AI Chats", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("LLM Conversations", StringComparison.OrdinalIgnoreCase);
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
        var recognized = new HashSet<int>();
        var lines = normalized.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
        var addRegex = new Regex("addPoint\\[(\\d+)\\]\\s*=\\s*\"((?:\\\\.|[^\"\\\\])*)\"\\s*;\\s*addPriority\\[\\1\\]\\s*=\\s*([1-5])\\s*;", RegexOptions.IgnoreCase);
        var priorityRegex = new Regex("setPriority\\[(\\d+)\\]\\s*=\\s*([1-5])\\s*;", RegexOptions.IgnoreCase);
        var archiveRegex = new Regex("archivePoint\\[(\\d+)\\]\\s*=\\s*\"((?:\\\\.|[^\"\\\\])*)\"\\s*;", RegexOptions.IgnoreCase);
        var updateRegex = new Regex("updatePoint\\[(\\d+)\\]\\s*=\\s*\"((?:\\\\.|[^\"\\\\])*)\"\\s*;", RegexOptions.IgnoreCase);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            var add = addRegex.Match(line);
            if (add.Success) result.Adds.Add(new(int.Parse(add.Groups[1].Value, CultureInfo.InvariantCulture), DecodeLlmValue(add.Groups[2].Value), int.Parse(add.Groups[3].Value, CultureInfo.InvariantCulture)));
            else if (priorityRegex.Match(line) is { Success: true } priority) result.Priorities.Add(new(int.Parse(priority.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(priority.Groups[2].Value, CultureInfo.InvariantCulture)));
            else if (archiveRegex.Match(line) is { Success: true } archive) result.Archives.Add(new(int.Parse(archive.Groups[1].Value, CultureInfo.InvariantCulture), DecodeLlmValue(archive.Groups[2].Value)));
            else if (updateRegex.Match(line) is { Success: true } update) result.Updates.Add(new(int.Parse(update.Groups[1].Value, CultureInfo.InvariantCulture), DecodeLlmValue(update.Groups[2].Value)));
            else { result.SkippedLines.Add(line); continue; }
            recognized.Add(i);
        }
        return result;
    }

    public async Task ApplyExtractionAsync(string username, Companion companion, CompanionInterview interview,
        IEnumerable<CompanionAddProposal> adds, IEnumerable<CompanionPriorityProposal> priorities,
        IEnumerable<CompanionArchiveProposal> archives, IEnumerable<CompanionUpdateProposal> updates)
    {
        var existing = await GetPointsAsync(username, companion.Id, true);
        int added = 0, reprioritized = 0, archived = 0;
        foreach (var proposal in adds)
        {
            if (string.IsNullOrWhiteSpace(proposal.Text)) continue;
            await SavePointAsync(new CompanionDataPoint { Username = username, CompanionId = companion.Id, Text = proposal.Text.Trim(), Priority = proposal.Priority, Source = "interview", InterviewId = interview.Id });
            added++;
        }
        foreach (var proposal in priorities)
        {
            var point = existing.FirstOrDefault(x => x.Id == proposal.PointId); if (point == null) continue;
            point.Priority = proposal.Priority; await SavePointAsync(point); reprioritized++;
        }
        foreach (var proposal in archives)
        {
            var point = existing.FirstOrDefault(x => x.Id == proposal.PointId); if (point == null) continue;
            point.Status = "archived"; await SavePointAsync(point); archived++;
        }
        foreach (var proposal in updates)
        {
            var point = existing.FirstOrDefault(x => x.Id == proposal.PointId); if (point == null || string.IsNullOrWhiteSpace(proposal.Text)) continue;
            point.Text = proposal.Text.Trim(); await SavePointAsync(point);
        }
        interview.PointsAdded += added;
        interview.PointsReprioritized += reprioritized;
        interview.PointsArchived += archived;
        interview.Notes = $"Extraction applied {DateTime.Now:yyyy-MM-dd HH:mm}";
        await SaveInterviewAsync(interview);
    }

    public static string DecodeLlmValue(string value) =>
        value.Replace("\\\"", "\"").Replace("\\\\", "\\").Trim();
}
