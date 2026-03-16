using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page for handling broken streak decisions - one activity at a time
/// </summary>
public class StreakBrokenPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ActivityService _activities;
    private readonly ExpService _exp;
    private readonly string _gameId;
    
    private List<(Activity activity, int brokenStreak, int penalty)> _brokenStreaks;
    private int _currentIndex = 0;
    
    // Track results
    private int _totalPenalty = 0;
    private List<string> _penaltyDetails = new();
    private List<(Activity activity, int fromLevel, int toLevel, int expLost)> _levelDowns = new();
    
    // UI elements
    private Label _progressLabel;
    private Label _activityNameLabel;
    private Label _streakInfoLabel;
    private Label _missedDateLabel;
    private Label _penaltyLabel;
    private Button _acceptButton;
    private Frame _levelDownWarning;
    private Label _levelDownLabel;
    
    // Result
    private TaskCompletionSource<bool> _tcs = new();

    public StreakBrokenPage(
        AuthService auth, 
        ActivityService activities, 
        ExpService exp,
        string gameId,
        List<(Activity activity, int brokenStreak, int penalty)> brokenStreaks)
    {
        _auth = auth;
        _activities = activities;
        _exp = exp;
        _gameId = gameId;
        _brokenStreaks = brokenStreaks;
        
        Title = "Streak Broken";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        
        BuildUI();
        ShowCurrentStreak();
    }

    public Task<bool> GetResultAsync() => _tcs.Task;

    public int TotalPenalty => _totalPenalty;
    public List<string> PenaltyDetails => _penaltyDetails;
    public List<(Activity activity, int fromLevel, int toLevel, int expLost)> LevelDowns => _levelDowns;

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 20
        };

        // Progress indicator
        _progressLabel = new Label
        {
            Text = "1 of 1",
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center
        };
        mainStack.Children.Add(_progressLabel);

        // Warning header
        var headerFrame = new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BackgroundColor = Color.FromArgb("#FFCDD2"),
            BorderColor = Color.FromArgb("#F44336"),
            HasShadow = false
        };

        var headerStack = new VerticalStackLayout { Spacing = 8 };
        
        headerStack.Children.Add(new Label
        {
            Text = "⚠️ Streak Broken",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#C62828"),
            HorizontalOptions = LayoutOptions.Center
        });

        headerStack.Children.Add(new Label
        {
            Text = "Your streak was broken. Choose how to handle it.",
            FontSize = 14,
            TextColor = Color.FromArgb("#D32F2F"),
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center
        });

        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        // Activity info card
        var activityFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var activityStack = new VerticalStackLayout { Spacing = 12 };

        activityStack.Children.Add(new Label
        {
            Text = "Activity:",
            FontSize = 12,
            TextColor = Color.FromArgb("#999")
        });

        _activityNameLabel = new Label
        {
            Text = "Activity Name",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        activityStack.Children.Add(_activityNameLabel);

        _streakInfoLabel = new Label
        {
            Text = "🔥 7 day streak was broken",
            FontSize = 16,
            TextColor = Color.FromArgb("#FF5722"),
            FontAttributes = FontAttributes.Bold
        };
        activityStack.Children.Add(_streakInfoLabel);

        _missedDateLabel = new Label
        {
            Text = "Missed: March 14, 2026",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        };
        activityStack.Children.Add(_missedDateLabel);

        _penaltyLabel = new Label
        {
            Text = "Penalty: -14 EXP",
            FontSize = 14,
            TextColor = Color.FromArgb("#F44336")
        };
        activityStack.Children.Add(_penaltyLabel);

        activityFrame.Content = activityStack;
        mainStack.Children.Add(activityFrame);

        // Level down warning (hidden by default)
        _levelDownWarning = new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = Color.FromArgb("#FFF3E0"),
            BorderColor = Color.FromArgb("#FF9800"),
            HasShadow = false,
            IsVisible = false
        };

        _levelDownLabel = new Label
        {
            Text = "⚠️ Accepting will also LEVEL DOWN to level X",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#E65100"),
            HorizontalTextAlignment = TextAlignment.Center
        };
        _levelDownWarning.Content = _levelDownLabel;
        mainStack.Children.Add(_levelDownWarning);

        // Options
        var optionsLabel = new Label
        {
            Text = "Choose an option:",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            Margin = new Thickness(0, 10, 0, 0)
        };
        mainStack.Children.Add(optionsLabel);

        // Accept Penalty button
        _acceptButton = new Button
        {
            Text = "✅ Accept Penalty",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 50
        };
        _acceptButton.Clicked += OnAcceptPenaltyClicked;
        mainStack.Children.Add(_acceptButton);

        // Day before yesterday didn't count button
        var dayBeforeBtn = new Button
        {
            Text = "📅 That Day Didn't Count",
            BackgroundColor = Color.FromArgb("#2196F3"),
            TextColor = Colors.White,
            FontSize = 16,
            CornerRadius = 8,
            HeightRequest = 50
        };
        dayBeforeBtn.Clicked += OnDayDidntCountClicked;
        mainStack.Children.Add(dayBeforeBtn);

        // Add explanation
        mainStack.Children.Add(new Label
        {
            Text = "Use if the missed day was an exception (sick, travel, etc.)",
            FontSize = 11,
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, -12, 0, 0)
        });

        // Forgot to click button
        var forgotBtn = new Button
        {
            Text = "🔄 Forgot to Log That Day",
            BackgroundColor = Color.FromArgb("#9C27B0"),
            TextColor = Colors.White,
            FontSize = 16,
            CornerRadius = 8,
            HeightRequest = 50
        };
        forgotBtn.Clicked += OnForgotToClickClicked;
        mainStack.Children.Add(forgotBtn);

        // Add explanation
        mainStack.Children.Add(new Label
        {
            Text = "Use if you did the activity but forgot to log it",
            FontSize = 11,
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, -12, 0, 0)
        });

        var scrollView = new ScrollView { Content = mainStack };
        Content = scrollView;
    }

    private async void ShowCurrentStreak()
    {
        if (_currentIndex >= _brokenStreaks.Count)
        {
            // All done
            _tcs.TrySetResult(true);
            await Navigation.PopModalAsync();
            return;
        }

        var (activity, brokenStreak, penalty) = _brokenStreaks[_currentIndex];

        _progressLabel.Text = $"{_currentIndex + 1} of {_brokenStreaks.Count}";
        _activityNameLabel.Text = activity.Name;
        _streakInfoLabel.Text = $"🔥 {brokenStreak} day streak was broken";
        
        // The missed day is 2 days ago (day before yesterday)
        var missedDate = DateTime.Now.AddDays(-2);
        _missedDateLabel.Text = $"Missed: {missedDate:dddd, MMMM d}";
        
        _penaltyLabel.Text = $"Penalty: {penalty} EXP";

        // Check for level down
        if (activity.HasLevelCap && activity.LevelDownOnStreakBreak)
        {
            var (currentLevel, _, _) = await _exp.GetProgressAsync(_auth.CurrentUsername, _gameId);
            if (currentLevel > activity.LevelCapAt)
            {
                _levelDownWarning.IsVisible = true;
                _levelDownLabel.Text = $"⚠️ Accepting will also LEVEL DOWN to level {activity.LevelCapAt}";
                _acceptButton.Text = $"✅ Accept Penalty + Level Down";
                _acceptButton.BackgroundColor = Color.FromArgb("#FF9800");
            }
            else
            {
                _levelDownWarning.IsVisible = false;
                _acceptButton.Text = "✅ Accept Penalty";
                _acceptButton.BackgroundColor = Color.FromArgb("#4CAF50");
            }
        }
        else
        {
            _levelDownWarning.IsVisible = false;
            _acceptButton.Text = "✅ Accept Penalty";
            _acceptButton.BackgroundColor = Color.FromArgb("#4CAF50");
        }
    }

    private async void OnAcceptPenaltyClicked(object sender, EventArgs e)
    {
        var (activity, brokenStreak, penalty) = _brokenStreaks[_currentIndex];

        // Apply penalty
        if (penalty < 0)
        {
            await _exp.ApplyExpAsync(_auth.CurrentUsername, _gameId, $"{activity.Name} (Streak Broken)", penalty, activity.Id);
            _totalPenalty += penalty;
            _penaltyDetails.Add($"{activity.Name}: {brokenStreak} day streak → {penalty} EXP");
        }

        // Check for level down
        if (activity.HasLevelCap && activity.LevelDownOnStreakBreak)
        {
            var levelDownResult = await _exp.HandleStreakBreakLevelDownAsync(
                _auth.CurrentUsername, 
                _gameId, 
                activity);

            if (levelDownResult.leveledDown)
            {
                _levelDowns.Add((activity, levelDownResult.fromLevel, levelDownResult.toLevel, levelDownResult.expLost));
            }
        }

        MoveToNext();
    }

    private async void OnDayDidntCountClicked(object sender, EventArgs e)
    {
        var (activity, brokenStreak, _) = _brokenStreaks[_currentIndex];

        // Restore the streak - the missed day was 2 days ago (day before yesterday)
        // Set LastDisplayDayUsed to day before yesterday so today's check passes
        activity.DisplayDayStreak = brokenStreak;
        activity.LastDisplayDayUsed = DateTime.Now.AddDays(-2);
        await _activities.UpdateActivityAsync(activity);

        string missedDate = DateTime.Now.AddDays(-2).ToString("MMM dd");
        await DisplayAlert("Streak Restored", 
            $"'{activity.Name}' streak of {brokenStreak} days has been restored.\n\nReason: {missedDate} excluded", 
            "OK");

        MoveToNext();
    }

    private async void OnForgotToClickClicked(object sender, EventArgs e)
    {
        var (activity, brokenStreak, _) = _brokenStreaks[_currentIndex];

        // Restore the streak - user claims they did it 2 days ago but forgot to log
        activity.DisplayDayStreak = brokenStreak;
        activity.LastDisplayDayUsed = DateTime.Now.AddDays(-2);
        await _activities.UpdateActivityAsync(activity);

        string missedDate = DateTime.Now.AddDays(-2).ToString("MMM dd");
        await DisplayAlert("Streak Restored", 
            $"'{activity.Name}' streak of {brokenStreak} days has been restored.\n\nReason: Retroactive log for {missedDate}", 
            "OK");

        MoveToNext();
    }

    private void MoveToNext()
    {
        _currentIndex++;
        ShowCurrentStreak();
    }

    protected override bool OnBackButtonPressed()
    {
        // Prevent back button - must make a choice
        return true;
    }
}
