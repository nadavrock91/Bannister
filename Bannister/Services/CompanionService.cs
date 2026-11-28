using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Bannister.Models;

namespace Bannister.Services;

public sealed record CompanionAddProposal(
    int Index,
    string Text,
    int Priority,
    string? GameName = null,
    string? GameId = null,
    string? Category = null);
public sealed record CompanionPriorityProposal(int PointId, int Priority);
public sealed record CompanionArchiveProposal(int PointId, string Reason);
public sealed record CompanionUpdateProposal(int PointId, string Text);
public sealed record CompanionApplyResult(int Added, int Reprioritized, int Archived, int Updated, int IdeaLoggingFailures, IReadOnlyList<ExpAwardResult> Awards);
public sealed record CompanionPromptResult(string Prompt, int OmittedPointCount);
public sealed record CompanionRephraseChange(int PointId, string OldText, string NewText);
public sealed record CompanionRephraseParseResult(
    IReadOnlyList<CompanionRephraseChange> Changes,
    IReadOnlyList<int> IdsNotFound,
    IReadOnlyList<int> IdsOutsideScope,
    IReadOnlyList<int> UnchangedIds,
    IReadOnlyList<int> EmptyTextIds,
    IReadOnlyList<string> Warnings,
    int RecognizedAssignmentCount);
public sealed record CompanionRephraseApplyResult(int Updated, int Skipped, int IdeasLogged, int IdeaLoggingFailures);
public sealed record ContextGameMigrationResult(
    Companion Persona,
    int MergedPersonas,
    int ReassignedPoints,
    int ReassignedInterviews,
    bool CreatedPersona);
public sealed record CompanionAvatarState(int Level, byte[]? LevelImage, int ImageFit, int? LevelImageId);
public sealed record CompanionGamePointCount(string GameId, string DisplayName, int PointCount);
public sealed record CompanionStats(
    int ActivePointCount,
    int ArchivedPointCount,
    int CompletedInterviewCount,
    IReadOnlyList<CompanionGamePointCount> GamesCovered,
    int ExpEarnedToday);

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
    private const string InterviewCompleted = "interview_completed";
    private const string DataPointAdded = "data_point_added";
    private const string DataPointRefined = "data_point_refined";

    public static OuterGameRequirements Requirements { get; } = new(
        "context_game", "The Context Game",
        [
            new(InterviewCompleted, "Context Game: Interview completed", 20, "Apply the extraction from a completed interview."),
            new(DataPointAdded, "Context Game: Data point added", 2, "Save a new data point by hand or accept one from an interview extraction."),
            new(DataPointRefined, "Context Game: Data point refined", 1, "Awarded when an interview refines an existing context point (old version archived, new version added).")
        ]);

    private readonly DatabaseService _db;
    private readonly OuterGameService _outerGames;
    private readonly IdeaLoggerService _ideaLogger;
    private readonly GameService _games;
    private readonly ExpService _exp;
    private readonly ExpAwarder _expAwarder;
    public CompanionService(
        DatabaseService db,
        IdeaLoggerService ideaLogger,
        OuterGameService outerGames,
        GameService games,
        ExpService exp,
        ExpAwarder expAwarder)
    {
        _db = db;
        _outerGames = outerGames;
        _ideaLogger = ideaLogger;
        _games = games;
        _exp = exp;
        _expAwarder = expAwarder;
    }

    private async Task<SQLite.ISQLiteAsyncConnection> ConnectionAsync() =>
        await _db.GetConnectionAsync();

    public async Task<List<Companion>> GetCompanionsAsync(string username)
    {
        var conn = await ConnectionAsync();
        return await conn.Table<Companion>().Where(x => x.Username == username)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync();
    }

    public async Task<ContextGameMigrationResult> EnsureSinglePersonaAsync(string username)
    {
        var items = await GetCompanionsAsync(username);
        if (items.Count == 0)
        {
            var created = new Companion
            {
                Username = username,
                Nickname = "Morpheus",
                Scope = "meta",
                InterviewStance = "get to know the user across all of Bannister",
                SortOrder = 0,
                DefaultQuestionCount = 10
            };
            await SaveCompanionAsync(created);
            return new ContextGameMigrationResult(created, 0, 0, 0, true);
        }

        var persona = items.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).First();
        var redundant = items.Where(x => x.Id != persona.Id).ToList();
        int reassignedPoints = 0;
        int reassignedInterviews = 0;
        var conn = await ConnectionAsync();

        foreach (var extra in redundant)
        {
            reassignedPoints += await conn.Table<CompanionDataPoint>()
                .Where(x => x.Username == username && x.CompanionId == extra.Id)
                .CountAsync();
            reassignedInterviews += await conn.Table<CompanionInterview>()
                .Where(x => x.Username == username && x.CompanionId == extra.Id)
                .CountAsync();
        }

        bool personaNeedsUpdate = persona.Scope != "meta" || persona.GameId != null ||
            !string.IsNullOrWhiteSpace(persona.Topic) || persona.SortOrder != 0;
        if (redundant.Count > 0 || personaNeedsUpdate)
        {
            await conn.RunInTransactionAsync(db =>
            {
                foreach (var extra in redundant)
                {
                    db.Execute(
                        "UPDATE companion_data_points SET CompanionId = ? WHERE Username = ? AND CompanionId = ?",
                        persona.Id, username, extra.Id);
                    db.Execute(
                        "UPDATE companion_interviews SET CompanionId = ? WHERE Username = ? AND CompanionId = ?",
                        persona.Id, username, extra.Id);
                    db.Delete(extra);
                }

                persona.Scope = "meta";
                persona.GameId = null;
                persona.Topic = "";
                persona.SortOrder = 0;
                db.Update(persona);
            });
        }

        return new ContextGameMigrationResult(
            persona, redundant.Count, reassignedPoints, reassignedInterviews, false);
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
        foreach (var image in await conn.Table<CompanionLevelImage>().Where(x => x.CompanionId == item.Id).ToListAsync())
            await conn.DeleteAsync(image);
        foreach (var point in await conn.Table<CompanionDataPoint>().Where(x => x.CompanionId == item.Id).ToListAsync())
            await conn.DeleteAsync(point);
        foreach (var interview in await conn.Table<CompanionInterview>().Where(x => x.CompanionId == item.Id).ToListAsync())
            await conn.DeleteAsync(interview);
        await conn.DeleteAsync(item);
    }

    public async Task<Game?> GetContextGameAsync(string username)
    {
        var matches = (await _games.GetGamesAsync(username))
            .Where(game => OuterGameService.NamesMatch(game.DisplayName, Requirements.GameName))
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    public async Task<int> GetContextGameLevelAsync(string username)
    {
        var game = await GetContextGameAsync(username);
        return game == null ? 1 : (await _exp.GetProgressAsync(username, game.GameId)).level;
    }

    public async Task<CompanionStats> GetStatsAsync(string username, int companionId)
    {
        var conn = await ConnectionAsync();
        var points = await conn.Table<CompanionDataPoint>()
            .Where(x => x.Username == username && x.CompanionId == companionId)
            .ToListAsync();
        var interviews = await conn.Table<CompanionInterview>()
            .Where(x => x.Username == username && x.CompanionId == companionId)
            .ToListAsync();
        var activePoints = points.Where(x => x.Status == "active").ToList();
        var gamesById = (await _games.GetGamesAsync(username))
            .ToDictionary(x => x.GameId, StringComparer.OrdinalIgnoreCase);
        var gamesCovered = activePoints
            .Where(x => !string.IsNullOrWhiteSpace(x.GameId))
            .GroupBy(x => x.GameId!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CompanionGamePointCount(
                group.Key,
                gamesById.TryGetValue(group.Key, out var game) ? game.DisplayName : group.Key,
                group.Count()))
            .OrderByDescending(x => x.PointCount)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var contextGame = await GetContextGameAsync(username);
        int expToday = contextGame == null
            ? 0
            : await _db.GetExpEarnedTodayAsync(username, contextGame.GameId);

        return new CompanionStats(
            activePoints.Count,
            points.Count(x => x.Status == "archived"),
            interviews.Count(x => !string.IsNullOrWhiteSpace(x.Notes)),
            gamesCovered,
            expToday);
    }

    public async Task<CompanionAvatarState> GetAvatarStateAsync(string username, Companion companion)
    {
        int level = await GetContextGameLevelAsync(username);
        var image = await (await ConnectionAsync()).Table<CompanionLevelImage>()
            .Where(x => x.Username == username && x.CompanionId == companion.Id && x.Level <= level)
            .OrderByDescending(x => x.Level)
            .FirstOrDefaultAsync();
        return new CompanionAvatarState(
            level,
            image?.ImageBytes,
            ImageFitCycle.Normalize(image?.ImageFit ?? companion.AvatarImageFit),
            image?.Id);
    }

    public async Task<List<CompanionLevelImage>> GetLevelImagesAsync(string username, int companionId) =>
        await (await ConnectionAsync()).Table<CompanionLevelImage>()
            .Where(x => x.Username == username && x.CompanionId == companionId)
            .OrderBy(x => x.Level)
            .ToListAsync();

    public async Task<bool> SaveLevelImageAsync(
        string username,
        int companionId,
        int level,
        byte[] imageBytes,
        bool replaceExisting)
    {
        if (level is < 1 or > ExpEngine.MaxLevel)
            throw new ArgumentOutOfRangeException(nameof(level));
        if (imageBytes.Length == 0)
            throw new ArgumentException("The image is empty.", nameof(imageBytes));

        bool saved = false;
        var conn = await ConnectionAsync();
        await conn.RunInTransactionAsync(db =>
        {
            var existing = db.Table<CompanionLevelImage>().FirstOrDefault(x =>
                x.Username == username && x.CompanionId == companionId && x.Level == level);
            if (existing != null)
            {
                if (!replaceExisting) return;
                existing.ImageBytes = imageBytes;
                existing.ImageFit = ImageFitCycle.DefaultMode;
                existing.CreatedAt = DateTime.UtcNow;
                db.Update(existing);
            }
            else
            {
                db.Insert(new CompanionLevelImage
                {
                    Username = username,
                    CompanionId = companionId,
                    Level = level,
                    ImageBytes = imageBytes,
                    CreatedAt = DateTime.UtcNow
                });
            }
            saved = true;
        });
        return saved;
    }

    public async Task<bool> ChangeLevelImageLevelAsync(
        string username,
        int companionId,
        int imageId,
        int newLevel,
        bool replaceExisting)
    {
        if (newLevel is < 1 or > ExpEngine.MaxLevel)
            throw new ArgumentOutOfRangeException(nameof(newLevel));

        bool changed = false;
        var conn = await ConnectionAsync();
        await conn.RunInTransactionAsync(db =>
        {
            var image = db.Find<CompanionLevelImage>(imageId);
            if (image == null || image.Username != username || image.CompanionId != companionId)
                return;
            if (image.Level == newLevel)
            {
                changed = true;
                return;
            }

            var collision = db.Table<CompanionLevelImage>().FirstOrDefault(x =>
                x.Username == username && x.CompanionId == companionId && x.Level == newLevel);
            if (collision != null)
            {
                if (!replaceExisting) return;
                db.Delete(collision);
            }
            image.Level = newLevel;
            db.Update(image);
            changed = true;
        });
        return changed;
    }

    public async Task DeleteLevelImageAsync(string username, int companionId, int imageId)
    {
        var conn = await ConnectionAsync();
        await conn.RunInTransactionAsync(db =>
        {
            var image = db.Find<CompanionLevelImage>(imageId);
            if (image != null && image.Username == username && image.CompanionId == companionId)
                db.Delete(image);
        });
    }

    public async Task<bool> UpdateLevelImageFitAsync(
        string username,
        int companionId,
        int imageId,
        int imageFit)
    {
        bool updated = false;
        var conn = await ConnectionAsync();
        await conn.RunInTransactionAsync(db =>
        {
            var image = db.Find<CompanionLevelImage>(imageId);
            if (image == null || image.Username != username || image.CompanionId != companionId)
                return;
            image.ImageFit = ImageFitCycle.Normalize(imageFit);
            db.Update(image);
            updated = true;
        });
        return updated;
    }

    public async Task<List<CompanionDataPoint>> GetPointsAsync(string username, int companionId, bool includeArchived = false)
    {
        var conn = await ConnectionAsync();
        var items = await conn.Table<CompanionDataPoint>()
            .Where(x => x.Username == username && x.CompanionId == companionId).ToListAsync();
        return items.Where(x => includeArchived || x.Status == "active")
            .OrderByDescending(x => x.Priority).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<List<string>> GetPointCategoriesAsync(string username)
    {
        var conn = await ConnectionAsync();
        var points = await conn.Table<CompanionDataPoint>()
            .Where(x => x.Username == username)
            .ToListAsync();
        return points.Select(x => x.Category)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task SavePointAsync(CompanionDataPoint point)
    {
        point.Priority = Math.Clamp(point.Priority, 1, 5);
        if (point.VisibilityOverride.HasValue && point.VisibilityOverride.Value is not (0 or 1 or 2))
            point.VisibilityOverride = null;
        point.UpdatedAt = DateTime.UtcNow;
        var conn = await ConnectionAsync();
        if (point.Id == 0) await conn.InsertAsync(point); else await conn.UpdateAsync(point);
    }

    public static int GetEffectiveVisibility(
        CompanionDataPoint point,
        IReadOnlyDictionary<string, Game> gamesById)
    {
        if (point.VisibilityOverride.HasValue)
            return point.VisibilityOverride.Value;
        if (!string.IsNullOrWhiteSpace(point.GameId) &&
            gamesById.TryGetValue(point.GameId, out var game))
            return game.GameVisibility;
        return 2;
    }

    public static bool IsVisibleInDisplayMode(
        CompanionDataPoint point,
        IReadOnlyDictionary<string, Game> gamesById,
        ActivityDisplayMode displayMode)
    {
        int visibility = GetEffectiveVisibility(point, gamesById);
        return displayMode switch
        {
            ActivityDisplayMode.PublicOnly => visibility is 1 or 2,
            ActivityDisplayMode.PrivateOnly => visibility is 0 or 2,
            _ => true
        };
    }

    public async Task<(int IdeaLoggingFailures, ExpAwardResult Award)> SaveManualPointAsync(
        string username,
        Companion companion,
        string scopeLabel,
        string text,
        int priority,
        string? gameId,
        string? category,
        int? visibilityOverride)
    {
        var point = new CompanionDataPoint
        {
            Username = username,
            CompanionId = companion.Id,
            Text = text.Trim(),
            Priority = priority,
            Source = "manual",
            GameId = gameId,
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            VisibilityOverride = visibilityOverride
        };
        await SavePointAsync(point);
        int ideaLoggingFailures;
        try
        {
            ideaLoggingFailures = (await LogNewContextPointsAsync(username, companion, scopeLabel, new[] { point }, null)).Failures;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[COMPANIONS] Failed to log manual context point {point.Id}: {ex}");
            ideaLoggingFailures = 1;
        }

        var award = await _outerGames.AwardAsync(username, Requirements, DataPointAdded);
        return (ideaLoggingFailures, award);
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
        if (interview.Id == 0)
        {
            await conn.InsertAsync(interview);
            return;
        }

        await conn.RunInTransactionAsync(db =>
        {
            // A screen can still hold a copy from before the award was committed.
            var stored = db.Find<CompanionInterview>(interview.Id);
            if (stored == null) return;
            interview.ExpAwarded = stored.ExpAwarded;
            db.Update(interview);
        });
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
        if (interview.IdeaId.HasValue)
            return category;

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

        int ideaId = await _ideaLogger.LogIdeaAsync(request, IdeaLogMode.Silent)
            ?? throw new InvalidOperationException("Silent idea logging did not return an idea Id.");
        interview.IdeaId = ideaId;
        await SaveInterviewAsync(interview);

        return category;
    }

    private static string BuildInterviewIdeaBody(Companion companion, CompanionInterview interview, string scopeLabel, DateTime startedAt)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Context Game persona: {companion.Nickname}");
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

    public async Task<CompanionPromptResult> BuildInterviewPromptAsync(
        string username,
        Companion companion,
        int questionCount,
        string scopeLabel,
        IReadOnlyList<Game> games,
        ActivityDisplayMode displayMode)
    {
        var gamesById = games.ToDictionary(x => x.GameId, StringComparer.OrdinalIgnoreCase);
        int omitted = 0;
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
        sb.AppendLine("WHAT THE USER HAS TOLD YOU ABOUT THEMSELVES (in their own words):");
        var allPoints = await GetPointsAsync(username, companion.Id);
        var points = allPoints.Where(x => IsVisibleInDisplayMode(x, gamesById, displayMode)).ToList();
        omitted = allPoints.Count - points.Count;
        if (points.Count == 0) sb.AppendLine("- Nothing yet.");
        foreach (var point in points) sb.AppendLine($"- (Priority {point.Priority}) {point.Text}");
        sb.AppendLine();
        sb.AppendLine("Begin the interview now with your first question.");
        return new CompanionPromptResult(sb.ToString(), omitted);
    }

    public static string BuildExtractionPrompt(
        Companion companion,
        CompanionInterview interview,
        IReadOnlyList<CompanionDataPoint> points,
        IReadOnlyList<Game> games)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are extracting durable knowledge for the Bannister Context Game persona {companion.Nickname} from an interview transcript.");
        sb.AppendLine("Extract only useful facts, preferences, motivations, constraints, patterns, goals, or corrections. Avoid duplicates and temporary conversational details.");
        sb.AppendLine();
        sb.AppendLine("FIRST-PERSON WRITING RULES:");
        sb.AppendLine("Write every addPoint and updatePoint text in the first person, as the user would say it about themselves (for example, \"I want...\", \"I go to Tel Aviv on Sundays...\", or \"I feel a gap between how I see myself and how others treat me\").");
        sb.AppendLine("Never use \"he\", \"she\", \"the user\", or the user's name in a point.");
        sb.AppendLine("Keep each point a single, self-contained statement.");
        sb.AppendLine("Rewrite example: \"He wants to become more confident in social situations.\" becomes \"I want to become more confident in social situations.\"");
        sb.AppendLine();
        sb.AppendLine("CURRENT DATA POINTS:");
        if (points.Count == 0) sb.AppendLine("- None.");
        var gamesById = games.ToDictionary(x => x.GameId, StringComparer.OrdinalIgnoreCase);
        foreach (var point in points)
        {
            string gameName = !string.IsNullOrWhiteSpace(point.GameId) && gamesById.TryGetValue(point.GameId, out var game)
                ? game.DisplayName
                : "No game";
            string category = string.IsNullOrWhiteSpace(point.Category) ? "No category" : point.Category;
            sb.AppendLine($"[ID:{point.Id}] [Priority:{point.Priority}] [Game:{gameName}] [Category:{category}] {point.Text}");
        }
        sb.AppendLine();
        sb.AppendLine("AVAILABLE GAMES (use these names exactly):");
        if (games.Count == 0) sb.AppendLine("- None.");
        foreach (var game in games) sb.AppendLine($"- {game.DisplayName}");
        sb.AppendLine();
        sb.AppendLine("INTERVIEW TRANSCRIPT:");
        sb.AppendLine(interview.Transcript);
        sb.AppendLine();
        sb.AppendLine("Return ONLY applicable lines in exactly these formats:");
        sb.AppendLine("addPoint[1] = \"text\"; addPriority[1] = 4;");
        sb.AppendLine("addGame[1] = \"game name\";");
        sb.AppendLine("addCategory[1] = \"free-text category\";");
        sb.AppendLine("setPriority[<existing id>] = 2;");
        sb.AppendLine("archivePoint[<existing id>] = \"reason\";");
        sb.AppendLine("updatePoint[<existing id>] = \"corrected text\";");
        sb.AppendLine("addGame and addCategory are optional and use the same index as addPoint. Use priorities 1-5, where 5 is most important. Number added points consecutively from 1. Do not include explanations or Markdown.");
        return sb.ToString();
    }

    public static string BuildRephrasePrompt(
        Companion companion,
        IReadOnlyList<CompanionDataPoint> points,
        string instruction)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Rephrase these Bannister Context Game data points.");
        sb.AppendLine($"Context Game persona: {companion.Nickname}");
        sb.AppendLine($"Instruction: {instruction.Trim()}");
        sb.AppendLine();
        sb.AppendLine("Apply the instruction to the text only. Keep each point's meaning unless the instruction explicitly asks you to change it.");
        sb.AppendLine("Do not add, drop, merge, split, or reorder ids. Return exactly one entry for every id, in the same order.");
        sb.AppendLine("Keep every id exactly as written. Do not change any game, category, visibility, priority, status, source, or date.");
        sb.AppendLine();
        sb.AppendLine("POINTS:");
        foreach (var point in points)
            sb.AppendLine($"[ID:{point.Id}] {point.Text}");
        sb.AppendLine();
        sb.AppendLine("Return only this strict block, with one line per id and no other text:");
        sb.AppendLine("BEGIN COMPANION REPHRASE");
        sb.AppendLine("point[<id>] = \"new text\";");
        sb.AppendLine("END COMPANION REPHRASE");
        sb.AppendLine("Escape any quotation mark inside the text as \\\".");
        return sb.ToString();
    }

    public static CompanionRephraseParseResult ParseRephraseResult(
        string response,
        IReadOnlyList<CompanionDataPoint> companionPoints,
        IReadOnlyCollection<int> exportedScopeIds)
    {
        var byId = companionPoints.ToDictionary(x => x.Id);
        var scope = exportedScopeIds.ToHashSet();
        var values = new Dictionary<int, string>();
        var warnings = new List<string>();
        var notFound = new HashSet<int>();
        var outsideScope = new HashSet<int>();
        var unchanged = new HashSet<int>();
        var empty = new HashSet<int>();
        var assignmentRegex = new Regex(
            """(?im)^\s*(?:point|updatePoint)\s*\[\s*(\d+)\s*\]\s*(?:=|:)\s*(?:"((?:\\.|[^"\\])*)"|'([^'\r\n]*)'|([^;\r\n]*?))\s*;?\s*$""",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        string normalized = TextParsing.NormalizeLineEndings(response);
        int recognized = 0;
        foreach (Match match in assignmentRegex.Matches(normalized))
        {
            recognized++;
            int id = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            string raw = match.Groups[2].Success ? match.Groups[2].Value :
                match.Groups[3].Success ? match.Groups[3].Value : match.Groups[4].Value;
            string text = DecodeLlmValue(raw).Trim();
            if (!values.TryAdd(id, text))
                warnings.Add($"Point {id} appeared more than once; the first entry was kept.");
        }

        var changes = new List<CompanionRephraseChange>();
        foreach (var (id, text) in values)
        {
            if (!byId.TryGetValue(id, out var point)) { notFound.Add(id); continue; }
            if (!scope.Contains(id)) { outsideScope.Add(id); continue; }
            if (string.IsNullOrWhiteSpace(text)) { empty.Add(id); continue; }
            if (string.Equals(point.Text.Trim(), text, StringComparison.Ordinal)) { unchanged.Add(id); continue; }
            changes.Add(new(id, point.Text, text));
        }
        foreach (int id in scope)
            if (!values.ContainsKey(id)) unchanged.Add(id);

        return new(
            changes.OrderBy(x => pointsOrder(exportedScopeIds, x.PointId)).ToList(),
            notFound.OrderBy(x => x).ToList(),
            outsideScope.OrderBy(x => x).ToList(),
            unchanged.OrderBy(x => x).ToList(),
            empty.OrderBy(x => x).ToList(),
            warnings,
            recognized);

        static int pointsOrder(IReadOnlyCollection<int> ids, int id)
        {
            int index = 0;
            foreach (int candidate in ids)
            {
                if (candidate == id) return index;
                index++;
            }
            return int.MaxValue;
        }
    }

    public static CompanionExtraction ParseExtraction(string response)
    {
        var result = new CompanionExtraction();
        var normalized = response.ReplaceLineEndings("\n").Replace("\"\"", "\"");
        var recognizedSpans = new List<(int Start, int Length)>();
        var addRegex = new Regex("addPoint\\[(\\d+)\\]\\s*=\\s*\"((?:\\\\.|[^\"\\\\])*)\"\\s*;", RegexOptions.IgnoreCase);
        var addPriorityRegex = new Regex("addPriority\\[(\\d+)\\]\\s*=\\s*([1-5])\\s*;", RegexOptions.IgnoreCase);
        var addGameRegex = new Regex("addGame\\[(\\d+)\\]\\s*=\\s*\"((?:\\\\.|[^\"\\\\])*)\"\\s*;", RegexOptions.IgnoreCase);
        var addCategoryRegex = new Regex("addCategory\\[(\\d+)\\]\\s*=\\s*\"((?:\\\\.|[^\"\\\\])*)\"\\s*;", RegexOptions.IgnoreCase);
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

        var addGames = ParseIndexedTextValues(normalized, addGameRegex, "addGame", recognizedSpans, result.Warnings);
        var addCategories = ParseIndexedTextValues(normalized, addCategoryRegex, "addCategory", recognizedSpans, result.Warnings);

        foreach (var point in addPoints)
        {
            int priority = 3;
            if (!addPriorities.TryGetValue(point.Index, out priority))
            {
                priority = 3;
                result.Warnings.Add($"Point {point.Index} has no addPriority[{point.Index}]; using priority 3");
            }
            addGames.TryGetValue(point.Index, out string? gameName);
            addCategories.TryGetValue(point.Index, out string? category);
            result.Adds.Add(new(point.Index, point.Text, priority, gameName, null, category));
        }

        foreach (var priority in addPriorities)
            if (!addIndexes.Contains(priority.Key))
                result.Warnings.Add($"addPriority[{priority.Key}] has no matching addPoint[{priority.Key}]");
        foreach (var game in addGames)
            if (!addIndexes.Contains(game.Key))
                result.Warnings.Add($"addGame[{game.Key}] has no matching addPoint[{game.Key}]");
        foreach (var category in addCategories)
            if (!addIndexes.Contains(category.Key))
                result.Warnings.Add($"addCategory[{category.Key}] has no matching addPoint[{category.Key}]");

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

    private static Dictionary<int, string> ParseIndexedTextValues(
        string response,
        Regex regex,
        string fieldName,
        List<(int Start, int Length)> recognizedSpans,
        List<string> warnings)
    {
        var values = new Dictionary<int, string>();
        foreach (Match match in regex.Matches(response))
        {
            recognizedSpans.Add((match.Index, match.Length));
            int index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            string value = DecodeLlmValue(match.Groups[2].Value);
            if (!values.TryAdd(index, value))
                warnings.Add($"Duplicate {fieldName}[{index}]; using the first value");
        }
        return values;
    }

    public static void ResolveExtractionGames(CompanionExtraction extraction, IReadOnlyList<Game> games)
    {
        for (int i = 0; i < extraction.Adds.Count; i++)
        {
            var add = extraction.Adds[i];
            if (string.IsNullOrWhiteSpace(add.GameName)) continue;
            var game = games.FirstOrDefault(x =>
                string.Equals(x.DisplayName.Trim(), add.GameName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (game == null)
            {
                extraction.Warnings.Add($"Point {add.Index} names unknown game '{add.GameName}'; leaving game empty");
                continue;
            }
            extraction.Adds[i] = add with { GameId = game.GameId, GameName = game.DisplayName };
        }
    }

    public async Task<CompanionApplyResult> ApplyExtractionAsync(string username, Companion companion, CompanionInterview? interview,
        string scopeLabel,
        IEnumerable<CompanionAddProposal> adds, IEnumerable<CompanionPriorityProposal> priorities,
        IEnumerable<CompanionArchiveProposal> archives, IEnumerable<CompanionUpdateProposal> updates)
    {
        var existing = await GetPointsAsync(username, companion.Id, true);
        var priorityProposals = priorities.ToList();
        var updateProposals = updates.ToList();
        var replacementIds = updateProposals
            .Where(x => !string.IsNullOrWhiteSpace(x.Text) && existing.Any(point => point.Id == x.PointId))
            .Select(x => x.PointId)
            .ToHashSet();
        int added = 0, reprioritized = 0, archived = 0, updated = 0;
        var addedPoints = new List<CompanionDataPoint>();
        var replacementPoints = new List<CompanionDataPoint>();
        var archivedPointIds = new HashSet<int>();
        foreach (var proposal in adds)
        {
            if (string.IsNullOrWhiteSpace(proposal.Text)) continue;
            var point = new CompanionDataPoint
            {
                Username = username,
                CompanionId = companion.Id,
                Text = proposal.Text.Trim(),
                Priority = proposal.Priority,
                Source = interview == null ? "manual" : "interview",
                InterviewId = interview?.Id,
                GameId = proposal.GameId,
                Category = string.IsNullOrWhiteSpace(proposal.Category) ? null : proposal.Category.Trim()
            };
            await SavePointAsync(point);
            addedPoints.Add(point);
            added++;
        }
        foreach (var proposal in priorityProposals)
        {
            var point = existing.FirstOrDefault(x => x.Id == proposal.PointId); if (point == null) continue;
            if (replacementIds.Contains(point.Id)) continue;
            point.Priority = proposal.Priority; await SavePointAsync(point); reprioritized++;
        }
        foreach (var proposal in archives)
        {
            var point = existing.FirstOrDefault(x => x.Id == proposal.PointId); if (point == null) continue;
            point.Status = "archived"; await SavePointAsync(point);
            archivedPointIds.Add(point.Id);
        }
        var replacedPointIds = new HashSet<int>();
        var conn = await ConnectionAsync();
        foreach (var proposal in updateProposals)
        {
            if (!replacedPointIds.Add(proposal.PointId) || string.IsNullOrWhiteSpace(proposal.Text)) continue;
            int? replacementPriority = priorityProposals.LastOrDefault(x => x.PointId == proposal.PointId)?.Priority;
            CompanionDataPoint? replacement = null;
            await conn.RunInTransactionAsync(db =>
            {
                var oldPoint = db.Find<CompanionDataPoint>(proposal.PointId);
                if (oldPoint == null || oldPoint.Username != username || oldPoint.CompanionId != companion.Id ||
                    oldPoint.ReplacedByPointId.HasValue)
                    return;

                var now = DateTime.UtcNow;
                replacement = new CompanionDataPoint
                {
                    Username = username,
                    CompanionId = companion.Id,
                    Text = proposal.Text.Trim(),
                    Priority = replacementPriority ?? oldPoint.Priority,
                    Status = "active",
                    Source = interview == null ? "manual" : "interview",
                    InterviewId = interview?.Id,
                    GameId = oldPoint.GameId,
                    Category = oldPoint.Category,
                    VisibilityOverride = oldPoint.VisibilityOverride,
                    ReplacesPointId = oldPoint.Id,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.Insert(replacement);

                oldPoint.Status = "archived";
                oldPoint.ReplacedByPointId = replacement.Id;
                oldPoint.UpdatedAt = now;
                db.Update(oldPoint);
            });
            if (replacement == null) continue;

            replacementPoints.Add(replacement);
            archivedPointIds.Add(proposal.PointId);
            if (replacementPriority.HasValue) reprioritized++;
            updated++;
        }
        archived = archivedPointIds.Count;
        if (interview != null)
        {
            interview.PointsAdded += added;
            interview.PointsReprioritized += reprioritized;
            interview.PointsArchived += archived;
            interview.Notes = $"Extraction applied {DateTime.Now:yyyy-MM-dd HH:mm}";
            await SaveInterviewAsync(interview);
        }

        int ideaLoggingFailures = 0;
        try
        {
            ideaLoggingFailures += (await LogNewContextPointsAsync(
                username, companion, scopeLabel, addedPoints.Concat(replacementPoints).ToList(), interview)).Failures;
        }
        catch (Exception ex)
        {
            ideaLoggingFailures += addedPoints.Count + replacementPoints.Count;
            System.Diagnostics.Debug.WriteLine($"[COMPANIONS] Failed to batch-log context points: {ex}");
        }

        var awards = new List<ExpAwardResult>();
        await using (_expAwarder.BeginBatch())
        {
            if (interview is { ExpAwarded: false })
            {
                var award = await _outerGames.AwardAsync(username, Requirements, InterviewCompleted, db =>
                {
                    int changed = db.Execute(
                        "UPDATE companion_interviews SET ExpAwarded = 1 WHERE Id = ? AND Username = ? AND ExpAwarded = 0",
                        interview.Id, username);
                    if (changed == 0 && db.Find<CompanionInterview>(interview.Id) is not { ExpAwarded: true })
                        throw new InvalidOperationException("The interview could not be marked as awarded.");
                    return changed != 0;
                });
                awards.Add(award);
            }
            foreach (var _ in addedPoints)
                awards.Add(await _outerGames.AwardAsync(username, Requirements, DataPointAdded));
            foreach (var _ in replacementPoints)
                awards.Add(await _outerGames.AwardAsync(username, Requirements, DataPointRefined));
        }

        return new CompanionApplyResult(added, reprioritized, archived, updated, ideaLoggingFailures, awards);
    }

    public async Task<CompanionRephraseApplyResult> ApplyRephraseAsync(
        string username,
        Companion companion,
        string scopeLabel,
        IReadOnlyCollection<int> exportedScopeIds,
        IEnumerable<CompanionRephraseChange> changes)
    {
        var allowed = exportedScopeIds.ToHashSet();
        var current = await GetPointsAsync(username, companion.Id, includeArchived: true);
        var byId = current.ToDictionary(x => x.Id);
        int updated = 0, skipped = 0;
        var updatedPoints = new List<CompanionDataPoint>();
        var conn = await ConnectionAsync();

        foreach (var change in changes)
        {
            if (!allowed.Contains(change.PointId) ||
                !byId.TryGetValue(change.PointId, out var point) ||
                string.IsNullOrWhiteSpace(change.NewText))
            {
                skipped++;
                continue;
            }

            string newText = change.NewText.Trim();
            if (string.Equals(point.Text, newText, StringComparison.Ordinal))
            {
                skipped++;
                continue;
            }

            // This path intentionally bypasses SavePointAsync: rephrasing changes only Text,
            // preserving UpdatedAt and every classification/source field. The old linked Ideas
            // item remains untouched; successful logging below replaces only this point's link.
            point.Text = newText;
            await conn.UpdateAsync(point);
            updatedPoints.Add(point);
            updated++;
        }

        int ideasLogged = 0, ideaLoggingFailures = 0;
        try
        {
            var ideaResult = await LogNewContextPointsAsync(
                username, companion, scopeLabel, updatedPoints, null, ideaCreatedAt: DateTime.Now);
            ideasLogged = ideaResult.Logged;
            ideaLoggingFailures = ideaResult.Failures;
        }
        catch (Exception ex)
        {
            ideaLoggingFailures = updatedPoints.Count;
            System.Diagnostics.Debug.WriteLine($"[COMPANIONS] Failed to batch-log rephrased context points: {ex}");
        }

        return new(updated, skipped, ideasLogged, ideaLoggingFailures);
    }

    private sealed record ContextPointIdeaLogResult(int Logged, int Failures);

    private async Task<ContextPointIdeaLogResult> LogNewContextPointsAsync(
        string username,
        Companion companion,
        string scopeLabel,
        IReadOnlyList<CompanionDataPoint> points,
        CompanionInterview? interview,
        DateTime? ideaCreatedAt = null)
    {
        if (points.Count == 0) return new(0, 0);

        var entries = new List<(CompanionDataPoint Point, IdeaLogRequest Request)>();
        foreach (var point in points)
        {
            string sourceLine = await GetContextPointSourceLineAsync(point, interview);
            entries.Add((point, BuildContextPointIdeaRequest(
                username, companion, scopeLabel, point, sourceLine,
                ideaCreatedAt ?? point.CreatedAt.ToLocalTime())));
        }

        var batch = await _ideaLogger.LogBatchSilentAsync(entries.Select(x => x.Request));
        int failures = batch.Failures.Count;
        var conn = await ConnectionAsync();
        foreach (var success in batch.Successes)
        {
            if (!success.IdeaId.HasValue) continue;
            var entry = entries.First(x => ReferenceEquals(x.Request, success.Request));
            try
            {
                entry.Point.IdeaId = success.IdeaId.Value;
                await conn.UpdateAsync(entry.Point);
            }
            catch (Exception ex)
            {
                failures++;
                System.Diagnostics.Debug.WriteLine($"[COMPANIONS] Failed to link context point {entry.Point.Id} to idea {success.IdeaId.Value}: {ex}");
            }
        }
        return new(batch.SavedCount, failures);
    }

    private async Task<string> GetContextPointSourceLineAsync(
        CompanionDataPoint point,
        CompanionInterview? knownInterview)
    {
        CompanionInterview? sourceInterview = knownInterview?.Id == point.InterviewId
            ? knownInterview
            : null;
        if (sourceInterview == null && point.InterviewId.HasValue)
        {
            sourceInterview = await (await ConnectionAsync()).Table<CompanionInterview>()
                .Where(x => x.Id == point.InterviewId.Value && x.Username == point.Username)
                .FirstOrDefaultAsync();
        }

        if (sourceInterview != null)
            return $"From interview: {sourceInterview.StartedAt.ToLocalTime():yyyy-MM-dd}";
        if (string.Equals(point.Source, "interview", StringComparison.OrdinalIgnoreCase))
            return $"From interview: {point.CreatedAt.ToLocalTime():yyyy-MM-dd}";
        return "From: manual";
    }

    private static IdeaLogRequest BuildContextPointIdeaRequest(
        string username,
        Companion companion,
        string scopeLabel,
        CompanionDataPoint point,
        string sourceLine,
        DateTime createdAt)
    {
        string text = point.Text.Trim();
        string title = text.Length <= 80 ? text : text[..79].TrimEnd() + "…";
        string body = $"{text}\n\nPriority: {point.Priority}\nContext Game persona: {companion.Nickname} ({scopeLabel})\n{sourceLine}";
        return new IdeaLogRequest
        {
            Username = username,
            Title = title,
            Body = body,
            Category = IdeasService.CompanionContextPointCategory,
            Subcategory = companion.Nickname,
            Rating = point.Priority * 20,
            CreatedAt = createdAt,
            IsLlmSource = true
        };
    }

    public static string DecodeLlmValue(string value) =>
        value.Replace("\\\"", "\"").Replace("\\\\", "\\").Trim();
}
