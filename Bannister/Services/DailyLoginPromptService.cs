using Bannister.Models;

namespace Bannister.Services;

public class DailyLoginPromptService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public DailyLoginPromptService(DatabaseService db)
    {
        _db = db;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<DailyLoginPrompt>();
        _initialized = true;
    }

    public async Task<List<DailyLoginPrompt>> GetPromptsAsync(string username, bool activeOnly = false)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var prompts = await conn.Table<DailyLoginPrompt>()
            .Where(p => p.Username == username)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .ToListAsync();

        return activeOnly ? prompts.Where(p => p.IsActive).ToList() : prompts;
    }

    public async Task<DailyLoginPrompt> AddPromptAsync(string username, string text, string fontColor, string backgroundColor)
    {
        await EnsureInitializedAsync();
        var prompts = await GetPromptsAsync(username);
        var prompt = new DailyLoginPrompt
        {
            Username = username,
            Text = text,
            FontColor = NormalizeHexColor(fontColor, "#FFFFFF"),
            BackgroundColor = NormalizeHexColor(backgroundColor, "#5B63EE"),
            SortOrder = prompts.Count == 0 ? 1 : prompts.Max(p => p.SortOrder) + 1
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(prompt);
        return prompt;
    }

    public async Task UpdatePromptAsync(DailyLoginPrompt prompt)
    {
        await EnsureInitializedAsync();
        prompt.FontColor = NormalizeHexColor(prompt.FontColor, "#FFFFFF");
        prompt.BackgroundColor = NormalizeHexColor(prompt.BackgroundColor, "#5B63EE");
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(prompt);
    }

    public async Task DeletePromptAsync(DailyLoginPrompt prompt)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync(prompt);
        await NormalizeOrderAsync(prompt.Username);
    }

    public async Task MovePromptAsync(DailyLoginPrompt prompt, int direction)
    {
        await EnsureInitializedAsync();
        var prompts = await GetPromptsAsync(prompt.Username);
        var index = prompts.FindIndex(p => p.Id == prompt.Id);
        var targetIndex = index + direction;
        if (index < 0 || targetIndex < 0 || targetIndex >= prompts.Count) return;

        (prompts[index].SortOrder, prompts[targetIndex].SortOrder) =
            (prompts[targetIndex].SortOrder, prompts[index].SortOrder);

        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(prompts[index]);
        await conn.UpdateAsync(prompts[targetIndex]);
        await NormalizeOrderAsync(prompt.Username);
    }

    private async Task NormalizeOrderAsync(string username)
    {
        var prompts = await GetPromptsAsync(username);
        var conn = await _db.GetConnectionAsync();

        for (int i = 0; i < prompts.Count; i++)
        {
            int desired = i + 1;
            if (prompts[i].SortOrder == desired) continue;
            prompts[i].SortOrder = desired;
            await conn.UpdateAsync(prompts[i]);
        }
    }

    private static string NormalizeHexColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;

        var color = value.Trim();
        if (!color.StartsWith("#")) color = "#" + color;

        if (color.Length != 7) return fallback;

        for (int i = 1; i < color.Length; i++)
        {
            if (!Uri.IsHexDigit(color[i])) return fallback;
        }

        return color.ToUpperInvariant();
    }
}
