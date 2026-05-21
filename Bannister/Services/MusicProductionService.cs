using Bannister.Models;
using SQLite;

namespace Bannister.Services;

public class MusicProductionService
{
    private readonly DatabaseService _db;

    public MusicProductionService(DatabaseService db)
    {
        _db = db;
    }

    private void EnsureWritable()
    {
        if (_db.IsReadOnly)
            throw new ReadOnlyDatabaseException("Music Production is read-only on secondary devices.");
    }

    private async Task EnsureProjectTableAsync(ISQLiteAsyncConnection conn)
    {
        if (_db.IsReadOnly) return;

        await conn.CreateTableAsync<MusicProject>();
        try { await conn.ExecuteAsync("ALTER TABLE music_projects ADD COLUMN MusicConversationLog TEXT DEFAULT ''"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE music_projects ADD COLUMN ProjectCategory TEXT DEFAULT ''"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE music_projects ADD COLUMN IsPublished INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE music_projects ADD COLUMN PublishedAt TEXT"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE music_projects ADD COLUMN ProjectedClipCount INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE music_projects ADD COLUMN ProjectedDays INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE music_projects ADD COLUMN FinalClipCount INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE music_projects ADD COLUMN ParentProjectId INTEGER"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE music_projects ADD COLUMN DraftVersion INTEGER DEFAULT 1"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE music_projects ADD COLUMN DraftSource TEXT DEFAULT 'manual'"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE music_projects ADD COLUMN IsLatest INTEGER DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE music_projects ADD COLUMN CompareToProjectId INTEGER"); } catch { }
    }

    private async Task EnsureLineTableAsync(ISQLiteAsyncConnection conn)
    {
        if (_db.IsReadOnly) return;

        await conn.CreateTableAsync<MusicLine>();
        try { await conn.ExecuteAsync("ALTER TABLE music_lines ADD COLUMN ProductionNotes TEXT DEFAULT ''"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE music_lines ADD COLUMN TargetEmotion TEXT DEFAULT ''"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE music_lines ADD COLUMN RhythmIntent TEXT DEFAULT ''"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE music_lines ADD COLUMN LayerNotes TEXT DEFAULT ''"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE music_lines ADD COLUMN SectionDecision TEXT DEFAULT ''"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE music_lines ADD COLUMN AssignedCueId INTEGER"); } catch { }
    }

    private async Task EnsureCueTableAsync(ISQLiteAsyncConnection conn)
    {
        if (_db.IsReadOnly) return;

        await conn.CreateTableAsync<MusicCue>();
    }

    private static bool IsMissingTable(SQLiteException ex)
    {
        return ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<MusicProject>> GetProjectsAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        await EnsureProjectTableAsync(conn);

        try
        {
            return await conn.Table<MusicProject>()
                .Where(p => p.Username == username)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }
        catch (SQLiteException ex) when (IsMissingTable(ex))
        {
            return new List<MusicProject>();
        }
    }

    public async Task<List<MusicProject>> GetActiveProjectsAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        await EnsureProjectTableAsync(conn);

        try
        {
            return await conn.Table<MusicProject>()
                .Where(p => p.Username == username && p.Status == "active")
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }
        catch (SQLiteException ex) when (IsMissingTable(ex))
        {
            return new List<MusicProject>();
        }
    }

    public async Task<MusicProject?> GetProjectByIdAsync(int projectId)
    {
        var conn = await _db.GetConnectionAsync();
        await EnsureProjectTableAsync(conn);

        try
        {
            return await conn.Table<MusicProject>()
                .Where(p => p.Id == projectId)
                .FirstOrDefaultAsync();
        }
        catch (SQLiteException ex) when (IsMissingTable(ex))
        {
            return null;
        }
    }

    public async Task<MusicProject> CreateProjectAsync(string username, string name, string category = "", string description = "")
    {
        EnsureWritable();

        var conn = await _db.GetConnectionAsync();
        await EnsureProjectTableAsync(conn);

        var project = new MusicProject
        {
            Username = username,
            Name = name,
            Description = description,
            ProjectCategory = category,
            CreatedAt = DateTime.UtcNow,
            Status = "active"
        };

        await conn.InsertAsync(project);
        return project;
    }

    public async Task UpdateProjectAsync(MusicProject project)
    {
        EnsureWritable();

        var conn = await _db.GetConnectionAsync();
        await EnsureProjectTableAsync(conn);
        await conn.UpdateAsync(project);
    }

    public async Task DeleteProjectAsync(int projectId)
    {
        EnsureWritable();

        var conn = await _db.GetConnectionAsync();
        await EnsureProjectTableAsync(conn);
        await EnsureLineTableAsync(conn);
        await EnsureCueTableAsync(conn);
        var project = await GetProjectByIdAsync(projectId);
        if (project == null) return;

        int rootId = project.ParentProjectId ?? project.Id;
        var family = await GetProjectDraftsAsync(rootId);

        foreach (var draft in family)
        {
            await conn.ExecuteAsync("DELETE FROM music_lines WHERE ProjectId = ?", draft.Id);
            await conn.ExecuteAsync("DELETE FROM music_cues WHERE ProjectId = ?", draft.Id);
            await conn.DeleteAsync(draft);
        }
    }

    public async Task<List<MusicProject>> GetProjectDraftsAsync(int projectId)
    {
        var conn = await _db.GetConnectionAsync();
        await EnsureProjectTableAsync(conn);

        var project = await GetProjectByIdAsync(projectId);
        if (project == null) return new List<MusicProject>();

        int rootId = project.ParentProjectId ?? project.Id;

        try
        {
            var allProjects = await conn.Table<MusicProject>().ToListAsync();
            return allProjects
                .Where(p => p.Id == rootId || p.ParentProjectId == rootId)
                .OrderBy(p => p.DraftVersion)
                .ToList();
        }
        catch (SQLiteException ex) when (IsMissingTable(ex))
        {
            return new List<MusicProject>();
        }
    }

    public async Task<int> GetNextDraftVersionAsync(int projectId)
    {
        var drafts = await GetProjectDraftsAsync(projectId);
        return drafts.Count > 0 ? drafts.Max(d => d.DraftVersion) + 1 : 2;
    }

    public async Task SetAsLatestAsync(int projectId)
    {
        EnsureWritable();

        var project = await GetProjectByIdAsync(projectId);
        if (project == null) return;

        var conn = await _db.GetConnectionAsync();
        await EnsureProjectTableAsync(conn);

        int rootId = project.ParentProjectId ?? project.Id;
        await conn.ExecuteAsync(
            "UPDATE music_projects SET IsLatest = 0 WHERE Id = ? OR ParentProjectId = ?",
            rootId, rootId);

        project.IsLatest = true;
        await conn.UpdateAsync(project);
    }

    public async Task<MusicProject?> GetLatestDraftAsync(int projectId)
    {
        var drafts = await GetProjectDraftsAsync(projectId);
        if (drafts.Count == 0) return null;

        return drafts.FirstOrDefault(d => d.IsLatest) ?? drafts.First();
    }

    public async Task SetCompareToAsync(int projectId, int? compareToProjectId)
    {
        EnsureWritable();

        var project = await GetProjectByIdAsync(projectId);
        if (project == null) return;

        project.CompareToProjectId = compareToProjectId;
        await UpdateProjectAsync(project);
    }

    public async Task<MusicProject?> GetComparisonProjectAsync(MusicProject? project)
    {
        if (project == null) return null;

        if (project.CompareToProjectId.HasValue)
            return await GetProjectByIdAsync(project.CompareToProjectId.Value);

        var drafts = await GetProjectDraftsAsync(project.Id);
        int currentIndex = drafts.FindIndex(d => d.Id == project.Id);
        if (currentIndex <= 0) return null;

        return drafts[currentIndex - 1];
    }

    public async Task<HashSet<int>> GetChangedLineOrdersAsync(int projectId, int compareToProjectId)
    {
        var changed = new HashSet<int>();
        var currentLines = await GetLinesAsync(projectId);
        var compareLines = await GetLinesAsync(compareToProjectId);
        var compareLookup = compareLines.ToDictionary(l => l.LineOrder);

        foreach (var line in currentLines)
        {
            if (!compareLookup.TryGetValue(line.LineOrder, out var compareLine))
            {
                changed.Add(line.LineOrder);
                continue;
            }

            if (IsDifferent(line.Music, compareLine.Music) ||
                IsDifferent(line.Script, compareLine.Script) ||
                IsDifferent(line.Visuals, compareLine.Visuals) ||
                IsDifferent(line.TargetEmotion, compareLine.TargetEmotion) ||
                IsDifferent(line.RhythmIntent, compareLine.RhythmIntent) ||
                IsDifferent(line.LayerNotes, compareLine.LayerNotes) ||
                IsDifferent(line.SectionDecision, compareLine.SectionDecision) ||
                line.AssignedCueId != compareLine.AssignedCueId)
            {
                changed.Add(line.LineOrder);
            }
        }

        return changed;
    }

    private static bool IsDifferent(string a, string b)
    {
        return !string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<MusicProject> CreateDraftVersionAsync(int sourceProjectId, string? customName = null)
    {
        EnsureWritable();

        var sourceProject = await GetProjectByIdAsync(sourceProjectId);
        if (sourceProject == null)
            throw new ArgumentException("Source project not found");

        int rootId = sourceProject.ParentProjectId ?? sourceProject.Id;
        int nextVersion = await GetNextDraftVersionAsync(sourceProjectId);

        var conn = await _db.GetConnectionAsync();
        await EnsureProjectTableAsync(conn);
        await EnsureLineTableAsync(conn);

        await conn.ExecuteAsync(
            "UPDATE music_projects SET IsLatest = 0 WHERE Id = ? OR ParentProjectId = ?",
            rootId, rootId);

        var draftProject = new MusicProject
        {
            Username = sourceProject.Username,
            Name = customName ?? sourceProject.Name,
            Description = sourceProject.Description,
            MusicConversationLog = sourceProject.MusicConversationLog,
            ProjectCategory = sourceProject.ProjectCategory,
            CreatedAt = DateTime.UtcNow,
            Status = "active",
            ParentProjectId = rootId,
            DraftVersion = nextVersion,
            DraftSource = "duplicate",
            IsLatest = true
        };

        await conn.InsertAsync(draftProject);

        var sourceLines = await GetLinesAsync(sourceProjectId);
        foreach (var sourceLine in sourceLines)
        {
            var newLine = new MusicLine
            {
                ProjectId = draftProject.Id,
                LineOrder = sourceLine.LineOrder,
                Music = sourceLine.Music,
                Script = sourceLine.Script,
                Visuals = sourceLine.Visuals,
                ProductionNotes = sourceLine.ProductionNotes,
                TargetEmotion = sourceLine.TargetEmotion,
                RhythmIntent = sourceLine.RhythmIntent,
                LayerNotes = sourceLine.LayerNotes,
                SectionDecision = sourceLine.SectionDecision,
                AssignedCueId = null,
                CreatedAt = DateTime.UtcNow
            };
            await conn.InsertAsync(newLine);
        }

        return draftProject;
    }

    public async Task<MusicProject> CreateDraftFromImportAsync(
        int sourceProjectId,
        string username,
        List<ImportedMusicLine> importedLines,
        string? customName = null,
        bool setAsLatest = false)
    {
        EnsureWritable();

        var sourceProject = await GetProjectByIdAsync(sourceProjectId);
        if (sourceProject == null)
            throw new ArgumentException("Source project not found");

        int rootId = sourceProject.ParentProjectId ?? sourceProject.Id;
        int nextVersion = await GetNextDraftVersionAsync(sourceProjectId);

        var conn = await _db.GetConnectionAsync();
        await EnsureProjectTableAsync(conn);
        await EnsureLineTableAsync(conn);

        if (setAsLatest)
        {
            await conn.ExecuteAsync(
                "UPDATE music_projects SET IsLatest = 0 WHERE Id = ? OR ParentProjectId = ?",
                rootId, rootId);
        }

        var draftProject = new MusicProject
        {
            Username = username,
            Name = customName ?? sourceProject.Name,
            Description = sourceProject.Description,
            MusicConversationLog = sourceProject.MusicConversationLog,
            ProjectCategory = sourceProject.ProjectCategory,
            CreatedAt = DateTime.UtcNow,
            Status = "active",
            ParentProjectId = rootId,
            DraftVersion = nextVersion,
            DraftSource = "ai-import",
            IsLatest = setAsLatest
        };

        await conn.InsertAsync(draftProject);

        for (int i = 0; i < importedLines.Count; i++)
        {
            var imported = importedLines[i];
            var line = new MusicLine
            {
                ProjectId = draftProject.Id,
                LineOrder = i + 1,
                Music = "",
                Script = imported.Script ?? "",
                Visuals = imported.Visual ?? "",
                TargetEmotion = "",
                RhythmIntent = "",
                LayerNotes = "",
                SectionDecision = "",
                AssignedCueId = null,
                CreatedAt = DateTime.UtcNow
            };

            await conn.InsertAsync(line);
        }

        return draftProject;
    }

    public async Task RenameDraftAsync(int projectId, string newName)
    {
        EnsureWritable();

        var project = await GetProjectByIdAsync(projectId);
        if (project == null || project.DraftVersion <= 1) return;

        project.Name = newName;
        await UpdateProjectAsync(project);
    }

    public async Task DeleteDraftAsync(int projectId)
    {
        EnsureWritable();

        var project = await GetProjectByIdAsync(projectId);
        if (project == null || project.ParentProjectId == null) return;

        var conn = await _db.GetConnectionAsync();
        await EnsureLineTableAsync(conn);
        await EnsureCueTableAsync(conn);
        await conn.ExecuteAsync("DELETE FROM music_lines WHERE ProjectId = ?", projectId);
        await conn.ExecuteAsync("DELETE FROM music_cues WHERE ProjectId = ?", projectId);
        await conn.DeleteAsync(project);
    }

    public async Task<List<MusicLine>> GetLinesAsync(int projectId)
    {
        var conn = await _db.GetConnectionAsync();
        await EnsureLineTableAsync(conn);

        try
        {
            return await conn.Table<MusicLine>()
                .Where(l => l.ProjectId == projectId)
                .OrderBy(l => l.LineOrder)
                .ToListAsync();
        }
        catch (SQLiteException ex) when (IsMissingTable(ex))
        {
            return new List<MusicLine>();
        }
    }

    public async Task<MusicLine?> GetLineByIdAsync(int lineId)
    {
        var conn = await _db.GetConnectionAsync();
        await EnsureLineTableAsync(conn);

        try
        {
            return await conn.Table<MusicLine>()
                .Where(l => l.Id == lineId)
                .FirstOrDefaultAsync();
        }
        catch (SQLiteException ex) when (IsMissingTable(ex))
        {
            return null;
        }
    }

    public async Task<MusicLine> AddLineAsync(int projectId, string music = "", string script = "", string visuals = "")
    {
        EnsureWritable();

        var conn = await _db.GetConnectionAsync();
        await EnsureLineTableAsync(conn);

        var existing = await GetLinesAsync(projectId);
        int nextOrder = existing.Count > 0 ? existing.Max(l => l.LineOrder) + 1 : 1;

        var line = new MusicLine
        {
            ProjectId = projectId,
            LineOrder = nextOrder,
            Music = music,
            Script = script,
            Visuals = visuals,
            RhythmIntent = "",
            SectionDecision = "",
            CreatedAt = DateTime.UtcNow
        };

        await conn.InsertAsync(line);
        return line;
    }

    public async Task UpdateLineAsync(MusicLine line)
    {
        EnsureWritable();

        var conn = await _db.GetConnectionAsync();
        await EnsureLineTableAsync(conn);
        line.ModifiedAt = DateTime.UtcNow;
        await conn.UpdateAsync(line);
    }

    public async Task DeleteLineAsync(int lineId)
    {
        EnsureWritable();

        var conn = await _db.GetConnectionAsync();
        await EnsureLineTableAsync(conn);
        var line = await GetLineByIdAsync(lineId);
        if (line == null) return;

        await conn.DeleteAsync<MusicLine>(lineId);
        await RenumberLinesAsync(line.ProjectId);
    }

    public async Task MoveLineUpAsync(int lineId)
    {
        EnsureWritable();

        var line = await GetLineByIdAsync(lineId);
        if (line == null || line.LineOrder <= 1) return;

        var conn = await _db.GetConnectionAsync();
        var lineAbove = await conn.Table<MusicLine>()
            .Where(l => l.ProjectId == line.ProjectId && l.LineOrder == line.LineOrder - 1)
            .FirstOrDefaultAsync();

        if (lineAbove == null) return;

        lineAbove.LineOrder = line.LineOrder;
        line.LineOrder -= 1;
        await conn.UpdateAsync(lineAbove);
        await conn.UpdateAsync(line);
    }

    public async Task MoveLineDownAsync(int lineId)
    {
        EnsureWritable();

        var line = await GetLineByIdAsync(lineId);
        if (line == null) return;

        var conn = await _db.GetConnectionAsync();
        var lineBelow = await conn.Table<MusicLine>()
            .Where(l => l.ProjectId == line.ProjectId && l.LineOrder == line.LineOrder + 1)
            .FirstOrDefaultAsync();

        if (lineBelow == null) return;

        lineBelow.LineOrder = line.LineOrder;
        line.LineOrder += 1;
        await conn.UpdateAsync(lineBelow);
        await conn.UpdateAsync(line);
    }

    private async Task RenumberLinesAsync(int projectId)
    {
        var lines = await GetLinesAsync(projectId);
        var conn = await _db.GetConnectionAsync();

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].LineOrder == i + 1) continue;
            lines[i].LineOrder = i + 1;
            await conn.UpdateAsync(lines[i]);
        }
    }

    public List<ImportedMusicLine> ParseMusicImport(string content)
    {
        var result = new Dictionary<int, ImportedMusicLine>();
        content = NormalizeImportContent(content);

        foreach (var rawLine in content.Split('\n'))
        {
            var trimmed = rawLine.Trim();
            var lowerTrimmed = trimmed.ToLowerInvariant();

            if (!lowerTrimmed.Contains("lines[") || !lowerTrimmed.Contains("]."))
                continue;

            int? lineNum = ExtractLineNumber(trimmed);
            if (lineNum == null) continue;

            if (!result.ContainsKey(lineNum.Value))
                result[lineNum.Value] = new ImportedMusicLine();

            var imported = result[lineNum.Value];
            if (lowerTrimmed.Contains(".script"))
                imported.Script = ExtractQuotedValue(trimmed);
            else if (lowerTrimmed.Contains(".visual"))
                imported.Visual = ExtractQuotedValue(trimmed);
        }

        return result
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => kvp.Value)
            .Where(l => !string.IsNullOrEmpty(l.Script) || !string.IsNullOrEmpty(l.Visual))
            .ToList();
    }

    private static string NormalizeImportContent(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;

        content = content.Replace('\u201C', '"');
        content = content.Replace('\u201D', '"');
        content = content.Replace('\u2018', '\'');
        content = content.Replace('\u2019', '\'');
        content = content.Replace('\u00A0', ' ');
        content = content.Replace('\u2007', ' ');
        content = content.Replace('\u202F', ' ');
        content = content.Replace("\r\n", "\n").Replace("\r", "\n");

        return content;
    }

    private static int? ExtractLineNumber(string line)
    {
        var lowerLine = line.ToLowerInvariant();
        int startIdx = lowerLine.IndexOf("lines[", StringComparison.Ordinal);
        if (startIdx < 0) return null;

        int numStart = startIdx + 6;
        int numEnd = line.IndexOf(']', numStart);
        if (numEnd < 0) return null;

        var numStr = line.Substring(numStart, numEnd - numStart);
        return int.TryParse(numStr, out int num) ? num : null;
    }

    private static string ExtractQuotedValue(string line)
    {
        int eqIdx = line.IndexOf('=');
        if (eqIdx < 0) return "";

        int firstQuote = line.IndexOf('"', eqIdx);
        if (firstQuote < 0) return "";

        int pos = firstQuote + 1;
        var sb = new System.Text.StringBuilder();

        while (pos < line.Length)
        {
            char c = line[pos];

            if (c == '\\' && pos + 1 < line.Length)
            {
                char next = line[pos + 1];
                if (next == '"')
                {
                    sb.Append('"');
                    pos += 2;
                    continue;
                }

                if (next == 'n')
                {
                    sb.Append('\n');
                    pos += 2;
                    continue;
                }

                if (next == '\\')
                {
                    sb.Append('\\');
                    pos += 2;
                    continue;
                }
            }

            if (c == '"') break;

            sb.Append(c);
            pos++;
        }

        return sb.ToString();
    }

    public async Task<List<MusicCue>> GetCuesAsync(int projectId)
    {
        var conn = await _db.GetConnectionAsync();
        await EnsureCueTableAsync(conn);

        try
        {
            return await conn.Table<MusicCue>()
                .Where(c => c.ProjectId == projectId)
                .OrderBy(c => c.Id)
                .ToListAsync();
        }
        catch (SQLiteException ex) when (IsMissingTable(ex))
        {
            return new List<MusicCue>();
        }
    }

    public async Task<MusicCue?> GetCueByIdAsync(int cueId)
    {
        var conn = await _db.GetConnectionAsync();
        await EnsureCueTableAsync(conn);

        try
        {
            return await conn.Table<MusicCue>()
                .Where(c => c.Id == cueId)
                .FirstOrDefaultAsync();
        }
        catch (SQLiteException ex) when (IsMissingTable(ex))
        {
            return null;
        }
    }

    public async Task<MusicCue> AddCueAsync(int projectId, string label, bool isPrimaryDNA = false)
    {
        EnsureWritable();

        var conn = await _db.GetConnectionAsync();
        await EnsureCueTableAsync(conn);

        var existing = await GetCuesAsync(projectId);
        var cue = new MusicCue
        {
            ProjectId = projectId,
            Label = label,
            IsPrimaryDNA = isPrimaryDNA || existing.Count == 0,
            VariationType = "Original",
            EnergyLevel = "Medium",
            Status = "NotGenerated",
            ReuseFlag = "Reusable",
            DurationSeconds = 30,
            CreatedAt = DateTime.UtcNow
        };

        await conn.InsertAsync(cue);
        return cue;
    }

    public async Task<MusicCue> CreateVariationCueAsync(int parentCueId, string variationType, string label)
    {
        EnsureWritable();

        var parent = await GetCueByIdAsync(parentCueId);
        if (parent == null)
            throw new ArgumentException("Parent cue not found");

        var conn = await _db.GetConnectionAsync();
        await EnsureCueTableAsync(conn);

        var cue = new MusicCue
        {
            ProjectId = parent.ProjectId,
            Label = label,
            IsPrimaryDNA = false,
            ParentCueId = parent.Id,
            VariationType = variationType,
            Mood = parent.Mood,
            Pulse = parent.Pulse,
            Motif = parent.Motif,
            EnergyLevel = parent.EnergyLevel,
            MustLoop = parent.MustLoop,
            MustSitUnderNarration = parent.MustSitUnderNarration,
            Status = "NotGenerated",
            ReuseFlag = "Reusable",
            DurationSeconds = parent.DurationSeconds <= 0 ? 30 : parent.DurationSeconds,
            CreatedAt = DateTime.UtcNow
        };

        await conn.InsertAsync(cue);
        return cue;
    }

    public async Task UpdateCueAsync(MusicCue cue)
    {
        EnsureWritable();

        var conn = await _db.GetConnectionAsync();
        await EnsureCueTableAsync(conn);
        await conn.UpdateAsync(cue);
    }

    public async Task DeleteCueAsync(int cueId)
    {
        EnsureWritable();

        var cue = await GetCueByIdAsync(cueId);
        if (cue == null) return;

        var conn = await _db.GetConnectionAsync();
        await EnsureCueTableAsync(conn);
        await EnsureLineTableAsync(conn);
        await conn.ExecuteAsync("UPDATE music_lines SET AssignedCueId = NULL WHERE AssignedCueId = ?", cueId);
        await conn.ExecuteAsync("UPDATE music_cues SET ParentCueId = NULL WHERE ParentCueId = ?", cueId);
        await conn.DeleteAsync(cue);
    }
}

public class ImportedMusicLine
{
    public string? Script { get; set; }
    public string? Visual { get; set; }
}
