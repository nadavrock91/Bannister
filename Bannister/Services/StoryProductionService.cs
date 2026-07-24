using Bannister.Models;
using SQLite;

namespace Bannister.Services;

public class StoryProductionService
{
    private readonly DatabaseService _db;

    public StoryProductionService(DatabaseService db)
    {
        _db = db;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private void EnsureWritable()
    {
        if (_db.IsReadOnly)
            throw new ReadOnlyDatabaseException("Story Production is read-only on secondary devices.");
    }

    #region Projects

    private async Task EnsureProjectTableAsync(ISQLiteAsyncConnection conn)
    {
        if (_db.IsReadOnly) return;

        await conn.CreateTableAsync<StoryProject>();
        
        // Migrate: add publication columns if they don't exist
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN IsPublished INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN PublishedAt TEXT"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN ProjectedClipCount INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN ProjectedDays INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN FinalClipCount INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN LlmConversationLog TEXT DEFAULT ''"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN ProjectCategory TEXT DEFAULT ''"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN YouTubeViews INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN YouTubeLikes INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN YouTubeComments INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN YouTubeAverageViewDurationSeconds INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN YouTubeShares INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN YouTubeNewFollowers INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN YouTubeStatsCapturedAt TEXT"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN FacebookViews INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN FacebookLikes INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN FacebookComments INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN FacebookAverageViewDurationSeconds INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN FacebookThreeSecondViews INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN FacebookOneMinuteViews INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN FacebookShares INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN FacebookNewFollowers INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN FacebookStatsCapturedAt TEXT"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN TikTokViews INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN TikTokLikes INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN TikTokComments INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN TikTokAverageWatchTimeSeconds INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN TikTokPercentWatchedFullVideo REAL DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN TikTokShares INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN TikTokNewFollowers INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN TikTokStatsCapturedAt TEXT"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN IsProduced INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN ProducedAt TEXT"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN StatsSourceDraftProjectId INTEGER"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE story_projects ADD COLUMN WritingProcess TEXT DEFAULT ''"); } catch { }
        try { await conn.CreateTableAsync<WritingProcessDefinition>(); } catch { }
    }

    private async Task BackfillProducedStateAsync(ISQLiteAsyncConnection conn, string username)
    {
        if (_db.IsReadOnly || string.IsNullOrWhiteSpace(username)) return;

        string markerKey = $"bannister_isproduced_backfill_done_{username}";
        try
        {
            var done = await SecureStorage.GetAsync(markerKey);
            if (done == "true") return;

            var projects = await conn.Table<StoryProject>()
                .Where(p => p.Username == username && p.IsPublished && !p.IsProduced)
                .ToListAsync();

            foreach (var project in projects)
            {
                project.IsProduced = true;
                project.ProducedAt = project.PublishedAt ?? project.CreatedAt;
                await conn.UpdateAsync(project);
            }

            await SecureStorage.SetAsync(markerKey, "true");
            System.Diagnostics.Debug.WriteLine($"[STORY] Backfilled IsProduced for {projects.Count} published projects.");
        }
        catch
        {
        }
    }

    public async Task<List<StoryProject>> GetProjectsAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        await EnsureProjectTableAsync(conn);
        await BackfillProducedStateAsync(conn, username);
        
        return await conn.Table<StoryProject>()
            .Where(p => p.Username == username)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<StoryProject>> GetActiveProjectsAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        await EnsureProjectTableAsync(conn);
        await BackfillProducedStateAsync(conn, username);
        
        return await conn.Table<StoryProject>()
            .Where(p => p.Username == username && p.Status == "active")
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<StoryProject?> GetProjectByIdAsync(int projectId)
    {
        var conn = await _db.GetConnectionAsync();
        await EnsureProjectTableAsync(conn);
        
        return await conn.Table<StoryProject>()
            .Where(p => p.Id == projectId)
            .FirstOrDefaultAsync();
    }

    public async Task<StoryProject> CreateProjectAsync(string username, string name, string description = "", string writingProcess = "")
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        await EnsureProjectTableAsync(conn);
        
        var project = new StoryProject
        {
            Username = username,
            Name = name,
            Description = description,
            WritingProcess = writingProcess,
            CreatedAt = DateTime.UtcNow,
            Status = "active"
        };
        
        await conn.InsertAsync(project);
        System.Diagnostics.Debug.WriteLine($"[STORY] Created project: {name} (process: {writingProcess})");
        
        return project;
    }

    #region Writing Process Definitions

    public async Task<List<WritingProcessDefinition>> GetWritingProcessesAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        await EnsureProjectTableAsync(conn);
        return await conn.Table<WritingProcessDefinition>()
            .Where(p => p.Username == username)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<WritingProcessDefinition> AddWritingProcessAsync(string username, string name)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        await EnsureProjectTableAsync(conn);

        var process = new WritingProcessDefinition
        {
            Username = username,
            Name = name.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        await conn.InsertAsync(process);
        return process;
    }

    public async Task<bool> DeleteWritingProcessAsync(int id)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        var rows = await conn.DeleteAsync<WritingProcessDefinition>(id);
        return rows > 0;
    }

    public async Task<List<string>> GetWritingProcessNamesAsync(string username)
    {
        var processes = await GetWritingProcessesAsync(username);
        return processes.Select(p => p.Name).ToList();
    }

    #endregion

    public async Task UpdateProjectAsync(StoryProject project)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(project);
    }

    public async Task DeleteProjectAsync(int projectId)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        
        // Delete related rows first
        await conn.ExecuteAsync("DELETE FROM story_lines WHERE ProjectId = ?", projectId);
        try { await conn.ExecuteAsync("DELETE FROM story_points WHERE ProjectId = ?", projectId); } catch { }
        try { await conn.ExecuteAsync("DELETE FROM story_point_versions WHERE ProjectId = ?", projectId); } catch { }
        try { await conn.ExecuteAsync("DELETE FROM production_task_logs WHERE ProjectId = ?", projectId); } catch { }
        
        // Delete project
        await conn.DeleteAsync<StoryProject>(projectId);
        
        System.Diagnostics.Debug.WriteLine($"[STORY] Deleted project ID: {projectId}");
    }

    public async Task CompleteProjectAsync(int projectId)
    {
        EnsureWritable();
        var project = await GetProjectByIdAsync(projectId);
        if (project != null)
        {
            project.Status = "completed";
            project.CompletedAt = DateTime.UtcNow;
            await UpdateProjectAsync(project);
        }
    }

    /// <summary>
    /// Mark a project as fully produced with final clip count. Producing also implies published.
    /// </summary>
    public async Task MarkProducedAsync(int projectId, int finalClipCount)
    {
        EnsureWritable();
        var project = await GetProjectByIdAsync(projectId);
        if (project != null)
        {
            var now = DateTime.UtcNow;
            if (!project.IsPublished)
            {
                project.IsPublished = true;
                project.PublishedAt = now;
            }
            else if (!project.PublishedAt.HasValue)
            {
                project.PublishedAt = now;
            }

            project.IsProduced = true;
            project.ProducedAt = now;
            project.FinalClipCount = Math.Max(0, finalClipCount);
            project.Status = "completed";
            project.CompletedAt = now;
            await UpdateProjectAsync(project);
            System.Diagnostics.Debug.WriteLine($"[STORY] Produced project {projectId}: {finalClipCount} clips");
        }
    }

    /// <summary>
    /// Set initial projections for a project
    /// </summary>
    public async Task SetProjectionsAsync(int projectId, int projectedClips, int projectedDays)
    {
        EnsureWritable();
        var project = await GetProjectByIdAsync(projectId);
        if (project != null)
        {
            project.ProjectedClipCount = projectedClips;
            project.ProjectedDays = projectedDays;
            await UpdateProjectAsync(project);
        }
    }

    public async Task<bool> SetYouTubeStatsAsync(int projectId, int views, int likes, int comments, int averageViewDurationSeconds, int shares, int newFollowers)
    {
        EnsureWritable();
        var project = await GetProjectByIdAsync(projectId);
        if (project == null) return false;

        project.YouTubeViews = Math.Max(0, views);
        project.YouTubeLikes = Math.Max(0, likes);
        project.YouTubeComments = Math.Max(0, comments);
        project.YouTubeAverageViewDurationSeconds = Math.Max(0, averageViewDurationSeconds);
        project.YouTubeShares = Math.Max(0, shares);
        project.YouTubeNewFollowers = Math.Max(0, newFollowers);
        project.YouTubeStatsCapturedAt = DateTime.UtcNow;

        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(project);
        return true;
    }

    public async Task<bool> ClearYouTubeStatsAsync(int projectId)
    {
        EnsureWritable();
        var project = await GetProjectByIdAsync(projectId);
        if (project == null) return false;

        project.YouTubeViews = 0;
        project.YouTubeLikes = 0;
        project.YouTubeComments = 0;
        project.YouTubeAverageViewDurationSeconds = 0;
        project.YouTubeShares = 0;
        project.YouTubeNewFollowers = 0;
        project.YouTubeStatsCapturedAt = null;

        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(project);
        return true;
    }

    public async Task<bool> SetFacebookStatsAsync(int projectId, int views, int likes, int comments, int averageViewDurationSeconds, int threeSecondViews, int oneMinuteViews, int shares, int newFollowers)
    {
        EnsureWritable();
        var project = await GetProjectByIdAsync(projectId);
        if (project == null) return false;

        project.FacebookViews = Math.Max(0, views);
        project.FacebookLikes = Math.Max(0, likes);
        project.FacebookComments = Math.Max(0, comments);
        project.FacebookAverageViewDurationSeconds = Math.Max(0, averageViewDurationSeconds);
        project.FacebookThreeSecondViews = Math.Max(0, threeSecondViews);
        project.FacebookOneMinuteViews = Math.Max(0, oneMinuteViews);
        project.FacebookShares = Math.Max(0, shares);
        project.FacebookNewFollowers = Math.Max(0, newFollowers);
        project.FacebookStatsCapturedAt = DateTime.UtcNow;

        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(project);
        return true;
    }

    public async Task<bool> ClearFacebookStatsAsync(int projectId)
    {
        EnsureWritable();
        var project = await GetProjectByIdAsync(projectId);
        if (project == null) return false;

        project.FacebookViews = 0;
        project.FacebookLikes = 0;
        project.FacebookComments = 0;
        project.FacebookAverageViewDurationSeconds = 0;
        project.FacebookThreeSecondViews = 0;
        project.FacebookOneMinuteViews = 0;
        project.FacebookShares = 0;
        project.FacebookNewFollowers = 0;
        project.FacebookStatsCapturedAt = null;

        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(project);
        return true;
    }

    public async Task<bool> SetTikTokStatsAsync(int projectId, int views, int likes, int comments, int averageWatchTimeSeconds, double percentWatchedFullVideo, int shares, int newFollowers)
    {
        EnsureWritable();
        var project = await GetProjectByIdAsync(projectId);
        if (project == null) return false;

        project.TikTokViews = Math.Max(0, views);
        project.TikTokLikes = Math.Max(0, likes);
        project.TikTokComments = Math.Max(0, comments);
        project.TikTokAverageWatchTimeSeconds = Math.Max(0, averageWatchTimeSeconds);
        project.TikTokPercentWatchedFullVideo = Math.Clamp(percentWatchedFullVideo, 0.0, 100.0);
        project.TikTokShares = Math.Max(0, shares);
        project.TikTokNewFollowers = Math.Max(0, newFollowers);
        project.TikTokStatsCapturedAt = DateTime.UtcNow;

        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(project);
        return true;
    }

    public async Task<bool> ClearTikTokStatsAsync(int projectId)
    {
        EnsureWritable();
        var project = await GetProjectByIdAsync(projectId);
        if (project == null) return false;

        project.TikTokViews = 0;
        project.TikTokLikes = 0;
        project.TikTokComments = 0;
        project.TikTokAverageWatchTimeSeconds = 0;
        project.TikTokPercentWatchedFullVideo = 0.0;
        project.TikTokShares = 0;
        project.TikTokNewFollowers = 0;
        project.TikTokStatsCapturedAt = null;

        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(project);
        return true;
    }

    public async Task TogglePublishedAsync(int projectId, bool published)
    {
        EnsureWritable();
        var project = await GetProjectByIdAsync(projectId);
        if (project != null)
        {
            project.IsPublished = published;
            if (published && !project.PublishedAt.HasValue)
                project.PublishedAt = DateTime.UtcNow;

            await UpdateProjectAsync(project);
        }
    }

    /// <summary>
    /// Revert only full-production state. Publication history remains intact.
    /// </summary>
    public async Task UnmarkProducedAsync(int projectId)
    {
        EnsureWritable();
        var project = await GetProjectByIdAsync(projectId);
        if (project != null)
        {
            project.IsProduced = false;
            project.ProducedAt = null;
            await UpdateProjectAsync(project);
        }
    }

    public async Task<bool> SetStatsSourceDraftAsync(int projectId, int? statsSourceDraftProjectId)
    {
        EnsureWritable();
        var project = await GetProjectByIdAsync(projectId);
        if (project == null) return false;

        if (statsSourceDraftProjectId.HasValue)
        {
            var target = await GetProjectByIdAsync(statsSourceDraftProjectId.Value);
            if (target == null) return false;

            int projectRootId = project.ParentProjectId ?? project.Id;
            int targetRootId = target.ParentProjectId ?? target.Id;
            if (projectRootId != targetRootId) return false;
        }

        project.StatsSourceDraftProjectId = statsSourceDraftProjectId;
        await UpdateProjectAsync(project);
        return true;
    }

    public async Task<StoryProject?> ResolveStatsSourceDraftAsync(StoryProject project)
    {
        if (project.StatsSourceDraftProjectId.HasValue)
        {
            var selected = await GetProjectByIdAsync(project.StatsSourceDraftProjectId.Value);
            if (selected != null)
            {
                int projectRootId = project.ParentProjectId ?? project.Id;
                int selectedRootId = selected.ParentProjectId ?? selected.Id;
                if (projectRootId == selectedRootId)
                    return selected;
            }
        }

        var drafts = await GetProjectDraftsAsync(project.Id);
        if (drafts.Count == 0) return project;

        var latest = drafts.FirstOrDefault(d => d.IsLatest);
        if (latest != null) return latest;

        return drafts
            .OrderByDescending(d => d.DraftVersion)
            .FirstOrDefault() ?? project;
    }

    #endregion

    #region Lines

    private async Task EnsureStoryLineTableAsync(ISQLiteAsyncConnection conn)
    {
        if (_db.IsReadOnly) return;

        await conn.CreateTableAsync<StoryLine>();
        
        // Migrate: add new columns if they don't exist
        try
        {
            await conn.ExecuteAsync("ALTER TABLE story_lines ADD COLUMN ShotsJson TEXT DEFAULT ''");
        }
        catch { /* Column already exists */ }
        
        try
        {
            await conn.ExecuteAsync("ALTER TABLE story_lines ADD COLUMN ShotCount INTEGER DEFAULT 0");
        }
        catch { /* Column already exists */ }
        
        try
        {
            await conn.ExecuteAsync("ALTER TABLE story_lines ADD COLUMN ImagePrompt TEXT DEFAULT ''");
        }
        catch { /* Column already exists */ }
        
        try
        {
            await conn.ExecuteAsync("ALTER TABLE story_lines ADD COLUMN VideoPrompt TEXT DEFAULT ''");
        }
        catch { /* Column already exists */ }
    }

    public async Task<List<StoryLine>> GetLinesAsync(int projectId)
    {
        var conn = await _db.GetConnectionAsync();
        await EnsureStoryLineTableAsync(conn);
        
        return await conn.Table<StoryLine>()
            .Where(l => l.ProjectId == projectId)
            .OrderBy(l => l.LineOrder)
            .ToListAsync();
    }

    public async Task<StoryLine?> GetLineByIdAsync(int lineId)
    {
        var conn = await _db.GetConnectionAsync();
        await EnsureStoryLineTableAsync(conn);
        
        return await conn.Table<StoryLine>()
            .Where(l => l.Id == lineId)
            .FirstOrDefaultAsync();
    }

    public async Task<StoryLine> AddLineAsync(int projectId, string lineText, string visualDescription = "")
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        await EnsureStoryLineTableAsync(conn);
        
        // Get next order
        var existingLines = await GetLinesAsync(projectId);
        int nextOrder = existingLines.Count > 0 ? existingLines.Max(l => l.LineOrder) + 1 : 1;
        
        var line = new StoryLine
        {
            ProjectId = projectId,
            LineOrder = nextOrder,
            LineText = lineText,
            VisualDescription = visualDescription,
            CreatedAt = DateTime.UtcNow
        };
        
        await conn.InsertAsync(line);
        return line;
    }

    public async Task<StoryLine> InsertLineBeforeAsync(int projectId, int beforeOrder, string lineText, string visualDescription, bool isSilent)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StoryLine>();
        
        // Shift all lines at or after beforeOrder up by 1
        await conn.ExecuteAsync(
            "UPDATE StoryLine SET LineOrder = LineOrder + 1 WHERE ProjectId = ? AND LineOrder >= ?",
            projectId, beforeOrder);
        
        // Insert the new line at the target position
        var line = new StoryLine
        {
            ProjectId = projectId,
            LineOrder = beforeOrder,
            LineText = lineText,
            VisualDescription = visualDescription,
            IsSilent = isSilent,
            CreatedAt = DateTime.UtcNow
        };
        
        await conn.InsertAsync(line);
        System.Diagnostics.Debug.WriteLine($"[STORY] Inserted line at position {beforeOrder}");
        return line;
    }

    public async Task UpdateLineAsync(StoryLine line)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        line.ModifiedAt = DateTime.UtcNow;
        await conn.UpdateAsync(line);
    }

    public async Task DeleteLineAsync(int lineId)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<StoryLine>(lineId);
    }

    public async Task ToggleVisualPreparedAsync(int lineId)
    {
        EnsureWritable();
        var line = await GetLineByIdAsync(lineId);
        if (line != null)
        {
            line.VisualPrepared = !line.VisualPrepared;
            line.ModifiedAt = DateTime.UtcNow;
            await UpdateLineAsync(line);
        }
    }

    public async Task ReorderLinesAsync(int projectId, List<int> lineIds)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        
        for (int i = 0; i < lineIds.Count; i++)
        {
            await conn.ExecuteAsync(
                "UPDATE StoryLine SET LineOrder = ? WHERE Id = ?",
                i + 1, lineIds[i]);
        }
    }

    public async Task MoveLineUpAsync(int lineId)
    {
        EnsureWritable();
        var line = await GetLineByIdAsync(lineId);
        if (line == null || line.LineOrder <= 1) return;
        
        var conn = await _db.GetConnectionAsync();
        
        int targetOrder = line.LineOrder - 1;
        int projectId = line.ProjectId;
        
        var lineAbove = await conn.Table<StoryLine>()
            .Where(l => l.ProjectId == projectId && l.LineOrder == targetOrder)
            .FirstOrDefaultAsync();
        
        if (lineAbove != null)
        {
            lineAbove.LineOrder = line.LineOrder;
            line.LineOrder = targetOrder;
            
            await conn.UpdateAsync(lineAbove);
            await conn.UpdateAsync(line);
        }
    }

    public async Task MoveLineDownAsync(int lineId)
    {
        EnsureWritable();
        var line = await GetLineByIdAsync(lineId);
        if (line == null) return;
        
        var conn = await _db.GetConnectionAsync();
        
        int targetOrder = line.LineOrder + 1;
        int projectId = line.ProjectId;
        
        var lineBelow = await conn.Table<StoryLine>()
            .Where(l => l.ProjectId == projectId && l.LineOrder == targetOrder)
            .FirstOrDefaultAsync();
        
        if (lineBelow != null)
        {
            lineBelow.LineOrder = line.LineOrder;
            line.LineOrder = targetOrder;
            
            await conn.UpdateAsync(lineBelow);
            await conn.UpdateAsync(line);
        }
    }

    #endregion

    #region Stats

    public async Task<(int totalLines, int preparedLines)> GetProjectStatsAsync(int projectId)
    {
        var lines = await GetLinesAsync(projectId);
        int total = lines.Count;
        int prepared = lines.Count(l => l.VisualPrepared);
        return (total, prepared);
    }

    #endregion

    #region Drafts

    /// <summary>
    /// Get all drafts for a project (including the original)
    /// </summary>
    public async Task<List<StoryProject>> GetProjectDraftsAsync(int projectId)
    {
        var conn = await _db.GetConnectionAsync();
        var project = await GetProjectByIdAsync(projectId);
        if (project == null) return new List<StoryProject>();

        // Find the root project ID
        int rootId = project.ParentProjectId ?? project.Id;

        // Get all projects that share this root (original + all drafts)
        var allProjects = await conn.Table<StoryProject>().ToListAsync();
        
        return allProjects
            .Where(p => p.Id == rootId || p.ParentProjectId == rootId)
            .OrderBy(p => p.DraftVersion)
            .ToList();
    }

    /// <summary>
    /// Get the next draft version number for a project
    /// </summary>
    public async Task<int> GetNextDraftVersionAsync(int projectId)
    {
        var drafts = await GetProjectDraftsAsync(projectId);
        return drafts.Count > 0 ? drafts.Max(d => d.DraftVersion) + 1 : 2;
    }

    /// <summary>
    /// Rename a draft
    /// </summary>
    public async Task RenameDraftAsync(int projectId, string newName)
    {
        EnsureWritable();
        var project = await GetProjectByIdAsync(projectId);
        if (project != null)
        {
            project.Name = newName;
            await UpdateProjectAsync(project);
            System.Diagnostics.Debug.WriteLine($"[STORY] Renamed draft ID {projectId} to: {newName}");
        }
    }

    /// <summary>
    /// Delete a draft (not allowed for original projects)
    /// </summary>
    public async Task DeleteDraftAsync(int projectId)
    {
        EnsureWritable();
        var project = await GetProjectByIdAsync(projectId);
        if (project == null) return;
        
        // Don't allow deleting original projects
        if (project.ParentProjectId == null)
        {
            System.Diagnostics.Debug.WriteLine($"[STORY] Cannot delete original project ID {projectId}");
            return;
        }

        var conn = await _db.GetConnectionAsync();
        
        // Delete all lines for this draft
        await conn.ExecuteAsync("DELETE FROM story_lines WHERE ProjectId = ?", projectId);
        
        // Delete the draft project
        await conn.DeleteAsync(project);
        
        System.Diagnostics.Debug.WriteLine($"[STORY] Deleted draft ID {projectId}: {project.Name}");
    }

    /// <summary>
    /// Set a draft as the "latest" for its project family
    /// </summary>
    public async Task SetAsLatestAsync(int projectId)
    {
        EnsureWritable();
        var project = await GetProjectByIdAsync(projectId);
        if (project == null) return;

        var conn = await _db.GetConnectionAsync();
        
        // Find root project
        int rootId = project.ParentProjectId ?? project.Id;
        
        // Clear IsLatest on all drafts in this family
        await conn.ExecuteAsync(
            "UPDATE story_projects SET IsLatest = 0 WHERE Id = ? OR ParentProjectId = ?",
            rootId, rootId);
        
        // Set this one as latest
        project.IsLatest = true;
        await conn.UpdateAsync(project);
        
        System.Diagnostics.Debug.WriteLine($"[STORY] Set draft ID {projectId} as latest");
    }

    /// <summary>
    /// Get the latest draft for a project family, or the original if none marked
    /// </summary>
    public async Task<StoryProject?> GetLatestDraftAsync(int projectId)
    {
        var drafts = await GetProjectDraftsAsync(projectId);
        if (drafts.Count == 0) return null;
        
        // Find the one marked as latest
        var latest = drafts.FirstOrDefault(d => d.IsLatest);
        
        // If none marked, return the original (first one)
        return latest ?? drafts.First();
    }

    /// <summary>
    /// Get the project to compare against (manual override or auto previous version)
    /// </summary>
    public async Task<StoryProject?> GetComparisonProjectAsync(StoryProject? project)
    {
        if (project == null) return null;
        
        // If manual comparison is set, use that
        if (project.CompareToProjectId.HasValue)
        {
            return await GetProjectByIdAsync(project.CompareToProjectId.Value);
        }
        
        // Auto: compare to previous version
        var drafts = await GetProjectDraftsAsync(project.Id);
        int currentIndex = drafts.FindIndex(d => d.Id == project.Id);
        
        // If this is the first version (original), nothing to compare to
        if (currentIndex <= 0) return null;
        
        // Return the previous version
        return drafts[currentIndex - 1];
    }

    /// <summary>
    /// Set manual comparison target for a draft
    /// </summary>
    public async Task SetCompareToAsync(int projectId, int? compareToProjectId)
    {
        EnsureWritable();
        var project = await GetProjectByIdAsync(projectId);
        if (project == null) return;
        
        project.CompareToProjectId = compareToProjectId;
        await UpdateProjectAsync(project);
        
        System.Diagnostics.Debug.WriteLine($"[STORY] Set compare target for {projectId} to {compareToProjectId?.ToString() ?? "auto"}");
    }

    /// <summary>
    /// Get set of line orders that differ between two projects
    /// Uses case-insensitive comparison with 10+ character difference threshold
    /// </summary>
    public async Task<HashSet<int>> GetChangedLineOrdersAsync(int projectId, int compareToProjectId)
    {
        var changed = new HashSet<int>();
        
        var currentLines = await GetLinesAsync(projectId);
        var compareLines = await GetLinesAsync(compareToProjectId);
        
        // Build lookup for compare lines
        var compareLookup = compareLines.ToDictionary(l => l.LineOrder);
        
        foreach (var line in currentLines)
        {
            if (!compareLookup.TryGetValue(line.LineOrder, out var compareLine))
            {
                // New line (doesn't exist in comparison)
                changed.Add(line.LineOrder);
            }
            else
            {
                // Check if difference is significant (10+ chars different, case-insensitive)
                bool scriptChanged = IsSignificantlyDifferent(line.LineText, compareLine.LineText);
                bool visualChanged = IsSignificantlyDifferent(line.VisualDescription, compareLine.VisualDescription);
                bool silentChanged = line.IsSilent != compareLine.IsSilent;
                
                if (scriptChanged || visualChanged || silentChanged)
                {
                    changed.Add(line.LineOrder);
                }
            }
        }
        
        return changed;
    }

    /// <summary>
    /// Check if two strings are significantly different (10+ chars, case-insensitive)
    /// </summary>
    private bool IsSignificantlyDifferent(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return false;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
        {
            // One is empty, other is not - check if non-empty has 10+ chars
            return (a?.Length ?? 0) + (b?.Length ?? 0) >= 10;
        }
        
        // Case-insensitive comparison
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return false;
        
        // Calculate character difference (simple approach: length diff + changed chars)
        string aLower = a.ToLowerInvariant();
        string bLower = b.ToLowerInvariant();
        
        int diffCount = Math.Abs(a.Length - b.Length);
        int minLen = Math.Min(aLower.Length, bLower.Length);
        
        for (int i = 0; i < minLen; i++)
        {
            if (aLower[i] != bLower[i]) diffCount++;
        }
        
        return diffCount >= 10;
    }

    /// <summary>
    /// Get a specific line from a project by line order
    /// </summary>
    public async Task<StoryLine?> GetLineByOrderAsync(int projectId, int lineOrder)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<StoryLine>()
            .Where(l => l.ProjectId == projectId && l.LineOrder == lineOrder)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Copy line content from current to comparison target (to neutralize highlight)
    /// </summary>
    public async Task CopyLineToComparisonAsync(int currentProjectId, int compareToProjectId, int lineOrder)
    {
        EnsureWritable();
        var currentLine = await GetLineByOrderAsync(currentProjectId, lineOrder);
        var compareLine = await GetLineByOrderAsync(compareToProjectId, lineOrder);
        
        if (currentLine == null) return;
        
        var conn = await _db.GetConnectionAsync();
        
        if (compareLine == null)
        {
            // Create new line in comparison project
            var newLine = new StoryLine
            {
                ProjectId = compareToProjectId,
                LineOrder = lineOrder,
                LineText = currentLine.LineText,
                VisualDescription = currentLine.VisualDescription,
                IsSilent = currentLine.IsSilent,
                CreatedAt = DateTime.UtcNow
            };
            await conn.InsertAsync(newLine);
        }
        else
        {
            // Update existing line
            compareLine.LineText = currentLine.LineText;
            compareLine.VisualDescription = currentLine.VisualDescription;
            compareLine.IsSilent = currentLine.IsSilent;
            compareLine.ModifiedAt = DateTime.UtcNow;
            await conn.UpdateAsync(compareLine);
        }
        
        System.Diagnostics.Debug.WriteLine($"[STORY] Copied line {lineOrder} from project {currentProjectId} to {compareToProjectId}");
    }

    /// <summary>
    /// Import visual assets and completion status from another draft for matching lines
    /// Matches based on similar script text (case-insensitive, within threshold)
    /// </summary>
    /// <returns>Number of lines that had visuals imported</returns>
    public async Task<int> ImportVisualsFromDraftAsync(int targetProjectId, int sourceProjectId)
    {
        EnsureWritable();
        var targetLines = await GetLinesAsync(targetProjectId);
        var sourceLines = await GetLinesAsync(sourceProjectId);
        
        var conn = await _db.GetConnectionAsync();
        int importedCount = 0;
        
        foreach (var targetLine in targetLines)
        {
            // Skip if target already has visual prepared
            if (targetLine.VisualPrepared) continue;
            
            // Find matching source line (same order with similar text)
            var sourceLine = sourceLines.FirstOrDefault(s => 
                s.LineOrder == targetLine.LineOrder && 
                !IsSignificantlyDifferent(s.LineText, targetLine.LineText));
            
            if (sourceLine != null && sourceLine.VisualPrepared)
            {
                // Copy visual data
                targetLine.VisualAssetPath = sourceLine.VisualAssetPath;
                targetLine.VisualPrepared = sourceLine.VisualPrepared;
                targetLine.ModifiedAt = DateTime.UtcNow;
                
                await conn.UpdateAsync(targetLine);
                importedCount++;
                
                System.Diagnostics.Debug.WriteLine($"[STORY] Imported visual for line {targetLine.LineOrder} from project {sourceProjectId}");
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"[STORY] Imported {importedCount} visuals from project {sourceProjectId} to {targetProjectId}");
        return importedCount;
    }

    /// <summary>
    /// Create a new draft from imported lines
    /// </summary>
    public async Task<StoryProject> CreateDraftFromImportAsync(
        int sourceProjectId, 
        string username,
        List<ImportedLine> importedLines,
        string? customName = null,
        bool setAsLatest = false)
    {
        EnsureWritable();
        var sourceProject = await GetProjectByIdAsync(sourceProjectId);
        if (sourceProject == null)
            throw new ArgumentException("Source project not found");

        // Determine root project
        int rootId = sourceProject.ParentProjectId ?? sourceProject.Id;
        int nextVersion = await GetNextDraftVersionAsync(sourceProjectId);

        var conn = await _db.GetConnectionAsync();

        // If setting as latest, clear others first
        if (setAsLatest)
        {
            await conn.ExecuteAsync(
                "UPDATE story_projects SET IsLatest = 0 WHERE Id = ? OR ParentProjectId = ?",
                rootId, rootId);
        }

        // Create new draft project
        var draftProject = new StoryProject
        {
            Username = username,
            Name = customName ?? sourceProject.Name,
            Description = sourceProject.Description,
            LlmConversationLog = sourceProject.LlmConversationLog,
            ProjectCategory = sourceProject.ProjectCategory,
            CreatedAt = DateTime.UtcNow,
            Status = "active",
            ParentProjectId = rootId,
            DraftVersion = nextVersion,
            DraftSource = "ai-import",
            IsLatest = setAsLatest
        };

        await conn.InsertAsync(draftProject);
        System.Diagnostics.Debug.WriteLine($"[STORY] Created draft v{nextVersion}: {draftProject.Name}" + (setAsLatest ? " (Latest)" : ""));

        // Add all imported lines
        for (int i = 0; i < importedLines.Count; i++)
        {
            var imported = importedLines[i];
            var line = new StoryLine
            {
                ProjectId = draftProject.Id,
                LineOrder = i + 1,
                LineText = imported.Script ?? "",
                VisualDescription = imported.Visual ?? "",
                IsSilent = imported.IsSilent,
                ImagePrompt = imported.ImagePrompt ?? "",
                VideoPrompt = imported.VideoPrompt ?? "",
                CreatedAt = DateTime.UtcNow
            };
            
            await conn.InsertAsync(line);
            
            // Parse and save shots if provided
            if (!string.IsNullOrEmpty(imported.Shots))
            {
                var shots = ParseShotsString(imported.Shots, imported.ImagePrompt, imported.VideoPrompt, imported.ShotPrompts);
                if (shots.Count > 0)
                {
                    line.ShotsJson = System.Text.Json.JsonSerializer.Serialize(shots);
                    line.ShotCount = shots.Count;
                    await conn.UpdateAsync(line);
                }
            }
        }

        return draftProject;
    }

    /// <summary>
    /// Create a new draft version by duplicating an existing draft
    /// </summary>
    public async Task<StoryProject> CreateDraftVersionAsync(int sourceProjectId, string? customName = null)
    {
        EnsureWritable();
        var sourceProject = await GetProjectByIdAsync(sourceProjectId);
        if (sourceProject == null)
            throw new ArgumentException("Source project not found");

        // Determine root project
        int rootId = sourceProject.ParentProjectId ?? sourceProject.Id;
        int nextVersion = await GetNextDraftVersionAsync(sourceProjectId);

        var conn = await _db.GetConnectionAsync();

        // Clear IsLatest from all versions
        await conn.ExecuteAsync(
            "UPDATE story_projects SET IsLatest = 0 WHERE Id = ? OR ParentProjectId = ?",
            rootId, rootId);

        // Create new draft project
        var draftProject = new StoryProject
        {
            Username = sourceProject.Username,
            Name = customName ?? sourceProject.Name,
            Description = sourceProject.Description,
            LlmConversationLog = sourceProject.LlmConversationLog,
            ProjectCategory = sourceProject.ProjectCategory,
            CreatedAt = DateTime.UtcNow,
            Status = "active",
            ParentProjectId = rootId,
            DraftVersion = nextVersion,
            DraftSource = "duplicate",
            IsLatest = true
        };

        await conn.InsertAsync(draftProject);
        System.Diagnostics.Debug.WriteLine($"[STORY] Created draft v{nextVersion} from project {sourceProjectId}");

        // Copy all lines from source
        var sourceLines = await GetLinesAsync(sourceProjectId);
        foreach (var sourceLine in sourceLines)
        {
            var newLine = new StoryLine
            {
                ProjectId = draftProject.Id,
                LineOrder = sourceLine.LineOrder,
                LineText = sourceLine.LineText,
                VisualDescription = sourceLine.VisualDescription,
                VisualAssetPath = sourceLine.VisualAssetPath,
                VisualPrepared = sourceLine.VisualPrepared,
                IsSilent = sourceLine.IsSilent,
                DurationSeconds = sourceLine.DurationSeconds,
                ProductionNotes = sourceLine.ProductionNotes,
                ShotsJson = sourceLine.ShotsJson,
                ShotCount = sourceLine.ShotCount,
                ImagePrompt = sourceLine.ImagePrompt,
                VideoPrompt = sourceLine.VideoPrompt,
                CreatedAt = DateTime.UtcNow
            };
            await conn.InsertAsync(newLine);
        }

        return draftProject;
    }

    /// <summary>
    /// Parse shots string like "shot1: desc | shot2: desc" into VisualShot list
    /// For single-shot visuals, copies the line's ImagePrompt/VideoPrompt into the shot
    /// For multi-shot visuals, applies per-shot prompts from shotPrompts dictionary
    /// </summary>
    private List<VisualShot> ParseShotsString(
        string shotsStr, 
        string? lineImagePrompt = null, 
        string? lineVideoPrompt = null,
        Dictionary<int, (string? ImagePrompt, string? VideoPrompt)>? shotPrompts = null)
    {
        var shots = new List<VisualShot>();
        if (string.IsNullOrWhiteSpace(shotsStr)) return shots;
        
        // Split by | delimiter
        var parts = shotsStr.Split('|', StringSplitOptions.RemoveEmptyEntries);
        
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            if (string.IsNullOrEmpty(part)) continue;
            
            // Try to parse "shotN: description" or just use as description
            string description = part;
            if (part.Contains(':'))
            {
                var colonIdx = part.IndexOf(':');
                description = part.Substring(colonIdx + 1).Trim();
            }
            
            int shotIndex = i + 1;
            
            // Check for per-shot prompts
            string? shotImagePrompt = null;
            string? shotVideoPrompt = null;
            
            if (shotPrompts != null && shotPrompts.TryGetValue(shotIndex, out var prompts))
            {
                shotImagePrompt = prompts.ImagePrompt;
                shotVideoPrompt = prompts.VideoPrompt;
            }
            
            shots.Add(new VisualShot
            {
                Index = shotIndex,
                Description = description,
                ImagePrompt = shotImagePrompt ?? "",
                VideoPrompt = shotVideoPrompt ?? "",
                Done = false
            });
        }
        
        // If there's only one shot and no per-shot prompts were provided, copy the line-level prompts to it
        if (shots.Count == 1 && string.IsNullOrEmpty(shots[0].ImagePrompt) && string.IsNullOrEmpty(shots[0].VideoPrompt))
        {
            shots[0].ImagePrompt = lineImagePrompt ?? "";
            shots[0].VideoPrompt = lineVideoPrompt ?? "";
        }
        
        return shots;
    }

    /// <summary>
    /// Parse C# code format into ImportedLine objects
    /// Expects: lines[N].Script = "text"; lines[N].Visual = "text";
    /// </summary>
    public List<ImportedLine> ParseMarkdownImport(string content)
    {
        var result = new Dictionary<int, ImportedLine>();
        
        // Normalize the content: replace non-breaking spaces, smart quotes, etc.
        content = NormalizeContent(content);
        
        var textLines = content.Split('\n');
        bool currentIsSilent = false;

        foreach (var rawLine in textLines)
        {
            var trimmed = rawLine.Trim();
            var lowerTrimmed = trimmed.ToLower();
            
            // Check for type comment: // NARRATION or // VISUAL-ONLY
            if (trimmed.StartsWith("//"))
            {
                var comment = trimmed.ToUpper();
                if (comment.Contains("VISUAL-ONLY") || comment.Contains("VISUAL ONLY") || comment.Contains("SILENT"))
                    currentIsSilent = true;
                else if (comment.Contains("NARRATION"))
                    currentIsSilent = false;
                continue;
            }
            
            // Parse: lines[N].Script = "text"; (case-insensitive)
            if (lowerTrimmed.Contains("lines[") && lowerTrimmed.Contains("]."))
            {
                int? lineNum = ExtractLineNumber(trimmed);
                if (lineNum == null) continue;
                
                // Ensure we have an entry for this line
                if (!result.ContainsKey(lineNum.Value))
                {
                    result[lineNum.Value] = new ImportedLine { IsSilent = currentIsSilent };
                }
                
                var imported = result[lineNum.Value];
                
                // Extract the field and value (case-insensitive)
                if (lowerTrimmed.Contains(".script"))
                {
                    imported.Script = ExtractQuotedValue(trimmed);
                    // If script is empty, it's visual-only
                    if (string.IsNullOrEmpty(imported.Script))
                        imported.IsSilent = true;
                    else
                        imported.IsSilent = false;
                }
                else if (lowerTrimmed.Contains(".visual"))
                {
                    imported.Visual = ExtractQuotedValue(trimmed);
                }
                else if (lowerTrimmed.Contains("].shots") || lowerTrimmed.Contains("].shots "))
                {
                    // Match ".shots" field but not ".shot1_" or ".shot2_"
                    imported.Shots = ExtractQuotedValue(trimmed);
                }
                else if (System.Text.RegularExpressions.Regex.IsMatch(lowerTrimmed, @"]\.shot(\d+)_imageprompt"))
                {
                    // Parse Shot#_ImagePrompt
                    var match = System.Text.RegularExpressions.Regex.Match(lowerTrimmed, @"]\.shot(\d+)_imageprompt");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int shotIndex))
                    {
                        string prompt = ExtractQuotedValue(trimmed);
                        if (!imported.ShotPrompts.ContainsKey(shotIndex))
                            imported.ShotPrompts[shotIndex] = (null, null);
                        var existing = imported.ShotPrompts[shotIndex];
                        imported.ShotPrompts[shotIndex] = (prompt, existing.VideoPrompt);
                    }
                }
                else if (System.Text.RegularExpressions.Regex.IsMatch(lowerTrimmed, @"]\.shot(\d+)_videoprompt"))
                {
                    // Parse Shot#_VideoPrompt
                    var match = System.Text.RegularExpressions.Regex.Match(lowerTrimmed, @"]\.shot(\d+)_videoprompt");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int shotIndex))
                    {
                        string prompt = ExtractQuotedValue(trimmed);
                        if (!imported.ShotPrompts.ContainsKey(shotIndex))
                            imported.ShotPrompts[shotIndex] = (null, null);
                        var existing = imported.ShotPrompts[shotIndex];
                        imported.ShotPrompts[shotIndex] = (existing.ImagePrompt, prompt);
                    }
                }
                else if (lowerTrimmed.Contains(".imageprompt"))
                {
                    imported.ImagePrompt = ExtractQuotedValue(trimmed);
                }
                else if (lowerTrimmed.Contains(".videoprompt"))
                {
                    imported.VideoPrompt = ExtractQuotedValue(trimmed);
                }
            }
        }

        // Convert to ordered list
        return result.OrderBy(kvp => kvp.Key)
                     .Select(kvp => kvp.Value)
                     .Where(l => !string.IsNullOrEmpty(l.Script) || !string.IsNullOrEmpty(l.Visual))
                     .ToList();
    }

    /// <summary>
    /// Normalize content to handle copy-paste issues from web browsers
    /// </summary>
    private string NormalizeContent(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;
        
        // Replace smart/curly quotes with straight quotes
        content = content.Replace('\u201C', '"');  // left double quote
        content = content.Replace('\u201D', '"');  // right double quote
        content = content.Replace('\u2018', '\''); // left single quote
        content = content.Replace('\u2019', '\''); // right single quote
        
        // Replace non-breaking spaces with regular spaces
        content = content.Replace('\u00A0', ' ');  // NBSP
        content = content.Replace('\u2007', ' ');  // figure space
        content = content.Replace('\u202F', ' ');  // narrow NBSP
        
        // Normalize line endings
        content = content.Replace("\r\n", "\n").Replace("\r", "\n");
        
        return content;
    }

    /// <summary>
    /// Extract line number from lines[N].Field pattern
    /// </summary>
    private int? ExtractLineNumber(string line)
    {
        var lowerLine = line.ToLower();
        int startIdx = lowerLine.IndexOf("lines[");
        if (startIdx < 0) return null;
        
        int numStart = startIdx + 6; // length of "lines["
        int numEnd = line.IndexOf(']', numStart);
        if (numEnd < 0) return null;
        
        var numStr = line.Substring(numStart, numEnd - numStart);
        if (int.TryParse(numStr, out int num))
            return num;
        
        return null;
    }

    /// <summary>
    /// Extract value from between quotes: = "value";
    /// Handles escaped quotes
    /// </summary>
    private string ExtractQuotedValue(string line)
    {
        int eqIdx = line.IndexOf('=');
        if (eqIdx < 0) return "";
        
        // Find first quote after =
        int firstQuote = line.IndexOf('"', eqIdx);
        if (firstQuote < 0) return "";
        
        // Find closing quote (handle escaped quotes)
        int pos = firstQuote + 1;
        var sb = new System.Text.StringBuilder();
        
        while (pos < line.Length)
        {
            char c = line[pos];
            
            if (c == '\\' && pos + 1 < line.Length)
            {
                // Escaped character
                char next = line[pos + 1];
                if (next == '"')
                {
                    sb.Append('"');
                    pos += 2;
                    continue;
                }
                else if (next == 'n')
                {
                    sb.Append('\n');
                    pos += 2;
                    continue;
                }
                else if (next == '\\')
                {
                    sb.Append('\\');
                    pos += 2;
                    continue;
                }
            }
            
            if (c == '"')
            {
                // End of string
                break;
            }
            
            sb.Append(c);
            pos++;
        }
        
        return sb.ToString();
    }

    #endregion

    #region Shots

    /// <summary>
    /// Parse shots JSON from a line
    /// </summary>
    public List<VisualShot> GetShots(StoryLine line)
    {
        if (string.IsNullOrEmpty(line.ShotsJson))
            return new List<VisualShot>();
        
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<VisualShot>>(line.ShotsJson) 
                   ?? new List<VisualShot>();
        }
        catch
        {
            return new List<VisualShot>();
        }
    }

    /// <summary>
    /// Save shots to a line
    /// </summary>
    public async Task SaveShotsAsync(StoryLine line, List<VisualShot> shots)
    {
        EnsureWritable();
        line.ShotsJson = System.Text.Json.JsonSerializer.Serialize(shots);
        line.ShotCount = shots.Count;
        line.ModifiedAt = DateTime.UtcNow;
        
        // Update VisualPrepared based on all shots being done
        if (shots.Count > 0)
        {
            line.VisualPrepared = shots.All(s => s.Done);
        }
        
        await UpdateLineAsync(line);
    }

    /// <summary>
    /// Add a single shot to a line
    /// </summary>
    public async Task AddShotAsync(StoryLine line, string description, string imagePrompt = "", string videoPrompt = "")
    {
        EnsureWritable();
        var shots = GetShots(line);
        shots.Add(new VisualShot
        {
            Index = shots.Count + 1,
            Description = description,
            ImagePrompt = imagePrompt,
            VideoPrompt = videoPrompt
        });
        await SaveShotsAsync(line, shots);
    }

    /// <summary>
    /// Delete a shot from a line
    /// </summary>
    public async Task DeleteShotAsync(StoryLine line, int shotIndex)
    {
        EnsureWritable();
        var shots = GetShots(line);
        if (shotIndex >= 0 && shotIndex < shots.Count)
        {
            shots.RemoveAt(shotIndex);
            // Re-index
            for (int i = 0; i < shots.Count; i++)
            {
                shots[i].Index = i + 1;
            }
            await SaveShotsAsync(line, shots);
        }
    }

    /// <summary>
    /// Marks all lines in a project as having completed visuals.
    /// Used for video imports where footage already exists - no need to generate images or videos.
    /// </summary>
    public async Task MarkAllVisualsCompleteAsync(int projectId)
    {
        var lines = await GetLinesAsync(projectId);
        var conn = await _db.GetConnectionAsync();
        
        foreach (var line in lines)
        {
            line.VisualPrepared = true;
            line.ModifiedAt = DateTime.UtcNow;
            
            // Also mark any shots as complete
            var shots = GetShots(line);
            if (shots.Count > 0)
            {
                foreach (var shot in shots)
                {
                    shot.Task1_ImageGenerated = true;
                    shot.Task2_VideoGenerated = true;
                    shot.Done = true;
                }
                line.ShotsJson = System.Text.Json.JsonSerializer.Serialize(shots);
            }
            
            await conn.UpdateAsync(line);
        }
        
        System.Diagnostics.Debug.WriteLine($"[STORY] Marked all visuals complete for project {projectId} ({lines.Count} lines)");
    }

    #endregion

    #region Task Time Logging

    private async Task EnsureTaskLogTableAsync()
    {
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<ProductionTaskLog>();
        
        // Migrate: add new columns if they don't exist
        try { await conn.ExecuteAsync("ALTER TABLE production_task_logs ADD COLUMN LineText TEXT DEFAULT ''"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE production_task_logs ADD COLUMN VisualDescription TEXT DEFAULT ''"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE production_task_logs ADD COLUMN ShotDescription TEXT DEFAULT ''"); } catch { }
    }

    /// <summary>
    /// Log a completed production task with time spent
    /// </summary>
    public async Task LogTaskTimeAsync(
        string username,
        int projectId,
        int lineId, 
        int shotIndex, 
        string taskType, 
        int? minutesSpent, 
        string description = "",
        string lineText = "",
        string visualDescription = "",
        string shotDescription = "")
    {
        EnsureWritable();
        await EnsureTaskLogTableAsync();
        var conn = await _db.GetConnectionAsync();

        var log = new ProductionTaskLog
        {
            Username = username,
            ProjectId = projectId,
            LineId = lineId,
            ShotIndex = shotIndex,
            TaskType = taskType,
            MinutesSpent = minutesSpent,
            Description = description,
            LineText = lineText,
            VisualDescription = visualDescription,
            ShotDescription = shotDescription,
            CompletedAt = DateTime.UtcNow
        };

        await conn.InsertAsync(log);
        System.Diagnostics.Debug.WriteLine($"[STORY] Logged task: {taskType}, {minutesSpent ?? 0} min");
    }

    /// <summary>
    /// Get average minutes per task type for a user
    /// </summary>
    public async Task<Dictionary<string, double>> GetAverageTaskTimesAsync(string username)
    {
        await EnsureTaskLogTableAsync();
        var conn = await _db.GetConnectionAsync();

        var logs = await conn.Table<ProductionTaskLog>()
            .Where(l => l.Username == username && l.MinutesSpent != null)
            .ToListAsync();

        var averages = new Dictionary<string, double>();

        var grouped = logs.GroupBy(l => l.TaskType);
        foreach (var group in grouped)
        {
            var times = group.Where(l => l.MinutesSpent.HasValue).Select(l => l.MinutesSpent!.Value).ToList();
            if (times.Count > 0)
            {
                averages[group.Key] = times.Average();
            }
        }

        return averages;
    }

    /// <summary>
    /// Get task logs for a project
    /// </summary>
    public async Task<List<ProductionTaskLog>> GetProjectTaskLogsAsync(int projectId)
    {
        await EnsureTaskLogTableAsync();
        var conn = await _db.GetConnectionAsync();

        return await conn.Table<ProductionTaskLog>()
            .Where(l => l.ProjectId == projectId)
            .OrderByDescending(l => l.CompletedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get all task logs for a user (for export)
    /// </summary>
    public async Task<List<ProductionTaskLog>> GetAllTaskLogsAsync(string username)
    {
        await EnsureTaskLogTableAsync();
        var conn = await _db.GetConnectionAsync();

        return await conn.Table<ProductionTaskLog>()
            .Where(l => l.Username == username)
            .OrderByDescending(l => l.CompletedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Calculate estimated time remaining for a project based on average task times
    /// </summary>
    public async Task<(int estimatedMinutes, int remainingTasks, Dictionary<string, int> taskBreakdown)> 
        GetProjectTimeEstimateAsync(string username, int projectId)
    {
        var averages = await GetAverageTaskTimesAsync(username);
        var lines = await GetLinesAsync(projectId);

        int remainingTasks = 0;
        int estimatedMinutes = 0;
        var taskBreakdown = new Dictionary<string, int>
        {
            { "image_generated", 0 },
            { "video_generated", 0 },
            { "shot_done", 0 }
        };

        foreach (var line in lines)
        {
            if (line.VisualPrepared) continue;

            var shots = GetShots(line);
            if (shots.Count == 0)
            {
                // Single visual line - count as one "shot_done" task
                taskBreakdown["shot_done"]++;
                remainingTasks++;
            }
            else
            {
                foreach (var shot in shots)
                {
                    if (!shot.Task1_ImageGenerated)
                    {
                        taskBreakdown["image_generated"]++;
                        remainingTasks++;
                    }
                    if (!shot.Task2_VideoGenerated)
                    {
                        taskBreakdown["video_generated"]++;
                        remainingTasks++;
                    }
                    if (!shot.Done && shot.Task1_ImageGenerated && shot.Task2_VideoGenerated)
                    {
                        taskBreakdown["shot_done"]++;
                        remainingTasks++;
                    }
                }
            }
        }

        // Calculate estimated time
        foreach (var kvp in taskBreakdown)
        {
            if (averages.TryGetValue(kvp.Key, out double avgTime))
            {
                estimatedMinutes += (int)Math.Ceiling(avgTime * kvp.Value);
            }
            else
            {
                // Default estimates if no data: image=5min, video=3min, finalize=2min
                int defaultMin = kvp.Key switch
                {
                    "image_generated" => 5,
                    "video_generated" => 3,
                    "shot_done" => 2,
                    _ => 3
                };
                estimatedMinutes += defaultMin * kvp.Value;
            }
        }

        return (estimatedMinutes, remainingTasks, taskBreakdown);
    }

    #endregion

    #region Story Points

    private async Task EnsureStoryPointsTableAsync()
    {
        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StoryPoint>();
        if (!_db.IsReadOnly) await conn.CreateTableAsync<StoryPointVersion>();
        
        // Migrate: add Version column if it doesn't exist
        try { await conn.ExecuteAsync("ALTER TABLE story_points ADD COLUMN Version INTEGER DEFAULT 1"); } catch { }
        // Migrate: add IsLatest column if it doesn't exist
        try { await conn.ExecuteAsync("ALTER TABLE story_point_versions ADD COLUMN IsLatest INTEGER DEFAULT 0"); } catch { }
    }

    /// <summary>
    /// Set a specific version as the "latest" (clears others)
    /// </summary>
    public async Task SetStoryPointVersionAsLatestAsync(int projectId, int version)
    {
        EnsureWritable();
        await EnsureStoryPointsTableAsync();
        var conn = await _db.GetConnectionAsync();
        
        // Clear all IsLatest for this project
        await conn.ExecuteAsync(
            "UPDATE story_point_versions SET IsLatest = 0 WHERE ProjectId = ?", projectId);
        
        // Set the specified version as latest
        await conn.ExecuteAsync(
            "UPDATE story_point_versions SET IsLatest = 1 WHERE ProjectId = ? AND Version = ?", 
            projectId, version);
    }

    /// <summary>
    /// Get the latest version number for a project's story points
    /// </summary>
    public async Task<int> GetLatestStoryPointVersionAsync(int projectId)
    {
        await EnsureStoryPointsTableAsync();
        var conn = await _db.GetConnectionAsync();

        var versions = await conn.Table<StoryPointVersion>()
            .Where(v => v.ProjectId == projectId)
            .ToListAsync();
        
        if (versions.Count == 0)
        {
            // Check if there are any points - if so, create version 1 metadata
            var existingPoints = await conn.Table<StoryPoint>()
                .Where(p => p.ProjectId == projectId)
                .ToListAsync();
            
            if (existingPoints.Count > 0)
            {
                var v1 = new StoryPointVersion
                {
                    ProjectId = projectId,
                    Version = 1,
                    Name = "Initial",
                    CreatedAt = existingPoints.Min(p => p.CreatedAt)
                };
                await conn.InsertAsync(v1);
                return 1;
            }
            return 1; // Default to version 1
        }
        
        return versions.Max(v => v.Version);
    }

    /// <summary>
    /// Get all versions for a project
    /// </summary>
    public async Task<List<StoryPointVersion>> GetStoryPointVersionsAsync(int projectId)
    {
        await EnsureStoryPointsTableAsync();
        var conn = await _db.GetConnectionAsync();

        return await conn.Table<StoryPointVersion>()
            .Where(v => v.ProjectId == projectId)
            .OrderByDescending(v => v.Version)
            .ToListAsync();
    }

    /// <summary>
    /// Create a new version (snapshot current, start fresh)
    /// </summary>
    public async Task<int> CreateNewStoryPointVersionAsync(int projectId, string? name = null)
    {
        EnsureWritable();
        await EnsureStoryPointsTableAsync();
        var conn = await _db.GetConnectionAsync();

        int currentVersion = await GetLatestStoryPointVersionAsync(projectId);
        int newVersion = currentVersion + 1;

        // Create version metadata
        var versionMeta = new StoryPointVersion
        {
            ProjectId = projectId,
            Version = newVersion,
            Name = name ?? $"Version {newVersion}",
            CreatedAt = DateTime.UtcNow
        };
        await conn.InsertAsync(versionMeta);

        return newVersion;
    }

    /// <summary>
    /// Duplicate points from one version to another (for "start from snapshot")
    /// </summary>
    public async Task DuplicatePointsToVersionAsync(int projectId, int fromVersion, int toVersion)
    {
        EnsureWritable();
        await EnsureStoryPointsTableAsync();
        var conn = await _db.GetConnectionAsync();

        var sourcePoints = await conn.Table<StoryPoint>()
            .Where(p => p.ProjectId == projectId && p.Version == fromVersion)
            .ToListAsync();

        foreach (var source in sourcePoints)
        {
            var copy = new StoryPoint
            {
                ProjectId = source.ProjectId,
                Version = toVersion,
                Text = source.Text,
                Category = source.Category,
                Subcategory = source.Subcategory,
                IsSubcategoryLocked = source.IsSubcategoryLocked,
                IsCategoryLocked = source.IsCategoryLocked,
                DisplayOrder = source.DisplayOrder,
                Notes = source.Notes,
                CreatedAt = DateTime.UtcNow
            };
            await conn.InsertAsync(copy);
        }
    }

    /// <summary>
    /// Get all story points for a project at a specific version
    /// </summary>
    public async Task<List<StoryPoint>> GetStoryPointsAsync(int projectId, int? version = null)
    {
        await EnsureStoryPointsTableAsync();
        var conn = await _db.GetConnectionAsync();

        int targetVersion = version ?? await GetLatestStoryPointVersionAsync(projectId);

        return await conn.Table<StoryPoint>()
            .Where(p => p.ProjectId == projectId && p.Version == targetVersion)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();
    }

    /// <summary>
    /// Get story points for a project filtered by category at a specific version
    /// </summary>
    public async Task<List<StoryPoint>> GetStoryPointsByCategoryAsync(int projectId, string category, int? version = null)
    {
        await EnsureStoryPointsTableAsync();
        var conn = await _db.GetConnectionAsync();

        int targetVersion = version ?? await GetLatestStoryPointVersionAsync(projectId);

        return await conn.Table<StoryPoint>()
            .Where(p => p.ProjectId == projectId && p.Category == category && p.Version == targetVersion)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();
    }

    /// <summary>
    /// Add a new story point (always to latest version)
    /// </summary>
    public async Task<StoryPoint> AddStoryPointAsync(int projectId, string text, string category = "active", string? subcategory = null)
    {
        EnsureWritable();
        await EnsureStoryPointsTableAsync();
        var conn = await _db.GetConnectionAsync();

        int currentVersion = await GetLatestStoryPointVersionAsync(projectId);

        // Ensure version 1 metadata exists
        var versionExists = await conn.Table<StoryPointVersion>()
            .Where(v => v.ProjectId == projectId && v.Version == currentVersion)
            .FirstOrDefaultAsync();
        if (versionExists == null)
        {
            var v1 = new StoryPointVersion
            {
                ProjectId = projectId,
                Version = currentVersion,
                Name = "Initial",
                CreatedAt = DateTime.UtcNow
            };
            await conn.InsertAsync(v1);
        }

        // For active category, get max order within subcategory for current version
        List<StoryPoint> existing;
        if (category == "active" && !string.IsNullOrEmpty(subcategory))
        {
            existing = await conn.Table<StoryPoint>()
                .Where(p => p.ProjectId == projectId && p.Version == currentVersion && p.Category == category && p.Subcategory == subcategory)
                .ToListAsync();
        }
        else
        {
            existing = await conn.Table<StoryPoint>()
                .Where(p => p.ProjectId == projectId && p.Version == currentVersion && p.Category == category)
                .ToListAsync();
        }
        int maxOrder = existing.Count > 0 ? existing.Max(p => p.DisplayOrder) : -1;

        var point = new StoryPoint
        {
            ProjectId = projectId,
            Version = currentVersion,
            Text = text,
            Category = category,
            Subcategory = subcategory ?? (category == "active" ? "chronological" : ""),
            DisplayOrder = maxOrder + 1,
            CreatedAt = DateTime.UtcNow
        };

        await conn.InsertAsync(point);
        return point;
    }

    /// <summary>
    /// Insert a new story point before an existing point (shifts others down)
    /// </summary>
    public async Task<StoryPoint> InsertStoryPointBeforeAsync(int projectId, int beforePointId, string text, string category = "active", string? subcategory = null)
    {
        EnsureWritable();
        await EnsureStoryPointsTableAsync();
        var conn = await _db.GetConnectionAsync();

        // Get the target point
        var targetPoint = await conn.Table<StoryPoint>()
            .Where(p => p.Id == beforePointId)
            .FirstOrDefaultAsync();

        if (targetPoint == null)
        {
            // Fallback to add at end
            return await AddStoryPointAsync(projectId, text, category, subcategory);
        }

        int targetOrder = targetPoint.DisplayOrder;
        int version = targetPoint.Version;

        // Get all points at or after target in same category/subcategory
        List<StoryPoint> pointsToShift;
        if (category == "active" && !string.IsNullOrEmpty(subcategory))
        {
            pointsToShift = await conn.Table<StoryPoint>()
                .Where(p => p.ProjectId == projectId && p.Version == version && 
                       p.Category == category && p.Subcategory == subcategory &&
                       p.DisplayOrder >= targetOrder)
                .ToListAsync();
        }
        else
        {
            pointsToShift = await conn.Table<StoryPoint>()
                .Where(p => p.ProjectId == projectId && p.Version == version && 
                       p.Category == category && p.DisplayOrder >= targetOrder)
                .ToListAsync();
        }

        // Shift all points down by 1
        foreach (var p in pointsToShift)
        {
            p.DisplayOrder += 1;
            await conn.UpdateAsync(p);
        }

        // Create new point at target position
        var newPoint = new StoryPoint
        {
            ProjectId = projectId,
            Version = version,
            Text = text,
            Category = category,
            Subcategory = subcategory ?? (category == "active" ? "chronological" : ""),
            DisplayOrder = targetOrder,
            CreatedAt = DateTime.UtcNow
        };

        await conn.InsertAsync(newPoint);
        return newPoint;
    }

    /// <summary>
    /// Update a story point
    /// </summary>
    public async Task UpdateStoryPointAsync(StoryPoint point)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        point.ModifiedAt = DateTime.UtcNow;
        await conn.UpdateAsync(point);
    }

    /// <summary>
    /// Delete a story point
    /// </summary>
    public async Task DeleteStoryPointAsync(int pointId)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<StoryPoint>(pointId);
    }

    /// <summary>
    /// Move a story point to a different category
    /// </summary>
    public async Task MoveStoryPointToCategoryAsync(int pointId, string newCategory, string? newSubcategory = null)
    {
        EnsureWritable();
        await EnsureStoryPointsTableAsync();
        var conn = await _db.GetConnectionAsync();

        var point = await conn.Table<StoryPoint>()
            .Where(p => p.Id == pointId)
            .FirstOrDefaultAsync();

        if (point != null)
        {
            // For active category with subcategory, get max order within subcategory for same version
            List<StoryPoint> existing;
            if (newCategory == "active" && !string.IsNullOrEmpty(newSubcategory))
            {
                existing = await conn.Table<StoryPoint>()
                    .Where(p => p.ProjectId == point.ProjectId && p.Version == point.Version && p.Category == newCategory && p.Subcategory == newSubcategory)
                    .ToListAsync();
            }
            else
            {
                existing = await conn.Table<StoryPoint>()
                    .Where(p => p.ProjectId == point.ProjectId && p.Version == point.Version && p.Category == newCategory)
                    .ToListAsync();
            }
            int maxOrder = existing.Count > 0 ? existing.Max(p => p.DisplayOrder) : -1;

            point.Category = newCategory;
            point.Subcategory = newSubcategory ?? (newCategory == "active" ? "chronological" : "");
            point.DisplayOrder = maxOrder + 1;
            point.ModifiedAt = DateTime.UtcNow;
            await conn.UpdateAsync(point);
        }
    }

    /// <summary>
    /// Update a story point's subcategory (for active points)
    /// </summary>
    public async Task UpdateStoryPointSubcategoryAsync(int pointId, string newSubcategory)
    {
        EnsureWritable();
        await EnsureStoryPointsTableAsync();
        var conn = await _db.GetConnectionAsync();

        var point = await conn.Table<StoryPoint>()
            .Where(p => p.Id == pointId)
            .FirstOrDefaultAsync();

        if (point != null && point.Category == "active")
        {
            // Get max display order in new subcategory for same version
            var existing = await conn.Table<StoryPoint>()
                .Where(p => p.ProjectId == point.ProjectId && p.Version == point.Version && p.Category == "active" && p.Subcategory == newSubcategory)
                .ToListAsync();
            int maxOrder = existing.Count > 0 ? existing.Max(p => p.DisplayOrder) : -1;

            point.Subcategory = newSubcategory;
            point.DisplayOrder = maxOrder + 1;
            point.ModifiedAt = DateTime.UtcNow;
            await conn.UpdateAsync(point);
        }
    }

    /// <summary>
    /// Reorder a story point within its category (move up or down)
    /// </summary>
    public async Task ReorderStoryPointAsync(int pointId, bool moveUp)
    {
        EnsureWritable();
        await EnsureStoryPointsTableAsync();
        var conn = await _db.GetConnectionAsync();

        var point = await conn.Table<StoryPoint>()
            .Where(p => p.Id == pointId)
            .FirstOrDefaultAsync();

        if (point == null) return;

        // Only reorder within same version
        var categoryPoints = await conn.Table<StoryPoint>()
            .Where(p => p.ProjectId == point.ProjectId && p.Version == point.Version && p.Category == point.Category)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();

        int currentIndex = categoryPoints.FindIndex(p => p.Id == pointId);
        if (currentIndex < 0) return;

        int swapIndex = moveUp ? currentIndex - 1 : currentIndex + 1;
        if (swapIndex < 0 || swapIndex >= categoryPoints.Count) return;

        // Swap display orders
        var other = categoryPoints[swapIndex];
        int tempOrder = point.DisplayOrder;
        point.DisplayOrder = other.DisplayOrder;
        other.DisplayOrder = tempOrder;

        await conn.UpdateAsync(point);
        await conn.UpdateAsync(other);
    }

    /// <summary>
    /// Delete a specific version and all its points
    /// </summary>
    public async Task DeleteStoryPointVersionAsync(int projectId, int version)
    {
        EnsureWritable();
        await EnsureStoryPointsTableAsync();
        var conn = await _db.GetConnectionAsync();

        // Delete all points in this version
        var points = await conn.Table<StoryPoint>()
            .Where(p => p.ProjectId == projectId && p.Version == version)
            .ToListAsync();
        foreach (var p in points)
        {
            await conn.DeleteAsync(p);
        }

        // Delete version metadata
        var versionMeta = await conn.Table<StoryPointVersion>()
            .Where(v => v.ProjectId == projectId && v.Version == version)
            .FirstOrDefaultAsync();
        if (versionMeta != null)
        {
            await conn.DeleteAsync(versionMeta);
        }
    }

    /// <summary>
    /// Rename a version
    /// </summary>
    public async Task RenameStoryPointVersionAsync(int projectId, int version, string newName)
    {
        EnsureWritable();
        await EnsureStoryPointsTableAsync();
        var conn = await _db.GetConnectionAsync();

        var versionMeta = await conn.Table<StoryPointVersion>()
            .Where(v => v.ProjectId == projectId && v.Version == version)
            .FirstOrDefaultAsync();
        
        if (versionMeta != null)
        {
            versionMeta.Name = newName;
            await conn.UpdateAsync(versionMeta);
        }
    }

    #endregion
}

/// <summary>
/// Represents a line imported from AI-generated content
/// </summary>
public class ImportedLine
{
    public string? Script { get; set; }
    public string? Visual { get; set; }
    public bool IsSilent { get; set; } = false;
    public string? Shots { get; set; }           // "shot1: desc | shot2: desc" format
    public string? ImagePrompt { get; set; }
    public string? VideoPrompt { get; set; }
    
    // Per-shot prompts: key = shot index (1-based), value = (imagePrompt, videoPrompt)
    public Dictionary<int, (string? ImagePrompt, string? VideoPrompt)> ShotPrompts { get; set; } = new();
}
