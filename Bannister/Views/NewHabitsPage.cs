using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

[QueryProperty(nameof(Frequency), "frequency")]
public class NewHabitsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly GameService _games;
    private readonly NewHabitService _newHabits;
    private readonly ActivityService _activities;
    private readonly ExpService _exp;
    private readonly DatabaseService _db;

    private string _frequency = "Daily";
    public string Frequency
    {
        get => _frequency;
        set
        {
            _frequency = value;
            UpdateForFrequency();
        }
    }

    private Label _lblHeader;
    private Label _lblSubheader;
    private Label _lblAllowance;
    private Label _lblAvailableSlots;
    private Label _lblActiveHeader;
    private Label _lblAllowanceCountdown;
    private VerticalStackLayout _activeHabitsContainer;
    private VerticalStackLayout _pendingHabitsContainer;
    private VerticalStackLayout _graduatedHabitsContainer;
    private Button _btnAddHabit;
    private Button _btnAddPending;
    private Frame _allowanceFrame;

    public NewHabitsPage(AuthService auth, GameService games, NewHabitService newHabits, 
        ActivityService activities, ExpService exp, DatabaseService db)
    {
        _auth = auth;
        _games = games;
        _newHabits = newHabits;
        _activities = activities;
        _exp = exp;
        _db = db;

        Title = "Daily Habits";
        BackgroundColor = Color.FromArgb("#5C6BC0"); // Default indigo for Daily

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateForFrequency();
        await LoadHabitsAsync();
    }

    private void UpdateForFrequency()
    {
        // Update colors based on frequency
        string bgColor = _frequency switch
        {
            "Daily" => "#5C6BC0",   // Indigo
            "Weekly" => "#00897B",  // Teal
            "Monthly" => "#8E24AA", // Purple
            _ => "#5C6BC0"
        };

        string graduationText = _frequency switch
        {
            "Daily" => "Complete every day for 7 days to graduate",
            "Weekly" => "Complete every week for 4 weeks to graduate",
            "Monthly" => "Complete every month for 3 months to graduate",
            _ => "Complete every day for 7 days to graduate"
        };

        string headerEmoji = _frequency switch
        {
            "Daily" => "🌅",
            "Weekly" => "📆",
            "Monthly" => "📅",
            _ => "🌅"
        };

        BackgroundColor = Color.FromArgb(bgColor);
        Title = $"{_frequency} Habits";

        if (_lblHeader != null)
            _lblHeader.Text = $"{headerEmoji} {_frequency} Habits";
        
        if (_lblSubheader != null)
            _lblSubheader.Text = graduationText;

        if (_lblActiveHeader != null)
            _lblActiveHeader.Text = $"🔥 {_frequency} Habits";
    }

    private void BuildUI()
    {
        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16
        };

        // Header
        _lblHeader = new Label
        {
            Text = "🌅 Daily Habits",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        };
        mainStack.Children.Add(_lblHeader);

        _lblSubheader = new Label
        {
            Text = "Complete every day for 7 days to graduate",
            TextColor = Colors.White,
            Opacity = 0.9
        };
        mainStack.Children.Add(_lblSubheader);

        // Allowance display
        _allowanceFrame = new Frame
        {
            BackgroundColor = Color.FromArgb("#2E7D32"),
            Padding = 16,
            CornerRadius = 12,
            HasShadow = true,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var allowanceTap = new TapGestureRecognizer();
        allowanceTap.Tapped += OnAllowanceTapped;
        _allowanceFrame.GestureRecognizers.Add(allowanceTap);

        var allowanceStack = new VerticalStackLayout { Spacing = 8 };

        _lblAllowance = new Label
        {
            Text = "Allowance: 1",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center
        };
        allowanceStack.Children.Add(_lblAllowance);

        _lblAvailableSlots = new Label
        {
            Text = "0 slots available",
            FontSize = 14,
            TextColor = Colors.White,
            Opacity = 0.9,
            HorizontalTextAlignment = TextAlignment.Center
        };
        allowanceStack.Children.Add(_lblAvailableSlots);

        // Countdown to allowance increase
        _lblAllowanceCountdown = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Colors.White,
            Opacity = 0.8,
            HorizontalTextAlignment = TextAlignment.Center
        };
        allowanceStack.Children.Add(_lblAllowanceCountdown);

        allowanceStack.Children.Add(new Label
        {
            Text = "tap to edit",
            FontSize = 11,
            TextColor = Colors.White,
            Opacity = 0.6,
            HorizontalTextAlignment = TextAlignment.Center
        });

        _allowanceFrame.Content = allowanceStack;
        mainStack.Children.Add(_allowanceFrame);

        // Add habit button
        _btnAddHabit = new Button
        {
            Text = "+ Add New Habit",
            BackgroundColor = Color.FromArgb("#666"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold,
            IsEnabled = false
        };
        _btnAddHabit.Clicked += OnAddHabitClicked;
        mainStack.Children.Add(_btnAddHabit);

        // Active habits section
        _lblActiveHeader = new Label
        {
            Text = "🔥 Daily Habits",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            Margin = new Thickness(0, 16, 0, 8)
        };
        mainStack.Children.Add(_lblActiveHeader);

        _activeHabitsContainer = new VerticalStackLayout { Spacing = 8 };
        mainStack.Children.Add(_activeHabitsContainer);

        // Pending habits section
        var pendingHeader = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Margin = new Thickness(0, 16, 0, 8)
        };

        pendingHeader.Add(new Label
        {
            Text = "⏳ Pending Habits",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        _btnAddPending = new Button
        {
            Text = "+ Add to Queue",
            BackgroundColor = Color.FromArgb("#7B1FA2"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 12,
            Padding = new Thickness(12, 6),
            HeightRequest = 36
        };
        _btnAddPending.Clicked += OnAddPendingClicked;
        pendingHeader.Add(_btnAddPending, 1, 0);

        mainStack.Children.Add(pendingHeader);

        _pendingHabitsContainer = new VerticalStackLayout { Spacing = 8 };
        mainStack.Children.Add(_pendingHabitsContainer);

        // Graduated habits section
        mainStack.Children.Add(new Label
        {
            Text = "🎓 Graduated Habits",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            Margin = new Thickness(0, 16, 0, 8)
        });

        _graduatedHabitsContainer = new VerticalStackLayout { Spacing = 8 };
        mainStack.Children.Add(_graduatedHabitsContainer);

        // Info section
        var infoFrame = new Frame
        {
            BackgroundColor = Color.FromArgb("#C8E6C9"),
            Padding = 16,
            CornerRadius = 12,
            HasShadow = false,
            Margin = new Thickness(0, 16, 0, 0)
        };

        infoFrame.Content = new Label
        {
            Text = "📖 How it works:\n\n" +
                   "• You start with 1 allowance slot\n" +
                   "• Add a new habit to track\n" +
                   "• Do it daily - you gain EXP\n" +
                   "• Miss a day - you lose EXP and the habit fails\n" +
                   "• Complete 7 days → habit graduates!\n" +
                   "• Graduate ALL habits → allowance increases\n" +
                   "• Fail = -1 allowance slot (min 1)\n\n" +
                   "💡 Use Pending to queue habits for later!",
            TextColor = Color.FromArgb("#1B5E20"),
            FontSize = 13,
            LineHeight = 1.4
        };
        mainStack.Children.Add(infoFrame);

        scrollView.Content = mainStack;
        Content = scrollView;
    }

    private async void OnAllowanceTapped(object? sender, EventArgs e)
    {
        var allowance = await _newHabits.GetOrCreateAllowanceAsync(_auth.CurrentUsername, _frequency);
        
        string result = await DisplayActionSheet(
            $"{_frequency} Allowance: {allowance.CurrentAllowance}",
            "Cancel",
            null,
            "✏️ Edit Allowance",
            "📊 View Stats");
        
        if (result == "✏️ Edit Allowance")
        {
            string input = await DisplayPromptAsync(
                "Edit Allowance",
                $"Current {_frequency} allowance: {allowance.CurrentAllowance}\n\nEnter new value:",
                initialValue: allowance.CurrentAllowance.ToString(),
                keyboard: Keyboard.Numeric);
            
            if (!string.IsNullOrEmpty(input) && int.TryParse(input, out int newValue) && newValue >= 1)
            {
                allowance.CurrentAllowance = newValue;
                if (newValue > allowance.HighestAllowance)
                    allowance.HighestAllowance = newValue;
                // Reset the graduation counter to match new allowance
                allowance.GraduationsUntilIncrease = newValue;
                await _newHabits.UpdateAllowanceAsync(allowance);
                await LoadHabitsAsync();
            }
        }
        else if (result == "📊 View Stats")
        {
            await DisplayAlert($"{_frequency} Habit Stats",
                $"Current Allowance: {allowance.CurrentAllowance}\n" +
                $"Highest Allowance: {allowance.HighestAllowance}\n" +
                $"Total Graduated: {allowance.TotalGraduated}\n" +
                $"Total Failed: {allowance.TotalFailed}",
                "OK");
        }
    }

    private async Task LoadHabitsAsync()
    {
        // Check for missed habits across all games - but only for this frequency
        var allActiveHabits = await _newHabits.GetAllActiveHabitsAsync(_auth.CurrentUsername);
        var frequencyHabits = allActiveHabits.Where(h => h.Frequency == _frequency).ToList();
        var missedHabits = new List<NewHabit>();
        var checkedGames = new HashSet<string>();
        
        foreach (var habit in frequencyHabits)
        {
            if (!checkedGames.Contains(habit.Game))
            {
                checkedGames.Add(habit.Game);
                var missed = await _newHabits.CheckMissedHabitsAsync(_auth.CurrentUsername, habit.Game, _exp);
                // Only add missed habits of this frequency
                missedHabits.AddRange(missed.Where(h => h.Frequency == _frequency));
            }
        }
        
        if (missedHabits.Count > 0)
        {
            await DisplayAlert($"{_frequency} Habits Failed",
                $"{missedHabits.Count} habit(s) failed because you missed:\n\n" +
                string.Join("\n", missedHabits.Select(h => $"• {h.HabitName}")),
                "OK");
        }

        // Check for habits ready to graduate (with confirmation)
        await CheckAndPromptGraduationsAsync();

        // Load frequency-specific allowance
        var allowance = await _newHabits.GetOrCreateAllowanceAsync(_auth.CurrentUsername, _frequency);
        var availableSlots = await _newHabits.GetAvailableSlotsAsync(_auth.CurrentUsername, _frequency);

        _lblAllowance.Text = $"Allowance: {allowance.CurrentAllowance}";
        _lblAvailableSlots.Text = availableSlots > 0
            ? $"{availableSlots} slot(s) available"
            : "No slots available - complete a habit first!";

        // Update countdown to allowance increase
        int habitsUntilIncrease = await _newHabits.GetHabitsUntilAllowanceIncreaseAsync(_auth.CurrentUsername, _frequency);
        if (habitsUntilIncrease > 0)
        {
            string habitWord = habitsUntilIncrease == 1 ? "habit" : "habits";
            _lblAllowanceCountdown.Text = $"📈 {habitsUntilIncrease} {habitWord} until allowance increase";
            _lblAllowanceCountdown.IsVisible = true;
        }
        else
        {
            _lblAllowanceCountdown.IsVisible = false;
        }

        _btnAddHabit.IsEnabled = availableSlots > 0;
        _btnAddHabit.BackgroundColor = availableSlots > 0
            ? Color.FromArgb("#FF9800")
            : Color.FromArgb("#666");

        // Reload active habits (they may have changed after graduation)
        allActiveHabits = await _newHabits.GetAllActiveHabitsAsync(_auth.CurrentUsername);

        // Load active habits for this frequency
        _activeHabitsContainer.Children.Clear();
        var activeHabits = allActiveHabits.Where(h => h.Frequency == _frequency).ToList();

        if (activeHabits.Count == 0)
        {
            _activeHabitsContainer.Children.Add(new Label
            {
                Text = $"No {_frequency.ToLower()} habits yet. Add one to get started!",
                TextColor = Colors.White,
                Opacity = 0.7,
                FontSize = 14,
                HorizontalTextAlignment = TextAlignment.Center
            });
        }
        else
        {
            foreach (var habit in activeHabits)
            {
                _activeHabitsContainer.Children.Add(CreateActiveHabitCard(habit, availableSlots));
            }
        }

        // Load pending habits for this frequency
        _pendingHabitsContainer.Children.Clear();
        var allPendingHabits = await _newHabits.GetAllPendingHabitsAsync(_auth.CurrentUsername);
        var pendingHabits = allPendingHabits.Where(h => h.Frequency == _frequency).ToList();

        if (pendingHabits.Count == 0)
        {
            _pendingHabitsContainer.Children.Add(new Label
            {
                Text = "No pending habits. Add habits to your queue!",
                TextColor = Colors.White,
                Opacity = 0.7,
                FontSize = 14,
                HorizontalTextAlignment = TextAlignment.Center
            });
        }
        else
        {
            for (int i = 0; i < pendingHabits.Count; i++)
            {
                _pendingHabitsContainer.Children.Add(
                    CreatePendingHabitCard(pendingHabits[i], i, pendingHabits.Count, availableSlots));
            }
        }

        // Load graduated habits for this frequency
        _graduatedHabitsContainer.Children.Clear();
        var allGraduatedHabits = await _newHabits.GetAllGraduatedHabitsAsync(_auth.CurrentUsername);
        var graduatedHabits = allGraduatedHabits.Where(h => h.Frequency == _frequency).Take(10).ToList();

        if (graduatedHabits.Count == 0)
        {
            _graduatedHabitsContainer.Children.Add(new Label
            {
                Text = "No graduated habits yet.",
                TextColor = Colors.White,
                Opacity = 0.7,
                FontSize = 14,
                HorizontalTextAlignment = TextAlignment.Center
            });
        }
        else
        {
            foreach (var habit in graduatedHabits)
            {
                _graduatedHabitsContainer.Children.Add(CreateGraduatedHabitCard(habit));
            }
        }
    }

    /// <summary>
    /// Check for habits ready to graduate and prompt user for each one.
    /// After all graduations, check if allowance increase is available.
    /// </summary>
    private async Task CheckAndPromptGraduationsAsync()
    {
        var habitsReadyToGraduate = await _newHabits.GetHabitsReadyToGraduateAsync(_auth.CurrentUsername, _frequency);

        foreach (var habit in habitsReadyToGraduate)
        {
            // Confirmation dialog for each graduation
            bool confirm = await DisplayAlert(
                "🎓 Ready to Graduate!",
                $"'{habit.HabitName}' has completed {habit.DaysToGraduate} {GetUnitText()}!\n\n" +
                $"Graduate this habit?",
                "Graduate",
                "Not Yet");

            if (confirm)
            {
                await _newHabits.GraduateHabitAsync(habit);
                
                await DisplayAlert("🎉 Congratulations!",
                    $"'{habit.HabitName}' has graduated!\n\n" +
                    $"Great work completing {habit.DaysToGraduate} {GetUnitText()} in a row!",
                    "Awesome!");
            }
        }

        // After processing all graduations, check if allowance increase is available
        bool isEligible = await _newHabits.IsEligibleForAllowanceIncreaseAsync(_auth.CurrentUsername, _frequency);
        
        if (isEligible)
        {
            var allowance = await _newHabits.GetOrCreateAllowanceAsync(_auth.CurrentUsername, _frequency);
            
            bool increaseAllowance = await DisplayAlert(
                "📈 Allowance Increase Available!",
                $"You've graduated {allowance.CurrentAllowance} habits!\n\n" +
                $"Current allowance: {allowance.CurrentAllowance}\n" +
                $"New allowance: {allowance.CurrentAllowance + 1}\n\n" +
                $"Increase your {_frequency.ToLower()} allowance?",
                "Yes, Increase!",
                "Not Now");

            if (increaseAllowance)
            {
                await _newHabits.IncreaseAllowanceAsync(_auth.CurrentUsername, _frequency);
                
                await DisplayAlert("🎊 Allowance Increased!",
                    $"Your {_frequency.ToLower()} allowance is now {allowance.CurrentAllowance + 1}!\n\n" +
                    $"You can now track more habits at once.",
                    "Great!");
            }
            else
            {
                // User declined - reset the counter so it doesn't keep prompting
                await _newHabits.ResetGraduationCounterAsync(_auth.CurrentUsername, _frequency);
            }
        }
    }

    private string GetUnitText()
    {
        return _frequency switch
        {
            "Daily" => "days",
            "Weekly" => "weeks",
            "Monthly" => "months",
            _ => "days"
        };
    }

    private Frame CreateActiveHabitCard(NewHabit habit, int availableSlots)
    {
        var frame = new Frame
        {
            BackgroundColor = Colors.White,
            Padding = 16,
            CornerRadius = 12,
            HasShadow = true
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            RowSpacing = 4
        };

        // Habit name
        grid.Add(new Label
        {
            Text = habit.HabitName,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        }, 0, 0);

        // Game
        grid.Add(new Label
        {
            Text = $"📁 {habit.Game}",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        }, 0, 1);

        // Progress circles
        var progressStack = new HorizontalStackLayout { Spacing = 4 };
        for (int i = 0; i < habit.DaysToGraduate; i++)
        {
            progressStack.Children.Add(new Label
            {
                Text = i < habit.ConsecutiveDays ? "🟢" : "⚪",
                FontSize = 16
            });
        }
        grid.Add(progressStack, 0, 2);
        Grid.SetColumnSpan(progressStack, 2);

        // Status
        bool isDoneToday = habit.LastAppliedDate?.Date == DateTime.UtcNow.Date;
        bool isReadyToGraduate = habit.ConsecutiveDays >= habit.DaysToGraduate;
        
        string statusText;
        if (isReadyToGraduate)
        {
            statusText = "🎓 Ready to graduate!";
        }
        else if (isDoneToday)
        {
            statusText = "✅ Done today!";
        }
        else
        {
            statusText = $"⏳ {habit.DaysRemaining} {(_frequency == "Daily" ? "days" : _frequency == "Weekly" ? "weeks" : "months")} to go";
        }

        grid.Add(new Label
        {
            Text = statusText,
            FontSize = 12,
            TextColor = isReadyToGraduate ? Color.FromArgb("#4CAF50") : Color.FromArgb("#666"),
            FontAttributes = isReadyToGraduate ? FontAttributes.Bold : FontAttributes.None
        }, 0, 3);

        // Action buttons
        var buttonStack = new HorizontalStackLayout { Spacing = 8 };

        // Edit progress button
        var btnEditProgress = new Button
        {
            Text = "✏️",
            BackgroundColor = Color.FromArgb("#2196F3"),
            TextColor = Colors.White,
            FontSize = 14,
            WidthRequest = 40,
            HeightRequest = 40,
            Padding = 0,
            CornerRadius = 8
        };
        btnEditProgress.Clicked += async (s, e) => await EditHabitProgressAsync(habit);
        buttonStack.Children.Add(btnEditProgress);

        // Move to pending button
        var btnMoveToPending = new Button
        {
            Text = "⏸️",
            BackgroundColor = Color.FromArgb("#7B1FA2"),
            TextColor = Colors.White,
            FontSize = 14,
            WidthRequest = 40,
            HeightRequest = 40,
            Padding = 0,
            CornerRadius = 8
        };
        btnMoveToPending.Clicked += async (s, e) => await MoveToPendingAsync(habit);
        buttonStack.Children.Add(btnMoveToPending);

        // Delete button
        var btnDelete = new Button
        {
            Text = "🗑️",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#F44336"),
            FontSize = 16,
            WidthRequest = 40,
            HeightRequest = 40,
            Padding = 0
        };
        btnDelete.Clicked += async (s, e) => await DeleteHabitAsync(habit);
        buttonStack.Children.Add(btnDelete);

        grid.Add(buttonStack, 1, 3);

        frame.Content = grid;
        return frame;
    }

    private Frame CreatePendingHabitCard(NewHabit habit, int index, int total, int availableSlots)
    {
        var frame = new Frame
        {
            BackgroundColor = Color.FromArgb("#E1BEE7"),
            Padding = 12,
            CornerRadius = 10,
            HasShadow = true
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };

        // Order number
        grid.Add(new Label
        {
            Text = $"#{index + 1}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#7B1FA2"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        // Info stack
        var infoStack = new VerticalStackLayout { Spacing = 2 };
        infoStack.Children.Add(new Label
        {
            Text = habit.HabitName,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });
        infoStack.Children.Add(new Label
        {
            Text = $"📁 {habit.Game}",
            FontSize = 11,
            TextColor = Color.FromArgb("#666")
        });
        grid.Add(infoStack, 1, 0);

        // Action buttons
        var buttonStack = new HorizontalStackLayout { Spacing = 4 };

        // Move up
        if (index > 0)
        {
            var btnUp = new Button
            {
                Text = "▲",
                BackgroundColor = Color.FromArgb("#7B1FA2"),
                TextColor = Colors.White,
                FontSize = 12,
                WidthRequest = 32,
                HeightRequest = 32,
                Padding = 0,
                CornerRadius = 6
            };
            btnUp.Clicked += async (s, e) =>
            {
                await _newHabits.MovePendingHabitUpAsync(habit);
                await LoadHabitsAsync();
            };
            buttonStack.Children.Add(btnUp);
        }

        // Move down
        if (index < total - 1)
        {
            var btnDown = new Button
            {
                Text = "▼",
                BackgroundColor = Color.FromArgb("#7B1FA2"),
                TextColor = Colors.White,
                FontSize = 12,
                WidthRequest = 32,
                HeightRequest = 32,
                Padding = 0,
                CornerRadius = 6
            };
            btnDown.Clicked += async (s, e) =>
            {
                await _newHabits.MovePendingHabitDownAsync(habit);
                await LoadHabitsAsync();
            };
            buttonStack.Children.Add(btnDown);
        }

        // Activate button
        var btnActivate = new Button
        {
            Text = "▶️",
            BackgroundColor = availableSlots > 0 ? Color.FromArgb("#4CAF50") : Color.FromArgb("#999"),
            TextColor = Colors.White,
            FontSize = 12,
            WidthRequest = 32,
            HeightRequest = 32,
            Padding = 0,
            CornerRadius = 6,
            IsEnabled = availableSlots > 0
        };
        btnActivate.Clicked += async (s, e) => await ActivatePendingAsync(habit);
        buttonStack.Children.Add(btnActivate);

        // Delete button
        var btnDelete = new Button
        {
            Text = "✕",
            BackgroundColor = Color.FromArgb("#F44336"),
            TextColor = Colors.White,
            FontSize = 12,
            WidthRequest = 32,
            HeightRequest = 32,
            Padding = 0,
            CornerRadius = 6
        };
        btnDelete.Clicked += async (s, e) => await DeletePendingAsync(habit);
        buttonStack.Children.Add(btnDelete);

        grid.Add(buttonStack, 2, 0);

        frame.Content = grid;
        return frame;
    }

    private Frame CreateGraduatedHabitCard(NewHabit habit)
    {
        var frame = new Frame
        {
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            Padding = 12,
            CornerRadius = 8,
            HasShadow = false
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };

        grid.Add(new Label
        {
            Text = "🎓",
            FontSize = 20,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        var infoStack = new VerticalStackLayout();
        infoStack.Children.Add(new Label
        {
            Text = habit.HabitName,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2E7D32")
        });
        infoStack.Children.Add(new Label
        {
            Text = habit.Game,
            FontSize = 11,
            TextColor = Color.FromArgb("#666")
        });
        grid.Add(infoStack, 1, 0);

        grid.Add(new Label
        {
            Text = habit.CompletedAt?.ToString("MMM dd") ?? "",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        }, 2, 0);

        frame.Content = grid;
        return frame;
    }

    private async Task MoveToPendingAsync(NewHabit habit)
    {
        bool confirm = await DisplayAlert(
            "Move to Pending",
            $"Move '{habit.HabitName}' to pending?\n\nThis will pause the habit (no penalty) and free up your slot.",
            "Move to Pending",
            "Cancel");

        if (confirm)
        {
            await _newHabits.MoveToPendingAsync(habit);
            await LoadHabitsAsync();
        }
    }

    private async Task ActivatePendingAsync(NewHabit habit)
    {
        var availableSlots = await _newHabits.GetAvailableSlotsAsync(_auth.CurrentUsername, _frequency);
        if (availableSlots <= 0)
        {
            await DisplayAlert("No Slots", $"No {_frequency.ToLower()} allowance slots available. Complete or pause an active habit first!", "OK");
            return;
        }

        await _newHabits.ActivatePendingHabitAsync(habit);
        await DisplayAlert("Habit Activated!",
            $"'{habit.HabitName}' is now active!\n\nRemember to complete it regularly to avoid penalties.",
            "Got it!");
        await LoadHabitsAsync();
    }

    private async Task DeletePendingAsync(NewHabit habit)
    {
        bool confirm = await DisplayAlert(
            "Remove from Queue",
            $"Remove '{habit.HabitName}' from the pending queue?\n\n(The activities will NOT be deleted)",
            "Remove",
            "Cancel");

        if (confirm)
        {
            await _newHabits.DeletePendingHabitAsync(habit);
            await LoadHabitsAsync();
        }
    }

    private async Task DeleteHabitAsync(NewHabit habit)
    {
        bool confirm = await DisplayAlert(
            "Remove Habit",
            $"Remove '{habit.HabitName}' from habit tracking?\n\nThis will count as a failure and reduce your allowance.\n\n(The activities themselves will NOT be deleted)",
            "Remove",
            "Cancel");

        if (confirm)
        {
            await _newHabits.FailHabitManualAsync(habit, _exp, habit.Game);
            await LoadHabitsAsync();
        }
    }

    private async Task EditHabitProgressAsync(NewHabit habit)
    {
        string result = await DisplayActionSheet(
            $"Edit: {habit.HabitName}",
            "Cancel",
            null,
            $"✏️ Edit Days ({habit.ConsecutiveDays}/{habit.DaysToGraduate})",
            "📅 Set Last Applied Date",
            "🔄 Reset Progress to 0");

        if (result == $"✏️ Edit Days ({habit.ConsecutiveDays}/{habit.DaysToGraduate})")
        {
            string input = await DisplayPromptAsync(
                "Edit Consecutive Days",
                $"Current: {habit.ConsecutiveDays} / {habit.DaysToGraduate}\n\nEnter new value (0-{habit.DaysToGraduate}):",
                initialValue: habit.ConsecutiveDays.ToString(),
                keyboard: Keyboard.Numeric);

            if (!string.IsNullOrEmpty(input) && int.TryParse(input, out int newDays))
            {
                newDays = Math.Max(0, Math.Min(newDays, habit.DaysToGraduate));
                habit.ConsecutiveDays = newDays;
                await _newHabits.UpdateHabitAsync(habit);
                await LoadHabitsAsync();
            }
        }
        else if (result == "📅 Set Last Applied Date")
        {
            // Show options for last applied date
            string dateChoice = await DisplayActionSheet(
                "Set Last Applied Date",
                "Cancel",
                null,
                "Today",
                "Yesterday",
                "2 days ago",
                "Clear (never applied)");

            if (dateChoice == "Today")
            {
                habit.LastAppliedDate = DateTime.UtcNow.Date;
                await _newHabits.UpdateHabitAsync(habit);
                await LoadHabitsAsync();
            }
            else if (dateChoice == "Yesterday")
            {
                habit.LastAppliedDate = DateTime.UtcNow.Date.AddDays(-1);
                await _newHabits.UpdateHabitAsync(habit);
                await LoadHabitsAsync();
            }
            else if (dateChoice == "2 days ago")
            {
                habit.LastAppliedDate = DateTime.UtcNow.Date.AddDays(-2);
                await _newHabits.UpdateHabitAsync(habit);
                await LoadHabitsAsync();
            }
            else if (dateChoice == "Clear (never applied)")
            {
                habit.LastAppliedDate = null;
                await _newHabits.UpdateHabitAsync(habit);
                await LoadHabitsAsync();
            }
        }
        else if (result == "🔄 Reset Progress to 0")
        {
            bool confirm = await DisplayAlert(
                "Reset Progress",
                $"Reset '{habit.HabitName}' progress to 0 days?\n\nThis will NOT count as a failure.",
                "Reset",
                "Cancel");

            if (confirm)
            {
                habit.ConsecutiveDays = 0;
                habit.LastAppliedDate = null;
                await _newHabits.UpdateHabitAsync(habit);
                await LoadHabitsAsync();
            }
        }
    }

    private async void OnAddPendingClicked(object? sender, EventArgs e)
    {
        var allGames = await _games.GetGamesAsync(_auth.CurrentUsername);
        if (allGames.Count == 0)
        {
            await DisplayAlert("No Games", "Please create a game first.", "OK");
            return;
        }

        string positiveChoice = await DisplayActionSheet(
            $"Add to {_frequency} Pending - Select Positive Activity",
            "Cancel",
            null,
            "Select existing activity",
            "Create new activity");

        if (positiveChoice == "Cancel" || string.IsNullOrEmpty(positiveChoice))
            return;

        Activity? positiveActivity = null;
        Activity? negativeActivity = null;

        if (positiveChoice == "Select existing activity")
        {
            var selectionPage = new ActivitySelectionPage(_activities, _games, _auth.CurrentUsername, "Select Activity for Habit", negativeOnly: false);
            await Navigation.PushModalAsync(selectionPage);
            positiveActivity = await selectionPage.GetSelectedActivityAsync();
            if (positiveActivity == null) return;

            negativeActivity = await GetOrCreateNegativeActivityAsync(positiveActivity);
            if (negativeActivity == null) return;
        }
        else if (positiveChoice == "Create new activity")
        {
            string? selectedGameId = null;
            if (allGames.Count == 1)
            {
                selectedGameId = allGames[0].GameId;
            }
            else
            {
                var gameNames = allGames.Select(g => g.DisplayName).ToArray();
                string? gameChoice = await DisplayActionSheet("Select Game", "Cancel", null, gameNames);
                if (gameChoice == "Cancel" || string.IsNullOrEmpty(gameChoice)) return;
                selectedGameId = allGames.FirstOrDefault(g => g.DisplayName == gameChoice)?.GameId;
                if (selectedGameId == null) return;
            }
            
            positiveActivity = await ActivityCreationPage.CreateActivityModalAsync(
                Navigation, _auth, _activities, _games, selectedGameId);
            if (positiveActivity == null) return;
            
            await DisplayAlert("Now Create Penalty Activity",
                $"Great! '{positiveActivity.Name}' created.\n\n" +
                "Now let's create the penalty activity for when you miss.",
                "Continue");
            
            negativeActivity = await CreatePrefillledNegativeActivityAsync(positiveActivity);
            if (negativeActivity == null)
            {
                bool selectExisting = await DisplayAlert(
                    "No Penalty Activity",
                    "You didn't create a penalty activity. Would you like to select an existing one?",
                    "Select Existing",
                    "Cancel");
                
                if (selectExisting)
                {
                    negativeActivity = await GetOrCreateNegativeActivityAsync(positiveActivity);
                    if (negativeActivity == null) return;
                }
                else
                {
                    return;
                }
            }
        }

        if (positiveActivity != null && negativeActivity != null)
        {
            // Create pending habit with frequency
            var conn = await _db.GetConnectionAsync();
            await conn.CreateTableAsync<NewHabit>();

            int daysToGraduate = _frequency switch
            {
                "Daily" => 7,
                "Weekly" => 4,
                "Monthly" => 3,
                _ => 7
            };

            var pendingHabits = await _newHabits.GetAllPendingHabitsAsync(_auth.CurrentUsername);
            int maxOrder = pendingHabits.Count > 0 ? pendingHabits.Max(h => h.PendingOrder) : 0;

            var newHabit = new NewHabit
            {
                Username = _auth.CurrentUsername,
                Game = positiveActivity.Game,
                HabitName = positiveActivity.Name,
                PositiveActivityId = positiveActivity.Id,
                NegativeActivityId = negativeActivity.Id,
                ConsecutiveDays = 0,
                DaysToGraduate = daysToGraduate,
                Frequency = _frequency,
                Status = "pending",
                PendingOrder = maxOrder + 1
            };

            await conn.InsertAsync(newHabit);

            await DisplayAlert("Added to Queue!",
                $"'{positiveActivity.Name}' added to {_frequency.ToLower()} pending.\n\nActivate it when you have an available slot!",
                "OK");

            await LoadHabitsAsync();
        }
    }

    private async void OnAddHabitClicked(object? sender, EventArgs e)
    {
        var allGames = await _games.GetGamesAsync(_auth.CurrentUsername);
        if (allGames.Count == 0)
        {
            await DisplayAlert("No Games", "Please create a game first.", "OK");
            return;
        }

        string positiveChoice = await DisplayActionSheet(
            $"Add {_frequency} Habit - Select Positive Activity",
            "Cancel",
            null,
            "Select existing activity",
            "Create new activity");

        if (positiveChoice == "Cancel" || string.IsNullOrEmpty(positiveChoice))
            return;

        Activity? positiveActivity = null;
        Activity? negativeActivity = null;

        if (positiveChoice == "Select existing activity")
        {
            var selectionPage = new ActivitySelectionPage(_activities, _games, _auth.CurrentUsername, "Select Activity for Habit", negativeOnly: false);
            await Navigation.PushModalAsync(selectionPage);
            positiveActivity = await selectionPage.GetSelectedActivityAsync();
            if (positiveActivity == null) return;

            negativeActivity = await GetOrCreateNegativeActivityAsync(positiveActivity);
            if (negativeActivity == null) return;
        }
        else if (positiveChoice == "Create new activity")
        {
            string? selectedGameId = null;
            if (allGames.Count == 1)
            {
                selectedGameId = allGames[0].GameId;
            }
            else
            {
                var gameNames = allGames.Select(g => g.DisplayName).ToArray();
                string? gameChoice = await DisplayActionSheet("Select Game", "Cancel", null, gameNames);
                if (gameChoice == "Cancel" || string.IsNullOrEmpty(gameChoice)) return;
                selectedGameId = allGames.FirstOrDefault(g => g.DisplayName == gameChoice)?.GameId;
                if (selectedGameId == null) return;
            }
            
            positiveActivity = await ActivityCreationPage.CreateActivityModalAsync(
                Navigation, _auth, _activities, _games, selectedGameId);
            if (positiveActivity == null) return;
            
            await DisplayAlert("Now Create Penalty Activity",
                $"Great! '{positiveActivity.Name}' created.\n\n" +
                "Now let's create the penalty activity for when you miss.\n\n" +
                "The name and image will be pre-filled for you.",
                "Continue");
            
            negativeActivity = await CreatePrefillledNegativeActivityAsync(positiveActivity);
            if (negativeActivity == null)
            {
                bool selectExisting = await DisplayAlert(
                    "No Penalty Activity",
                    "You didn't create a penalty activity. Would you like to select an existing one?",
                    "Select Existing",
                    "Cancel Habit Creation");
                
                if (selectExisting)
                {
                    negativeActivity = await GetOrCreateNegativeActivityAsync(positiveActivity);
                    if (negativeActivity == null) return;
                }
                else
                {
                    return;
                }
            }
        }

        if (positiveActivity != null && negativeActivity != null)
        {
            await CreateHabitFromActivitiesAsync(positiveActivity, negativeActivity);
        }
    }

    private async Task<Activity?> CreatePrefillledNegativeActivityAsync(Activity positiveActivity)
    {
        int absLevel = Math.Abs(positiveActivity.MeaningfulUntilLevel);
        
        string negativeName = $"{positiveActivity.Name} (Negative)";
        string negativeLevel = (-absLevel).ToString();
        string negativeImage = positiveActivity.ImagePath ?? "";
        string negativeCategory = "Negative";

        var activity = await ActivityCreationPage.CreateActivityModalAsync(
            Navigation, 
            _auth, 
            _activities, 
            _games, 
            positiveActivity.Game,
            prefillName: negativeName,
            prefillLevel: negativeLevel,
            prefillImage: negativeImage,
            prefillCategory: negativeCategory,
            isNegative: true);

        return activity;
    }

    private async Task<Activity?> GetOrCreateNegativeActivityAsync(Activity positiveActivity)
    {
        string negativeChoice = await DisplayActionSheet(
            "Select Penalty Activity",
            "Cancel",
            null,
            "Select existing negative activity",
            "Create new negative activity");

        if (negativeChoice == "Cancel" || string.IsNullOrEmpty(negativeChoice))
            return null;

        if (negativeChoice == "Select existing negative activity")
        {
            var selectionPage = new ActivitySelectionPage(_activities, _games, _auth.CurrentUsername, "Select Penalty Activity", negativeOnly: true);
            await Navigation.PushModalAsync(selectionPage);
            return await selectionPage.GetSelectedActivityAsync();
        }
        else if (negativeChoice == "Create new negative activity")
        {
            return await CreatePrefillledNegativeActivityAsync(positiveActivity);
        }

        return null;
    }

    private async Task CreateHabitFromActivitiesAsync(Activity positive, Activity negative)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<NewHabit>();

        var availableSlots = await _newHabits.GetAvailableSlotsAsync(_auth.CurrentUsername, _frequency);
        System.Diagnostics.Debug.WriteLine($"[NEW HABIT PAGE] Available {_frequency} slots before create: {availableSlots}");
        
        if (availableSlots <= 0)
        {
            await DisplayAlert("No Slots", $"No {_frequency.ToLower()} allowance slots available. Complete an existing habit first!", "OK");
            return;
        }

        // Set days to graduate based on frequency
        int daysToGraduate = _frequency switch
        {
            "Daily" => 7,
            "Weekly" => 4,
            "Monthly" => 3,
            _ => 7
        };

        var newHabit = new NewHabit
        {
            Username = _auth.CurrentUsername,
            Game = positive.Game,
            HabitName = positive.Name,
            PositiveActivityId = positive.Id,
            NegativeActivityId = negative.Id,
            ConsecutiveDays = 0,
            DaysToGraduate = daysToGraduate,
            Frequency = _frequency,
            StartedAt = DateTime.UtcNow,
            Status = "active"
        };
        
        System.Diagnostics.Debug.WriteLine($"[NEW HABIT PAGE] Creating {_frequency} habit: {newHabit.HabitName}");
        
        await conn.InsertAsync(newHabit);
        
        System.Diagnostics.Debug.WriteLine($"[NEW HABIT PAGE] Habit inserted with ID: {newHabit.Id}");

        string unitText = _frequency switch
        {
            "Daily" => "days",
            "Weekly" => "weeks",
            "Monthly" => "months",
            _ => "days"
        };

        await DisplayAlert("Habit Created!",
            $"'{positive.Name}' is now being tracked!\n\n" +
            $"📁 Game: {positive.Game}\n" +
            $"✅ Complete it: {positive.ExpGain:+#;-#;0} EXP\n" +
            $"❌ Miss it: {negative.ExpGain:+#;-#;0} EXP\n\n" +
            $"Complete {daysToGraduate} {unitText} to graduate!",
            "Let's go!");

        await LoadHabitsAsync();
    }
}
