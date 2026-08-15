using Bannister.Models;

namespace Bannister.Services;

public class EmotionService
{
    private readonly DatabaseService _db;

    public EmotionService(DatabaseService db)
    {
        _db = db;
    }

    public async Task EnsureInitializedAsync()
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<Emotion>();
    }

    public async Task<List<Emotion>> GetActiveEmotionsAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<Emotion>()
            .Where(e => e.Username == username && e.Status == "active")
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Emotion>> GetArchivedEmotionsAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<Emotion>()
            .Where(e => e.Username == username && e.Status == "archived")
            .OrderByDescending(e => e.ArchivedAt)
            .ToListAsync();
    }

    public async Task<Emotion> CreateEmotionAsync(string username, string name, string category, int intensity, string description = "", string notes = "")
    {
        var conn = await _db.GetConnectionAsync();
        var emotion = new Emotion
        {
            Username = username,
            Name = name,
            Category = category,
            Intensity = intensity,
            Description = description,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            Status = "active"
        };
        await conn.InsertAsync(emotion);
        return emotion;
    }

    public async Task ArchiveEmotionAsync(int emotionId)
    {
        var conn = await _db.GetConnectionAsync();
        var emotion = await conn.Table<Emotion>().Where(e => e.Id == emotionId).FirstOrDefaultAsync();
        if (emotion == null) return;
        emotion.Status = "archived";
        emotion.ArchivedAt = DateTime.UtcNow;
        await conn.UpdateAsync(emotion);
    }

    public async Task RestoreEmotionAsync(int emotionId)
    {
        var conn = await _db.GetConnectionAsync();
        var emotion = await conn.Table<Emotion>().Where(e => e.Id == emotionId).FirstOrDefaultAsync();
        if (emotion == null) return;
        emotion.Status = "active";
        emotion.ArchivedAt = null;
        await conn.UpdateAsync(emotion);
    }

    public async Task UpdateEmotionAsync(Emotion emotion)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(emotion);
    }

    public async Task DeleteEmotionAsync(int emotionId)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<Emotion>(emotionId);
    }
}
