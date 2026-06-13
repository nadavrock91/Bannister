using Bannister.Models;

namespace Bannister.Services;

public class CustomGameService
{
    private readonly DatabaseService _db;

    public CustomGameService(DatabaseService db)
    {
        _db = db;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private void EnsureWritable()
    {
        if (_db.IsReadOnly)
        {
            throw new ReadOnlyDatabaseException("Custom Games are read-only on secondary devices.");
        }
    }

    public async Task<List<CustomGameSummary>> GetCustomGamesAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        var games = await conn.Table<CustomGame>()
            .Where(g => g.Username == username)
            .ToListAsync();
        var instances = await conn.Table<CustomGameInstance>()
            .Where(i => i.Username == username && !i.InProgress)
            .ToListAsync();

        return games
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var gameInstances = instances.Where(i => i.GameId == g.Id).ToList();
                int? topScore = null;
                if (gameInstances.Count > 0)
                {
                    topScore = g.HigherIsBetter
                        ? gameInstances.Max(i => i.FinalScore)
                        : gameInstances.Min(i => i.FinalScore);
                }

                return new CustomGameSummary(g, gameInstances.Count, topScore);
            })
            .ToList();
    }

    public async Task<CustomGame?> GetGameAsync(int id)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<CustomGame>().FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<CustomGame> AddGameAsync(string username, string name, int endType, int? endValueSeconds, DateTime? endValueDate, int? endValueAmount, bool higherIsBetter)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        var game = new CustomGame
        {
            Username = username,
            Name = name,
            EndType = endType,
            EndValueSeconds = endValueSeconds,
            EndValueDate = endValueDate,
            EndValueAmount = endValueAmount,
            HigherIsBetter = higherIsBetter,
            CreatedAt = DateTime.UtcNow
        };
        await conn.InsertAsync(game);
        return game;
    }

    public async Task UpdateGameAsync(CustomGame game)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(game);
    }

    public async Task DeleteGameAsync(int gameId)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        var buttons = await conn.Table<CustomGameButton>().Where(b => b.GameId == gameId).ToListAsync();
        var instances = await conn.Table<CustomGameInstance>().Where(i => i.GameId == gameId).ToListAsync();
        foreach (var button in buttons)
        {
            await conn.DeleteAsync(button);
        }
        foreach (var instance in instances)
        {
            await conn.DeleteAsync(instance);
        }

        var game = await GetGameAsync(gameId);
        if (game != null)
        {
            await conn.DeleteAsync(game);
        }
    }

    public async Task<List<CustomGameButton>> GetButtonsAsync(int gameId)
    {
        var conn = await _db.GetConnectionAsync();
        var buttons = await conn.Table<CustomGameButton>().Where(b => b.GameId == gameId).ToListAsync();
        return buttons
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<CustomGameButton> AddButtonAsync(int gameId, string label, int pointValue, string color, int sortOrder)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        var button = new CustomGameButton
        {
            GameId = gameId,
            Label = label,
            PointValue = pointValue,
            Color = color,
            SortOrder = sortOrder
        };
        await conn.InsertAsync(button);
        return button;
    }

    public async Task UpdateButtonAsync(CustomGameButton button)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(button);
    }

    public async Task DeleteButtonAsync(int buttonId)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        var button = await conn.Table<CustomGameButton>().FirstOrDefaultAsync(b => b.Id == buttonId);
        if (button != null)
        {
            await conn.DeleteAsync(button);
        }
    }

    public async Task<CustomGameInstance?> GetActiveInstanceAsync(int gameId, string username)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<CustomGameInstance>()
            .FirstOrDefaultAsync(i => i.GameId == gameId && i.Username == username && i.InProgress);
    }

    public async Task<CustomGameInstance> StartInstanceAsync(int gameId, string username)
    {
        EnsureWritable();
        var active = await GetActiveInstanceAsync(gameId, username);
        if (active != null)
        {
            return active;
        }

        var conn = await _db.GetConnectionAsync();
        var instance = new CustomGameInstance
        {
            GameId = gameId,
            Username = username,
            StartedAt = DateTime.UtcNow,
            FinalScore = 0,
            InProgress = true
        };
        await conn.InsertAsync(instance);
        return instance;
    }

    public async Task<CustomGameInstance?> UpdateInstanceScoreAsync(int instanceId, int scoreDelta)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        var instance = await conn.Table<CustomGameInstance>().FirstOrDefaultAsync(i => i.Id == instanceId);
        if (instance == null || !instance.InProgress)
        {
            return instance;
        }

        instance.FinalScore += scoreDelta;
        await conn.UpdateAsync(instance);
        return instance;
    }

    public async Task<CustomGameInstance?> EndInstanceAsync(int instanceId)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        var instance = await conn.Table<CustomGameInstance>().FirstOrDefaultAsync(i => i.Id == instanceId);
        if (instance == null)
        {
            return null;
        }

        instance.InProgress = false;
        instance.EndedAt = DateTime.UtcNow;
        await conn.UpdateAsync(instance);
        return instance;
    }

    public async Task DeleteInstanceAsync(int instanceId)
    {
        EnsureWritable();
        var conn = await _db.GetConnectionAsync();
        var instance = await conn.Table<CustomGameInstance>().FirstOrDefaultAsync(i => i.Id == instanceId);
        if (instance != null)
        {
            await conn.DeleteAsync(instance);
        }
    }

    public async Task<List<CustomGameInstance>> GetTopScoresAsync(int gameId, string username, bool sortHigherFirst)
    {
        var conn = await _db.GetConnectionAsync();
        var scores = await conn.Table<CustomGameInstance>()
            .Where(i => i.GameId == gameId && i.Username == username && !i.InProgress)
            .ToListAsync();

        return sortHigherFirst
            ? scores.OrderByDescending(i => i.FinalScore).ThenBy(i => i.EndedAt ?? i.StartedAt).ToList()
            : scores.OrderBy(i => i.FinalScore).ThenBy(i => i.EndedAt ?? i.StartedAt).ToList();
    }
}

public sealed record CustomGameSummary(CustomGame Game, int FinishedInstanceCount, int? TopScore);
