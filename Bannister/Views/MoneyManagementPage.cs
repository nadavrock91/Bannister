using Bannister.Models;
using Bannister.Services;
using System.Globalization;

namespace Bannister.Views;

public class MoneyManagementPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly MoneyManagementService _money;

    private Label _summaryLabel;
    private Switch _showInactiveSwitch;
    private VerticalStackLayout _toolbarContainer;
    private VerticalStackLayout _gridContainer;
    private List<MonthlyExpense> _currentExpenses = new();
    private MonthlyExpense? _selectedExpense;

    public MoneyManagementPage(AuthService auth, MoneyManagementService money)
    {
        _auth = auth;
        _money = money;
        Title = "Money Management";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadExpensesAsync();
    }

    private void BuildUI()
    {
        var mainGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            }
        };

        var header = new Grid
        {
            Padding = new Thickness(12, 10, 12, 6),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };

        _summaryLabel = new Label
        {
            Text = "Monthly expenses: $0.00",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1B5E20"),
            VerticalOptions = LayoutOptions.Center
        };
        header.Add(_summaryLabel, 0, 0);

        var addBtn = new Button
        {
            Text = "+ Add",
            FontSize = 13,
            HeightRequest = 38,
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 6,
            Padding = new Thickness(14, 0)
        };
        addBtn.Clicked += async (s, e) => await AddExpenseAsync();
        header.Add(addBtn, 1, 0);

        var deleteBtn = new Button
        {
            Text = "Delete",
            FontSize = 13,
            HeightRequest = 38,
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            CornerRadius = 6,
            Padding = new Thickness(14, 0)
        };
        deleteBtn.Clicked += async (s, e) => await DeleteSelectedExpenseAsync();
        header.Add(deleteBtn, 2, 0);

        mainGrid.Add(header, 0, 0);

        var toolbarArea = new VerticalStackLayout
        {
            Padding = new Thickness(12, 0, 12, 4),
            Spacing = 6
        };

        var optionsRow = new HorizontalStackLayout { Spacing = 8 };
        optionsRow.Children.Add(new Label
        {
            Text = "Show inactive",
            FontSize = 12,
            TextColor = Color.FromArgb("#555"),
            VerticalOptions = LayoutOptions.Center
        });
        _showInactiveSwitch = new Switch { IsToggled = true, VerticalOptions = LayoutOptions.Center };
        _showInactiveSwitch.Toggled += async (s, e) => await LoadExpensesAsync();
        optionsRow.Children.Add(_showInactiveSwitch);
        toolbarArea.Children.Add(optionsRow);

        _toolbarContainer = new VerticalStackLayout { Spacing = 4 };
        toolbarArea.Children.Add(_toolbarContainer);
        mainGrid.Add(toolbarArea, 0, 1);

        var scrollView = new ScrollView { Orientation = ScrollOrientation.Both };
        _gridContainer = new VerticalStackLayout { Padding = new Thickness(12, 4), Spacing = 4 };
        scrollView.Content = _gridContainer;
        mainGrid.Add(scrollView, 0, 2);

        Content = mainGrid;
    }

    private async Task LoadExpensesAsync()
    {
        _toolbarContainer.Children.Clear();
        _gridContainer.Children.Clear();
        _gridContainer.Children.Add(new ActivityIndicator { IsRunning = true, Color = Color.FromArgb("#2E7D32") });

        _currentExpenses = await _money.GetMonthlyExpensesAsync(_auth.CurrentUsername, _showInactiveSwitch.IsToggled);
        _selectedExpense = null;
        UpdateSummary();
        BuildDataGrid();
    }

    private void UpdateSummary()
    {
        var activeTotal = _money.SumMonthlyExpenses(_currentExpenses);
        var shownTotal = _money.SumMonthlyExpenses(_currentExpenses, activeOnly: false);
        int activeCount = _currentExpenses.Count(e => e.IsActive);

        _summaryLabel.Text = _showInactiveSwitch.IsToggled && shownTotal != activeTotal
            ? $"Monthly active: {FormatMoney(activeTotal)} | shown: {FormatMoney(shownTotal)} | {activeCount}/{_currentExpenses.Count} active"
            : $"Monthly expenses: {FormatMoney(activeTotal)} | {activeCount} active";
    }

    private void BuildDataGrid()
    {
        _toolbarContainer.Children.Clear();
        _gridContainer.Children.Clear();

        if (_currentExpenses.Count == 0)
        {
            _gridContainer.Children.Add(new Label
            {
                Text = "No routine monthly expenses. Click + Add to log one.",
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(20)
            });
            return;
        }

        var headers = new List<string> { "Id", "Name", "Category", "Amount", "DueDay", "IsActive", "Notes", "CreatedAt", "UpdatedAt" };
        var displayRows = new List<List<string>>();
        var fullRows = new List<List<string>>();

        foreach (var expense in _currentExpenses)
        {
            var row = BuildExpenseRow(expense);
            fullRows.Add(row);
            displayRows.Add(row.Select(v => v.Length > 50 ? v.Substring(0, 47) + "..." : v).ToList());
        }

        var dataGrid = DataGridView.Create(headers, displayRows)
            .WithHeaderStyle(Color.FromArgb("#2E7D32"), Colors.White)
            .WithAlternateRowColor(Color.FromArgb("#F1F8E9"))
            .WithColumnWidths(60, 220)
            .WithCellPadding(6)
            .WithFontSize(12, 12)
            .WithFullRows(fullRows)
            .WithIdColumn("Id")
            .OnCellTapped((s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _currentExpenses.Count)
                    _selectedExpense = _currentExpenses[e.RowIndex];
            })
            .WithUpdateCallback(UpdateExpenseGridCellAsync)
            .Build();

        _toolbarContainer.Children.Add(dataGrid.ToolbarView);
        _gridContainer.Children.Add(dataGrid.GridView);
    }

    private static List<string> BuildExpenseRow(MonthlyExpense expense)
    {
        return new List<string>
        {
            expense.Id.ToString(CultureInfo.InvariantCulture),
            expense.Name,
            expense.Category,
            expense.Amount.ToString("0.##", CultureInfo.InvariantCulture),
            expense.DueDay.ToString(CultureInfo.InvariantCulture),
            expense.IsActive ? "true" : "false",
            expense.Notes ?? "",
            expense.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            expense.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
        };
    }

    private async Task<bool> UpdateExpenseGridCellAsync(string idValue, string columnName, string newValue)
    {
        bool updated = await _money.UpdateCellAsync(_auth.CurrentUsername, idValue, columnName, newValue);
        if (!updated)
            return false;

        _currentExpenses = await _money.GetMonthlyExpensesAsync(_auth.CurrentUsername, _showInactiveSwitch.IsToggled);
        UpdateSummary();
        return true;
    }

    private async Task AddExpenseAsync()
    {
        string? name = await DisplayPromptAsync("New Monthly Expense", "Expense name:", "Add", "Cancel", placeholder: "Rent, subscription, insurance...");
        if (string.IsNullOrWhiteSpace(name))
            return;

        string? amountText = await DisplayPromptAsync("Amount", "Monthly amount:", "Save", "Cancel", keyboard: Keyboard.Numeric, placeholder: "0.00");
        if (!TryParseAmount(amountText, out decimal amount))
        {
            await DisplayAlert("Invalid amount", "Enter a valid number for the monthly amount.", "OK");
            return;
        }

        string? category = await DisplayPromptAsync("Category", "Category:", "Save", "Skip", initialValue: "General");
        string? dueDayText = await DisplayPromptAsync("Due Day", "Day of month:", "Save", "Skip", keyboard: Keyboard.Numeric, initialValue: "1");
        int dueDay = int.TryParse(dueDayText, out int parsedDay) ? parsedDay : 1;
        string? notes = await DisplayPromptAsync("Notes", "Optional notes:", "Save", "Skip", initialValue: "");

        await _money.AddMonthlyExpenseAsync(_auth.CurrentUsername, name, amount, category ?? "General", dueDay, notes ?? "");
        await LoadExpensesAsync();
    }

    private async Task DeleteSelectedExpenseAsync()
    {
        if (_selectedExpense == null)
        {
            await DisplayAlert("No row selected", "Select a row in the grid first.", "OK");
            return;
        }

        if (!await DisplayAlert("Delete expense?", $"Delete \"{_selectedExpense.Name}\"?", "Delete", "Cancel"))
            return;

        await _money.DeleteMonthlyExpenseAsync(_selectedExpense.Id);
        await LoadExpensesAsync();
    }

    private static bool TryParseAmount(string? value, out decimal amount)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            amount = 0;
            return false;
        }

        var cleaned = value.Trim().Replace("$", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal);
        return decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out amount)
            || decimal.TryParse(cleaned, NumberStyles.Currency, CultureInfo.CurrentCulture, out amount);
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("C2", CultureInfo.CurrentCulture);
    }
}
