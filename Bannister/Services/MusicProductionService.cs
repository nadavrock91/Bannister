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
    }

    private async Task EnsureLineTableAsync(ISQLiteAsyncConnection conn)
    {
        if (_db.IsReadOnly) return;

        await conn.CreateTableAsync<MusicLine>();
        try { await conn.ExecuteAsync("ALTER TABLE music_lines ADD COLUMN ProductionNotes TEXT DEFAULT ''"); } catch { }
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
        await EnsureLineTableAsync(conn);
        await conn.ExecuteAsync("DELETE FROM music_lines WHERE ProjectId = ?", projectId);
        await conn.DeleteAsync<MusicProject>(projectId);
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
}
