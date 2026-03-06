using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class NewHabitsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly GameService _games;
    private readonly NewHabitService _newHabits;
    private readonly ActivityService _activities;
    private readonly ExpService _exp;
    private readonly DatabaseService _db;

    private Label _lblAllowance;
    private Label _lblAvailableSlots;
    private VerticalStackLayout _activeHabitsContainer;
    private VerticalStackLayout _pendingHabitsContainer;
    private VerticalStackLayout _graduatedHabitsContainer;
    private Button _btnAddHabit;
    private Button _btnAddPending;

    public NewHabitsPage(AuthService auth, GameService games, NewHabitService newHabits, 
        ActivityService activities, ExpService exp, DatabaseService db)
    {
        _auth = auth;
        _games = games;
        _newHabits = newHabits;
        _activities = activities;
        _exp = exp;
        _db = db;

        Title = "New Habits";
        BackgroundColor = Color.FromArgb("#6B73FF");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadHabitsAsync();
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
        mainStack.Children.Add(new Label
        {
            Text = "🌱 New Habits",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        mainStack.Children.Add(new Label
        {
            Text = "Build new habits one at a time. Complete 7 days to graduate and unlock more slots.",
            TextColor = Colors.White,
            Opacity = 0.9
        });

        // Allowance display (global, not per-game)
        var allowanceFrame = new Frame
        {
            BackgroundColor = Color.FromArgb("#4CAF50"),
            Padding = 16,
            CornerRadius = 12,
            HasShadow = true,
            Margin = new Thickness(0, 8, 0, 0)
        };

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

        allowanceFrame.Content = allowanceStack;
        mainStack.Children.Add(allowanceFrame);

        // Add habit button
        _btnAddHabit = new Button
        {
            Text = "+ Add New Habit",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold,
            IsEnabled = false
        };
        _btnAddHabit.Clicked += OnAddHabitClicked;
        mainStack.Children.Add(_btnAddHabit);

        // Active habits section
        mainStack.Children.Add(new Label
        {
            Text = "🔥 Active Habits",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            Margin = new Thickness(0, 16, 0, 8)
        });

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
            BackgroundColor = Color.FromArgb("#9C27B0"),
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
            BackgroundColor = Color.FromArgb("#E3F2FD"),
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
                   "• Graduate = +1 allowance slot\n" +
                   "• Fail = -1 allowance slot (min 1)\n\n" +
                   "💡 Use Pending to queue habits for later!",
            TextColor = Color.FromArgb("#1565C0"),
            FontSize = 13,
            LineHeight = 1.4
        };
        mainStack.Children.Add(infoFrame);

        scrollView.Content = mainStack;
        Content = scrollView;
    }

    private async Task LoadHabitsAsync()
    {
        // Check for missed habits across all games
        var allActiveHabits = await _newHabits.GetAllActiveHabitsAsync(_auth.CurrentUsername);
        var missedHabits = new List<NewHabit>();
        var checkedGames = new HashSet<string>();
        
        foreach (var habit in allActiveHabits)
        {
            if (!checkedGames.Contains(habit.Game))
            {
                checkedGames.Add(habit.Game);
                var missed = await _newHabits.CheckMissedHabitsAsync(_auth.CurrentUsername, habit.Game, _exp);
                missedHabits.AddRange(missed);
            }
        }
        
        if (missedHabits.Count > 0)
        {
            await DisplayAlert("Habits Failed",
                $"{missedHabits.Count} habit(s) failed because you missed a day:\n\n" +
                string.Join("\n", missedHabits.Select(h => $"• {h.HabitName}")),
                "OK");
        }

        // Check for graduations
        await CheckGraduationsAsync();

        // Load global allowance
        var allowance = await _newHabits.GetOrCreateAllowanceAsync(_auth.CurrentUsername);
        var availableSlots = await _newHabits.GetAvailableSlotsAsync(_auth.CurrentUsername);

        _lblAllowance.Text = $"Allowance: {allowance.CurrentAllowance}";
        _lblAvailableSlots.Text = availableSlots > 0
            ? $"{availableSlots} slot(s) available"
            : "No slots available - complete a habit first!";

        _btnAddHabit.IsEnabled = availableSlots > 0;
        _btnAddHabit.BackgroundColor = availableSlots > 0
            ? Color.FromArgb("#FF9800")
            : Color.FromArgb("#999");

        // Load ALL active habits across all games
        _activeHabitsContainer.Children.Clear();
        var activeHabits = await _newHabits.GetAllActiveHabitsAsync(_auth.CurrentUsername);

        if (activeHabits.Count == 0)
        {
            _activeHabitsContainer.Children.Add(new Label
            {
                Text = "No active habits. Add one to get started!",
                TextColor = Colors.White,
                Opacity = 0.7,
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

        // Load pending habits
        _pendingHabitsContainer.Children.Clear();
        var pendingHabits = await _newHabits.GetAllPendingHabitsAsync(_auth.CurrentUsername);

        if (pendingHabits.Count == 0)
        {
            _pendingHabitsContainer.Children.Add(new Label
            {
                Text = "No pending habits. Add habits to your queue!",
                TextColor = Colors.White,
                Opacity = 0.7,
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

        // Load graduated habits (across all games)
        _graduatedHabitsContainer.Children.Clear();
        var graduatedHabits = await _newHabits.GetAllGraduatedHabitsAsync(_auth.CurrentUsername);

        if (graduatedHabits.Count == 0)
        {
            _graduatedHabitsContainer.Children.Add(new Label
            {
                Text = "No graduated habits yet.",
                TextColor = Colors.White,
                Opacity = 0.7,
                HorizontalTextAlignment = TextAlignment.Center
            });
        }
        else
        {
            foreach (var habit in graduatedHabits.Take(10)) // Show only last 10
            {
                _graduatedHabitsContainer.Children.Add(CreateGraduatedHabitCard(habit));
            }
        }
    }

    private async Task CheckGraduationsAsync()
    {
        var activeHabits = await _newHabits.GetAllActiveHabitsAsync(_auth.CurrentUsername);

        foreach (var habit in activeHabits)
        {
            if (habit.ConsecutiveDays >= habit.DaysToGraduate)
            {
                await _newHabits.GraduateHabitAsync(habit);

                await DisplayAlert("🎓 Habit Graduated!",
                    $"'{habit.HabitName}' has graduated!\n\n" +
                    "You've earned +1 allowance slot!",
                    "Awesome!");
            }
        }
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

        // Progress
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
        bool isDoneToday = habit.LastAppliedDate?.Date == DateTime.Now.Date;
        string statusText = isDoneToday
            ? "✅ Done today!"
            : $"⏳ {habit.DaysRemaining} days to go";

        grid.Add(new Label
        {
            Text = statusText,
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        }, 0, 3);

        // Action buttons row
        var buttonStack = new HorizontalStackLayout { Spacing = 8 };

        // Move to pending button
        var btnMoveToPending = new Button
        {
            Text = "⏸️",
            BackgroundColor = Color.FromArgb("#9C27B0"),
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

        // Activate button (only if slots available)
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

        var stack = new HorizontalStackLayout { Spacing = 8 };

        stack.Children.Add(new Label
        {
            Text = "🎓",
            FontSize = 20,
            VerticalOptions = LayoutOptions.Center
        });

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
        stack.Children.Add(infoStack);

        stack.Children.Add(new Label
        {
            Text = habit.CompletedAt?.ToString("MMM dd") ?? "",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.EndAndExpand
        });

        frame.Content = stack;
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
        var availableSlots = await _newHabits.GetAvailableSlotsAsync(_auth.CurrentUsername);
        if (availableSlots <= 0)
        {
            await DisplayAlert("No Slots", "No allowance slots available. Complete or pause an active habit first!", "OK");
            return;
        }

        // Get the positive activity
        var positiveActivity = await _activities.GetActivityAsync(habit.PositiveActivityId);
        if (positiveActivity == null)
        {
            await DisplayAlert("Error", "Could not find the activity.", "OK");
            return;
        }

        // Check if negative activity is already set
        Activity? negativeActivity = null;
        if (habit.NegativeActivityId > 0)
        {
            negativeActivity = await _activities.GetActivityAsync(habit.NegativeActivityId);
        }

        // If no negative activity, ask user to select/create one
        if (negativeActivity == null)
        {
            await DisplayAlert("Set Penalty Activity",
                $"Before activating '{habit.HabitName}', you need to set a penalty activity.\n\nThis is what happens if you miss a day.",
                "Continue");

            negativeActivity = await GetOrCreateNegativeActivityAsync(positiveActivity);
            if (negativeActivity == null)
            {
                await DisplayAlert("Cancelled", "You need a penalty activity to activate a habit.", "OK");
                return;
            }

            // Update the habit with the negative activity ID
            habit.NegativeActivityId = negativeActivity.Id;
        }

        await _newHabits.ActivatePendingHabitAsync(habit);
        await DisplayAlert("Habit Activated!",
            $"'{habit.HabitName}' is now active!\n\nRemember to do it daily to avoid penalties.",
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
            var allowance = await _newHabits.GetOrCreateAllowanceAsync(_auth.CurrentUsername);
            allowance.CurrentAllowance = Math.Max(1, allowance.CurrentAllowance - 1);
            allowance.TotalFailed++;
            
            var conn = await _db.GetConnectionAsync();
            await conn.UpdateAsync(allowance);

            // Only delete the habit tracking record, NOT the activities
            await conn.DeleteAsync(habit);
            
            await LoadHabitsAsync();
        }
    }

    private async void OnAddPendingClicked(object? sender, EventArgs e)
    {
        // For pending, just select the positive activity - negative will be set when activating
        var allGames = await _games.GetGamesAsync(_auth.CurrentUsername);
        if (allGames.Count == 0)
        {
            await DisplayAlert("No Games", "Please create a game first.", "OK");
            return;
        }

        Activity? positiveActivity = null;

        // Go directly to activity selection
        var selectionPage = new ActivitySelectionPage(_activities, _games, _auth.CurrentUsername, "Select Activity for Pending Queue", negativeOnly: false);
        await Navigation.PushModalAsync(selectionPage);
        positiveActivity = await selectionPage.GetSelectedActivityAsync();
        
        if (positiveActivity == null) 
            return;

        // Create pending habit with just the positive activity (negative = 0 for now)
        await _newHabits.CreatePendingHabitAsync(
            _auth.CurrentUsername,
            positiveActivity.Game,
            positiveActivity.Name,
            positiveActivity.Id,
            0); // No negative activity yet - will be set when activating

        await DisplayAlert("Added to Queue!",
            $"'{positiveActivity.Name}' added to pending.\n\nWhen you activate it, you'll set up the penalty activity.",
            "OK");

        await LoadHabitsAsync();
    }

    private async void OnAddHabitClicked(object? sender, EventArgs e)
    {
        // First, need to select a game for creating new activities
        var allGames = await _games.GetGamesAsync(_auth.CurrentUsername);
        if (allGames.Count == 0)
        {
            await DisplayAlert("No Games", "Please create a game first.", "OK");
            return;
        }

        // Step 1: Choose positive activity source
        string positiveChoice = await DisplayActionSheet(
            "Add Habit - Select Positive Activity",
            "Cancel",
            null,
            "Select existing activity",
            "Create new activity");

        if (positiveChoice == "Cancel" || string.IsNullOrEmpty(positiveChoice))
            return;

        Activity? positiveActivity = null;
        Activity? negativeActivity = null;
        bool createdNewPositive = false;

        if (positiveChoice == "Select existing activity")
        {
            var selectionPage = new ActivitySelectionPage(_activities, _games, _auth.CurrentUsername, "Select Activity for Habit", negativeOnly: false);
            await Navigation.PushModalAsync(selectionPage);
            positiveActivity = await selectionPage.GetSelectedActivityAsync();
            if (positiveActivity == null) return;

            // For existing activity, still need to select/create negative
            negativeActivity = await GetOrCreateNegativeActivityAsync(positiveActivity);
            if (negativeActivity == null) return;
        }
        else if (positiveChoice == "Create new activity")
        {
            // Ask which game
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
            
            // Create positive activity
            positiveActivity = await ActivityCreationPage.CreateActivityModalAsync(
                Navigation, _auth, _activities, _games, selectedGameId);
            if (positiveActivity == null) return;
            
            createdNewPositive = true;
            
            // Automatically launch negative activity creation with prefilled values
            await DisplayAlert("Now Create Penalty Activity",
                $"Great! '{positiveActivity.Name}' created.\n\n" +
                "Now let's create the penalty activity for when you miss a day.\n\n" +
                "The name and image will be pre-filled for you.",
                "Continue");
            
            // Create prefilled negative activity
            negativeActivity = await CreatePrefillledNegativeActivityAsync(positiveActivity);
            if (negativeActivity == null)
            {
                // User cancelled - ask if they want to select existing instead
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
                    return; // Cancel the whole habit creation
                }
            }
        }

        // Create the habit
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
            "Add Habit - Select Penalty Activity",
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

        var availableSlots = await _newHabits.GetAvailableSlotsAsync(_auth.CurrentUsername);
        System.Diagnostics.Debug.WriteLine($"[NEW HABIT PAGE] Available slots before create: {availableSlots}");
        
        if (availableSlots <= 0)
        {
            await DisplayAlert("No Slots", "No allowance slots available. Complete an existing habit first!", "OK");
            return;
        }

        var newHabit = new NewHabit
        {
            Username = _auth.CurrentUsername,
            Game = positive.Game,
            HabitName = positive.Name,
            PositiveActivityId = positive.Id,
            NegativeActivityId = negative.Id,
            ConsecutiveDays = 0,
            StartedAt = DateTime.UtcNow,
            Status = "active"
        };
        
        System.Diagnostics.Debug.WriteLine($"[NEW HABIT PAGE] Creating habit: {newHabit.HabitName}, User: {newHabit.Username}, Game: {newHabit.Game}, Status: {newHabit.Status}");
        
        await conn.InsertAsync(newHabit);
        
        System.Diagnostics.Debug.WriteLine($"[NEW HABIT PAGE] Habit inserted with ID: {newHabit.Id}");

        await DisplayAlert("Habit Created!",
            $"'{positive.Name}' is now being tracked!\n\n" +
            $"📁 Game: {positive.Game}\n" +
            $"✅ Do it daily: {positive.ExpGain:+#;-#;0} EXP\n" +
            $"❌ Miss a day: {negative.ExpGain:+#;-#;0} EXP\n\n" +
            "Complete 7 days to graduate!",
            "Let's go!");

        await LoadHabitsAsync();
    }
}
