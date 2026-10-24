using Bannister.Models;

namespace Bannister.Services;

public class AiServiceService
{
    private readonly DatabaseService _db;

    public AiServiceService(DatabaseService db)
    {
        _db = db;
    }

    public async Task<List<AiService>> GetAllAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<AiService>()
            .Where(item => item.Username == username)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(AiService item)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(item);
    }

    public async Task UpdateAsync(AiService item)
    {
        item.UpdatedAt = DateTime.UtcNow;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(item);
    }

    public async Task DeleteAsync(int id)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<AiService>(id);
    }
}
