using ConversationPractice.Models;
using ConversationPractice.Services;
using Microsoft.Maui.Controls;

namespace ConversationPractice.Views;

/// <summary>
/// Browse and select conversation practice scenarios
/// Pure C# implementation - no XAML needed
/// </summary>
public class ConversationListPage : ContentPage
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
        BackgroundColor = Colors.White;

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
        var headerFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 0,
            BackgroundColor = Color.FromArgb("#F8F9FA"),
            HasShadow = false
        };

        var headerStack = new VerticalStackLayout { Spacing = 12 };

        headerStack.Children.Add(new Label
        {
            Text = "💬 Practice Difficult Conversations",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        headerStack.Children.Add(new Label
        {
            Text = "Build confidence through AI-powered role-play scenarios",
            FontSize = 14,
            TextColor = Color.FromArgb("#666")
        });

        pickerScenarioType = new Picker
        {
            Title = "All Scenarios",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#333")
        };
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
        var bottomGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Padding = 16,
            BackgroundColor = Color.FromArgb("#F8F9FA"),
            ColumnSpacing = 12
        };

        var createButton = new Button
        {
            Text = "📝 Create Custom Scenario",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 50
        };
        createButton.Clicked += OnCreateScenario;
        Grid.SetColumn(createButton, 0);
        bottomGrid.Children.Add(createButton);

        var manageButton = new Button
        {
            Text = "🔧 Manage",
            BackgroundColor = Colors.Transparent,
            BorderColor = Color.FromArgb("#FF9800"),
            BorderWidth = 2,
            TextColor = Color.FromArgb("#FF9800"),
            CornerRadius = 8,
            HeightRequest = 50
        };
        manageButton.Clicked += OnManageCustom;
        Grid.SetColumn(manageButton, 1);
        bottomGrid.Children.Add(manageButton);

        var statsButton = new Button
        {
            Text = "📊 Stats",
            BackgroundColor = Colors.Transparent,
            BorderColor = Color.FromArgb("#5B63EE"),
            BorderWidth = 2,
            TextColor = Color.FromArgb("#5B63EE"),
            CornerRadius = 8,
            HeightRequest = 50,
            WidthRequest = 100
        };
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
            scenariosContainer.Children.Add(new Label
            {
                Text = "No scenarios yet. Create your first one!",
                FontSize = 16,
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        // Group by scenario type
        var grouped = conversations.GroupBy(c => c.ScenarioType).OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            // Section header
            scenariosContainer.Children.Add(new Label
            {
                Text = group.Key,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#333"),
                Margin = new Thickness(0, 16, 0, 8)
            });

            // Scenarios in this group
            foreach (var conversation in group.OrderBy(c => c.DifficultyLevel).ThenBy(c => c.Title))
            {
                scenariosContainer.Children.Add(BuildScenarioCard(conversation));
            }
        }
    }

    private Frame BuildScenarioCard(Conversation conversation)
    {
        var frame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Color.FromArgb("#E0E0E0"),
            Margin = new Thickness(0, 0, 0, 8)
        };

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

        infoStack.Children.Add(new Label
        {
            Text = conversation.Title,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        infoStack.Children.Add(new Label
        {
            Text = conversation.Description,
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        // Difficulty stars
        string stars = string.Concat(Enumerable.Repeat("⭐", conversation.DifficultyLevel)) +
                       string.Concat(Enumerable.Repeat("☆", 5 - conversation.DifficultyLevel));
        
        infoStack.Children.Add(new Label
        {
            Text = stars,
            FontSize = 12,
            TextColor = Color.FromArgb("#FFB300")
        });

        // Level and EXP bar
        var levelExpStack = new VerticalStackLayout { Spacing = 4, Margin = new Thickness(0, 8, 0, 0) };

        var levelLabel = new Label
        {
            Text = $"Level {conversation.CurrentLevel}",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#5B63EE")
        };
        levelExpStack.Children.Add(levelLabel);

        // EXP progress bar
        if (conversation.CurrentLevel < 100)
        {
            int expNeeded = 100 * conversation.CurrentLevel; // GetExpForNextLevel formula
            double progress = expNeeded > 0 ? (double)conversation.CurrentLevelExp / expNeeded : 0;

            var expFrame = new Frame
            {
                Padding = 0,
                CornerRadius = 4,
                HeightRequest = 8,
                BackgroundColor = Color.FromArgb("#E0E0E0"),
                HasShadow = false
            };

            var expBar = new BoxView
            {
                Color = Color.FromArgb("#5B63EE"),
                WidthRequest = 200 * progress,
                HorizontalOptions = LayoutOptions.Start
            };

            expFrame.Content = expBar;
            levelExpStack.Children.Add(expFrame);

            levelExpStack.Children.Add(new Label
            {
                Text = $"{conversation.CurrentLevelExp} / {expNeeded} EXP",
                FontSize = 10,
                TextColor = Color.FromArgb("#999")
            });
        }
        else
        {
            levelExpStack.Children.Add(new Label
            {
                Text = "⭐ MAX LEVEL",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#FFB300")
            });
        }

        infoStack.Children.Add(levelExpStack);

        // Stats
        if (conversation.TimesCompleted > 0)
        {
            infoStack.Children.Add(new Label
            {
                Text = $"✅ Practiced {conversation.TimesCompleted} time{(conversation.TimesCompleted == 1 ? "" : "s")}",
                FontSize = 11,
                TextColor = Color.FromArgb("#4CAF50"),
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        Grid.SetColumn(infoStack, 1);
        mainLayout.Children.Add(infoStack);

        // Arrow
        var arrowLabel = new Label
        {
            Text = "▶",
            FontSize = 20,
            TextColor = Color.FromArgb("#5B63EE"),
            VerticalOptions = LayoutOptions.Center
        };
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
