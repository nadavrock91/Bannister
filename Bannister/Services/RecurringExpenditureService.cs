using Bannister.Models;

namespace Bannister.Services;

public class RecurringExpenditureService
{
    private readonly DatabaseService _db;

    public RecurringExpenditureService(DatabaseService db)
    {
        _db = db;
    }

    public async Task<List<RecurringExpenditure>> GetAllAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<RecurringExpenditure>()
            .Where(item => item.Username == username)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(RecurringExpenditure item)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(item);
    }

    public async Task UpdateAsync(RecurringExpenditure item)
    {
        item.UpdatedAt = DateTime.UtcNow;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(item);
    }

    public async Task DeleteAsync(int id)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<RecurringExpenditure>(id);
    }
}
