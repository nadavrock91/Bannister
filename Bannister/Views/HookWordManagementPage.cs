using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class HookWordManagementPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly HookWordService _hookWordService;

    private Label _countLabel = null!;
    private Entry _searchEntry = null!;
    private Button _activeToggle = null!;
    private Button _removedToggle = null!;
    private Button _allToggle = null!;
    private Button _bulkToggle = null!;
    private HorizontalStackLayout _bulkActionsRow = null!;
    private VerticalStackLayout _wordsStack = null!;

    private string _statusFilter = "active";
    private bool _bulkSelectMode;
    private List<HookWord> _allRows = new();
    private readonly HashSet<int> _selectedIds = new();

    public HookWordManagementPage(AuthService auth, HookWordService hookWordService)
    {
        _auth = auth;
        _hookWordService = hookWordService;
        Title = "Hook Word Pool Management";

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 12
        };

        stack.Children.Add((new Label
        {
            Text = " Hook Word Pool",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText1, classifyPageText: true));

        _countLabel = (new Label
        {
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true);
        stack.Children.Add(_countLabel);

        _searchEntry = (new Entry
        {
            Placeholder = "Search words...",
            FontSize = 14
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        _searchEntry.TextChanged += (_, _) => RenderWords();
        stack.Children.Add(_searchEntry);

        _activeToggle = BuildToggleButton("Active", "active");
        _removedToggle = BuildToggleButton("Removed", "removed");
        _allToggle = BuildToggleButton("All", "all");
        stack.Children.Add(new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { _activeToggle, _removedToggle, _allToggle }
        });

        _bulkToggle = (new Button
        {
            Text = "Bulk select",
            CornerRadius = 8,
            HeightRequest = 36,
            HorizontalOptions = LayoutOptions.Start
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
        _bulkToggle.Clicked += (_, _) =>
        {
            _bulkSelectMode = !_bulkSelectMode;
            _selectedIds.Clear();
            _bulkToggle.Text = _bulkSelectMode ? "Exit bulk select" : "Bulk select";
            _bulkActionsRow.IsVisible = _bulkSelectMode;
            RenderWords();
        };
        stack.Children.Add(_bulkToggle);

        _bulkActionsRow = new HorizontalStackLayout
        {
            Spacing = 8,
            IsVisible = false,
            Children =
            {
                BuildBulkButton("Remove selected", ThemeKey.WarningBackground, ThemeKey.WarningText, async () =>
                {
                    await _hookWordService.BulkRemoveAsync(_selectedIds.ToList());
                    _selectedIds.Clear();
                    await LoadAsync();
                }),
                BuildBulkButton("Restore selected", ThemeKey.SuccessBackground, ThemeKey.SuccessText, async () =>
                {
                    await _hookWordService.BulkRestoreAsync(_selectedIds.ToList());
                    _selectedIds.Clear();
                    await LoadAsync();
                }),
                BuildBulkButton("Delete selected", ThemeKey.ErrorBackground, ThemeKey.ErrorText, async () =>
                {
                    if (_selectedIds.Count == 0) return;
                    var ok = await DisplayAlert("Delete permanently?", $"Delete {_selectedIds.Count} selected word(s)? This cannot be undone.", "Delete", "Cancel");
                    if (!ok) return;
                    await _hookWordService.BulkDeleteAsync(_selectedIds.ToList());
                    _selectedIds.Clear();
                    await LoadAsync();
                })
            }
        };
        stack.Children.Add(_bulkActionsRow);

        _wordsStack = new VerticalStackLayout { Spacing = 6 };
        stack.Children.Add(_wordsStack);

        Content = new ScrollView { Content = stack };
        RefreshToggleStyles();
    }

    private Button BuildToggleButton(string text, string filter)
    {
        var button = new Button
        {
            Text = text,
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12,
            Padding = new Thickness(14, 0)
        };
        button.Clicked += async (_, _) =>
        {
            _statusFilter = filter;
            _selectedIds.Clear();
            RefreshToggleStyles();
            await LoadAsync();
        };
        return button;
    }

    private Button BuildBulkButton(string text, ThemeKey background, ThemeKey textColor, Func<Task> action)
    {
        var button = (new Button
        {
            Text = text,
            CornerRadius = 8,
            HeightRequest = 34,
            FontSize = 12
        }).ThemeColor(VisualElement.BackgroundColorProperty, background).ThemeColor(Button.TextColorProperty, textColor);
        button.Clicked += async (_, _) => await action();
        return button;
    }

    private void RefreshToggleStyles()
    {
        StyleToggle(_activeToggle, _statusFilter == "active");
        StyleToggle(_removedToggle, _statusFilter == "removed");
        StyleToggle(_allToggle, _statusFilter == "all");
    }

    private static void StyleToggle(Button button, bool selected)
    {
        button.ThemeColor(VisualElement.BackgroundColorProperty, selected ? ThemeKey.ButtonPrimaryBackground : ThemeKey.NeutralButtonBackground);
        button.ThemeColor(Button.TextColorProperty, selected ? ThemeKey.ButtonPrimaryText : ThemeKey.NeutralButtonText);
    }

    private async Task LoadAsync()
    {
        var username = _auth.CurrentUsername;
        var activeCount = await _hookWordService.CountActiveAsync(username);
        var removedCount = await _hookWordService.CountRemovedAsync(username);
        _countLabel.Text = $"{activeCount} active, {removedCount} removed";

        _allRows = _statusFilter switch
        {
            "removed" => await _hookWordService.GetRemovedAsync(username),
            "all" => await _hookWordService.GetAllAsync(username),
            _ => await _hookWordService.GetActiveAsync(username)
        };

        RenderWords();
    }

    private void RenderWords()
    {
        _wordsStack.Children.Clear();

        var query = (_searchEntry.Text ?? "").Trim();
        var rows = _allRows;
        if (!string.IsNullOrWhiteSpace(query))
            rows = rows.Where(w => w.Word.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        if (rows.Count == 0)
        {
            _wordsStack.Children.Add((new Label
            {
                Text = "No words match the current filter.",
                FontSize = 13,
                FontAttributes = FontAttributes.Italic,
                Margin = new Thickness(0, 20)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
            return;
        }

        foreach (var word in rows)
            _wordsStack.Children.Add(BuildWordRow(word));
    }

    private View BuildWordRow(HookWord word)
    {
        var row = (new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8,
            Padding = new Thickness(10)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var check = new CheckBox
        {
            IsVisible = _bulkSelectMode,
            IsChecked = _selectedIds.Contains(word.Id),
            VerticalOptions = LayoutOptions.Center
        };
        check.CheckedChanged += (_, e) =>
        {
            if (e.Value)
                _selectedIds.Add(word.Id);
            else
                _selectedIds.Remove(word.Id);
        };
        row.Add(check, 0, 0);

        row.Add((new Label
        {
            Text = word.Word,
            FontSize = 15,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true), 1, 0);

        var actions = new HorizontalStackLayout { Spacing = 6 };
        if (word.Status == "active")
        {
            actions.Children.Add(BuildRowButton("Remove", ThemeKey.WarningBackground, ThemeKey.WarningText, async () =>
            {
                await _hookWordService.RemoveWordAsync(word.Id);
                await LoadAsync();
            }));
        }
        else
        {
            actions.Children.Add(BuildRowButton("Restore", ThemeKey.SuccessBackground, ThemeKey.SuccessText, async () =>
            {
                await _hookWordService.RestoreWordAsync(word.Id);
                await LoadAsync();
            }));
        }

        actions.Children.Add(BuildRowButton("Delete", ThemeKey.ErrorBackground, ThemeKey.ErrorText, async () =>
        {
            var ok = await DisplayAlert("Delete permanently?", $"Delete '{word.Word}' forever?", "Delete", "Cancel");
            if (!ok) return;
            await _hookWordService.DeletePermanentlyAsync(word.Id);
            await LoadAsync();
        }));

        row.Add(actions, 2, 0);
        return row;
    }

    private Button BuildRowButton(string text, ThemeKey background, ThemeKey textColor, Func<Task> action)
    {
        var button = (new Button
        {
            Text = text,
            CornerRadius = 6,
            FontSize = 11,
            HeightRequest = 30,
            Padding = new Thickness(10, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, background).ThemeColor(Button.TextColorProperty, textColor);
        button.Clicked += async (_, _) => await action();
        return button;
    }
}
