using Bannister.Models;
using Bannister.Services;
using System.Globalization;

namespace Bannister.Views;

public class ListsPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly ListsService _lists;
    private readonly IdeaLoggerService? _ideaLogger;

    private Picker _listPicker;
    private Label _statusLabel;
    private VerticalStackLayout _gridToolbarContainer;
    private VerticalStackLayout _gridContainer;
    private List<UserList> _currentLists = new();
    private List<UserListItem> _currentItems = new();
    private UserList? _selectedList;
    private UserListItem? _selectedItem;
    private bool _isLoadingLists;

    public ListsPage(AuthService auth, ListsService lists, IdeaLoggerService? ideaLogger = null)
    {
        _auth = auth;
        _lists = lists;
        _ideaLogger = ideaLogger
            ?? Application.Current?.Handler?.MauiContext?.Services
                .GetService<IdeaLoggerService>();
        Title = "Lists";

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
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        _listPicker = (new Picker
        {
            Title = "Select list...",
            FontSize = 13
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        _listPicker.SelectedIndexChanged += async (s, e) => await OnListSelectedAsync();
        topRow.Add(_listPicker, 0, 0);

        var newListBtn = CreateHeaderButton("+ List", ThemeKey.SuccessButtonBackground, ThemeKey.ButtonPrimaryText);
        newListBtn.Clicked += async (s, e) => await CreateListAsync();
        topRow.Add(newListBtn, 1, 0);

        var addItemBtn = CreateHeaderButton("+ Item", ThemeKey.ButtonPrimaryBackground, ThemeKey.ButtonPrimaryText);
        addItemBtn.Clicked += async (s, e) => await AddItemAsync();
        topRow.Add(addItemBtn, 2, 0);

        var deleteItemBtn = CreateHeaderButton("Delete Item", ThemeKey.ErrorBackground, ThemeKey.ErrorText);
        deleteItemBtn.Clicked += async (s, e) => await DeleteSelectedItemAsync();
        topRow.Add(deleteItemBtn, 3, 0);

        var deleteListBtn = CreateHeaderButton("Delete List", ThemeKey.ErrorBackground, ThemeKey.ErrorText);
        deleteListBtn.Clicked += async (_, _) =>
        {
            if (_selectedList == null) return;
            bool confirm = await DisplayAlert(
                "Delete List",
                $"Delete \"{_selectedList.Name}\" and all its items? This cannot be undone.",
                "Delete", "Cancel");
            if (!confirm) return;
            await _lists.DeleteListAsync(_selectedList);
            _selectedList = null;
            await LoadListsAsync();
        };
        topRow.Add(deleteListBtn, 4, 0);

        var upBtn = CreateHeaderButton("Up", ThemeKey.HighlightIndigo, ThemeKey.HighlightIndigoText);
        upBtn.Clicked += async (s, e) => await MoveSelectedItemAsync(-1);
        topRow.Add(upBtn, 5, 0);

        var downBtn = CreateHeaderButton("Down", ThemeKey.HighlightIndigo, ThemeKey.HighlightIndigoText);
        downBtn.Clicked += async (s, e) => await MoveSelectedItemAsync(1);
        topRow.Add(downBtn, 6, 0);

        mainGrid.Add(topRow, 0, 0);

        var fixedArea = new VerticalStackLayout
        {
            Padding = new Thickness(12, 0, 12, 4),
            Spacing = 4
        };
        _statusLabel = (new Label
        {
            Text = "",
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true);
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

    private static Button CreateHeaderButton(string text, ThemeKey background, ThemeKey textColor)
    {
        return (new Button
        {
            Text = text,
            FontSize = 12,
            HeightRequest = 38,
            CornerRadius = 6,
            Padding = new Thickness(12, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, background).ThemeColor(Button.TextColorProperty, textColor);
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
            _gridContainer.Children.Add((new Label { Text = "Create a list to start.", Margin = new Thickness(8, 20) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
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
            _gridContainer.Children.Add((new Label { Text = "No items in this list.", Margin = new Thickness(8, 20) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
            return;
        }

        var headers = new List<string> { "Id", "ListOrder", "Text", "Notes", "Priority", "CreatedAt", "UpdatedAt", "Done" };
        var fullRows = _currentItems.Select(BuildGridRow).ToList();
        var displayRows = fullRows
            .Select(row => row.Select(v => v.Length > 50 ? v.Substring(0, 47) + "..." : v).ToList())
            .ToList();

        var dataGrid = DataGridView.Create(headers, displayRows)
            .WithHeaderStyle(AppColors.Get(ThemeKey.HighlightIndigoText), AppColors.Get(ThemeKey.ButtonPrimaryText))
            .WithAlternateRowColor(AppColors.Get(ThemeKey.HighlightViolet))
            .WithColumnWidths(60, 240)
            .WithCellPadding(6)
            .WithFontSize(12, 12)
            .WithFullRows(fullRows)
            .WithIdColumn("Id")
            .OnCellTapped((s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _currentItems.Count)
                {
                    _selectedItem = _currentItems[e.RowIndex];
                    if (e.ColumnName == "Priority")
                        _ = CyclePriorityAsync(_selectedItem);
                    else if (e.ColumnName == "Done")
                        _ = ToggleDoneAsync(_selectedItem);
                }
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
        string priorityDisplay = item.Priority switch
        {
            1 => " High",
            2 => " Med",
            3 => " Low",
            _ => "—"
        };

        return new List<string>
        {
            item.Id.ToString(CultureInfo.InvariantCulture),
            item.SortOrder.ToString(CultureInfo.InvariantCulture),
            item.Text,
            item.Notes ?? "",
            priorityDisplay,
            item.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            item.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            item.IsExecuted ? "✅" : "⬜"
        };
    }

    private async Task CyclePriorityAsync(UserListItem item)
    {
        item.Priority = (item.Priority + 1) % 4;
        await _lists.UpdateItemAsync(item);
        await LoadItemsAsync();
    }

    private async Task ToggleDoneAsync(UserListItem item)
    {
        item.IsExecuted = !item.IsExecuted;
        item.UpdatedAt = DateTime.Now;
        await _lists.UpdateItemAsync(item);
        await LoadItemsAsync();
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

        var inputPage = new MultiLineItemInputPage();
        await Navigation.PushModalAsync(new NavigationPage(inputPage));
        string? result = await inputPage.WaitForResultAsync();
        if (string.IsNullOrWhiteSpace(result))
            return;

        string text = result.Trim();
        string? notes = await DisplayPromptAsync("Notes", "Optional notes:", "Save", "Skip", initialValue: "");
        string? priorityStr = await DisplayActionSheet("Priority", "Skip (None)", null, " High", " Medium", " Low");
        int priority = priorityStr switch
        {
            " High" => 1,
            " Medium" => 2,
            " Low" => 3,
            _ => 0
        };
        await _lists.AddItemAsync(_selectedList.Id, text, notes ?? "", priority);

        // Offer to also log to Idea Logger
        bool logIdea = await DisplayAlert(
            "Log as Idea?",
            "Would you also like to log " +
            $"\"{text}\" to the Idea Logger?",
            "Yes, Log It",
            "No Thanks");

        if (logIdea && _ideaLogger != null)
        {
            await _ideaLogger.LogIdeaAsync(
                new IdeaLogRequest
                {
                    Username = _auth.CurrentUsername,
                    Title = text,
                    Body = text,
                    Category = notes ?? ""
                },
                IdeaLogMode.Interactive,
                this);
        }

        await LoadItemsAsync();
    }

    private sealed class MultiLineItemInputPage : ContentPage
    {
        private readonly TaskCompletionSource<string?> _resultSource = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Editor _editor;
        private bool _isClosing;

        public MultiLineItemInputPage()
        {
            Title = "Add Items";
            this.ThemeColor(BackgroundColorProperty, ThemeKey.SecondarySurface);

            _editor = (new Editor
            {
                Placeholder = "One item per line...",
                HeightRequest = 220,
                AutoSize = EditorAutoSizeOption.Disabled,
                VerticalOptions = LayoutOptions.Start
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);

            var addButton = (new Button
            {
                Text = "Add",
                CornerRadius = 6
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ButtonPrimaryBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
            addButton.Clicked += async (_, _) => await CloseAsync(_editor.Text);

            var cancelButton = (new Button
            {
                Text = "Cancel",
                CornerRadius = 6
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
            cancelButton.Clicked += async (_, _) => await CloseAsync(null);

            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    (new Label
                    {
                        Text = "Enter one item per line.",
                        FontSize = 16
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true),
                    _editor,
                    new HorizontalStackLayout
                    {
                        Spacing = 10,
                        HorizontalOptions = LayoutOptions.End,
                        Children = { cancelButton, addButton }
                    }
                }
            };
        }

        public Task<string?> WaitForResultAsync() => _resultSource.Task;

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _editor.Focus();
        }

        protected override void OnDisappearing()
        {
            if (!_isClosing)
                _resultSource.TrySetResult(null);
            base.OnDisappearing();
        }

        private async Task CloseAsync(string? result)
        {
            if (_isClosing)
                return;

            _isClosing = true;
            await Navigation.PopModalAsync();
            _resultSource.TrySetResult(result);
        }
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
