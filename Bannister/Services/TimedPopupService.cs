using Bannister.Models;

namespace Bannister.Services;

public class TimedPopupService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public TimedPopupService(DatabaseService db)
    {
        _db = db;
    }

    private async Task InitAsync()
    {
        if (_initialized) return;
        await _db.EnsureTableAsync<TimedPopup>();
        _initialized = true;
    }

    public async Task<List<TimedPopup>> GetPopupsAsync(string username)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<TimedPopup>()
            .Where(popup => popup.Username == username)
            .OrderBy(popup => popup.Id)
            .ToListAsync();
    }

    public async Task AddPopupAsync(TimedPopup popup)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(popup);
    }

    public async Task UpdatePopupAsync(TimedPopup popup)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(popup);
    }

    public async Task DeletePopupAsync(int id)
    {
        await InitAsync();
        if (_db.IsReadOnly) return;
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<TimedPopup>(id);
    }
}
