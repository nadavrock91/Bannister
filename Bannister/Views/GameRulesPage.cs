using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page for managing game rules/clarifications.
/// Supports add, edit, delete, and reorder.
/// </summary>
public class GameRulesPage : Bannister.Views.ModeThemedPage
{
    private readonly DatabaseService _db;
    private readonly string _username;
    private VerticalStackLayout _rulesList;
    private List<GameRule> _rules = new();

    public GameRulesPage(DatabaseService db, string username)
    {
        _db = db;
        _username = username;

        Title = "Rules of the Game";


        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await EnsureTableAsync();
        await LoadRulesAsync();
    }

    private async Task EnsureTableAsync()
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<GameRule>();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout
        {
            Spacing = 0
        };

        // Header
        var headerFrame = (new Frame
        {
            Padding = 20,
            CornerRadius = 0,
            BorderColor = Colors.Transparent,
            HasShadow = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground);

        var headerStack = new VerticalStackLayout { Spacing = 8 };

        headerStack.Children.Add((new Label
        {
            Text = "📜 Rules of the Game",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1, classifyPageText: true));

        headerStack.Children.Add((new Label
        {
            Text = "Your personal clarifications and rules for the EXP system",
            FontSize = 14
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightYellow, ThemeKey.PageText1, classifyPageText: true));

        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        // Add button
        var addButton = (new Button
        {
            Text = "➕ Add New Rule",
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 48,
            Margin = new Thickness(16, 16, 16, 8)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        addButton.Clicked += OnAddRuleClicked;
        mainStack.Children.Add(addButton);

        // Hint
        mainStack.Children.Add((new Label
        {
            Text = "💡 Use ▲▼ to reorder • Tap to edit • ✕ to delete",
            FontSize = 12,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 0, 8)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));

        // Scrollable rules list
        var scrollView = new ScrollView();
        _rulesList = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 12
        };
        scrollView.Content = _rulesList;
        mainStack.Children.Add(scrollView);

        Content = mainStack;
    }

    private async Task LoadRulesAsync()
    {
        _rulesList.Children.Clear();

        var conn = await _db.GetConnectionAsync();
        _rules = await conn.Table<GameRule>()
            .Where(r => r.Username == _username)
            .OrderBy(r => r.DisplayOrder)
            .ToListAsync();

        if (_rules.Count == 0)
        {
            _rulesList.Children.Add((new Frame
            {
                Padding = 20,
                CornerRadius = 12,
                BorderColor = Colors.Transparent,
                HasShadow = false,
                Content = (new Label
                {
                    Text = "No rules yet.\n\nAdd rules to clarify how your EXP system works.\n\nExamples:\n• \"Designations may be canceled up to start date\"\n• \"EXP applies from moment of adding activity\"\n• \"Be honest in reward and punishment\"",
                    FontSize = 14,
                    HorizontalTextAlignment = TextAlignment.Center
                }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText1, classifyPageText: true)
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground));
            return;
        }

        for (int i = 0; i < _rules.Count; i++)
        {
            _rulesList.Children.Add(BuildRuleCard(_rules[i], i));
        }
    }

    private Frame BuildRuleCard(GameRule rule, int index)
    {
        var frame = (new Frame
        {
            Padding = 12,
            CornerRadius = 10,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 40 },  // Order number
                new ColumnDefinition { Width = GridLength.Star },  // Rule text
                new ColumnDefinition { Width = GridLength.Auto }   // Buttons
            },
            ColumnSpacing = 12
        };

        // Order number
        var orderLabel = (new Label
        {
            Text = $"#{index + 1}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightIndigoText, ThemeKey.PageText1, classifyPageText: true);
        Grid.SetColumn(orderLabel, 0);
        grid.Children.Add(orderLabel);

        // Rule text (tappable to edit)
        var textLabel = (new Label
        {
            Text = rule.Text,
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true);
        
        var textContainer = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center };
        textContainer.Children.Add(textLabel);
        
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (s, e) => await EditRuleAsync(rule);
        textContainer.GestureRecognizers.Add(tapGesture);
        
        Grid.SetColumn(textContainer, 1);
        grid.Children.Add(textContainer);

        // Buttons
        var buttonStack = new HorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };

        // Move up
        if (index > 0)
        {
            var btnUp = (new Button
            {
                Text = "▲",
                FontSize = 14,
                WidthRequest = 36,
                HeightRequest = 36,
                Padding = 0,
                CornerRadius = 6
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
            btnUp.Clicked += async (s, e) => await MoveRuleAsync(rule, -1);
            buttonStack.Children.Add(btnUp);
        }

        // Move down
        if (index < _rules.Count - 1)
        {
            var btnDown = (new Button
            {
                Text = "▼",
                FontSize = 14,
                WidthRequest = 36,
                HeightRequest = 36,
                Padding = 0,
                CornerRadius = 6
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
            btnDown.Clicked += async (s, e) => await MoveRuleAsync(rule, 1);
            buttonStack.Children.Add(btnDown);
        }

        // Delete
        var btnDelete = (new Button
        {
            Text = "✕",
            FontSize = 14,
            WidthRequest = 36,
            HeightRequest = 36,
            Padding = 0,
            CornerRadius = 6
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
        btnDelete.Clicked += async (s, e) => await DeleteRuleAsync(rule);
        buttonStack.Children.Add(btnDelete);

        Grid.SetColumn(buttonStack, 2);
        grid.Children.Add(buttonStack);

        frame.Content = grid;
        return frame;
    }

    private async void OnAddRuleClicked(object? sender, EventArgs e)
    {
        string? text = await DisplayPromptAsync(
            "Add Rule",
            "Enter your rule or clarification:",
            placeholder: "e.g., Designations may be canceled up to start date",
            maxLength: 500);

        if (string.IsNullOrWhiteSpace(text))
            return;

        var conn = await _db.GetConnectionAsync();

        // Get next display order
        int maxOrder = _rules.Count > 0 ? _rules.Max(r => r.DisplayOrder) : -1;

        var rule = new GameRule
        {
            Username = _username,
            Text = text.Trim(),
            DisplayOrder = maxOrder + 1,
            CreatedAt = DateTime.UtcNow
        };

        await conn.InsertAsync(rule);
        await LoadRulesAsync();
    }

    private async Task EditRuleAsync(GameRule rule)
    {
        string? text = await DisplayPromptAsync(
            "Edit Rule",
            "Update your rule:",
            initialValue: rule.Text,
            maxLength: 500);

        if (string.IsNullOrWhiteSpace(text))
            return;

        var conn = await _db.GetConnectionAsync();
        rule.Text = text.Trim();
        rule.ModifiedAt = DateTime.UtcNow;
        await conn.UpdateAsync(rule);
        await LoadRulesAsync();
    }

    private async Task DeleteRuleAsync(GameRule rule)
    {
        bool confirm = await DisplayAlert(
            "Delete Rule",
            $"Delete this rule?\n\n\"{rule.Text}\"",
            "Delete",
            "Cancel");

        if (!confirm)
            return;

        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync(rule);
        await LoadRulesAsync();
    }

    private async Task MoveRuleAsync(GameRule rule, int direction)
    {
        int currentIndex = _rules.IndexOf(rule);
        int newIndex = currentIndex + direction;

        if (newIndex < 0 || newIndex >= _rules.Count)
            return;

        // Swap display orders
        var otherRule = _rules[newIndex];
        int tempOrder = rule.DisplayOrder;
        rule.DisplayOrder = otherRule.DisplayOrder;
        otherRule.DisplayOrder = tempOrder;

        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(rule);
        await conn.UpdateAsync(otherRule);

        await LoadRulesAsync();
    }
}
