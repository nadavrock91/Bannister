using Bannister.Models;

namespace Bannister.Services;

public class ListsService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public ListsService(DatabaseService db)
    {
        _db = db;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<UserList>();
        await conn.CreateTableAsync<UserListItem>();
        _initialized = true;
    }

    public async Task<List<UserList>> GetListsAsync(string username)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<UserList>()
            .Where(l => l.Username == username)
            .OrderBy(l => l.Name)
            .ToListAsync();
    }

    public async Task<UserList> AddListAsync(string username, string name)
    {
        await EnsureInitializedAsync();
        var list = new UserList
        {
            Username = username,
            Name = name.Trim(),
            CreatedAt = DateTime.Now
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(list);
        return list;
    }

    public async Task DeleteListAsync(UserList list)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var items = await GetItemsAsync(list.Id);
        foreach (var item in items)
            await conn.DeleteAsync(item);

        await conn.DeleteAsync(list);
    }

    public async Task<List<UserListItem>> GetItemsAsync(int listId)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<UserListItem>()
            .Where(i => i.ListId == listId)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<UserListItem> AddItemAsync(int listId, string text, string notes = "")
    {
        await EnsureInitializedAsync();
        var items = await GetItemsAsync(listId);
        var item = new UserListItem
        {
            ListId = listId,
            SortOrder = items.Count == 0 ? 1 : items.Max(i => i.SortOrder) + 1,
            Text = text.Trim(),
            Notes = notes.Trim(),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(item);
        return item;
    }

    public async Task UpdateItemAsync(UserListItem item)
    {
        await EnsureInitializedAsync();
        item.UpdatedAt = DateTime.Now;
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(item);
    }

    public async Task DeleteItemAsync(UserListItem item)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync(item);
        await NormalizeSortOrderAsync(item.ListId);
    }

    public async Task<bool> UpdateItemCellAsync(string idValue, string columnName, string newValue)
    {
        await EnsureInitializedAsync();
        if (!int.TryParse(idValue, out int id))
            return false;

        var conn = await _db.GetConnectionAsync();
        var item = await conn.FindAsync<UserListItem>(id);
        if (item == null)
            return false;

        switch (columnName)
        {
            case "ListOrder":
                if (!int.TryParse(newValue.Trim(), out int order)) return false;
                item.SortOrder = Math.Max(1, order);
                break;
            case "Text":
                if (string.IsNullOrWhiteSpace(newValue)) return false;
                item.Text = newValue.Trim();
                break;
            case "Notes":
                item.Notes = newValue.Trim();
                break;
            default:
                return false;
        }

        await UpdateItemAsync(item);
        await NormalizeSortOrderAsync(item.ListId);
        return true;
    }

    public async Task MoveItemAsync(UserListItem item, int direction)
    {
        await EnsureInitializedAsync();
        var items = await GetItemsAsync(item.ListId);
        var index = items.FindIndex(i => i.Id == item.Id);
        var otherIndex = index + direction;

        if (index < 0 || otherIndex < 0 || otherIndex >= items.Count)
            return;

        var other = items[otherIndex];
        (item.SortOrder, other.SortOrder) = (other.SortOrder, item.SortOrder);

        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(item);
        await conn.UpdateAsync(other);
        await NormalizeSortOrderAsync(item.ListId);
    }

    private async Task NormalizeSortOrderAsync(int listId)
    {
        var items = await GetItemsAsync(listId);
        var conn = await _db.GetConnectionAsync();

        for (int i = 0; i < items.Count; i++)
        {
            int desired = i + 1;
            if (items[i].SortOrder == desired)
                continue;

            items[i].SortOrder = desired;
            items[i].UpdatedAt = DateTime.Now;
            await conn.UpdateAsync(items[i]);
        }
    }
}
