using Bannister.Models;
using SQLite;

namespace Bannister.Services;

public class CommandsCasinoService
{
    private readonly DatabaseService _db;
    private readonly AuthService _auth;
    private bool _initialized;

    public CommandsCasinoService(DatabaseService db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private void EnsureWritable()
    {
        if (_db.IsReadOnly)
            throw new ReadOnlyDatabaseException("Commands Casino is read-only on secondary devices.");
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        var conn = await _db.GetConnectionAsync();
        if (!_db.IsReadOnly)
        {
            await conn.CreateTableAsync<CasinoChip>();
            try { await conn.ExecuteAsync("ALTER TABLE casino_chips ADD COLUMN Category TEXT DEFAULT ''"); } catch { }
            await conn.CreateTableAsync<CasinoPreset>();
            await conn.CreateTableAsync<CasinoLostChip>();
        }

        _initialized = true;
    }

    public async Task<List<CasinoChip>> GetChipsAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        try
        {
            return await conn.Table<CasinoChip>()
                .Where(c => c.Username == username)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }
        catch (SQLiteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            return new List<CasinoChip>();
        }
    }

    public async Task<List<string>> GetChipCategoriesAsync(string username)
    {
        var chips = await GetChipsAsync(username);
        return chips
            .Select(c => c.Category?.Trim() ?? "")
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<CasinoChip> AddChipAsync(string username, string name, string category = "")
    {
        EnsureWritable();
        await EnsureInitializedAsync();

        var chip = new CasinoChip
        {
            Username = username,
            Name = name.Trim(),
            Category = category.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(chip);
        return chip;
    }

    public async Task SetChipCategoryAsync(int chipId, string category)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var chip = await conn.FindAsync<CasinoChip>(chipId);
        if (chip == null) return;

        chip.Category = category.Trim();
        await conn.UpdateAsync(chip);
    }

    public async Task DeleteChipAsync(int id)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<CasinoChip>(id);
    }

    public Task RemoveChipAsync(int id) => DeleteChipAsync(id);

    public async Task<List<CasinoPreset>> GetPresetsAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        try
        {
            return await conn.Table<CasinoPreset>()
                .Where(p => p.Username == username)
                .OrderBy(p => p.Text)
                .ToListAsync();
        }
        catch (SQLiteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            return new List<CasinoPreset>();
        }
    }

    public async Task<CasinoPreset> AddPresetAsync(string username, string text)
    {
        EnsureWritable();
        await EnsureInitializedAsync();

        var preset = new CasinoPreset
        {
            Username = username,
            Text = text.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(preset);
        return preset;
    }

    public async Task DeletePresetAsync(int id)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<CasinoPreset>(id);
    }

    public async Task<List<CasinoLostChip>> GetLostChipsAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        try
        {
            return await conn.Table<CasinoLostChip>()
                .Where(l => l.Username == username)
                .OrderByDescending(l => l.LostAt)
                .ToListAsync();
        }
        catch (SQLiteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            return new List<CasinoLostChip>();
        }
    }

    public async Task<CasinoLostChip> RecordLostChipAsync(string username, string chipName, string commandText, int deadlineSeconds)
    {
        EnsureWritable();
        await EnsureInitializedAsync();

        var lost = new CasinoLostChip
        {
            Username = username,
            ChipName = chipName.Trim(),
            CommandText = commandText.Trim(),
            DeadlineSeconds = deadlineSeconds,
            LostAt = DateTime.UtcNow
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(lost);
        return lost;
    }

    public async Task ClearLostChipsAsync(string username)
    {
        EnsureWritable();
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var rows = await GetLostChipsAsync(username);
        foreach (var row in rows)
            await conn.DeleteAsync(row);
    }
}
