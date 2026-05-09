using Bannister.Models;
using Bannister.Services;
using System.Globalization;

namespace Bannister.Views;

public class ListsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ListsService _lists;

    private Picker _listPicker;
    private Label _statusLabel;
    private VerticalStackLayout _gridToolbarContainer;
    private VerticalStackLayout _gridContainer;
    private List<UserList> _currentLists = new();
    private List<UserListItem> _currentItems = new();
    private UserList? _selectedList;
    private UserListItem? _selectedItem;
    private bool _isLoadingLists;

    public ListsPage(AuthService auth, ListsService lists)
    {
        _auth = auth;
        _lists = lists;
        Title = "Lists";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadListsAsync();
    }

    private void BuildUI()
    {
        var mainGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };

        var topRow = new Grid
        {
            Padding = new Thickness(12, 10, 12, 4),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        _listPicker = new Picker
        {
            Title = "Select list...",
            FontSize = 13,
            BackgroundColor = Colors.White
        };
        _listPicker.SelectedIndexChanged += async (s, e) => await OnListSelectedAsync();
        topRow.Add(_listPicker, 0, 0);

        var newListBtn = CreateHeaderButton("+ List", Color.FromArgb("#2E7D32"), Colors.White);
        newListBtn.Clicked += async (s, e) => await CreateListAsync();
        topRow.Add(newListBtn, 1, 0);

        var addItemBtn = CreateHeaderButton("+ Item", Color.FromArgb("#1565C0"), Colors.White);
        addItemBtn.Clicked += async (s, e) => await AddItemAsync();
        topRow.Add(addItemBtn, 2, 0);

        var deleteItemBtn = CreateHeaderButton("Delete", Color.FromArgb("#FFEBEE"), Color.FromArgb("#C62828"));
        deleteItemBtn.Clicked += async (s, e) => await DeleteSelectedItemAsync();
        topRow.Add(deleteItemBtn, 3, 0);

        var upBtn = CreateHeaderButton("Up", Color.FromArgb("#E8EAF6"), Color.FromArgb("#283593"));
        upBtn.Clicked += async (s, e) => await MoveSelectedItemAsync(-1);
        topRow.Add(upBtn, 4, 0);

        var downBtn = CreateHeaderButton("Down", Color.FromArgb("#E8EAF6"), Color.FromArgb("#283593"));
        downBtn.Clicked += async (s, e) => await MoveSelectedItemAsync(1);
        topRow.Add(downBtn, 5, 0);

        mainGrid.Add(topRow, 0, 0);

        var fixedArea = new VerticalStackLayout
        {
            Padding = new Thickness(12, 0, 12, 4),
            Spacing = 4
        };
        _statusLabel = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        };
        fixedArea.Children.Add(_statusLabel);
        _gridToolbarContainer = new VerticalStackLayout { Spacing = 4 };
        fixedArea.Children.Add(_gridToolbarContainer);
        mainGrid.Add(fixedArea, 0, 1);

        var scrollView = new ScrollView { Orientation = ScrollOrientation.Both };
        _gridContainer = new VerticalStackLayout { Padding = new Thickness(12, 4), Spacing = 4 };
        scrollView.Content = _gridContainer;
        mainGrid.Add(scrollView, 0, 2);

        Content = mainGrid;
    }

    private static Button CreateHeaderButton(string text, Color background, Color textColor)
    {
        return new Button
        {
            Text = text,
            FontSize = 12,
            HeightRequest = 38,
            BackgroundColor = background,
            TextColor = textColor,
            CornerRadius = 6,
            Padding = new Thickness(12, 0)
        };
    }

    private async Task LoadListsAsync()
    {
        _isLoadingLists = true;
        _currentLists = await _lists.GetListsAsync(_auth.CurrentUsername);
        var selectedId = _selectedList?.Id;

        _listPicker.Items.Clear();
        foreach (var list in _currentLists)
            _listPicker.Items.Add(list.Name);

        if (_currentLists.Count == 0)
        {
            _selectedList = null;
            _selectedItem = null;
            _statusLabel.Text = "No lists yet.";
            _gridToolbarContainer.Children.Clear();
            _gridContainer.Children.Clear();
            _gridContainer.Children.Add(new Label { Text = "Create a list to start.", TextColor = Color.FromArgb("#888"), Margin = new Thickness(8, 20) });
            _isLoadingLists = false;
            return;
        }

        int index = selectedId.HasValue ? _currentLists.FindIndex(l => l.Id == selectedId.Value) : 0;
        if (index < 0) index = 0;
        _listPicker.SelectedIndex = index;
        _selectedList = _currentLists[index];
        _isLoadingLists = false;
        await LoadItemsAsync();
    }

    private async Task OnListSelectedAsync()
    {
        if (_isLoadingLists)
            return;

        if (_listPicker.SelectedIndex < 0 || _listPicker.SelectedIndex >= _currentLists.Count)
            return;

        _selectedList = _currentLists[_listPicker.SelectedIndex];
        await LoadItemsAsync();
    }

    private async Task LoadItemsAsync()
    {
        _selectedItem = null;
        _gridToolbarContainer.Children.Clear();
        _gridContainer.Children.Clear();

        if (_selectedList == null)
            return;

        _currentItems = await _lists.GetItemsAsync(_selectedList.Id);
        _statusLabel.Text = $"{_selectedList.Name} | {_currentItems.Count} item(s)";

        if (_currentItems.Count == 0)
        {
            _gridContainer.Children.Add(new Label { Text = "No items in this list.", TextColor = Color.FromArgb("#888"), Margin = new Thickness(8, 20) });
            return;
        }

        var headers = new List<string> { "Id", "ListOrder", "Text", "Notes", "CreatedAt", "UpdatedAt" };
        var fullRows = _currentItems.Select(BuildGridRow).ToList();
        var displayRows = fullRows
            .Select(row => row.Select(v => v.Length > 50 ? v.Substring(0, 47) + "..." : v).ToList())
            .ToList();

        var dataGrid = DataGridView.Create(headers, displayRows)
            .WithHeaderStyle(Color.FromArgb("#5B63EE"), Colors.White)
            .WithAlternateRowColor(Color.FromArgb("#F8F9FF"))
            .WithColumnWidths(60, 240)
            .WithCellPadding(6)
            .WithFontSize(12, 12)
            .WithFullRows(fullRows)
            .WithIdColumn("Id")
            .OnCellTapped((s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _currentItems.Count)
                    _selectedItem = _currentItems[e.RowIndex];
            })
            .WithUpdateCallback(async (idValue, columnName, newValue) =>
            {
                bool updated = await _lists.UpdateItemCellAsync(idValue, columnName, newValue);
                if (updated) await LoadItemsAsync();
                return updated;
            })
            .Build();

        _gridToolbarContainer.Children.Add(dataGrid.ToolbarView);
        _gridContainer.Children.Add(dataGrid.GridView);
    }

    private static List<string> BuildGridRow(UserListItem item)
    {
        return new List<string>
        {
            item.Id.ToString(CultureInfo.InvariantCulture),
            item.SortOrder.ToString(CultureInfo.InvariantCulture),
            item.Text,
            item.Notes ?? "",
            item.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            item.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
        };
    }

    private async Task CreateListAsync()
    {
        string? name = await DisplayPromptAsync("New List", "List name:", "Create", "Cancel", placeholder: "List name...");
        if (string.IsNullOrWhiteSpace(name))
            return;

        _selectedList = await _lists.AddListAsync(_auth.CurrentUsername, name.Trim());
        await LoadListsAsync();
    }

    private async Task AddItemAsync()
    {
        if (_selectedList == null)
        {
            await DisplayAlert("No list", "Create or select a list first.", "OK");
            return;
        }

        string? text = await DisplayPromptAsync("New Item", "Item text:", "Add", "Cancel", placeholder: "Item...");
        if (string.IsNullOrWhiteSpace(text))
            return;

        string? notes = await DisplayPromptAsync("Notes", "Optional notes:", "Save", "Skip", initialValue: "");
        await _lists.AddItemAsync(_selectedList.Id, text.Trim(), notes ?? "");
        await LoadItemsAsync();
    }

    private async Task DeleteSelectedItemAsync()
    {
        if (_selectedItem == null)
        {
            await DisplayAlert("No item selected", "Select an item in the grid first.", "OK");
            return;
        }

        if (!await DisplayAlert("Delete item?", $"Delete \"{_selectedItem.Text}\"?", "Delete", "Cancel"))
            return;

        await _lists.DeleteItemAsync(_selectedItem);
        await LoadItemsAsync();
    }

    private async Task MoveSelectedItemAsync(int direction)
    {
        if (_selectedItem == null)
        {
            await DisplayAlert("No item selected", "Select an item in the grid first.", "OK");
            return;
        }

        await _lists.MoveItemAsync(_selectedItem, direction);
        await LoadItemsAsync();
    }
}
