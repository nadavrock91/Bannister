using ConversationPractice.Models;
using Bannister.Services;
using ConversationPractice.Services;
using Microsoft.Maui.Controls;

namespace ConversationPractice.Views;

/// <summary>
/// Browse and select conversation practice scenarios
/// Pure C# implementation - no XAML needed
/// </summary>
public class ConversationListPage : Bannister.Views.ModeThemedPage
{
    private readonly ConversationService _conversationService;
    private readonly string? _username;
    private List<Conversation> _allConversations = new();

    private Picker pickerScenarioType;
    private VerticalStackLayout scenariosContainer;

    public ConversationListPage(ConversationService conversationService, string? username = null)
    {
        _conversationService = conversationService;
        _username = username;

        Title = "Conversation Practice";


        BuildUI();
    }

    private void BuildUI()
    {
        var mainGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            },
            Padding = 0
        };

        // Header
        var headerFrame = (new Frame
        {
            Padding = 16,
            CornerRadius = 0,
            HasShadow = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);

        var headerStack = new VerticalStackLayout { Spacing = 12 };

        headerStack.Children.Add((new Label
        {
            Text = "💬 Practice Difficult Conversations",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        headerStack.Children.Add((new Label
        {
            Text = "Build confidence through AI-powered role-play scenarios",
            FontSize = 14
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        pickerScenarioType = (new Picker
        {
            Title = "All Scenarios"
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(Picker.TextColorProperty, ThemeKey.InputText);
        pickerScenarioType.SelectedIndexChanged += OnScenarioTypeChanged;
        headerStack.Children.Add(pickerScenarioType);

        headerFrame.Content = headerStack;
        Grid.SetRow(headerFrame, 0);
        mainGrid.Children.Add(headerFrame);

        // Scenarios list
        var scrollView = new ScrollView();
        scenariosContainer = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 12
        };
        scrollView.Content = scenariosContainer;
        Grid.SetRow(scrollView, 1);
        mainGrid.Children.Add(scrollView);

        // Bottom action bar
        var bottomGrid = (new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Padding = 16,
            ColumnSpacing = 12
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);

        var createButton = (new Button
        {
            Text = "📝 Create Custom Scenario",
            CornerRadius = 8,
            HeightRequest = 50
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
        createButton.Clicked += OnCreateScenario;
        Grid.SetColumn(createButton, 0);
        bottomGrid.Children.Add(createButton);

        var manageButton = (new Button
        {
            Text = "🔧 Manage",
            BackgroundColor = Colors.Transparent,
            BorderWidth = 2,
            CornerRadius = 8,
            HeightRequest = 50
        }).ThemeColor(Button.BorderColorProperty, ThemeKey.WarningAccentText).ThemeColor(Button.TextColorProperty, ThemeKey.WarningAccentText);
        manageButton.Clicked += OnManageCustom;
        Grid.SetColumn(manageButton, 1);
        bottomGrid.Children.Add(manageButton);

        var statsButton = (new Button
        {
            Text = "📊 Stats",
            BackgroundColor = Colors.Transparent,
            BorderWidth = 2,
            CornerRadius = 8,
            HeightRequest = 50,
            WidthRequest = 100
        }).ThemeColor(Button.BorderColorProperty, ThemeKey.SecondaryAccentText).ThemeColor(Button.TextColorProperty, ThemeKey.SecondaryAccentText);
        statsButton.Clicked += OnViewStats;
        Grid.SetColumn(statsButton, 2);
        bottomGrid.Children.Add(statsButton);

        Grid.SetRow(bottomGrid, 2);
        mainGrid.Children.Add(bottomGrid);

        Content = mainGrid;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadConversationsAsync();
    }

    private async Task LoadConversationsAsync()
    {
        _allConversations = await _conversationService.GetConversationsAsync(_username);

        // Populate scenario type filter
        var types = _allConversations
            .Select(c => c.ScenarioType)
            .Distinct()
            .OrderBy(t => t)
            .ToList();
        types.Insert(0, "All Scenarios");

        pickerScenarioType.ItemsSource = types;
        pickerScenarioType.SelectedIndex = 0;

        DisplayConversations(_allConversations);
    }

    private void DisplayConversations(List<Conversation> conversations)
    {
        scenariosContainer.Children.Clear();

        if (conversations.Count == 0)
        {
            scenariosContainer.Children.Add((new Label
            {
                Text = "No scenarios yet. Create your first one!",
                FontSize = 16,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 40, 0, 0)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
            return;
        }

        // Group by scenario type
        var grouped = conversations.GroupBy(c => c.ScenarioType).OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            // Section header
            scenariosContainer.Children.Add((new Label
            {
                Text = group.Key,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 16, 0, 8)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

            // Scenarios in this group
            foreach (var conversation in group.OrderBy(c => c.DifficultyLevel).ThenBy(c => c.Title))
            {
                scenariosContainer.Children.Add(BuildScenarioCard(conversation));
            }
        }
    }

    private Frame BuildScenarioCard(Conversation conversation)
    {
        var frame = (new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            HasShadow = true,
            Margin = new Thickness(0, 0, 0, 8)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (s, e) => await OnScenarioTappedAsync(conversation);
        frame.GestureRecognizers.Add(tapGesture);

        var mainLayout = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(60) },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };

        // Icon
        var iconLabel = new Label
        {
            Text = conversation.Icon,
            FontSize = 40,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(iconLabel, 0);
        mainLayout.Children.Add(iconLabel);

        // Info
        var infoStack = new VerticalStackLayout { Spacing = 4 };

        infoStack.Children.Add((new Label
        {
            Text = conversation.Title,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        infoStack.Children.Add((new Label
        {
            Text = conversation.Description,
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        // Difficulty stars
        string stars = string.Concat(Enumerable.Repeat("⭐", conversation.DifficultyLevel)) +
                       string.Concat(Enumerable.Repeat("☆", 5 - conversation.DifficultyLevel));
        
        infoStack.Children.Add((new Label
        {
            Text = stars,
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightYellowButton, ThemeKey.PageText3, classifyPageText: true));

        // Level and EXP bar
        var levelExpStack = new VerticalStackLayout { Spacing = 4, Margin = new Thickness(0, 8, 0, 0) };

        var levelLabel = (new Label
        {
            Text = $"Level {conversation.CurrentLevel}",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.SecondaryAccentText, ThemeKey.PageText1, classifyPageText: true);
        levelExpStack.Children.Add(levelLabel);

        // EXP progress bar
        if (conversation.CurrentLevel < 100)
        {
            int expNeeded = 100 * conversation.CurrentLevel; // GetExpForNextLevel formula
            double progress = expNeeded > 0 ? (double)conversation.CurrentLevelExp / expNeeded : 0;

            var expFrame = (new Frame
            {
                Padding = 0,
                CornerRadius = 4,
                HeightRequest = 8,
                HasShadow = false
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator);

            var expBar = (new BoxView
            {
                WidthRequest = 200 * progress,
                HorizontalOptions = LayoutOptions.Start
            }).ThemeColor(BoxView.ColorProperty, ThemeKey.SecondaryButtonBackground);

            expFrame.Content = expBar;
            levelExpStack.Children.Add(expFrame);

            levelExpStack.Children.Add((new Label
            {
                Text = $"{conversation.CurrentLevelExp} / {expNeeded} EXP",
                FontSize = 10
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        }
        else
        {
            levelExpStack.Children.Add((new Label
            {
                Text = "⭐ MAX LEVEL",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold
            }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightYellowButton, ThemeKey.PageText3, classifyPageText: true));
        }

        infoStack.Children.Add(levelExpStack);

        // Stats
        if (conversation.TimesCompleted > 0)
        {
            infoStack.Children.Add((new Label
            {
                Text = $"✅ Practiced {conversation.TimesCompleted} time{(conversation.TimesCompleted == 1 ? "" : "s")}",
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.SuccessLightText, ThemeKey.PageText3, classifyPageText: true));
        }

        Grid.SetColumn(infoStack, 1);
        mainLayout.Children.Add(infoStack);

        // Arrow
        var arrowLabel = (new Label
        {
            Text = "▶",
            FontSize = 20,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.SecondaryAccentText, ThemeKey.PageText1, classifyPageText: true);
        Grid.SetColumn(arrowLabel, 2);
        mainLayout.Children.Add(arrowLabel);

        frame.Content = mainLayout;
        return frame;
    }

    private async Task OnScenarioTappedAsync(Conversation conversation)
    {
        // Navigate to tree builder page to build conversation branches
        await Navigation.PushAsync(new TreeBuilderPage(_conversationService, conversation));
    }

    private void OnScenarioTypeChanged(object? sender, EventArgs e)
    {
        if (pickerScenarioType.SelectedIndex <= 0)
        {
            DisplayConversations(_allConversations);
        }
        else
        {
            var selectedType = pickerScenarioType.SelectedItem?.ToString();
            var filtered = _allConversations.Where(c => c.ScenarioType == selectedType).ToList();
            DisplayConversations(filtered);
        }
    }

    private async void OnCreateScenario(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new AddScenarioPage(_conversationService, _username));
    }

    private async void OnManageCustom(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new ManageScenariosPage(_conversationService, _username));
    }

    private async void OnViewStats(object? sender, EventArgs e)
    {
        // TODO: Create stats page
        await DisplayAlert("Stats", "Statistics page coming soon!", "OK");
    }
}
