using Bannister.Models;
using Bannister.Services;
using System.Globalization;

namespace Bannister.Views;

public class AssetsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly MoneyManagementService _money;

    private Label _summaryLabel;
    private Label _selectedNotesLabel;
    private VerticalStackLayout _toolbarContainer;
    private VerticalStackLayout _gridContainer;
    private List<AssetItem> _currentAssets = new();
    private AssetItem? _selectedAsset;

    public AssetsPage(AuthService auth, MoneyManagementService money)
    {
        _auth = auth;
        _money = money;
        Title = "Assets";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAssetsAsync();
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
            Text = "Assets total: $0.00",
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
            Padding = new Thickness(14, 0),
            IsVisible = !_money.IsReadOnly
        };
        addBtn.Clicked += async (s, e) => await AddAssetAsync();
        header.Add(addBtn, 1, 0);

        var deleteBtn = new Button
        {
            Text = "Delete",
            FontSize = 13,
            HeightRequest = 38,
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            CornerRadius = 6,
            Padding = new Thickness(14, 0),
            IsVisible = !_money.IsReadOnly
        };
        deleteBtn.Clicked += async (s, e) => await DeleteSelectedAssetAsync();
        header.Add(deleteBtn, 2, 0);

        mainGrid.Add(header, 0, 0);

        var toolbarArea = new VerticalStackLayout
        {
            Padding = new Thickness(12, 0, 12, 4),
            Spacing = 6
        };

        _selectedNotesLabel = new Label
        {
            Text = "Select an asset to view notes.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        toolbarArea.Children.Add(_selectedNotesLabel);

        _toolbarContainer = new VerticalStackLayout { Spacing = 4 };
        toolbarArea.Children.Add(_toolbarContainer);
        mainGrid.Add(toolbarArea, 0, 1);

        var scrollView = new ScrollView { Orientation = ScrollOrientation.Both };
        _gridContainer = new VerticalStackLayout { Padding = new Thickness(12, 4), Spacing = 4 };
        scrollView.Content = _gridContainer;
        mainGrid.Add(scrollView, 0, 2);

        Content = mainGrid;
    }

    private async Task LoadAssetsAsync()
    {
        _toolbarContainer.Children.Clear();
        _gridContainer.Children.Clear();
        _gridContainer.Children.Add(new ActivityIndicator { IsRunning = true, Color = Color.FromArgb("#2E7D32") });

        _currentAssets = await _money.GetAssetsAsync(_auth.CurrentUsername);
        _selectedAsset = null;
        UpdateSummary();
        UpdateSelectedNotes();
        BuildDataGrid();
    }

    private void UpdateSummary()
    {
        _summaryLabel.Text = $"Assets total: {FormatMoney(_money.SumAssets(_currentAssets))}";
    }

    private void UpdateSelectedNotes()
    {
        if (_selectedAsset == null)
        {
            _selectedNotesLabel.Text = "Select an asset to view notes.";
            return;
        }

        var notes = string.IsNullOrWhiteSpace(_selectedAsset.Notes) ? "(no notes)" : _selectedAsset.Notes;
        _selectedNotesLabel.Text = $"Notes for {_selectedAsset.Name}: {notes}";
    }

    private void BuildDataGrid()
    {
        _toolbarContainer.Children.Clear();
        _gridContainer.Children.Clear();

        if (_currentAssets.Count == 0)
        {
            _gridContainer.Children.Add(new Label
            {
                Text = "No assets yet. Click + Add to log one.",
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(20)
            });
            return;
        }

        var headers = new List<string> { "Id", "Name", "Units", "Value/Unit", "Total", "Notes", "CreatedAt" };
        var displayRows = new List<List<string>>();
        var fullRows = new List<List<string>>();

        foreach (var asset in _currentAssets)
        {
            var row = BuildAssetRow(asset);
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
                if (e.RowIndex >= 0 && e.RowIndex < _currentAssets.Count)
                {
                    _selectedAsset = _currentAssets[e.RowIndex];
                    UpdateSelectedNotes();
                }
            })
            .WithUpdateCallback(UpdateAssetGridCellAsync)
            .Build();

        _toolbarContainer.Children.Add(dataGrid.ToolbarView);
        _gridContainer.Children.Add(dataGrid.GridView);
    }

    private static List<string> BuildAssetRow(AssetItem asset)
    {
        return new List<string>
        {
            asset.Id.ToString(CultureInfo.InvariantCulture),
            asset.Name,
            asset.Units.ToString("0.####", CultureInfo.InvariantCulture),
            FormatMoney(asset.ValuePerUnit),
            FormatMoney(asset.Units * asset.ValuePerUnit),
            asset.Notes ?? "",
            asset.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
        };
    }

    private async Task<bool> UpdateAssetGridCellAsync(string idValue, string columnName, string newValue)
    {
        if (_money.IsReadOnly)
            return false;

        bool updated = await _money.UpdateAssetCellAsync(_auth.CurrentUsername, idValue, columnName, newValue);
        if (!updated)
            return false;

        _currentAssets = await _money.GetAssetsAsync(_auth.CurrentUsername);
        _selectedAsset = _currentAssets.FirstOrDefault(a => a.Id.ToString(CultureInfo.InvariantCulture) == idValue);
        UpdateSummary();
        UpdateSelectedNotes();
        BuildDataGrid();
        return true;
    }

    private async Task AddAssetAsync()
    {
        string? name = await DisplayPromptAsync("New Asset", "Asset name:", "Add", "Cancel", placeholder: "Stocks, cash, property...");
        if (string.IsNullOrWhiteSpace(name))
            return;

        string? unitsText = await DisplayPromptAsync("Units", "How many units?", "Save", "Cancel", keyboard: Keyboard.Numeric, placeholder: "0");
        if (!TryParseDouble(unitsText, out double units))
        {
            await DisplayAlert("Invalid units", "Enter a valid number for units.", "OK");
            return;
        }

        string? valueText = await DisplayPromptAsync("Value Per Unit", "Value per unit:", "Save", "Cancel", keyboard: Keyboard.Numeric, placeholder: "0.00");
        if (!TryParseDouble(valueText, out double valuePerUnit))
        {
            await DisplayAlert("Invalid value", "Enter a valid number for value per unit.", "OK");
            return;
        }

        string? notes = await DisplayPromptAsync("Notes", "Optional notes:", "Save", "Skip", initialValue: "");

        await _money.AddAssetAsync(_auth.CurrentUsername, name, units, valuePerUnit, notes ?? "");
        await LoadAssetsAsync();
    }

    private async Task DeleteSelectedAssetAsync()
    {
        if (_selectedAsset == null)
        {
            await DisplayAlert("No row selected", "Select a row in the grid first.", "OK");
            return;
        }

        if (!await DisplayAlert("Delete asset?", $"Delete \"{_selectedAsset.Name}\"?", "Delete", "Cancel"))
            return;

        await _money.DeleteAssetAsync(_selectedAsset.Id);
        await LoadAssetsAsync();
    }

    private static bool TryParseDouble(string? value, out double result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = 0;
            return false;
        }

        var cleaned = value.Trim().Replace("$", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal);
        return double.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result)
            || double.TryParse(cleaned, NumberStyles.Currency, CultureInfo.CurrentCulture, out result);
    }

    private static string FormatMoney(double value)
    {
        return value.ToString("C2", CultureInfo.CurrentCulture);
    }
}
