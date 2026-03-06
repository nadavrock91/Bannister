using ConversationPractice.Models;
using ConversationPractice.Services;
using Microsoft.Maui.Controls;

namespace ConversationPractice.Views;

/// <summary>
/// Page for creating custom conversation practice scenarios
/// Pure C# implementation - no XAML
/// </summary>
public class AddScenarioPage : ContentPage
{
    private readonly ConversationService _conversationService;
    private readonly string? _username;

    private Entry txtTitle;
    private Entry txtScenarioType;
    private Editor txtDescription;
    private Entry txtUserRole;
    private Entry txtAiRole;
    private Editor txtSystemPrompt;
    private Editor txtContext;
    private Slider sliderDifficulty;
    private Label lblDifficulty;
    private Entry txtIcon;

    public AddScenarioPage(ConversationService conversationService, string? username = null)
    {
        _conversationService = conversationService;
        _username = username;

        Title = "Create Custom Scenario";
        BackgroundColor = Colors.White;

        BuildUI();
    }

    private void BuildUI()
    {
        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16
        };

        // Header
        mainStack.Children.Add(new Label
        {
            Text = "Create Custom Conversation Scenario",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        mainStack.Children.Add(new Label
        {
            Text = "Design your own AI role-play scenario for practice",
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            Margin = new Thickness(0, 0, 0, 12)
        });

        // Title
        mainStack.Children.Add(CreateFieldLabel("Scenario Title *", "e.g., 'Difficult Client Negotiation'"));
        txtTitle = new Entry
        {
            Placeholder = "Enter scenario title",
            FontSize = 14
        };
        mainStack.Children.Add(txtTitle);

        // Scenario Type
        mainStack.Children.Add(CreateFieldLabel("Scenario Type *", "Category like 'Sales Call', 'Interview', etc."));
        txtScenarioType = new Entry
        {
            Placeholder = "e.g., Sales Call, Job Interview, Customer Service",
            FontSize = 14
        };
        mainStack.Children.Add(txtScenarioType);

        // Description
        mainStack.Children.Add(CreateFieldLabel("Description", "Brief overview of the scenario"));
        txtDescription = new Editor
        {
            Placeholder = "What is this scenario about?",
            HeightRequest = 80,
            FontSize = 14
        };
        mainStack.Children.Add(txtDescription);

        // Your Role
        mainStack.Children.Add(CreateFieldLabel("Your Role *", "Who you'll be playing as"));
        txtUserRole = new Entry
        {
            Placeholder = "e.g., Sales Representative, Job Candidate",
            Text = "User",
            FontSize = 14
        };
        mainStack.Children.Add(txtUserRole);

        // AI's Role
        mainStack.Children.Add(CreateFieldLabel("AI's Role *", "Who the AI will play"));
        txtAiRole = new Entry
        {
            Placeholder = "e.g., Difficult Client, Hiring Manager",
            Text = "AI",
            FontSize = 14
        };
        mainStack.Children.Add(txtAiRole);

        // System Prompt (CRITICAL)
        mainStack.Children.Add(CreateFieldLabel("AI Instructions (System Prompt) *", "Tell the AI how to behave - be specific!"));
        
        var promptHelp = new Label
        {
            Text = "💡 Tip: Be specific about personality, objections, and how the AI should respond to different approaches.",
            FontSize = 12,
            TextColor = Color.FromArgb("#FF9800"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        mainStack.Children.Add(promptHelp);

        txtSystemPrompt = new Editor
        {
            Placeholder = "Example: You are a skeptical customer who has been burned by salespeople before. Start hostile but gradually warm up if the sales rep shows genuine understanding...",
            HeightRequest = 150,
            FontSize = 14
        };
        mainStack.Children.Add(txtSystemPrompt);

        // Context (Optional)
        mainStack.Children.Add(CreateFieldLabel("Additional Context", "Background info (optional)"));
        txtContext = new Editor
        {
            Placeholder = "Any background information or context for the scenario",
            HeightRequest = 80,
            FontSize = 14
        };
        mainStack.Children.Add(txtContext);

        // Difficulty
        mainStack.Children.Add(CreateFieldLabel("Difficulty Level", "How challenging is this scenario?"));
        
        var difficultyStack = new HorizontalStackLayout
        {
            Spacing = 12
        };
        
        sliderDifficulty = new Slider
        {
            Minimum = 1,
            Maximum = 5,
            Value = 3,
            HorizontalOptions = LayoutOptions.FillAndExpand
        };
        sliderDifficulty.ValueChanged += OnDifficultyChanged;
        
        lblDifficulty = new Label
        {
            Text = "⭐⭐⭐☆☆ (3/5)",
            FontSize = 16,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 120
        };
        
        difficultyStack.Children.Add(sliderDifficulty);
        difficultyStack.Children.Add(lblDifficulty);
        mainStack.Children.Add(difficultyStack);

        // Icon
        mainStack.Children.Add(CreateFieldLabel("Icon/Emoji", "Single emoji to represent this scenario"));
        txtIcon = new Entry
        {
            Placeholder = "e.g., 💼, 📞, 🤝",
            Text = "💬",
            FontSize = 14,
            MaxLength = 4
        };
        mainStack.Children.Add(txtIcon);

        // Buttons
        var buttonGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12,
            Margin = new Thickness(0, 20, 0, 0)
        };

        var btnSave = new Button
        {
            Text = "Create Scenario",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold
        };
        btnSave.Clicked += OnSaveClicked;
        Grid.SetColumn(btnSave, 0);
        buttonGrid.Children.Add(btnSave);

        var btnCancel = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#999"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 50
        };
        btnCancel.Clicked += OnCancelClicked;
        Grid.SetColumn(btnCancel, 1);
        buttonGrid.Children.Add(btnCancel);

        mainStack.Children.Add(buttonGrid);

        scrollView.Content = mainStack;
        Content = scrollView;
    }

    private VerticalStackLayout CreateFieldLabel(string text, string hint)
    {
        var stack = new VerticalStackLayout { Spacing = 4 };
        
        stack.Children.Add(new Label
        {
            Text = text,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        if (!string.IsNullOrEmpty(hint))
        {
            stack.Children.Add(new Label
            {
                Text = hint,
                FontSize = 12,
                TextColor = Color.FromArgb("#999")
            });
        }

        return stack;
    }

    private void OnDifficultyChanged(object? sender, ValueChangedEventArgs e)
    {
        int difficulty = (int)Math.Round(e.NewValue);
        string stars = string.Concat(Enumerable.Repeat("⭐", difficulty)) +
                       string.Concat(Enumerable.Repeat("☆", 5 - difficulty));
        lblDifficulty.Text = $"{stars} ({difficulty}/5)";
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(txtTitle.Text))
        {
            await DisplayAlert("Validation Error", "Please enter a scenario title", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(txtScenarioType.Text))
        {
            await DisplayAlert("Validation Error", "Please enter a scenario type", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(txtSystemPrompt.Text))
        {
            await DisplayAlert("Validation Error", "Please enter AI instructions (system prompt)", "OK");
            return;
        }

        try
        {
            var conversation = new Conversation
            {
                Username = _username,
                Title = txtTitle.Text.Trim(),
                ScenarioType = txtScenarioType.Text.Trim(),
                Description = txtDescription.Text?.Trim() ?? "",
                UserRole = txtUserRole.Text?.Trim() ?? "User",
                AiRole = txtAiRole.Text?.Trim() ?? "AI",
                SystemPrompt = txtSystemPrompt.Text.Trim(),
                Context = txtContext.Text?.Trim(),
                DifficultyLevel = (int)Math.Round(sliderDifficulty.Value),
                Icon = txtIcon.Text?.Trim() ?? "💬",
                IsTemplate = false,
                IsActive = true
            };

            await _conversationService.CreateConversationAsync(conversation);

            await DisplayAlert("Success", "Custom scenario created!", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to create scenario: {ex.Message}", "OK");
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
