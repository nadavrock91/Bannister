using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page for managing game rules/clarifications.
/// Supports add, edit, delete, and reorder.
/// </summary>
public class GameRulesPage : ContentPage
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
        BackgroundColor = Color.FromArgb("#F5F7FC");

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
        var headerFrame = new Frame
        {
            Padding = 20,
            CornerRadius = 0,
            BackgroundColor = Color.FromArgb("#5B63EE"),
            BorderColor = Colors.Transparent,
            HasShadow = false
        };

        var headerStack = new VerticalStackLayout { Spacing = 8 };

        headerStack.Children.Add(new Label
        {
            Text = "📜 Rules of the Game",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        headerStack.Children.Add(new Label
        {
            Text = "Your personal clarifications and rules for the EXP system",
            FontSize = 14,
            TextColor = Color.FromArgb("#FFFFFFCC")
        });

        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        // Add button
        var addButton = new Button
        {
            Text = "➕ Add New Rule",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 48,
            Margin = new Thickness(16, 16, 16, 8)
        };
        addButton.Clicked += OnAddRuleClicked;
        mainStack.Children.Add(addButton);

        // Hint
        mainStack.Children.Add(new Label
        {
            Text = "💡 Use ▲▼ to reorder • Tap to edit • ✕ to delete",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });

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
            _rulesList.Children.Add(new Frame
            {
                Padding = 20,
                CornerRadius = 12,
                BackgroundColor = Color.FromArgb("#E3F2FD"),
                BorderColor = Colors.Transparent,
                HasShadow = false,
                Content = new Label
                {
                    Text = "No rules yet.\n\nAdd rules to clarify how your EXP system works.\n\nExamples:\n• \"Designations may be canceled up to start date\"\n• \"EXP applies from moment of adding activity\"\n• \"Be honest in reward and punishment\"",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#1565C0"),
                    HorizontalTextAlignment = TextAlignment.Center
                }
            });
            return;
        }

        for (int i = 0; i < _rules.Count; i++)
        {
            _rulesList.Children.Add(BuildRuleCard(_rules[i], i));
        }
    }

    private Frame BuildRuleCard(GameRule rule, int index)
    {
        var frame = new Frame
        {
            Padding = 12,
            CornerRadius = 10,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            HasShadow = true
        };

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
        var orderLabel = new Label
        {
            Text = $"#{index + 1}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#5B63EE"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(orderLabel, 0);
        grid.Children.Add(orderLabel);

        // Rule text (tappable to edit)
        var textLabel = new Label
        {
            Text = rule.Text,
            FontSize = 14,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.WordWrap
        };
        
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
            var btnUp = new Button
            {
                Text = "▲",
                BackgroundColor = Color.FromArgb("#E3F2FD"),
                TextColor = Color.FromArgb("#1565C0"),
                FontSize = 14,
                WidthRequest = 36,
                HeightRequest = 36,
                Padding = 0,
                CornerRadius = 6
            };
            btnUp.Clicked += async (s, e) => await MoveRuleAsync(rule, -1);
            buttonStack.Children.Add(btnUp);
        }

        // Move down
        if (index < _rules.Count - 1)
        {
            var btnDown = new Button
            {
                Text = "▼",
                BackgroundColor = Color.FromArgb("#E3F2FD"),
                TextColor = Color.FromArgb("#1565C0"),
                FontSize = 14,
                WidthRequest = 36,
                HeightRequest = 36,
                Padding = 0,
                CornerRadius = 6
            };
            btnDown.Clicked += async (s, e) => await MoveRuleAsync(rule, 1);
            buttonStack.Children.Add(btnDown);
        }

        // Delete
        var btnDelete = new Button
        {
            Text = "✕",
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            FontSize = 14,
            WidthRequest = 36,
            HeightRequest = 36,
            Padding = 0,
            CornerRadius = 6
        };
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
