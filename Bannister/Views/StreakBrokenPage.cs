using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page for handling broken streak decisions - one activity at a time
/// </summary>
public class StreakBrokenPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly ActivityService _activities;
    private readonly ExpService _exp;
    private readonly StreakService _streaks;
    private readonly string _gameId;
    
    private List<(Activity activity, int brokenStreak, int penalty)> _brokenStreaks;
    private int _currentIndex = 0;
    
    // Track results
    private int _totalPenalty = 0;
    private List<string> _penaltyDetails = new();
    private List<(Activity activity, int fromLevel, int toLevel, int expLost)> _levelDowns = new();
    
    // UI elements
    private Label _progressLabel;
    private Label _activityNameLabel = new();
    private VerticalStackLayout _activityVisualContainer = new();
    private Label _streakInfoLabel;
    private Label _missedDateLabel;
    private Label _penaltyLabel;
    private Button _acceptButton;
    private Button _dayDidntCountButton;
    private Button _dayDidntCountWeeklyButton;
    private Button _forgotButton;
    private Frame _levelDownWarning;
    private Label _levelDownLabel;
    
    // Loading state
    private bool _isProcessing = false;
    
    // Result
    private TaskCompletionSource<bool> _tcs = new();

    public StreakBrokenPage(
        AuthService auth, 
        ActivityService activities, 
        ExpService exp,
        StreakService streaks,
        string gameId,
        List<(Activity activity, int brokenStreak, int penalty)> brokenStreaks)
    {
        _auth = auth;
        _activities = activities;
        _exp = exp;
        _streaks = streaks;
        _gameId = gameId;
        _brokenStreaks = brokenStreaks;
        
        Title = "Streak Broken";

        
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
        _progressLabel = (new Label
        {
            Text = "1 of 1",
            FontSize = 14,
            HorizontalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true);
        mainStack.Children.Add(_progressLabel);

        // Warning header
        var headerFrame = (new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            HasShadow = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.ErrorButtonBackground);

        var headerStack = new VerticalStackLayout { Spacing = 8 };
        
        headerStack.Children.Add((new Label
        {
            Text = "⚠️ Streak Broken",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ErrorText, ThemeKey.PageText1, classifyPageText: true));

        headerStack.Children.Add((new Label
        {
            Text = "Your streak was broken. Choose how to handle it.",
            FontSize = 14,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ErrorText, ThemeKey.PageText1, classifyPageText: true));

        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        // Activity info card
        var activityFrame = (new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            HasShadow = true,
            BorderColor = Colors.Transparent
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var activityStack = new VerticalStackLayout { Spacing = 12 };

        activityStack.Children.Add((new Label
        {
            Text = "Activity:",
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));

        _activityVisualContainer = new VerticalStackLayout { Spacing = 4 };
        activityStack.Children.Add(_activityVisualContainer);

        _streakInfoLabel = (new Label
        {
            Text = "🔥 7 day streak was broken",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningButtonBackground, ThemeKey.PageText1, classifyPageText: true);
        activityStack.Children.Add(_streakInfoLabel);

        _missedDateLabel = (new Label
        {
            Text = "Missed: March 14, 2026",
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true);
        activityStack.Children.Add(_missedDateLabel);

        _penaltyLabel = (new Label
        {
            Text = "Penalty: -14 EXP",
            FontSize = 14
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ErrorButtonBackground, ThemeKey.PageText1, classifyPageText: true);
        activityStack.Children.Add(_penaltyLabel);

        activityFrame.Content = activityStack;
        mainStack.Children.Add(activityFrame);

        // Level down warning (hidden by default)
        _levelDownWarning = (new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            HasShadow = false,
            IsVisible = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.WarningText);

        _levelDownLabel = (new Label
        {
            Text = "⚠️ Accepting will also LEVEL DOWN to level X",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText1, classifyPageText: true);
        _levelDownWarning.Content = _levelDownLabel;
        mainStack.Children.Add(_levelDownWarning);

        // Options
        var optionsLabel = (new Label
        {
            Text = "Choose an option:",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 10, 0, 0)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true);
        mainStack.Children.Add(optionsLabel);

        // Accept Penalty button
        _acceptButton = (new Button
        {
            Text = "✅ Accept Penalty",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 50
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _acceptButton.Clicked += OnAcceptPenaltyClicked;
        mainStack.Children.Add(_acceptButton);

        // Day before yesterday didn't count button
        _dayDidntCountButton = (new Button
        {
            Text = "📅 That Day Didn't Count",
            FontSize = 16,
            CornerRadius = 8,
            HeightRequest = 50
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _dayDidntCountButton.Clicked += OnDayDidntCountClicked;
        mainStack.Children.Add(_dayDidntCountButton);

        // Add explanation
        mainStack.Children.Add((new Label
        {
            Text = "Use if the missed day was an exception (sick, travel, etc.)",
            FontSize = 11,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, -12, 0, 0)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));

        // Day didn't count + change to weekly button
        _dayDidntCountWeeklyButton = (new Button
        {
            Text = "📅 Didn't Count + Change to Weekly",
            FontSize = 16,
            CornerRadius = 8,
            HeightRequest = 50
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightTealText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _dayDidntCountWeeklyButton.Clicked += OnDayDidntCountWeeklyClicked;
        mainStack.Children.Add(_dayDidntCountWeeklyButton);

        // Add explanation
        mainStack.Children.Add((new Label
        {
            Text = "Restore streak + set this activity to only display once a week",
            FontSize = 11,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, -12, 0, 0)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));

        // Forgot to click button
        _forgotButton = (new Button
        {
            Text = "🔄 Forgot to Log That Day",
            FontSize = 16,
            CornerRadius = 8,
            HeightRequest = 50
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _forgotButton.Clicked += OnForgotToClickClicked;
        mainStack.Children.Add(_forgotButton);

        // Add explanation
        mainStack.Children.Add((new Label
        {
            Text = "Use if you did the activity but forgot to log it",
            FontSize = 11,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, -12, 0, 0)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));

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
        _activityVisualContainer.Children.Clear();
        _activityVisualContainer.Children.Add(BuildActivityVisual(activity));
        _streakInfoLabel.Text = $"🔥 {brokenStreak} day streak was broken";
        
        // The missed day is yesterday
        var missedDate = DateTime.Now.AddDays(-1);
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
                _acceptButton.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningButtonBackground);
            }
            else
            {
                _levelDownWarning.IsVisible = false;
                _acceptButton.Text = "✅ Accept Penalty";
                _acceptButton.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground);
            }
        }
        else
        {
            _levelDownWarning.IsVisible = false;
            _acceptButton.Text = "✅ Accept Penalty";
            _acceptButton.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground);
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _isProcessing = !enabled;
        _acceptButton.IsEnabled = enabled;
        _dayDidntCountButton.IsEnabled = enabled;
        _dayDidntCountWeeklyButton.IsEnabled = enabled;
        _forgotButton.IsEnabled = enabled;
        
        // Visual feedback - dim buttons when disabled
        _acceptButton.Opacity = enabled ? 1.0 : 0.6;
        _dayDidntCountButton.Opacity = enabled ? 1.0 : 0.6;
        _dayDidntCountWeeklyButton.Opacity = enabled ? 1.0 : 0.6;
        _forgotButton.Opacity = enabled ? 1.0 : 0.6;
    }

    private static View BuildActivityVisual(Activity activity)
    {
        if (activity.IsImageOnly && !string.IsNullOrWhiteSpace(activity.ImagePath))
        {
            var stack = new VerticalStackLayout { Spacing = 4 };
            var img = new Image
            {
                Source = ImageSource.FromFile(ResolveImagePath(activity.ImagePath)),
                WidthRequest = 60,
                HeightRequest = 60,
                Aspect = Aspect.AspectFill
            };
            stack.Children.Add(img);

            var nameLabel = (new Label
            {
                Text = activity.Name,
                FontSize = 11,
                IsVisible = false,
                LineBreakMode = LineBreakMode.WordWrap
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true);
            var expandLabel = (new Label
            {
                Text = "▼ show name",
                FontSize = 10
            }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText3, classifyPageText: true);
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                nameLabel.IsVisible = !nameLabel.IsVisible;
                expandLabel.Text = nameLabel.IsVisible ? "▲ hide" : "▼ show name";
            };
            expandLabel.GestureRecognizers.Add(tap);
            stack.Children.Add(expandLabel);
            stack.Children.Add(nameLabel);
            return stack;
        }

        return (new Label
        {
            Text = activity.Name,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true);
    }

    private static string? ResolveImagePath(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath)) return null;
        if (Path.IsPathRooted(storedPath) && File.Exists(storedPath)) return storedPath;
        var filename = Path.IsPathRooted(storedPath) ? Path.GetFileName(storedPath) : storedPath;
        var fullPath = Path.Combine(FileSystem.AppDataDirectory, "ActivityImages", filename);
        return File.Exists(fullPath) ? fullPath : null;
    }

    private async void OnAcceptPenaltyClicked(object sender, EventArgs e)
    {
        if (_isProcessing) return;
        SetButtonsEnabled(false);
        
        try
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
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async void OnDayDidntCountClicked(object sender, EventArgs e)
    {
        if (_isProcessing) return;
        SetButtonsEnabled(false);
        
        try
        {
            var (activity, brokenStreak, _) = _brokenStreaks[_currentIndex];

            // Restore the DisplayDayStreak - the missed day was yesterday but it was an exception
            // Set LastDisplayDayUsed to yesterday so today continues the streak
            activity.DisplayDayStreak = brokenStreak;
            activity.LastDisplayDayUsed = DateTime.Now.AddDays(-1);
            
            // Also restore HabitStreak if it exists (to prevent reset on next use)
            // We don't know the original value, but keeping current value prevents unwanted reset
            // The HabitStreak only resets when RecordHabitCompletionAsync detects a gap,
            // so we update LastHabitDate to yesterday to prevent that
            if (activity.HabitType != "None" && activity.LastHabitDate.HasValue)
            {
                activity.LastHabitDate = DateTime.Now.AddDays(-1);
            }
            
            await _activities.UpdateActivityAsync(activity);

            // Also restore any broken StreakAttempt for this activity
            await RestoreStreakAttemptIfBroken(activity);

            string missedDate = DateTime.Now.AddDays(-1).ToString("MMM dd");
            await DisplayAlert("Streak Restored", 
                $"'{activity.Name}' streak of {brokenStreak} days has been restored.\n\nReason: {missedDate} excluded", 
                "OK");

            MoveToNext();
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async void OnForgotToClickClicked(object sender, EventArgs e)
    {
        if (_isProcessing) return;
        SetButtonsEnabled(false);
        
        try
        {
            var (activity, brokenStreak, _) = _brokenStreaks[_currentIndex];

            // Restore the DisplayDayStreak - user did the activity yesterday but forgot to log
            // Set LastDisplayDayUsed to yesterday so today continues the streak
            activity.DisplayDayStreak = brokenStreak;
            activity.LastDisplayDayUsed = DateTime.Now.AddDays(-1);
            
            // Also restore HabitStreak - update LastHabitDate to yesterday to prevent reset
            if (activity.HabitType != "None")
            {
                activity.LastHabitDate = DateTime.Now.AddDays(-1);
                // Increment HabitStreak since they claim they did it yesterday
                activity.HabitStreak++;
            }
            
            await _activities.UpdateActivityAsync(activity);
            
            // Also restore any broken StreakAttempt for this activity
            await RestoreStreakAttemptIfBroken(activity);
            
            // Award the EXP for the missed day (since they claim they did it)
            int expAmount = activity.ExpGain;
            string missedDate = DateTime.Now.AddDays(-1).ToString("MMM dd");
            
            if (expAmount > 0)
            {
                await _exp.ApplyExpAsync(
                    _auth.CurrentUsername, 
                    _gameId, 
                    $"{activity.Name} (Retroactive {missedDate})", 
                    expAmount, 
                    activity.Id);
                    
                await DisplayAlert("Streak Restored + EXP Awarded", 
                    $"'{activity.Name}' streak of {brokenStreak} days has been restored.\n\n+{expAmount} EXP awarded for {missedDate}", 
                    "OK");
            }
            else
            {
                await DisplayAlert("Streak Restored", 
                    $"'{activity.Name}' streak of {brokenStreak} days has been restored.\n\nReason: Retroactive log for {missedDate}", 
                    "OK");
            }

            MoveToNext();
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async void OnDayDidntCountWeeklyClicked(object sender, EventArgs e)
    {
        if (_isProcessing) return;
        SetButtonsEnabled(false);

        try
        {
            var (activity, brokenStreak, _) = _brokenStreaks[_currentIndex];

            // Ask which day of the week
            string[] days = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
            string[] dayAbbrevs = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            
            string picked = await DisplayActionSheet(
                "Display on which day?",
                "Cancel",
                null,
                days);

            if (string.IsNullOrEmpty(picked) || picked == "Cancel")
            {
                SetButtonsEnabled(true);
                return;
            }

            int dayIndex = Array.IndexOf(days, picked);
            string dayAbbrev = dayAbbrevs[dayIndex];

            // 1. Restore the streak (same as "Day Didn't Count")
            activity.DisplayDayStreak = brokenStreak;
            activity.LastDisplayDayUsed = DateTime.Now.AddDays(-1);
            
            if (activity.HabitType != "None" && activity.LastHabitDate.HasValue)
            {
                activity.LastHabitDate = DateTime.Now.AddDays(-1);
            }

            // 2. Change display days to only the selected day
            activity.DisplayDaysOfWeek = dayAbbrev;
            activity.DisplayDayOfMonth = 0; // Clear any day-of-month setting

            await _activities.UpdateActivityAsync(activity);

            // 3. Restore any broken StreakAttempt
            await RestoreStreakAttemptIfBroken(activity);

            string missedDate = DateTime.Now.AddDays(-1).ToString("MMM dd");
            await DisplayAlert("Streak Restored + Changed to Weekly",
                $"'{activity.Name}' streak of {brokenStreak} days restored.\n\n" +
                $"Now displays only on {picked}s.",
                "OK");

            MoveToNext();
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    /// <summary>
    /// Check if there's a recently broken StreakAttempt for this activity and reactivate it.
    /// This handles the case where StreakService.CheckAndBreakExpiredStreaksAsync already broke the streak.
    /// </summary>
    private async Task RestoreStreakAttemptIfBroken(Activity activity)
    {
        if (!activity.IsStreakTracked) return;
        
        var attempts = await _streaks.GetStreakAttemptsAsync(_auth.CurrentUsername, _gameId, activity.Id);
        
        // Check if there's an active streak - if so, nothing to restore
        var activeStreak = attempts.FirstOrDefault(a => a.IsActive);
        if (activeStreak != null) return;
        
        // Find the most recent ended streak (should be the one that just broke)
        var recentBroken = attempts
            .Where(a => !a.IsActive && a.EndedAt.HasValue)
            .OrderByDescending(a => a.EndedAt)
            .FirstOrDefault();
        
        if (recentBroken != null)
        {
            // Check if it was broken recently (within last 2 days)
            var daysSinceBroken = (DateTime.UtcNow - recentBroken.EndedAt.Value).TotalDays;
            if (daysSinceBroken <= 2)
            {
                // Reactivate this streak
                await _streaks.ReactivateStreakAsync(recentBroken.Id);
                System.Diagnostics.Debug.WriteLine($"[STREAK BROKEN PAGE] Reactivated streak attempt #{recentBroken.AttemptNumber} for {activity.Name}");
            }
        }
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
