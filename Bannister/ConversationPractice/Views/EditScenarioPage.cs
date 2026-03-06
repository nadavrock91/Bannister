using ConversationPractice.Models;
using ConversationPractice.Services;
using Microsoft.Maui.Controls;

namespace ConversationPractice.Views;

/// <summary>
/// Page for editing an existing conversation scenario
/// </summary>
public class EditScenarioPage : ContentPage
{
    private readonly ConversationService _conversationService;
    private readonly Conversation _scenario;
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

    public EditScenarioPage(ConversationService conversationService, Conversation scenario, string? username = null)
    {
        _conversationService = conversationService;
        _scenario = scenario;
        _username = username;

        Title = "Edit Scenario";
        BackgroundColor = Colors.White;

        BuildUI();
        LoadScenarioData();
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
            Text = "✏️ Edit Scenario",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        // Title
        mainStack.Children.Add(CreateFieldLabel("Scenario Title *"));
        txtTitle = new Entry { FontSize = 14 };
        mainStack.Children.Add(txtTitle);

        // Scenario Type
        mainStack.Children.Add(CreateFieldLabel("Scenario Type *"));
        txtScenarioType = new Entry { FontSize = 14 };
        mainStack.Children.Add(txtScenarioType);

        // Description
        mainStack.Children.Add(CreateFieldLabel("Description"));
        txtDescription = new Editor { HeightRequest = 80, FontSize = 14 };
        mainStack.Children.Add(txtDescription);

        // Your Role
        mainStack.Children.Add(CreateFieldLabel("Your Role *"));
        txtUserRole = new Entry { FontSize = 14 };
        mainStack.Children.Add(txtUserRole);

        // AI's Role
        mainStack.Children.Add(CreateFieldLabel("AI's Role *"));
        txtAiRole = new Entry { FontSize = 14 };
        mainStack.Children.Add(txtAiRole);

        // System Prompt
        mainStack.Children.Add(CreateFieldLabel("AI Instructions (System Prompt) *"));
        txtSystemPrompt = new Editor { HeightRequest = 150, FontSize = 14 };
        mainStack.Children.Add(txtSystemPrompt);

        // Context
        mainStack.Children.Add(CreateFieldLabel("Additional Context"));
        txtContext = new Editor { HeightRequest = 80, FontSize = 14 };
        mainStack.Children.Add(txtContext);

        // Difficulty
        mainStack.Children.Add(CreateFieldLabel("Difficulty Level"));
        
        var difficultyStack = new HorizontalStackLayout { Spacing = 12 };
        
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
        mainStack.Children.Add(CreateFieldLabel("Icon/Emoji"));
        txtIcon = new Entry { FontSize = 14, MaxLength = 4 };
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
            Text = "Save Changes",
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

    private Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        };
    }

    private void LoadScenarioData()
    {
        txtTitle.Text = _scenario.Title;
        txtScenarioType.Text = _scenario.ScenarioType;
        txtDescription.Text = _scenario.Description;
        txtUserRole.Text = _scenario.UserRole;
        txtAiRole.Text = _scenario.AiRole;
        txtSystemPrompt.Text = _scenario.SystemPrompt;
        txtContext.Text = _scenario.Context;
        sliderDifficulty.Value = _scenario.DifficultyLevel;
        txtIcon.Text = _scenario.Icon;
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
        // Validate
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
            await DisplayAlert("Validation Error", "Please enter AI instructions", "OK");
            return;
        }

        try
        {
            // Update scenario
            _scenario.Title = txtTitle.Text.Trim();
            _scenario.ScenarioType = txtScenarioType.Text.Trim();
            _scenario.Description = txtDescription.Text?.Trim() ?? "";
            _scenario.UserRole = txtUserRole.Text?.Trim() ?? "User";
            _scenario.AiRole = txtAiRole.Text?.Trim() ?? "AI";
            _scenario.SystemPrompt = txtSystemPrompt.Text.Trim();
            _scenario.Context = txtContext.Text?.Trim();
            _scenario.DifficultyLevel = (int)Math.Round(sliderDifficulty.Value);
            _scenario.Icon = txtIcon.Text?.Trim() ?? "💬";

            await _conversationService.UpdateConversationAsync(_scenario);

            await DisplayAlert("Success", "Scenario updated!", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to update scenario:\n{ex.Message}", "OK");
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
