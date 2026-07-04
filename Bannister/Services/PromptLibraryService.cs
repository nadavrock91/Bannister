using Bannister.Models;

namespace Bannister.Services;

public class PromptLibraryService
{
    private readonly DatabaseService _db;

    public PromptLibraryService(DatabaseService db)
    {
        _db = db;
    }

    public bool IsReadOnly => _db.IsReadOnly;

    private async Task EnsureTablesAsync()
    {
        if (_db.IsReadOnly) return;
        var conn = await _db.GetConnectionAsync();
        try { await conn.CreateTableAsync<PromptLibraryCategory>(); } catch { }
        try { await conn.CreateTableAsync<PromptLibraryPrompt>(); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE prompt_library_prompts ADD COLUMN SuccessCount INTEGER NOT NULL DEFAULT 0"); } catch { }
        try { await conn.ExecuteAsync("ALTER TABLE prompt_library_prompts ADD COLUMN FailureCount INTEGER NOT NULL DEFAULT 0"); } catch { }
    }

    public async Task<List<PromptLibraryCategory>> GetCategoriesAsync(string username)
    {
        await EnsureTablesAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<PromptLibraryCategory>()
            .Where(c => c.Username == username)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();
    }

    public async Task<PromptLibraryCategory?> GetCategoryAsync(int id)
    {
        await EnsureTablesAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<PromptLibraryCategory>()
            .Where(c => c.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<int> CreateCategoryAsync(string username, string name)
    {
        if (_db.IsReadOnly) return 0;
        await EnsureTablesAsync();
        var conn = await _db.GetConnectionAsync();
        var categories = await GetCategoriesAsync(username);
        var item = new PromptLibraryCategory
        {
            Username = username,
            Name = name.Trim(),
            SortOrder = categories.Count == 0 ? 1 : categories.Max(c => c.SortOrder) + 1,
            CreatedAt = DateTime.UtcNow
        };
        await conn.InsertAsync(item);
        return item.Id;
    }

    public async Task<bool> RenameCategoryAsync(int id, string newName)
    {
        if (_db.IsReadOnly) return false;
        await EnsureTablesAsync();
        var category = await GetCategoryAsync(id);
        if (category == null) return false;
        category.Name = newName.Trim();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(category);
        return true;
    }

    public async Task<bool> DeleteCategoryAsync(int id)
    {
        if (_db.IsReadOnly) return false;
        await EnsureTablesAsync();
        if (await GetPromptCountForCategoryAsync(id) > 0) return false;
        var category = await GetCategoryAsync(id);
        if (category == null) return false;
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync(category);
        return true;
    }

    public async Task<bool> DeleteCategoryAndPromptsAsync(int id)
    {
        if (_db.IsReadOnly) return false;
        await EnsureTablesAsync();
        var category = await GetCategoryAsync(id);
        if (category == null) return false;
        var conn = await _db.GetConnectionAsync();
        await conn.ExecuteAsync("DELETE FROM prompt_library_prompts WHERE CategoryId = ?", id);
        await conn.DeleteAsync(category);
        return true;
    }

    public async Task<bool> MoveCategoryUpAsync(int id, string username)
    {
        var categories = await GetCategoriesAsync(username);
        var index = categories.FindIndex(c => c.Id == id);
        if (index <= 0) return false;
        return await SwapCategorySortAsync(categories[index], categories[index - 1]);
    }

    public async Task<bool> MoveCategoryDownAsync(int id, string username)
    {
        var categories = await GetCategoriesAsync(username);
        var index = categories.FindIndex(c => c.Id == id);
        if (index < 0 || index >= categories.Count - 1) return false;
        return await SwapCategorySortAsync(categories[index], categories[index + 1]);
    }

    private async Task<bool> SwapCategorySortAsync(PromptLibraryCategory a, PromptLibraryCategory b)
    {
        if (_db.IsReadOnly) return false;
        var old = a.SortOrder;
        a.SortOrder = b.SortOrder;
        b.SortOrder = old;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(a);
        await conn.UpdateAsync(b);
        return true;
    }

    public async Task<List<PromptLibraryPrompt>> GetPromptsForCategoryAsync(int categoryId)
    {
        await EnsureTablesAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<PromptLibraryPrompt>()
            .Where(p => p.CategoryId == categoryId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();
    }

    public async Task<PromptLibraryPrompt?> GetPromptAsync(int id)
    {
        await EnsureTablesAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<PromptLibraryPrompt>()
            .Where(p => p.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<int> CreatePromptAsync(string username, int categoryId, string title, string body)
    {
        if (_db.IsReadOnly) return 0;
        await EnsureTablesAsync();
        var conn = await _db.GetConnectionAsync();
        var prompts = await GetPromptsForCategoryAsync(categoryId);
        var now = DateTime.UtcNow;
        var item = new PromptLibraryPrompt
        {
            Username = username,
            CategoryId = categoryId,
            Title = title.Trim(),
            Body = body.Trim(),
            SortOrder = prompts.Count == 0 ? 1 : prompts.Max(p => p.SortOrder) + 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        await conn.InsertAsync(item);
        return item.Id;
    }

    public async Task<bool> UpdatePromptAsync(int id, string title, string body)
    {
        if (_db.IsReadOnly) return false;
        await EnsureTablesAsync();
        var prompt = await GetPromptAsync(id);
        if (prompt == null) return false;
        prompt.Title = title.Trim();
        prompt.Body = body.Trim();
        prompt.UpdatedAt = DateTime.UtcNow;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(prompt);
        return true;
    }

    public async Task<bool> DeletePromptAsync(int id)
    {
        if (_db.IsReadOnly) return false;
        await EnsureTablesAsync();
        var prompt = await GetPromptAsync(id);
        if (prompt == null) return false;
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync(prompt);
        return true;
    }

    public async Task<bool> MovePromptUpAsync(int id, int categoryId)
    {
        var prompts = await GetPromptsForCategoryAsync(categoryId);
        var index = prompts.FindIndex(p => p.Id == id);
        if (index <= 0) return false;
        return await SwapPromptSortAsync(prompts[index], prompts[index - 1]);
    }

    public async Task<bool> MovePromptDownAsync(int id, int categoryId)
    {
        var prompts = await GetPromptsForCategoryAsync(categoryId);
        var index = prompts.FindIndex(p => p.Id == id);
        if (index < 0 || index >= prompts.Count - 1) return false;
        return await SwapPromptSortAsync(prompts[index], prompts[index + 1]);
    }

    private async Task<bool> SwapPromptSortAsync(PromptLibraryPrompt a, PromptLibraryPrompt b)
    {
        if (_db.IsReadOnly) return false;
        var old = a.SortOrder;
        a.SortOrder = b.SortOrder;
        b.SortOrder = old;
        a.UpdatedAt = DateTime.UtcNow;
        b.UpdatedAt = DateTime.UtcNow;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(a);
        await conn.UpdateAsync(b);
        return true;
    }

    public async Task<bool> MovePromptToCategoryAsync(int id, int newCategoryId)
    {
        if (_db.IsReadOnly) return false;
        await EnsureTablesAsync();
        var prompt = await GetPromptAsync(id);
        if (prompt == null) return false;
        var prompts = await GetPromptsForCategoryAsync(newCategoryId);
        prompt.CategoryId = newCategoryId;
        prompt.SortOrder = prompts.Count == 0 ? 1 : prompts.Max(p => p.SortOrder) + 1;
        prompt.UpdatedAt = DateTime.UtcNow;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(prompt);
        return true;
    }

    public async Task<int> GetPromptCountForCategoryAsync(int categoryId)
    {
        await EnsureTablesAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<PromptLibraryPrompt>()
            .Where(p => p.CategoryId == categoryId)
            .CountAsync();
    }

    public async Task<bool> IncrementSuccessAsync(int id)
    {
        if (_db.IsReadOnly) return false;
        await EnsureTablesAsync();
        var prompt = await GetPromptAsync(id);
        if (prompt == null) return false;
        prompt.SuccessCount++;
        prompt.UpdatedAt = DateTime.UtcNow;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(prompt);
        return true;
    }

    public async Task<bool> IncrementFailureAsync(int id)
    {
        if (_db.IsReadOnly) return false;
        await EnsureTablesAsync();
        var prompt = await GetPromptAsync(id);
        if (prompt == null) return false;
        prompt.FailureCount++;
        prompt.UpdatedAt = DateTime.UtcNow;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(prompt);
        return true;
    }

    public async Task<bool> SetStatsAsync(int id, int successCount, int failureCount)
    {
        if (_db.IsReadOnly) return false;
        await EnsureTablesAsync();
        var prompt = await GetPromptAsync(id);
        if (prompt == null) return false;
        prompt.SuccessCount = Math.Max(0, successCount);
        prompt.FailureCount = Math.Max(0, failureCount);
        prompt.UpdatedAt = DateTime.UtcNow;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(prompt);
        return true;
    }
}
