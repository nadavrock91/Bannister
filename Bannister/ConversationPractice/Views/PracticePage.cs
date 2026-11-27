using ConversationPractice.Models;
using Bannister.Services;
using ConversationPractice.Services;
using Microsoft.Maui.Controls;

namespace ConversationPractice.Views;

/// <summary>
/// Practice page with alternating turns:
/// - Even depth (0, 2, 4...) = THEM speaking (shown as text, random from siblings)
/// - Odd depth (1, 3, 5...) = YOU speaking (shown as buttons to pick)
/// </summary>
public class PracticePage : Bannister.Views.ModeThemedPage
{
    private readonly ConversationService _conversationService;
    private Conversation _conversation;
    private List<ConversationNode> _allNodes = new();
    
    private ConversationNode? _currentNode = null;
    private int _currentDepth = 0;
    private int _turnCount = 0;

    // UI Elements
    private Label lblTheirResponse;
    private Label lblTurnCount;
    private VerticalStackLayout yourChoicesContainer;
    private Button btnRestart;
    private VerticalStackLayout conversationHistory;
    private ScrollView historyScroll;
    private ScrollView mainScroll;

    public PracticePage(ConversationService conversationService, Conversation conversation)
    {
        _conversationService = conversationService;
        _conversation = conversation;

        Title = $"Practice: {conversation.Title}";


        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _allNodes = await _conversationService.GetNodesForConversationAsync(_conversation.Id);
        await StartConversationAsync();
    }

    private void BuildUI()
    {
        mainScroll = new ScrollView();
        
        var mainStack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 16
        };

        // Header
        var headerStack = new VerticalStackLayout { Spacing = 8 };

        headerStack.Children.Add((new Label
        {
            Text = $"🎭 {_conversation.Title}",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        headerStack.Children.Add((new Label
        {
            Text = $"You are: {_conversation.UserRole} | They are: {_conversation.AiRole}",
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        lblTurnCount = (new Label
        {
            Text = "Turn 0",
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true);
        headerStack.Children.Add(lblTurnCount);

        mainStack.Children.Add(headerStack);

        // Their response card (purple)
        var theirFrame = (new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            HasShadow = true,
            MinimumHeightRequest = 100
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.HighlightVioletText);

        var theirStack = new VerticalStackLayout { Spacing = 12 };

        theirStack.Children.Add((new Label
        {
            Text = $"🗣️ {_conversation.AiRole} says:",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText1, classifyPageText: true));

        lblTheirResponse = (new Label
        {
            Text = "Loading...",
            FontSize = 20,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true);
        theirStack.Children.Add(lblTheirResponse);

        theirFrame.Content = theirStack;
        mainStack.Children.Add(theirFrame);

        // Your choices container (blue)
        var yourFrame = (new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            HasShadow = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.InfoText);

        var yourOuterStack = new VerticalStackLayout { Spacing = 12 };

        yourOuterStack.Children.Add((new Label
        {
            Text = $"💬 Your response ({_conversation.UserRole}):",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText1, classifyPageText: true));

        yourChoicesContainer = new VerticalStackLayout { Spacing = 8 };
        yourOuterStack.Children.Add(yourChoicesContainer);

        yourFrame.Content = yourOuterStack;
        mainStack.Children.Add(yourFrame);

        // Restart button
        btnRestart = (new Button
        {
            Text = "🔄 Restart Conversation",
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 45
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnRestart.Clicked += OnRestartClicked;
        mainStack.Children.Add(btnRestart);

        // Conversation history
        mainStack.Children.Add((new Label
        {
            Text = "📜 Conversation History:",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 12, 0, 0)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText1, classifyPageText: true));

        historyScroll = new ScrollView { HeightRequest = 200 };
        conversationHistory = new VerticalStackLayout { Spacing = 8 };
        historyScroll.Content = conversationHistory;
        mainStack.Children.Add(historyScroll);

        mainScroll.Content = mainStack;
        Content = mainScroll;
    }

    private async Task StartConversationAsync()
    {
        _currentNode = null;
        _currentDepth = 0;
        _turnCount = 0;
        conversationHistory.Children.Clear();

        UpdateTurnCount();
        
        // Start with THEM speaking (depth 0) - pick random root node
        await ShowTheirTurnAsync(null);
    }

    private async Task ShowTheirTurnAsync(int? parentNodeId)
    {
        // Get children of parent (or root nodes if parent is null)
        var candidates = parentNodeId == null
            ? _allNodes.Where(n => n.ParentNodeId == null).ToList()
            : _allNodes.Where(n => n.ParentNodeId == parentNodeId).ToList();

        if (candidates.Count == 0)
        {
            // No more branches - conversation ended
            await EndConversationAsync();
            return;
        }

        // Pick random
        var random = new Random();
        var chosen = candidates[random.Next(candidates.Count)];

        // Add previous node to history if exists
        if (_currentNode != null)
        {
            bool wasYourTurn = _currentDepth % 2 == 1;
            AddToHistory(_currentNode.Text, wasYourTurn);
        }

        _currentNode = chosen;
        _currentDepth++;
        _turnCount++;
        UpdateTurnCount();

        // Update TimesReached
        chosen.TimesReached++;
        await _conversationService.UpdateNodeAsync(chosen);

        // Show their text
        lblTheirResponse.Text = chosen.Text;

        // Check if terminal
        if (chosen.IsTerminal)
        {
            AddToHistory(chosen.Text, false);
            await EndConversationAsync();
            return;
        }

        // Now show YOUR choices (children of this node)
        ShowYourChoices(chosen.Id);
    }

    private void ShowYourChoices(int parentNodeId)
    {
        yourChoicesContainer.Children.Clear();

        var yourOptions = _allNodes.Where(n => n.ParentNodeId == parentNodeId).ToList();

        if (yourOptions.Count == 0)
        {
            yourChoicesContainer.Children.Add((new Label
            {
                Text = "No response options defined",
                FontSize = 14,
                FontAttributes = FontAttributes.Italic
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));

            // Add a "Continue anyway" button
            var btnContinue = (new Button
            {
                Text = "End Conversation",
                CornerRadius = 8,
                FontSize = 14,
                HeightRequest = 45
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
            btnContinue.Clicked += async (s, e) => await EndConversationAsync();
            yourChoicesContainer.Children.Add(btnContinue);
            return;
        }

        foreach (var option in yourOptions)
        {
            var btn = (new Button
            {
                Text = option.Text,
                CornerRadius = 8,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                Padding = new Thickness(12, 10),
                LineBreakMode = LineBreakMode.WordWrap
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

            btn.Clicked += async (s, e) => await OnYourChoiceClicked(option);
            yourChoicesContainer.Children.Add(btn);
        }
    }

    private async Task OnYourChoiceClicked(ConversationNode chosenResponse)
    {
        // Add THEIR line to history
        if (_currentNode != null)
        {
            AddToHistory(_currentNode.Text, false);
        }

        // Add YOUR choice to history
        AddToHistory(chosenResponse.Text, true);

        // Update state
        _currentNode = chosenResponse;
        _currentDepth++;

        // Update TimesReached for your choice
        chosenResponse.TimesReached++;
        await _conversationService.UpdateNodeAsync(chosenResponse);

        // Check if your choice is terminal
        if (chosenResponse.IsTerminal)
        {
            await EndConversationAsync();
            return;
        }

        // Now it's THEIR turn - random from children of your choice
        await ShowTheirTurnAsync(chosenResponse.Id);
    }

    private async Task EndConversationAsync()
    {
        lblTheirResponse.Text = "--- Conversation Ended ---";
        yourChoicesContainer.Children.Clear();
        
        yourChoicesContainer.Children.Add((new Label
        {
            Text = "🏁 Practice complete!",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.SuccessButtonBackground, ThemeKey.PageText1, classifyPageText: true));

        // Show self-scoring dialog
        await ShowSelfScoringAsync();
    }

    private void AddToHistory(string text, bool isUser)
    {
        var historyFrame = (new Frame
        {
            Padding = 10,
            CornerRadius = 8,
            HasShadow = false,
            Margin = isUser ? new Thickness(40, 0, 0, 0) : new Thickness(0, 0, 40, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, isUser ? ThemeKey.InfoBackground : ThemeKey.HighlightViolet).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, isUser ? ThemeKey.InfoText : ThemeKey.HighlightVioletText);

        var stack = new VerticalStackLayout { Spacing = 4 };

        stack.Children.Add((new Label
        {
            Text = isUser ? $"You ({_conversation.UserRole}):" : $"{_conversation.AiRole}:",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, isUser ? ThemeKey.InfoText : ThemeKey.HighlightVioletText, ThemeKey.PageText3, classifyPageText: true));

        stack.Children.Add((new Label
        {
            Text = text,
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        historyFrame.Content = stack;
        conversationHistory.Children.Add(historyFrame);

        // Auto-scroll
        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(100);
            await historyScroll.ScrollToAsync(0, conversationHistory.Height, true);
        });
    }

    private void UpdateTurnCount()
    {
        lblTurnCount.Text = $"Turn {_turnCount}";
    }

    private async void OnRestartClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Restart?",
            "Start the conversation over from the beginning?",
            "Yes",
            "No"
        );

        if (confirm)
        {
            await StartConversationAsync();
        }
    }

    private async Task ShowSelfScoringAsync()
    {
        var scoringPage = (new ContentPage
        {
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);

        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 20,
            VerticalOptions = LayoutOptions.Center
        };

        stack.Children.Add((new Label
        {
            Text = "🎯 Practice Complete!",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        stack.Children.Add((new Label
        {
            Text = $"You completed this practice in {_turnCount} turns.",
            FontSize = 16,
            HorizontalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        stack.Children.Add((new Label
        {
            Text = "Rate your performance:",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 20, 0, 0)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        var slider = new Slider
        {
            Minimum = 1,
            Maximum = 100,
            Value = 50
        };
        slider.ThemeColor(Slider.MinimumTrackColorProperty, ThemeKey.SuccessButtonBackground)
            .ThemeColor(Slider.MaximumTrackColorProperty, ThemeKey.Separator);

        var lblScore = (new Label
        {
            Text = "50 EXP",
            FontSize = 48,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightIndigoText, ThemeKey.PageText1, classifyPageText: true);

        slider.ValueChanged += (s, e) =>
        {
            int score = (int)Math.Round(e.NewValue);
            lblScore.Text = $"{score} EXP";
        };

        stack.Children.Add(slider);
        stack.Children.Add(lblScore);

        stack.Children.Add((new Label
        {
            Text = "1 = Poor | 50 = Average | 100 = Perfect",
            FontSize = 12,
            HorizontalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));

        var btnSubmit = (new Button
        {
            Text = "Award EXP & Complete",
            CornerRadius = 8,
            FontSize = 18,
            HeightRequest = 60,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 20, 0, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

        TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

        btnSubmit.Clicked += async (s, e) =>
        {
            int score = (int)Math.Round(slider.Value);
            tcs.SetResult(score);
            await Navigation.PopModalAsync();
        };

        stack.Children.Add(btnSubmit);
        scoringPage.Content = stack;

        await Navigation.PushModalAsync(scoringPage);
        int finalScore = await tcs.Task;

        // Award EXP
        var result = await _conversationService.AwardExpAsync(_conversation.Id, finalScore);

        // Reload conversation
        _conversation = await _conversationService.GetConversationAsync(_conversation.Id);

        // Show result
        string message = $"✅ Awarded {finalScore} EXP!\n\n" +
                        $"Level: {result.newLevel}\n" +
                        $"EXP: {result.newLevelExp} / {_conversationService.GetExpForNextLevel(result.newLevel)}\n" +
                        $"Times Practiced: {_conversation.TimesCompleted}";

        if (result.leveledUp)
        {
            message = $"🎉 LEVEL UP! You're now Level {result.newLevel}!\n\n" + message;
        }

        await DisplayAlert("Practice Complete", message, "OK");
    }
}
