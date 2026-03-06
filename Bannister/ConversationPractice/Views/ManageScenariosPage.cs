using ConversationPractice.Models;
using ConversationPractice.Services;
using Microsoft.Maui.Controls;

namespace ConversationPractice.Views;

/// <summary>
/// Page for managing custom conversation scenarios (edit/delete)
/// </summary>
public class ManageScenariosPage : ContentPage
{
    private readonly ConversationService _conversationService;
    private readonly string? _username;
    private VerticalStackLayout scenarioList;

    public ManageScenariosPage(ConversationService conversationService, string? username = null)
    {
        _conversationService = conversationService;
        _username = username;

        Title = "Manage Scenarios";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadScenariosAsync();
    }

    private void BuildUI()
    {
        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 12
        };

        // Header
        mainStack.Children.Add(new Label
        {
            Text = "🔧 Manage Custom Scenarios",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        mainStack.Children.Add(new Label
        {
            Text = "Edit or delete your custom conversation scenarios. Default scenarios cannot be deleted.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Scenario list
        scenarioList = new VerticalStackLayout { Spacing = 12 };
        mainStack.Children.Add(scenarioList);

        scrollView.Content = mainStack;
        Content = scrollView;
    }

    private async Task LoadScenariosAsync()
    {
        scenarioList.Children.Clear();

        var scenarios = await _conversationService.GetConversationsAsync(_username);

        // Show only custom scenarios (non-templates)
        var customScenarios = scenarios.Where(s => !s.IsTemplate).ToList();

        if (customScenarios.Count == 0)
        {
            scenarioList.Children.Add(new Label
            {
                Text = "No custom scenarios yet.\n\nCreate one from the main screen!",
                FontSize = 14,
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        foreach (var scenario in customScenarios)
        {
            scenarioList.Children.Add(BuildScenarioCard(scenario));
        }
    }

    private Frame BuildScenarioCard(Conversation scenario)
    {
        var frame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            HasShadow = true
        };

        var mainStack = new VerticalStackLayout { Spacing = 12 };

        // Title and type
        var headerStack = new HorizontalStackLayout { Spacing = 8 };
        
        headerStack.Children.Add(new Label
        {
            Text = scenario.Icon,
            FontSize = 28,
            VerticalOptions = LayoutOptions.Center
        });

        var titleStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        
        titleStack.Children.Add(new Label
        {
            Text = scenario.Title,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        titleStack.Children.Add(new Label
        {
            Text = scenario.ScenarioType,
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });

        headerStack.Children.Add(titleStack);
        mainStack.Children.Add(headerStack);

        // Description
        if (!string.IsNullOrEmpty(scenario.Description))
        {
            mainStack.Children.Add(new Label
            {
                Text = scenario.Description,
                FontSize = 13,
                TextColor = Color.FromArgb("#666"),
                LineBreakMode = LineBreakMode.WordWrap
            });
        }

        // Stats
        var statsStack = new HorizontalStackLayout { Spacing = 16 };

        statsStack.Children.Add(new Label
        {
            Text = $"Level {scenario.CurrentLevel}",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#5B63EE")
        });

        statsStack.Children.Add(new Label
        {
            Text = $"✅ {scenario.TimesCompleted}x practiced",
            FontSize = 12,
            TextColor = Color.FromArgb("#4CAF50")
        });

        string stars = string.Concat(Enumerable.Repeat("⭐", scenario.DifficultyLevel)) +
                       string.Concat(Enumerable.Repeat("☆", 5 - scenario.DifficultyLevel));
        statsStack.Children.Add(new Label
        {
            Text = stars,
            FontSize = 12
        });

        mainStack.Children.Add(statsStack);

        // Action buttons
        var buttonGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var btnEdit = new Button
        {
            Text = "✏️ Edit",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 13
        };
        btnEdit.Clicked += (s, e) => OnEditScenarioClicked(scenario);
        Grid.SetColumn(btnEdit, 0);
        buttonGrid.Children.Add(btnEdit);

        var btnDelete = new Button
        {
            Text = "🗑️ Delete",
            BackgroundColor = Color.FromArgb("#F44336"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 13
        };
        btnDelete.Clicked += (s, e) => OnDeleteScenarioClicked(scenario);
        Grid.SetColumn(btnDelete, 1);
        buttonGrid.Children.Add(btnDelete);

        mainStack.Children.Add(buttonGrid);

        frame.Content = mainStack;
        return frame;
    }

    private async void OnEditScenarioClicked(Conversation scenario)
    {
        await Navigation.PushAsync(new EditScenarioPage(_conversationService, scenario, _username));
    }

    private async void OnDeleteScenarioClicked(Conversation scenario)
    {
        bool confirm = await DisplayAlert(
            "Delete Scenario",
            $"Delete '{scenario.Title}'?\n\nThis will also delete all conversation branches and practice history.",
            "Delete",
            "Cancel"
        );

        if (!confirm)
            return;

        try
        {
            // Delete all nodes for this conversation
            var nodes = await _conversationService.GetNodesForConversationAsync(scenario.Id);
            foreach (var node in nodes)
            {
                await _conversationService.DeleteNodeAsync(node.Id);
            }

            // Delete the conversation
            await _conversationService.DeleteConversationAsync(scenario.Id);

            await DisplayAlert("Deleted", $"'{scenario.Title}' has been deleted.", "OK");
            await LoadScenariosAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to delete scenario:\n{ex.Message}", "OK");
        }
    }
}
