using Bannister.Models;

namespace Bannister.Services;

public class ClipPromptTemplateService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public ClipPromptTemplateService(DatabaseService db) => _db = db;

    public async Task InitAsync()
    {
        if (_initialized) return;
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<ClipPromptTemplate>();
        _initialized = true;
    }

    public async Task<List<ClipPromptTemplate>> GetAllTemplatesAsync()
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<ClipPromptTemplate>()
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task SaveTemplateAsync(ClipPromptTemplate template)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        if (template.Id == 0)
            await conn.InsertAsync(template);
        else
            await conn.UpdateAsync(template);
    }

    public async Task DeleteTemplateAsync(int id)
    {
        await InitAsync();
        var conn = await _db.GetConnectionAsync();
        var template = await conn.FindAsync<ClipPromptTemplate>(id);
        if (template != null)
            await conn.DeleteAsync(template);
    }
}
