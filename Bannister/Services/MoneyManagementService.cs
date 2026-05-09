using Bannister.Models;
using SQLite;
using System.Globalization;

namespace Bannister.Services;

public class MoneyManagementService
{
    private readonly DatabaseService _db;
    private bool _initialized;

    public MoneyManagementService(DatabaseService db)
    {
        _db = db;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<MonthlyExpense>();
        _initialized = true;
    }

    public async Task<List<MonthlyExpense>> GetMonthlyExpensesAsync(string username, bool includeInactive = true)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        var rows = await conn.Table<MonthlyExpense>()
            .Where(e => e.Username == username)
            .ToListAsync();

        return rows
            .Where(e => includeInactive || e.IsActive)
            .OrderBy(e => e.DueDay)
            .ThenBy(e => e.Category)
            .ThenBy(e => e.Name)
            .ToList();
    }

    public async Task<MonthlyExpense> AddMonthlyExpenseAsync(string username, string name, decimal amount, string category = "General", int dueDay = 1, string notes = "")
    {
        await EnsureInitializedAsync();
        var expense = new MonthlyExpense
        {
            Username = username,
            Name = name.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim(),
            Amount = amount,
            DueDay = Math.Clamp(dueDay, 1, 31),
            Notes = notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(expense);
        return expense;
    }

    public async Task DeleteMonthlyExpenseAsync(int id)
    {
        await EnsureInitializedAsync();
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<MonthlyExpense>(id);
    }

    public async Task<bool> UpdateCellAsync(string username, string idValue, string columnName, string newValue)
    {
        await EnsureInitializedAsync();
        if (!int.TryParse(idValue, out int id))
            return false;

        var conn = await _db.GetConnectionAsync();
        var expense = await conn.FindAsync<MonthlyExpense>(id);
        if (expense == null || !string.Equals(expense.Username, username, StringComparison.Ordinal))
            return false;

        switch (columnName)
        {
            case "Name":
                if (string.IsNullOrWhiteSpace(newValue)) return false;
                expense.Name = newValue.Trim();
                break;
            case "Category":
                expense.Category = string.IsNullOrWhiteSpace(newValue) ? "General" : newValue.Trim();
                break;
            case "Amount":
                if (!TryParseAmount(newValue, out decimal amount)) return false;
                expense.Amount = amount;
                break;
            case "DueDay":
                if (!int.TryParse(newValue.Trim(), out int dueDay)) return false;
                expense.DueDay = Math.Clamp(dueDay, 1, 31);
                break;
            case "IsActive":
                if (!TryParseBool(newValue, out bool active)) return false;
                expense.IsActive = active;
                break;
            case "Notes":
                expense.Notes = newValue.Trim();
                break;
            default:
                return false;
        }

        expense.UpdatedAt = DateTime.UtcNow;
        await conn.UpdateAsync(expense);
        return true;
    }

    public decimal SumMonthlyExpenses(IEnumerable<MonthlyExpense> expenses, bool activeOnly = true)
    {
        return expenses
            .Where(e => !activeOnly || e.IsActive)
            .Sum(e => e.Amount);
    }

    private static bool TryParseAmount(string value, out decimal amount)
    {
        var cleaned = value.Trim().Replace("$", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal);
        return decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out amount)
            || decimal.TryParse(cleaned, NumberStyles.Currency, CultureInfo.CurrentCulture, out amount);
    }

    private static bool TryParseBool(string value, out bool result)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized is "true" or "yes" or "y" or "1" or "active")
        {
            result = true;
            return true;
        }

        if (normalized is "false" or "no" or "n" or "0" or "inactive")
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }
}
