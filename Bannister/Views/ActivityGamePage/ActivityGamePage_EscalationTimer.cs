using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Partial class containing Meaningful Escalation Timer UI and logic
/// </summary>
public partial class ActivityGamePage
{
    // UI Controls for escalation timer
    private Frame? _escalationTimerFrame;
    private ProgressBar? _escalationProgressBar;
    private Label? _escalationDaysLabel;
    private Button? _escalationResetButton;
    private Button? _escalationToggleButton;
    
    // Level caps panel
    private Frame? _levelCapsFrame;
    private VerticalStackLayout? _levelCapsStack;

    /// <summary>
    /// Build the Level Caps panel showing activities that cap progression
    /// </summary>
    private Frame BuildLevelCapsPanel()
    {
        _levelCapsFrame = new Frame
        {
            Padding = 12,
            CornerRadius = 12,
            BackgroundColor = Color.FromArgb("#FFF3E0"), // Light orange background
            HasShadow = true,
            BorderColor = Color.FromArgb("#FF9800"),
            Margin = new Thickness(0, 0, 0, 12),
            IsVisible = false // Hidden by default, shown when there are level caps
        };

        var stack = new VerticalStackLayout { Spacing = 8 };

        // Title
        var titleLabel = new Label
        {
            Text = "🔒 Level Caps",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#E65100")
        };
        stack.Children.Add(titleLabel);

        // Container for level cap items
        _levelCapsStack = new VerticalStackLayout { Spacing = 6 };
        stack.Children.Add(_levelCapsStack);

        _levelCapsFrame.Content = stack;
        return _levelCapsFrame;
    }

    /// <summary>
    /// Refresh the level caps panel with current data
    /// </summary>
    private async Task RefreshLevelCapsPanelAsync()
    {
        if (_game == null || _levelCapsStack == null || _levelCapsFrame == null) return;

        try
        {
            // Get current level first
            var (currentLevel, _, _) = await _exp.GetProgressAsync(_auth.CurrentUsername, _game.GameId);
            
            var levelCaps = await _exp.GetAllLevelCapsAsync(_auth.CurrentUsername, _game.GameId, currentLevel);
            
            _levelCapsStack.Children.Clear();

            if (levelCaps.Count == 0)
            {
                _levelCapsFrame.IsVisible = false;
                return;
            }

            _levelCapsFrame.IsVisible = true;

            foreach (var (activity, isBlocking) in levelCaps)
            {
                // Calculate days missing/blocking
                int daysNeeded = activity.LevelCapStreakRequired;
                int currentStreak = activity.DisplayDayStreak;
                
                var itemFrame = new Frame
                {
                    Padding = 8,
                    CornerRadius = 8,
                    BackgroundColor = isBlocking ? Color.FromArgb("#FFCDD2") : Color.FromArgb("#C8E6C9"),
                    HasShadow = false,
                    BorderColor = Colors.Transparent
                };

                var itemGrid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = GridLength.Auto }
                    },
                    ColumnSpacing = 8
                };

                // Activity info
                var infoStack = new VerticalStackLayout { Spacing = 2 };
                
                var nameLabel = new Label
                {
                    Text = activity.Name,
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#333"),
                    LineBreakMode = LineBreakMode.TailTruncation
                };
                infoStack.Children.Add(nameLabel);

                var detailLabel = new Label
                {
                    Text = $"Cap at Lv{activity.LevelCapAt} • Need {activity.LevelCapStreakRequired}d streak",
                    FontSize = 10,
                    TextColor = Color.FromArgb("#666")
                };
                infoStack.Children.Add(detailLabel);

                Grid.SetColumn(infoStack, 0);
                itemGrid.Children.Add(infoStack);

                if (isBlocking)
                {
                    // BLOCKING: Show fire emojis - one per day it's been blocking
                    // Calculate days blocking = days since we reached the cap level without required streak
                    // We use (required - current streak) as days blocking, minimum 1
                    int daysBlocking = Math.Max(1, daysNeeded - currentStreak);
                    
                    // Build the fire string - one 🔥 per day
                    string fireEmojis = string.Concat(Enumerable.Repeat("🔥", daysBlocking));
                    
                    var blockingStack = new VerticalStackLayout
                    {
                        HorizontalOptions = LayoutOptions.End,
                        Spacing = 2
                    };
                    
                    // Fire emojis row with BIG blocking days number
                    var fireRow = new HorizontalStackLayout
                    {
                        HorizontalOptions = LayoutOptions.End,
                        Spacing = 6
                    };
                    
                    // Fire emojis - wrap them so they stack vertically if many
                    var fireLabel = new Label
                    {
                        Text = fireEmojis,
                        FontSize = 18,
                        HorizontalOptions = LayoutOptions.End,
                        HorizontalTextAlignment = TextAlignment.End,
                        VerticalOptions = LayoutOptions.Center,
                        LineBreakMode = LineBreakMode.WordWrap,
                        MaximumWidthRequest = 60 // Forces wrapping after ~3 emojis
                    };
                    fireRow.Children.Add(fireLabel);
                    
                    // BIG blocking days number
                    var bigDaysLabel = new Label
                    {
                        Text = $"{daysBlocking}d",
                        FontSize = 28,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#D32F2F"),
                        VerticalOptions = LayoutOptions.Center
                    };
                    fireRow.Children.Add(bigDaysLabel);
                    
                    blockingStack.Children.Add(fireRow);
                    
                    // Progress toward unlock
                    var progressLabel = new Label
                    {
                        Text = $"{currentStreak}d / {daysNeeded}d streak",
                        FontSize = 10,
                        TextColor = Color.FromArgb("#666"),
                        HorizontalOptions = LayoutOptions.End
                    };
                    blockingStack.Children.Add(progressLabel);

                    // Status label
                    var statusLabel = new Label
                    {
                        Text = "⛔ BLOCKING",
                        FontSize = 9,
                        FontAttributes = FontAttributes.Bold,
                        HorizontalOptions = LayoutOptions.End,
                        TextColor = Color.FromArgb("#D32F2F")
                    };
                    blockingStack.Children.Add(statusLabel);
                    
                    Grid.SetColumn(blockingStack, 1);
                    itemGrid.Children.Add(blockingStack);
                }
                else
                {
                    // UNLOCKED: Normal display
                    var unlockedStack = new VerticalStackLayout 
                    { 
                        HorizontalOptions = LayoutOptions.End,
                        VerticalOptions = LayoutOptions.Center
                    };
                    
                    var streakLabel = new Label
                    {
                        Text = $"🔥 {activity.DisplayDayStreak}d",
                        FontSize = 11,
                        FontAttributes = FontAttributes.Bold,
                        HorizontalOptions = LayoutOptions.End,
                        TextColor = Color.FromArgb("#4CAF50")
                    };
                    unlockedStack.Children.Add(streakLabel);

                    var statusLabel = new Label
                    {
                        Text = "✅ UNLOCKED",
                        FontSize = 9,
                        FontAttributes = FontAttributes.Bold,
                        HorizontalOptions = LayoutOptions.End,
                        TextColor = Color.FromArgb("#388E3C")
                    };
                    unlockedStack.Children.Add(statusLabel);
                    
                    Grid.SetColumn(unlockedStack, 1);
                    itemGrid.Children.Add(unlockedStack);
                }

                itemFrame.Content = itemGrid;
                _levelCapsStack.Children.Add(itemFrame);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing level caps panel: {ex.Message}");
        }
    }

    /// <summary>
    /// Build the vertical escalation timer panel for the right side (compact version)
    /// </summary>
    private Frame BuildEscalationTimerPanel()
    {
        System.Diagnostics.Debug.WriteLine(">>> BuildEscalationTimerPanel() called");
        
        _escalationTimerFrame = new Frame
        {
            Padding = 10,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var stack = new VerticalStackLayout { Spacing = 6 };

        // Title and buttons in a row
        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 4
        };

        var titleLabel = new Label
        {
            Text = "Meaningful Escalation",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#333")
        };
        Grid.SetColumn(titleLabel, 0);
        headerGrid.Children.Add(titleLabel);

        // Reset button (small)
        _escalationResetButton = new Button
        {
            Text = "✓",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 4,
            FontSize = 10,
            Padding = new Thickness(6, 2),
            WidthRequest = 28,
            HeightRequest = 24
        };
        _escalationResetButton.Clicked += OnEscalationResetClicked;
        Grid.SetColumn(_escalationResetButton, 1);
        headerGrid.Children.Add(_escalationResetButton);

        // Toggle button (small)
        _escalationToggleButton = new Button
        {
            Text = "⏸",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 4,
            FontSize = 10,
            Padding = new Thickness(6, 2),
            WidthRequest = 28,
            HeightRequest = 24
        };
        _escalationToggleButton.Clicked += OnEscalationToggleClicked;
        Grid.SetColumn(_escalationToggleButton, 2);
        headerGrid.Children.Add(_escalationToggleButton);

        stack.Children.Add(headerGrid);

        // Days remaining label
        _escalationDaysLabel = new Label
        {
            Text = "30 days",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#4CAF50")
        };
        stack.Children.Add(_escalationDaysLabel);

        // Vertical progress bar container (REDUCED HEIGHT)
        var progressContainer = new Frame
        {
            Padding = 0,
            CornerRadius = 6,
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            HasShadow = false,
            HeightRequest = 120, // Reduced from 300
            WidthRequest = 30,   // Reduced from 40
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 4, 0, 4)
        };

        // Vertical progress bar (rotated horizontal bar)
        _escalationProgressBar = new ProgressBar
        {
            Progress = 1.0,
            ProgressColor = Color.FromArgb("#4CAF50"),
            BackgroundColor = Colors.Transparent,
            Rotation = -90,
            WidthRequest = 120, // Match container height
            HeightRequest = 30, // Match container width
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        progressContainer.Content = _escalationProgressBar;
        stack.Children.Add(progressContainer);

        // Info label (smaller)
        var infoLabel = new Label
        {
            Text = "Days to escalate",
            FontSize = 9,
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center
        };
        stack.Children.Add(infoLabel);

        _escalationTimerFrame.Content = stack;
        return _escalationTimerFrame;
    }

    /// <summary>
    /// Build a compact horizontal version of the escalation timer for mobile
    /// </summary>
    private Frame BuildEscalationTimerPanelMobile()
    {
        _escalationTimerFrame = new Frame
        {
            Padding = 12,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12,
            RowSpacing = 8,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            }
        };

        // Days remaining label (left)
        _escalationDaysLabel = new Label
        {
            Text = "30 days",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#4CAF50")
        };
        Grid.SetColumn(_escalationDaysLabel, 0);
        Grid.SetRowSpan(_escalationDaysLabel, 2);
        grid.Children.Add(_escalationDaysLabel);

        // Progress info (middle)
        var middleStack = new VerticalStackLayout { Spacing = 4 };

        var titleLabel = new Label
        {
            Text = "Meaningful Escalation",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        };
        middleStack.Children.Add(titleLabel);

        // Horizontal progress bar for mobile
        _escalationProgressBar = new ProgressBar
        {
            Progress = 1.0,
            ProgressColor = Color.FromArgb("#4CAF50"),
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            HeightRequest = 16
        };
        middleStack.Children.Add(_escalationProgressBar);

        var infoLabel = new Label
        {
            Text = "Days until you must meaningfully escalate",
            FontSize = 9,
            TextColor = Color.FromArgb("#666")
        };
        middleStack.Children.Add(infoLabel);

        Grid.SetColumn(middleStack, 1);
        Grid.SetRowSpan(middleStack, 2);
        grid.Children.Add(middleStack);

        // Reset button (right top)
        _escalationResetButton = new Button
        {
            Text = "✓",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 20,
            FontSize = 16,
            WidthRequest = 40,
            HeightRequest = 40,
            Padding = 0
        };
        _escalationResetButton.Clicked += OnEscalationResetClicked;
        Grid.SetColumn(_escalationResetButton, 2);
        Grid.SetRow(_escalationResetButton, 0);
        grid.Children.Add(_escalationResetButton);

        // Toggle button (right bottom)
        _escalationToggleButton = new Button
        {
            Text = "⏸",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 20,
            FontSize = 16,
            WidthRequest = 40,
            HeightRequest = 40,
            Padding = 0
        };
        _escalationToggleButton.Clicked += OnEscalationToggleClicked;
        Grid.SetColumn(_escalationToggleButton, 2);
        Grid.SetRow(_escalationToggleButton, 1);
        grid.Children.Add(_escalationToggleButton);

        _escalationTimerFrame.Content = grid;
        return _escalationTimerFrame;
    }

    /// <summary>
    /// Update the escalation timer display based on game state
    /// </summary>
    private async Task UpdateEscalationTimerAsync()
    {
        if (_game == null || _escalationDaysLabel == null || _escalationProgressBar == null)
            return;

        // Reload game to get fresh state
        _game = await _games.GetGameAsync(_auth.CurrentUsername, _game.GameId);
        if (_game == null) return;

        System.Diagnostics.Debug.WriteLine($">>> UpdateEscalationTimerAsync() called");
        System.Diagnostics.Debug.WriteLine($">>> IsEscalationTimerDisabled: {_game.IsEscalationTimerDisabled}");

        int daysRemaining = _game.DaysRemaining;
        double progress = _game.EscalationProgress;
        bool isDisabled = _game.IsEscalationTimerDisabled;

        System.Diagnostics.Debug.WriteLine($">>> DaysRemaining: {daysRemaining}");
        System.Diagnostics.Debug.WriteLine($">>> Progress: {progress}");

        // Update toggle button text
        if (_escalationToggleButton != null)
        {
            _escalationToggleButton.Text = isDisabled ? "▶" : "⏸";
            _escalationToggleButton.BackgroundColor = isDisabled ? 
                Color.FromArgb("#4CAF50") : Color.FromArgb("#FF9800");
        }

        // Update labels
        if (isDisabled)
        {
            _escalationDaysLabel.Text = "PAUSED";
            _escalationProgressBar.Progress = 1.0;
            _escalationDaysLabel.TextColor = Color.FromArgb("#999");
            _escalationProgressBar.ProgressColor = Color.FromArgb("#999");
        }
        else
        {
            _escalationDaysLabel.Text = daysRemaining == 1 ? "1 day" : $"{daysRemaining} days";
            _escalationProgressBar.Progress = progress;

            // Change colors based on days remaining
            Color textColor;
            Color barColor;

            if (daysRemaining > 15) // Green: 16-30 days
            {
                textColor = Color.FromArgb("#4CAF50");
                barColor = Color.FromArgb("#4CAF50");
            }
            else if (daysRemaining > 7) // Yellow: 8-15 days
            {
                textColor = Color.FromArgb("#FF9800");
                barColor = Color.FromArgb("#FF9800");
            }
            else if (daysRemaining > 0) // Red: 1-7 days
            {
                textColor = Color.FromArgb("#F44336");
                barColor = Color.FromArgb("#F44336");
            }
            else // Black/Dark: 0 days (expired)
            {
                textColor = Color.FromArgb("#212121");
                barColor = Color.FromArgb("#212121");
            }

            _escalationDaysLabel.TextColor = textColor;
            _escalationProgressBar.ProgressColor = barColor;
        }
        
        // Also refresh level caps panel
        await RefreshLevelCapsPanelAsync();
        
        System.Diagnostics.Debug.WriteLine($">>> UpdateEscalationTimerAsync() complete - showing {_escalationDaysLabel.Text}");
    }

    /// <summary>
    /// Check if escalation timer has expired and show accountability dialog
    /// </summary>
    private async Task CheckEscalationExpirationAsync()
    {
        if (_game == null) return;

        // Don't check if timer is disabled
        if (_game.IsEscalationTimerDisabled)
            return;

        // Don't check if timer hasn't been started yet
        if (!_game.LastMeaningfulEscalation.HasValue)
            return;

        bool hasExpired = await _games.HasEscalationExpiredAsync(_auth.CurrentUsername, _game.GameId);

        if (hasExpired)
        {
            // Check if we've already shown this dialog today
            string shownKey = $"EscalationExpired_Shown_{_auth.CurrentUsername}_{_game.GameId}";
            string lastShown = Preferences.Get(shownKey, "");
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            if (lastShown == today)
                return; // Already handled today

            Preferences.Set(shownKey, today);

            // Show accountability dialog
            bool acceptedLoss = await DisplayAlert(
                "⏰ Meaningful Escalation Required",
                $"You haven't meaningfully escalated in 30 days.\n\n" +
                $"A meaningful escalation means you've taken a significant step forward in your {_game.DisplayName} journey.\n\n" +
                $"Did you meaningfully escalate?",
                "You're right, I haven't", // Accept loss
                "I have!" // Claim escalation
            );

            if (acceptedLoss)
            {
                // User accepted they haven't escalated - lose 2 levels
                await ApplyEscalationPenaltyAsync();
            }
            else
            {
                // User claims they escalated - just reset timer
                await _games.ResetMeaningfulEscalationAsync(_auth.CurrentUsername, _game.GameId);
                await UpdateEscalationTimerAsync();
            }
        }
    }

    /// <summary>
    /// Apply the 2-level penalty for failing to escalate
    /// </summary>
    private async Task ApplyEscalationPenaltyAsync()
    {
        if (_game == null) return;

        // Get current level
        var (currentLevel, expIntoLevel, expNeeded) = await _exp.GetProgressAsync(_auth.CurrentUsername, _game.GameId);

        // Calculate EXP loss for 2 levels
        int expLoss = 0;

        // If at level 2 or higher, lose exactly 2 levels worth
        if (currentLevel >= 2)
        {
            // Calculate total EXP at current level start
            int currentLevelStartExp = ExpEngine.TotalExpAtLevelStart(currentLevel);
            
            // Calculate total EXP at 2 levels lower
            int targetLevel = currentLevel - 2;
            int targetLevelStartExp = ExpEngine.TotalExpAtLevelStart(targetLevel);
            
            // Loss is the difference plus current progress
            expLoss = (currentLevelStartExp - targetLevelStartExp) + expIntoLevel;
        }
        else if (currentLevel == 1)
        {
            // At level 1, lose all current EXP
            expLoss = expIntoLevel;
        }

        // Apply the penalty (negative EXP)
        if (expLoss > 0)
        {
            await _exp.ApplyExpAsync(
                _auth.CurrentUsername,
                _game.GameId,
                "❌ Meaningful Escalation Penalty",
                -expLoss,
                0 // No activity ID
            );
        }

        // Reset the timer
        await _games.ResetMeaningfulEscalationAsync(_auth.CurrentUsername, _game.GameId);

        // Refresh display
        await RefreshExpAsync();
        await UpdateEscalationTimerAsync();

        // Show result
        await DisplayAlert(
            "Penalty Applied",
            $"Lost 2 levels ({expLoss:N0} EXP)\n\n" +
            $"The timer has been reset to 30 days.\n\n" +
            $"Use this as motivation to meaningfully escalate soon!",
            "I understand"
        );
    }

    /// <summary>
    /// Handle manual reset button click
    /// </summary>
    private async void OnEscalationResetClicked(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(">>> Reset button clicked");
        if (_game == null) return;

        bool confirmed = await DisplayAlert(
            "Reset Escalation Timer?",
            "Have you meaningfully escalated your goals?\n\n" +
            "This will reset the timer back to 30 days.",
            "Yes, I have!",
            "Cancel"
        );

        if (confirmed)
        {
            await _games.ResetMeaningfulEscalationAsync(_auth.CurrentUsername, _game.GameId);
            await UpdateEscalationTimerAsync();

            await DisplayAlert(
                "Timer Reset",
                "Great work on your meaningful escalation! ✅\n\nTimer reset to 30 days.",
                "OK"
            );
        }
    }

    /// <summary>
    /// Handle toggle button click (enable/disable timer)
    /// </summary>
    private async void OnEscalationToggleClicked(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(">>> Toggle button clicked");
        if (_game == null) return;

        bool currentlyDisabled = _game.IsEscalationTimerDisabled;

        string title = currentlyDisabled ? "Enable Escalation Timer?" : "Disable Escalation Timer?";
        string message = currentlyDisabled ?
            "This will resume the countdown. The timer will continue from where it was paused." :
            "This will pause the timer. It won't count down or show expiration dialogs while disabled.";

        bool confirmed = await DisplayAlert(
            title,
            message,
            currentlyDisabled ? "Enable" : "Disable",
            "Cancel"
        );

        if (confirmed)
        {
            await _games.ToggleEscalationTimerAsync(_auth.CurrentUsername, _game.GameId);
            await UpdateEscalationTimerAsync();
        }
    }
}
