using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page showing all active dragons with collapsible attempt history
/// Similar to StreakDashboardPage but for dragon-slaying attempts
/// </summary>
public class ActiveDragonsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DragonService _dragons;
    private readonly AttemptService _attempts;

    private VerticalStackLayout _dragonsList;
    private ScrollView _mainScroll;
    private Picker _sortPicker;
    private string _currentSort = "Days (High to Low)";
    private Dictionary<string, bool> _expandedDragons = new(); // Track which dragons are expanded (key = Game+DragonTitle)
    private double _savedScrollY = 0;

    public ActiveDragonsPage(AuthService auth, DragonService dragons, AttemptService attempts)
    {
        _auth = auth;
        _dragons = dragons;
        _attempts = attempts;

        Title = "Active Dragons";
        BackgroundColor = Color.FromArgb("#F5F7FC");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ProcessAutoIncrementsAsync();
        await LoadDragonsAsync();
    }

    private void BuildUI()
    {
        _mainScroll = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 20
        };

        // Header
        var headerFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Color.FromArgb("#5B63EE"),
            BorderColor = Colors.Transparent,
            HasShadow = true
        };

        var headerStack = new VerticalStackLayout { Spacing = 4 };

        headerStack.Children.Add(new Label
        {
            Text = "🐉 Active Dragons",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        headerStack.Children.Add(new Label
        {
            Text = "Track your attempts to slay each dragon (reach Level 100)",
            FontSize = 13,
            TextColor = Color.FromArgb("#FFFFFFCC")
        });

        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        // Add Dragon button
        var btnAddDragon = new Button
        {
            Text = "➕ Add Dragon",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 44,
            HorizontalOptions = LayoutOptions.Center,
            Padding = new Thickness(20, 0)
        };
        btnAddDragon.Clicked += OnAddDragonClicked;
        mainStack.Children.Add(btnAddDragon);

        // Sort picker
        var sortStack = new HorizontalStackLayout 
        { 
            Spacing = 8, 
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };
        sortStack.Children.Add(new Label
        {
            Text = "Sort by:",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        });
        _sortPicker = new Picker
        {
            ItemsSource = new List<string>
            {
                "Days (High to Low)",
                "Days (Low to High)",
                "Alphabetical (A-Z)",
                "Alphabetical (Z-A)",
                "Recently Started"
            },
            SelectedIndex = 0,
            WidthRequest = 180,
            TextColor = Colors.Black
        };
        _sortPicker.SelectedIndexChanged += async (s, e) =>
        {
            if (_sortPicker.SelectedItem != null)
            {
                _currentSort = _sortPicker.SelectedItem.ToString()!;
                await LoadDragonsAsync();
            }
        };
        sortStack.Children.Add(_sortPicker);
        mainStack.Children.Add(sortStack);

        // Info
        mainStack.Children.Add(new Label
        {
            Text = "💡 Click ▶ to expand attempt history for each dragon",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center
        });

        // Dragons list container
        _dragonsList = new VerticalStackLayout { Spacing = 16 };
        mainStack.Children.Add(_dragonsList);

        _mainScroll.Content = mainStack;
        Content = _mainScroll;
    }

    private async Task LoadDragonsAsync()
    {
        _dragonsList.Children.Clear();

        var dragons = await _dragons.GetActiveDragonsAsync(_auth.CurrentUsername);

        if (dragons.Count == 0)
        {
            _dragonsList.Children.Add(new Label
            {
                Text = "No active dragons.\n\nClick 'Add Dragon' to create one!",
                FontSize = 14,
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        // Get attempts for all dragons to calculate days for sorting
        var dragonData = new List<(Dragon dragon, List<Attempt> attempts, int activeDays)>();
        foreach (var dragon in dragons)
        {
            var attempts = await _attempts.GetAttemptsForDragonAsync(_auth.CurrentUsername, dragon.Game, dragon.Title);
            var activeAttempt = attempts.FirstOrDefault(a => a.IsActive);
            
            // Get actual day count - use logs if auto-increment is enabled
            int days = 0;
            if (activeAttempt != null)
            {
                days = await GetActualDayCountAsync(dragon, activeAttempt);
            }
            
            dragonData.Add((dragon, attempts, days));
        }

        // Apply sorting
        IEnumerable<(Dragon dragon, List<Attempt> attempts, int activeDays)> sorted = _currentSort switch
        {
            "Days (High to Low)" => dragonData.OrderByDescending(d => d.activeDays),
            "Days (Low to High)" => dragonData.OrderBy(d => d.activeDays),
            "Alphabetical (A-Z)" => dragonData.OrderBy(d => d.dragon.Title),
            "Alphabetical (Z-A)" => dragonData.OrderByDescending(d => d.dragon.Title),
            "Recently Started" => dragonData.OrderByDescending(d => 
                d.attempts.FirstOrDefault(a => a.IsActive)?.StartedAt ?? DateTime.MinValue),
            _ => dragonData.OrderByDescending(d => d.activeDays)
        };

        foreach (var (dragon, attempts, activeDays) in sorted)
        {
            _dragonsList.Children.Add(await BuildDragonSectionAsync(dragon, attempts, activeDays));
        }
    }

    /// <summary>
    /// Get the actual day count for a dragon attempt.
    /// Uses DragonDayLog if auto-increment is enabled, otherwise calculates from dates.
    /// </summary>
    private async Task<int> GetActualDayCountAsync(Dragon dragon, Attempt attempt)
    {
        if (dragon.IsAutoIncrement)
        {
            // Get day count from logs
            var conn = await GetConnectionAsync();
            var logs = await conn.Table<DragonDayLog>()
                .Where(l => l.Username == _auth.CurrentUsername 
                    && l.Game == dragon.Game 
                    && l.DragonTitle == dragon.Title
                    && l.AttemptNumber == attempt.AttemptNumber)
                .ToListAsync();

            if (logs.Count > 0)
            {
                return logs.Max(l => l.DayNumber);
            }
        }
        
        // Fall back to calculated duration
        return attempt.DurationDays;
    }

    private string GetDragonKey(Dragon dragon) => $"{dragon.Game}|{dragon.Title}";

    private async Task<View> BuildDragonSectionAsync(Dragon dragon, List<Attempt> attempts, int displayDays)
    {
        var container = new VerticalStackLayout { Spacing = 8 };

        // Sort attempts: newest first (by attempt number descending)
        var sortedAttempts = attempts.OrderByDescending(a => a.AttemptNumber).ToList();
        var activeAttempt = sortedAttempts.FirstOrDefault(a => a.IsActive);
        var hasActiveAttempt = activeAttempt != null;

        string dragonKey = GetDragonKey(dragon);

        // Initialize expanded state if not set
        if (!_expandedDragons.ContainsKey(dragonKey))
        {
            _expandedDragons[dragonKey] = false;
        }

        bool isExpanded = _expandedDragons[dragonKey];

        // Dragon header card
        var headerFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = hasActiveAttempt ? Color.FromArgb("#E8EAF6") : Colors.White,
            BorderColor = hasActiveAttempt ? Color.FromArgb("#5B63EE") : Color.FromArgb("#E0E0E0"),
            HasShadow = true
        };

        var headerStack = new VerticalStackLayout { Spacing = 8 };

        // Title row with expand button and dragon image
        var titleRow = new HorizontalStackLayout { Spacing = 12 };

        // Expand/Collapse button
        var btnExpand = new Button
        {
            Text = isExpanded ? $"▼ ({sortedAttempts.Count})" : $"▶ ({sortedAttempts.Count})",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#5B63EE"),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 30,
            Padding = new Thickness(4, 0)
        };
        btnExpand.Clicked += (s, e) =>
        {
            _expandedDragons[dragonKey] = !_expandedDragons[dragonKey];
            _ = LoadDragonsAsync();
        };
        titleRow.Children.Add(btnExpand);

        // Dragon image (small)
        var dragonImage = new Image
        {
            WidthRequest = 40,
            HeightRequest = 40,
            Aspect = Aspect.AspectFill
        };
        string imagePath = GetFullImagePath(dragon.ImagePath);
        dragonImage.Source = !string.IsNullOrEmpty(imagePath) ? imagePath : "diet_dragon_default.png";
        titleRow.Children.Add(dragonImage);

        titleRow.Children.Add(new Label
        {
            Text = dragon.Title ?? "Unnamed Dragon",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.FillAndExpand
        });

        if (hasActiveAttempt)
        {
            titleRow.Children.Add(new Label
            {
                Text = "⚔️ Battling",
                FontSize = 14,
                TextColor = Color.FromArgb("#5B63EE"),
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            });
        }

        headerStack.Children.Add(titleRow);

        // Game name
        string gameDisplay = dragon.Game == "_standalone_" ? "Standalone Dragon" : dragon.Game;
        headerStack.Children.Add(new Label
        {
            Text = $"📁 {gameDisplay}",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            Margin = new Thickness(40, 0, 0, 0)
        });

        // Current attempt summary - use displayDays instead of activeAttempt.DurationDays
        if (hasActiveAttempt)
        {
            var attemptRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(40, 4, 0, 0) };

            attemptRow.Children.Add(new Label
            {
                Text = displayDays.ToString(),
                FontSize = 28,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#5B63EE"),
                VerticalOptions = LayoutOptions.Center
            });

            attemptRow.Children.Add(new Label
            {
                Text = displayDays == 1 ? "day battling" : "days battling",
                FontSize = 14,
                TextColor = Color.FromArgb("#666"),
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(0, 0, 0, 4)
            });

            attemptRow.Children.Add(new Label
            {
                Text = $"(Attempt #{activeAttempt!.AttemptNumber})",
                FontSize = 12,
                TextColor = Color.FromArgb("#999"),
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(8, 0, 0, 4)
            });

            headerStack.Children.Add(attemptRow);

            // Started date
            if (activeAttempt.StartedAt.HasValue)
            {
                headerStack.Children.Add(new Label
                {
                    Text = $"Started {activeAttempt.StartedAt.Value.ToLocalTime():MMM d, yyyy}",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#999"),
                    Margin = new Thickness(40, 0, 0, 0)
                });
            }
        }
        else
        {
            headerStack.Children.Add(new Label
            {
                Text = sortedAttempts.Count == 0 ? "No attempts yet" : "No active attempt",
                FontSize = 13,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic,
                Margin = new Thickness(40, 0, 0, 0)
            });
        }

        // Buttons row
        var buttonsRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(40, 8, 0, 0) };

        // Start New Attempt button (only show if no active attempt)
        if (!hasActiveAttempt)
        {
            var btnStartAttempt = new Button
            {
                Text = "⚔️ Start New Attempt",
                BackgroundColor = Color.FromArgb("#4CAF50"),
                TextColor = Colors.White,
                CornerRadius = 6,
                FontSize = 12,
                HeightRequest = 32,
                HorizontalOptions = LayoutOptions.Start
            };
            btnStartAttempt.Clicked += async (s, e) => await StartNewAttemptAsync(dragon, sortedAttempts);
            buttonsRow.Children.Add(btnStartAttempt);
        }
        else
        {
            // Edit Days button for active attempt
            var btnEditDays = new Button
            {
                Text = "✏️ Edit Days",
                BackgroundColor = Color.FromArgb("#5B63EE"),
                TextColor = Colors.White,
                CornerRadius = 6,
                FontSize = 12,
                HeightRequest = 32,
                HorizontalOptions = LayoutOptions.Start
            };
            btnEditDays.Clicked += async (s, e) => await EditActiveAttemptAsync(activeAttempt!, dragon, displayDays);
            buttonsRow.Children.Add(btnEditDays);

            // Mark Failed button
            var btnFailed = new Button
            {
                Text = "💀 Mark Failed",
                BackgroundColor = Color.FromArgb("#F44336"),
                TextColor = Colors.White,
                CornerRadius = 6,
                FontSize = 12,
                HeightRequest = 32,
                HorizontalOptions = LayoutOptions.Start
            };
            btnFailed.Clicked += async (s, e) => await MarkAttemptFailedAsync(dragon);
            buttonsRow.Children.Add(btnFailed);
        }

        // Add Past Attempt button
        var btnAddPast = new Button
        {
            Text = "+ Past Attempt",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 6,
            FontSize = 12,
            HeightRequest = 32,
            HorizontalOptions = LayoutOptions.Start
        };
        btnAddPast.Clicked += async (s, e) => await AddPastAttemptAsync(dragon);
        buttonsRow.Children.Add(btnAddPast);

        // Mark Irrelevant button
        var btnIrrelevant = new Button
        {
            Text = "💤",
            BackgroundColor = Color.FromArgb("#9E9E9E"),
            TextColor = Colors.White,
            CornerRadius = 6,
            FontSize = 12,
            HeightRequest = 32,
            WidthRequest = 40,
            HorizontalOptions = LayoutOptions.Start
        };
        btnIrrelevant.Clicked += async (s, e) => await MarkDragonIrrelevantAsync(dragon);
        buttonsRow.Children.Add(btnIrrelevant);

        // Mark Slain button
        var btnSlain = new Button
        {
            Text = "🏆",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 6,
            FontSize = 12,
            HeightRequest = 32,
            WidthRequest = 40,
            HorizontalOptions = LayoutOptions.Start
        };
        btnSlain.Clicked += async (s, e) => await MarkDragonSlainAsync(dragon);
        buttonsRow.Children.Add(btnSlain);

        headerStack.Children.Add(buttonsRow);

        // Second row of buttons (History, Auto-Increment)
        if (hasActiveAttempt)
        {
            var buttonsRow2 = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(40, 4, 0, 0) };

            // History button
            var btnHistory = new Button
            {
                Text = "📊 History",
                BackgroundColor = Color.FromArgb("#9C27B0"),
                TextColor = Colors.White,
                CornerRadius = 6,
                FontSize = 12,
                HeightRequest = 32,
                HorizontalOptions = LayoutOptions.Start
            };
            btnHistory.Clicked += async (s, e) => await ShowHistoryAsync(dragon, activeAttempt!);
            buttonsRow2.Children.Add(btnHistory);

            // Auto-Increment toggle
            var autoToggleStack = new HorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
            var chkAuto = new CheckBox
            {
                IsChecked = dragon.IsAutoIncrement,
                Color = Color.FromArgb("#4CAF50"),
                VerticalOptions = LayoutOptions.Center
            };
            chkAuto.CheckedChanged += async (s, e) => await ToggleAutoIncrementAsync(dragon, e.Value);
            autoToggleStack.Children.Add(chkAuto);
            autoToggleStack.Children.Add(new Label
            {
                Text = "Auto +1 Daily",
                FontSize = 12,
                TextColor = Color.FromArgb("#666"),
                VerticalOptions = LayoutOptions.Center
            });
            buttonsRow2.Children.Add(autoToggleStack);

            headerStack.Children.Add(buttonsRow2);
        }

        headerFrame.Content = headerStack;
        container.Children.Add(headerFrame);

        // Attempt cards (if expanded)
        if (isExpanded && sortedAttempts.Count > 0)
        {
            var attemptsContainer = new FlexLayout
            {
                Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
                JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
                AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Start,
                Margin = new Thickness(24, 0, 0, 0)
            };

            foreach (var attempt in sortedAttempts)
            {
                // Get actual days for this attempt too
                int attemptDays = await GetActualDayCountAsync(dragon, attempt);
                attemptsContainer.Children.Add(BuildAttemptCard(attempt, dragon, attemptDays));
            }

            container.Children.Add(attemptsContainer);
        }

        return container;
    }

    private Frame BuildAttemptCard(Attempt attempt, Dragon dragon, int displayDays)
    {
        var isActive = attempt.IsActive;
        var isFailed = attempt.FailedAt.HasValue;
        
        var borderColor = isActive ? Color.FromArgb("#5B63EE") : 
                          isFailed ? Color.FromArgb("#F44336") : 
                          Color.FromArgb("#4CAF50"); // Success (completed without fail)
        
        var bgColor = isActive ? Color.FromArgb("#E8EAF6") : 
                      isFailed ? Color.FromArgb("#FFEBEE") : 
                      Color.FromArgb("#E8F5E9");

        var frame = new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = bgColor,
            BorderColor = borderColor,
            HasShadow = false,
            WidthRequest = 120,
            Margin = new Thickness(0, 4, 8, 4)
        };

        // Make non-active cards tappable to edit
        if (!isActive)
        {
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (s, e) => await EditPastAttemptAsync(attempt, dragon);
            frame.GestureRecognizers.Add(tapGesture);
        }

        var stack = new VerticalStackLayout { Spacing = 4 };

        // Attempt number header
        stack.Children.Add(new Label
        {
            Text = $"Attempt #{attempt.AttemptNumber}",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = isActive ? Color.FromArgb("#3949AB") : 
                        isFailed ? Color.FromArgb("#C62828") : 
                        Color.FromArgb("#2E7D32"),
            HorizontalTextAlignment = TextAlignment.Center
        });

        // Days (big number) - use displayDays
        stack.Children.Add(new Label
        {
            Text = displayDays.ToString(),
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = isActive ? Color.FromArgb("#5B63EE") : Color.FromArgb("#333"),
            HorizontalTextAlignment = TextAlignment.Center
        });

        // "days" label
        stack.Children.Add(new Label
        {
            Text = displayDays == 1 ? "day" : "days",
            FontSize = 11,
            TextColor = Color.FromArgb("#666"),
            HorizontalTextAlignment = TextAlignment.Center
        });

        // Status
        if (isActive)
        {
            stack.Children.Add(new Label
            {
                Text = "⚔️ Active",
                FontSize = 10,
                TextColor = Color.FromArgb("#5B63EE"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }
        else if (isFailed)
        {
            stack.Children.Add(new Label
            {
                Text = "💀 Failed",
                FontSize = 10,
                TextColor = Color.FromArgb("#F44336"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }
        else
        {
            // Show "tap to edit" for past attempts
            stack.Children.Add(new Label
            {
                Text = "tap to edit",
                FontSize = 9,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        frame.Content = stack;
        return frame;
    }

    private async Task StartNewAttemptAsync(Dragon dragon, List<Attempt> existingAttempts)
    {
        int nextAttemptNumber = existingAttempts.Count > 0 
            ? existingAttempts.Max(a => a.AttemptNumber) + 1 
            : 1;

        bool confirm = await DisplayAlert(
            "Start New Attempt",
            $"Start Attempt #{nextAttemptNumber} to slay \"{dragon.Title}\"?\n\nThe timer will begin from today.",
            "Start",
            "Cancel");

        if (confirm)
        {
            await _attempts.StartNewAttemptAsync(_auth.CurrentUsername, dragon.Game, dragon.Title);
            
            _expandedDragons[GetDragonKey(dragon)] = true;
            _savedScrollY = _mainScroll.ScrollY;
            await LoadDragonsAsync();
            await RestoreScrollPositionAsync();
        }
    }

    private async Task EditActiveAttemptAsync(Attempt activeAttempt, Dragon dragon, int currentDays)
    {
        string daysStr = await DisplayPromptAsync(
            "Edit Active Attempt",
            $"Current: {currentDays} days\n\nEnter the correct number of days:",
            initialValue: currentDays.ToString(),
            keyboard: Keyboard.Numeric,
            placeholder: "e.g., 30");

        if (string.IsNullOrEmpty(daysStr) || !int.TryParse(daysStr, out int newDays) || newDays < 0)
        {
            return;
        }

        // If auto-increment is enabled, update the logs
        if (dragon.IsAutoIncrement)
        {
            await UpdateDayLogsAsync(dragon, activeAttempt, newDays);
        }
        else
        {
            await UpdateAttemptDaysAsync(activeAttempt.Id, newDays);
        }
        
        _expandedDragons[GetDragonKey(dragon)] = true;
        _savedScrollY = _mainScroll.ScrollY;
        await LoadDragonsAsync();
        await RestoreScrollPositionAsync();
    }

    /// <summary>
    /// Update the DragonDayLog entries to reflect the new day count
    /// </summary>
    private async Task UpdateDayLogsAsync(Dragon dragon, Attempt attempt, int newDayCount)
    {
        var conn = await GetConnectionAsync();
        
        // Get existing logs
        var logs = await conn.Table<DragonDayLog>()
            .Where(l => l.Username == _auth.CurrentUsername 
                && l.Game == dragon.Game 
                && l.DragonTitle == dragon.Title
                && l.AttemptNumber == attempt.AttemptNumber)
            .OrderBy(l => l.LogDate)
            .ToListAsync();

        int currentMax = logs.Count > 0 ? logs.Max(l => l.DayNumber) : 0;

        if (newDayCount > currentMax)
        {
            // Add more days
            var today = DateTime.UtcNow.Date;
            for (int i = currentMax + 1; i <= newDayCount; i++)
            {
                var log = new DragonDayLog
                {
                    Username = _auth.CurrentUsername,
                    Game = dragon.Game,
                    DragonTitle = dragon.Title,
                    AttemptNumber = attempt.AttemptNumber,
                    LogDate = today,
                    DayNumber = i,
                    Source = "manual",
                    Description = "Manual adjustment",
                    CreatedAt = DateTime.UtcNow
                };
                await conn.InsertAsync(log);
            }
        }
        else if (newDayCount < currentMax)
        {
            // Remove excess days (delete logs with DayNumber > newDayCount)
            var logsToDelete = logs.Where(l => l.DayNumber > newDayCount).ToList();
            foreach (var log in logsToDelete)
            {
                await conn.DeleteAsync(log);
            }
        }
    }

    private async Task EditPastAttemptAsync(Attempt attempt, Dragon dragon)
    {
        string daysStr = await DisplayPromptAsync(
            $"Edit Attempt #{attempt.AttemptNumber}",
            $"Current: {attempt.DurationDays} days\n\nEnter new number of days:",
            initialValue: attempt.DurationDays.ToString(),
            keyboard: Keyboard.Numeric,
            placeholder: "e.g., 30");

        if (string.IsNullOrEmpty(daysStr) || !int.TryParse(daysStr, out int newDays) || newDays < 0)
        {
            return;
        }

        await UpdateAttemptDaysAsync(attempt.Id, newDays);
        
        _expandedDragons[GetDragonKey(dragon)] = true;
        _savedScrollY = _mainScroll.ScrollY;
        await LoadDragonsAsync();
        await RestoreScrollPositionAsync();
    }

    private async Task MarkAttemptFailedAsync(Dragon dragon)
    {
        bool confirm = await DisplayAlert(
            "Mark Attempt Failed",
            $"Mark your current attempt to slay \"{dragon.Title}\" as failed?\n\nYou can start a new attempt afterwards.",
            "Yes, Failed",
            "Cancel");

        if (confirm)
        {
            await _attempts.MarkAttemptFailedAsync(_auth.CurrentUsername, dragon.Game, dragon.Title);
            
            _expandedDragons[GetDragonKey(dragon)] = true;
            _savedScrollY = _mainScroll.ScrollY;
            await LoadDragonsAsync();
            await RestoreScrollPositionAsync();
        }
    }

    private async Task AddPastAttemptAsync(Dragon dragon)
    {
        string daysStr = await DisplayPromptAsync(
            "Add Past Attempt",
            "How many days did this past attempt last?",
            keyboard: Keyboard.Numeric,
            placeholder: "e.g., 30");

        if (string.IsNullOrEmpty(daysStr) || !int.TryParse(daysStr, out int days) || days < 0)
        {
            return;
        }

        // Get existing attempts to determine the next number
        var attempts = await _attempts.GetAttemptsForDragonAsync(_auth.CurrentUsername, dragon.Game, dragon.Title);
        var activeAttempt = attempts.FirstOrDefault(a => a.IsActive);

        int newAttemptNumber;
        if (activeAttempt != null)
        {
            // Insert before the active attempt
            newAttemptNumber = activeAttempt.AttemptNumber;
            await BumpAttemptNumbersFromAsync(dragon, newAttemptNumber);
        }
        else
        {
            // No active attempt - add at the end
            newAttemptNumber = attempts.Count > 0 ? attempts.Max(a => a.AttemptNumber) + 1 : 1;
        }

        await AddManualAttemptAsync(dragon, newAttemptNumber, days);

        _expandedDragons[GetDragonKey(dragon)] = true;
        _savedScrollY = _mainScroll.ScrollY;
        await LoadDragonsAsync();
        await RestoreScrollPositionAsync();
    }

    private async Task UpdateAttemptDaysAsync(int attemptId, int newDays)
    {
        // This updates the attempt's StartedAt to achieve the desired duration
        var conn = await GetConnectionAsync();
        var attempt = await conn.GetAsync<Attempt>(attemptId);
        
        if (attempt != null)
        {
            // Calculate new start date to achieve the desired days
            var endTime = attempt.FailedAt ?? DateTime.UtcNow;
            attempt.StartedAt = endTime.AddDays(-newDays);
            await conn.UpdateAsync(attempt);
        }
    }

    private async Task BumpAttemptNumbersFromAsync(Dragon dragon, int fromNumber)
    {
        var conn = await GetConnectionAsync();
        var attempts = await conn.Table<Attempt>()
            .Where(a => a.Username == _auth.CurrentUsername && 
                       a.Game == dragon.Game && 
                       a.DragonTitle == dragon.Title && 
                       a.AttemptNumber >= fromNumber)
            .ToListAsync();

        // Sort descending so we update highest first (avoid conflicts)
        foreach (var attempt in attempts.OrderByDescending(a => a.AttemptNumber))
        {
            attempt.AttemptNumber++;
            await conn.UpdateAsync(attempt);
        }
    }

    private async Task AddManualAttemptAsync(Dragon dragon, int attemptNumber, int days)
    {
        var conn = await GetConnectionAsync();
        
        var attempt = new Attempt
        {
            Username = _auth.CurrentUsername,
            Game = dragon.Game,
            DragonTitle = dragon.Title,
            AttemptNumber = attemptNumber,
            StartedAt = DateTime.UtcNow.AddDays(-days),
            FailedAt = DateTime.UtcNow, // Mark as failed (past attempt)
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };
        
        await conn.InsertAsync(attempt);
    }

    private async Task<SQLite.SQLiteAsyncConnection> GetConnectionAsync()
    {
        // Access the database connection through a workaround
        // This assumes DatabaseService is registered and available
        var dbService = Application.Current?.Handler?.MauiContext?.Services.GetService<DatabaseService>();
        if (dbService != null)
        {
            return await dbService.GetConnectionAsync();
        }
        throw new InvalidOperationException("Could not get database connection");
    }

    private async Task RestoreScrollPositionAsync()
    {
        await Task.Delay(50);
        await _mainScroll.ScrollToAsync(0, _savedScrollY, false);
    }

    private static string GetFullImagePath(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
            return "";

        if (System.IO.Path.IsPathRooted(storedPath))
        {
            if (System.IO.File.Exists(storedPath))
                return storedPath;

            storedPath = System.IO.Path.GetFileName(storedPath);
        }

        string imagesFolder = System.IO.Path.Combine(FileSystem.AppDataDirectory, "ActivityImages");
        string fullPath = System.IO.Path.Combine(imagesFolder, storedPath);

        return System.IO.File.Exists(fullPath) ? fullPath : "";
    }

    private async void OnAddDragonClicked(object? sender, EventArgs e)
    {
        // Prompt for dragon title
        string? title = await DisplayPromptAsync(
            "Add New Dragon",
            "Enter a name for your dragon (the goal you want to achieve):",
            placeholder: "e.g., Master Spanish, Run a Marathon");

        if (string.IsNullOrWhiteSpace(title))
            return;

        // Ask if they want to assign to a game or keep it standalone
        string choice = await DisplayActionSheet(
            "Assign to Game?",
            "Cancel",
            null,
            "🚫 No Game (Standalone Dragon)",
            "📁 Select a Game");

        if (choice == "Cancel" || string.IsNullOrEmpty(choice))
            return;

        string gameId;

        if (choice.StartsWith("🚫"))
        {
            gameId = "_standalone_";
        }
        else
        {
            var games = await GetAllGamesAsync();
            
            if (games.Count == 0)
            {
                await DisplayAlert("No Games", "You don't have any games yet. Create a game first, or choose 'No Game' for a standalone dragon.", "OK");
                return;
            }

            var gameNames = games.Select(g => g.DisplayName).ToArray();
            string? selectedGame = await DisplayActionSheet("Select Game", "Cancel", null, gameNames);
            
            if (selectedGame == "Cancel" || string.IsNullOrEmpty(selectedGame))
                return;

            var game = games.FirstOrDefault(g => g.DisplayName == selectedGame);
            if (game == null)
                return;

            gameId = game.GameId;
        }

        // Ask for optional image
        string? imagePath = null;
        bool pickImage = await DisplayAlert(
            "Dragon Image",
            "Would you like to pick an image for this dragon?",
            "Pick Image",
            "Skip (Use Default)");

        if (pickImage)
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select dragon image",
                    FileTypes = FilePickerFileType.Images
                });

                if (result != null)
                {
                    string destFolder = System.IO.Path.Combine(FileSystem.AppDataDirectory, "ActivityImages");
                    System.IO.Directory.CreateDirectory(destFolder);

                    string filename = $"dragon_{DateTime.Now.Ticks}{System.IO.Path.GetExtension(result.FileName)}";
                    string fullPath = System.IO.Path.Combine(destFolder, filename);

                    using var sourceStream = await result.OpenReadAsync();
                    using var destStream = System.IO.File.Create(fullPath);
                    await sourceStream.CopyToAsync(destStream);

                    imagePath = filename;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error picking image: {ex.Message}");
            }
        }

        // Create the dragon
        var dragon = new Dragon
        {
            Username = _auth.CurrentUsername,
            Game = gameId,
            Title = title.Trim(),
            ImagePath = imagePath ?? "",
            SlainAt = null,
            CreatedAt = DateTime.UtcNow
        };

        var conn = await GetConnectionAsync();
        await conn.InsertAsync(dragon);

        await DisplayAlert("Dragon Created!", $"'{title}' has been added to your dragons.\n\nStart an attempt when you're ready to begin!", "OK");

        await LoadDragonsAsync();
    }

    private async Task<List<Game>> GetAllGamesAsync()
    {
        var conn = await GetConnectionAsync();
        return await conn.Table<Game>()
            .Where(g => g.Username == _auth.CurrentUsername)
            .ToListAsync();
    }

    private async Task ShowHistoryAsync(Dragon dragon, Attempt attempt)
    {
        var dbService = Application.Current?.Handler?.MauiContext?.Services.GetService<DatabaseService>();
        if (dbService == null) return;

        var historyPage = new DragonHistoryPage(dbService, dragon, attempt);
        await Navigation.PushAsync(historyPage);
    }

    private async Task ToggleAutoIncrementAsync(Dragon dragon, bool isEnabled)
    {
        var conn = await GetConnectionAsync();
        dragon.IsAutoIncrement = isEnabled;
        
        if (isEnabled && !dragon.LastAutoIncrementDate.HasValue)
        {
            // Set to yesterday so today gets incremented
            dragon.LastAutoIncrementDate = DateTime.UtcNow.Date.AddDays(-1);
        }
        
        await conn.UpdateAsync(dragon);
        
        string message = isEnabled 
            ? "Auto-increment enabled! Days will automatically increase each day."
            : "Auto-increment disabled.";
        await DisplayAlert("Auto-Increment", message, "OK");
    }

    private async Task MarkDragonIrrelevantAsync(Dragon dragon)
    {
        bool confirm = await DisplayAlert(
            "Mark as Irrelevant",
            $"Mark '{dragon.Title}' as irrelevant?\n\nThis will move it to the Irrelevant Dragons list. You can restore it later.",
            "Yes, Mark Irrelevant",
            "Cancel");

        if (confirm)
        {
            await _dragons.MarkDragonIrrelevantAsync(dragon.Id);
            await DisplayAlert("Moved", $"'{dragon.Title}' has been moved to Irrelevant Dragons.", "OK");
            await LoadDragonsAsync();
        }
    }

    private async Task MarkDragonSlainAsync(Dragon dragon)
    {
        bool confirm = await DisplayAlert(
            "🏆 Mark as Slain",
            $"Mark '{dragon.Title}' as SLAIN?\n\nThis means you've achieved your goal (reached Level 100)!\n\nThis will move it to the Slain Dragons list.",
            "Yes, I Slayed It! 🎉",
            "Cancel");

        if (confirm)
        {
            var conn = await GetConnectionAsync();
            dragon.SlainAt = DateTime.UtcNow;
            await conn.UpdateAsync(dragon);
            
            await DisplayAlert("🎉 Dragon Slain!", $"Congratulations! You've slain '{dragon.Title}'!\n\nIt has been moved to your Slain Dragons.", "OK");
            await LoadDragonsAsync();
        }
    }

    /// <summary>
    /// Process auto-increments for all dragons with IsAutoIncrement enabled.
    /// Should be called on page load.
    /// </summary>
    private async Task ProcessAutoIncrementsAsync()
    {
        var conn = await GetConnectionAsync();
        await conn.CreateTableAsync<DragonDayLog>();
        
        var dragons = await _dragons.GetActiveDragonsAsync(_auth.CurrentUsername);
        var today = DateTime.UtcNow.Date;

        foreach (var dragon in dragons.Where(d => d.IsAutoIncrement))
        {
            // Check if already processed today
            if (dragon.LastAutoIncrementDate.HasValue && dragon.LastAutoIncrementDate.Value.Date >= today)
                continue;

            // Get active attempt
            var attempts = await _attempts.GetAttemptsForDragonAsync(_auth.CurrentUsername, dragon.Game, dragon.Title);
            var activeAttempt = attempts.FirstOrDefault(a => a.IsActive);
            
            if (activeAttempt == null)
                continue;

            // Get current max day from logs
            var existingLogs = await conn.Table<DragonDayLog>()
                .Where(l => l.Username == _auth.CurrentUsername 
                    && l.Game == dragon.Game 
                    && l.DragonTitle == dragon.Title
                    && l.AttemptNumber == activeAttempt.AttemptNumber)
                .ToListAsync();
            
            int currentDay = existingLogs.Count > 0 ? existingLogs.Max(l => l.DayNumber) : activeAttempt.DurationDays;

            // Calculate how many days to log (in case app wasn't opened for multiple days)
            var lastDate = dragon.LastAutoIncrementDate?.Date ?? today.AddDays(-1);

            // Log each day since last increment
            for (var date = lastDate.AddDays(1); date <= today; date = date.AddDays(1))
            {
                currentDay++;
                
                var log = new DragonDayLog
                {
                    Username = _auth.CurrentUsername,
                    Game = dragon.Game,
                    DragonTitle = dragon.Title,
                    AttemptNumber = activeAttempt.AttemptNumber,
                    LogDate = date,
                    DayNumber = currentDay,
                    Source = "auto",
                    Description = "Daily use",
                    CreatedAt = DateTime.UtcNow
                };
                
                await conn.InsertAsync(log);
                System.Diagnostics.Debug.WriteLine($"[DRAGON AUTO] Logged day {currentDay} for {dragon.Title}");
            }

            // Update dragon's last increment date
            dragon.LastAutoIncrementDate = today;
            await conn.UpdateAsync(dragon);
        }
    }

    /// <summary>
    /// Manually log a day for a dragon attempt
    /// </summary>
    private async Task LogDragonDayAsync(Dragon dragon, Attempt attempt, string description = "Daily use")
    {
        var conn = await GetConnectionAsync();
        await conn.CreateTableAsync<DragonDayLog>();

        var today = DateTime.UtcNow.Date;
        
        // Check if already logged today
        var existingLog = await conn.Table<DragonDayLog>()
            .Where(l => l.Username == _auth.CurrentUsername 
                && l.Game == dragon.Game 
                && l.DragonTitle == dragon.Title
                && l.AttemptNumber == attempt.AttemptNumber
                && l.LogDate == today)
            .FirstOrDefaultAsync();

        if (existingLog != null)
        {
            await DisplayAlert("Already Logged", "Today's day has already been logged.", "OK");
            return;
        }

        // Get current day count
        var logs = await conn.Table<DragonDayLog>()
            .Where(l => l.Username == _auth.CurrentUsername 
                && l.Game == dragon.Game 
                && l.DragonTitle == dragon.Title
                && l.AttemptNumber == attempt.AttemptNumber)
            .ToListAsync();

        int dayNumber = logs.Count > 0 ? logs.Max(l => l.DayNumber) + 1 : attempt.DurationDays + 1;

        var log = new DragonDayLog
        {
            Username = _auth.CurrentUsername,
            Game = dragon.Game,
            DragonTitle = dragon.Title,
            AttemptNumber = attempt.AttemptNumber,
            LogDate = today,
            DayNumber = dayNumber,
            Source = "manual",
            Description = description,
            CreatedAt = DateTime.UtcNow
        };

        await conn.InsertAsync(log);
        await DisplayAlert("Day Logged", $"Day {dayNumber} logged for {dragon.Title}!", "OK");
    }
}
