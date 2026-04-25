using Bannister.Services;
using Bannister.Models;
using ConversationPractice.Services;
using ConversationPractice.Views;

namespace Bannister.Views;

public class HomePage : ContentPage
{
    private readonly AuthService _auth;
    private readonly GameService _games;
    private readonly DragonService _dragons;
    private readonly BackupService _backup;
    private readonly AttemptService _attempts;
    private readonly StreakService _streaks;
    private readonly DatabaseService _db;
    private readonly ExpService _exp;
    private readonly CountdownService _countdowns;
    private readonly LearningService _learning;
    private readonly ActivityService _activities;
    private readonly PromptService _prompts;
    private readonly StoryProductionService _storyProduction;
    private readonly TaskService _taskService;
    private readonly WeeklyChallengeService _challengeService;
    private readonly IdeasService _ideas;
    private readonly IdeaLoggerService _ideaLogger;
    private readonly ConversationService _conversationService;
    private readonly SubActivityService _subActivityService;
    private bool _introChecked = false;

    // UI Controls
    private Label _lblWelcome;
    private Button _btnGames;
    private Button _btnNewHabits;
    private Button _btnCharts;
    private Button _btnConversationPractice;
    private Button _btnPrompts;
    private Button _btnIdeas;
    private Button _btnStoryProduction;
    private Button _btnImageProduction;
    private Button _btnSubActivities;
    private Button _btnTasks;
    private Button _btnLearning;
    private Button _btnDatabases;
    private Button _btnDragons;
    private Button _btnStreaks;
    private Button _btnCountdowns;
    private Button _btnCalendar;
    private Grid _loadingOverlay;

    public HomePage(AuthService auth, GameService games, DragonService dragons,
        BackupService backup, AttemptService attempts, StreakService streaks, DatabaseService db, ExpService exp,
        CountdownService countdowns, LearningService learning, ActivityService activities, PromptService prompts,
        StoryProductionService storyProduction, TaskService taskService, WeeklyChallengeService challengeService,
        IdeasService ideas, IdeaLoggerService ideaLogger, ConversationService conversationService,
        SubActivityService subActivityService)
    {
        _auth = auth;
        _games = games;
        _dragons = dragons;
        _backup = backup;
        _attempts = attempts;
        _streaks = streaks;
        _db = db;
        _exp = exp;
        _countdowns = countdowns;
        _learning = learning;
        _activities = activities;
        _prompts = prompts;
        _storyProduction = storyProduction;
        _taskService = taskService;
        _challengeService = challengeService;
        _ideas = ideas;
        _ideaLogger = ideaLogger;
        _conversationService = conversationService;
        _subActivityService = subActivityService;

        Title = "Bannister";
        BackgroundColor = Color.FromArgb("#6B73FF");

        BuildUI();
    }

    private void BuildUI()
    {
        var mainGrid = new Grid();

        // Main Content ScrollView
        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16
        };

        // Header
        mainStack.Children.Add(new Label
        {
            Text = "Home",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        _lblWelcome = new Label
        {
            Text = "Welcome, user",
            TextColor = Colors.White
        };
        mainStack.Children.Add(_lblWelcome);

        // Build all navigation buttons, then sort alphabetically before adding
        var navButtons = new List<(string sortKey, Button btn)>();

        _btnCharts = CreateButton("📊 Charts", Color.FromArgb("#E3F2FD"), Color.FromArgb("#1565C0"));
        _btnCharts.Clicked += OnChartsClicked;
        navButtons.Add(("Charts", _btnCharts));

        _btnConversationPractice = CreateButton("💬 Conversation Practice", Color.FromArgb("#E8EAF6"), Color.FromArgb("#3F51B5"));
        _btnConversationPractice.Clicked += OnConversationPracticeClicked;
        navButtons.Add(("Conversation Practice", _btnConversationPractice));

        _btnCountdowns = CreateButton("⏳ Countdowns (0)", Color.FromArgb("#E1F5FE"), Color.FromArgb("#0277BD"));
        _btnCountdowns.Clicked += OnCountdownsClicked;
        navButtons.Add(("Countdowns", _btnCountdowns));

        _btnDatabases = CreateButton("🗄️ Databases", Color.FromArgb("#ECEFF1"), Color.FromArgb("#455A64"));
        _btnDatabases.Clicked += OnDatabasesClicked;
        navButtons.Add(("Databases", _btnDatabases));

        _btnDragons = CreateButton("🐉 Dragons (0)", Colors.White, Color.FromArgb("#5B63EE"));
        _btnDragons.Clicked += OnDragonsClicked;
        navButtons.Add(("Dragons", _btnDragons));

        _btnGames = CreateButton("🎮 Games (0)", Colors.White, Color.FromArgb("#5B63EE"), 56, true);
        _btnGames.Clicked += OnGamesClicked;
        navButtons.Add(("Games", _btnGames));

        _btnNewHabits = CreateButton("🌱 Habits", Color.FromArgb("#E8F5E9"), Color.FromArgb("#2E7D32"));
        _btnNewHabits.Clicked += OnNewHabitsClicked;
        navButtons.Add(("Habits", _btnNewHabits));

        _btnIdeas = CreateButton("💡 Ideas", Color.FromArgb("#FFF8E1"), Color.FromArgb("#F57C00"));
        _btnIdeas.Clicked += OnIdeasClicked;
        navButtons.Add(("Ideas", _btnIdeas));

        _btnImageProduction = CreateButton("🎨 Image Production", Color.FromArgb("#FCE4EC"), Color.FromArgb("#C62828"));
        _btnImageProduction.Clicked += OnImageProductionClicked;
        navButtons.Add(("Image Production", _btnImageProduction));

        _btnLearning = CreateButton("📚 Learning (Books & Videos)", Color.FromArgb("#FCE4EC"), Color.FromArgb("#C2185B"));
        _btnLearning.Clicked += OnLearningClicked;
        navButtons.Add(("Learning", _btnLearning));

        _btnPrompts = CreateButton("✨ Prompts", Color.FromArgb("#F3E5F5"), Color.FromArgb("#7B1FA2"));
        _btnPrompts.Clicked += OnPromptsClicked;
        navButtons.Add(("Prompts", _btnPrompts));

        _btnStoryProduction = CreateButton("🎬 Story Production", Color.FromArgb("#FFF8E1"), Color.FromArgb("#F57C00"));
        _btnStoryProduction.Clicked += OnStoryProductionClicked;
        navButtons.Add(("Story Production", _btnStoryProduction));

        _btnStreaks = CreateButton("🔥 Streaks (0 active)", Color.FromArgb("#FFF3E0"), Color.FromArgb("#E65100"));
        _btnStreaks.Clicked += OnStreaksClicked;
        navButtons.Add(("Streaks", _btnStreaks));

        _btnSubActivities = CreateButton("🔢 SubActivities", Color.FromArgb("#E0F7FA"), Color.FromArgb("#00838F"));
        _btnSubActivities.Clicked += OnSubActivitiesClicked;
        navButtons.Add(("SubActivities", _btnSubActivities));

        _btnTasks = CreateButton("📋 Tasks", Color.FromArgb("#E3F2FD"), Color.FromArgb("#1565C0"));
        _btnTasks.Clicked += OnTasksClicked;
        navButtons.Add(("Tasks", _btnTasks));

        _btnCalendar = CreateButton("📅 Calendar", Color.FromArgb("#E8EAF6"), Color.FromArgb("#283593"));
        _btnCalendar.Clicked += OnCalendarClicked;
        navButtons.Add(("Calendar", _btnCalendar));

        // Sort alphabetically and add to layout
        foreach (var (_, btn) in navButtons.OrderBy(b => b.sortKey, StringComparer.OrdinalIgnoreCase))
            mainStack.Children.Add(btn);

        // Logout (always last)
        var btnLogout = CreateButton("Logout", Colors.White, Color.FromArgb("#333333"));
        btnLogout.Margin = new Thickness(0, 16, 0, 0);
        btnLogout.Clicked += OnLogoutClicked;
        mainStack.Children.Add(btnLogout);

        scrollView.Content = mainStack;
        mainGrid.Children.Add(scrollView);

        // Loading Overlay
        _loadingOverlay = new Grid
        {
            IsVisible = false,
            BackgroundColor = Color.FromArgb("#80000000"),
            InputTransparent = false
        };

        var loadingStack = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 16
        };

        loadingStack.Children.Add(new ActivityIndicator
        {
            IsRunning = true,
            Color = Colors.White,
            WidthRequest = 50,
            HeightRequest = 50
        });

        loadingStack.Children.Add(new Label
        {
            Text = "Loading...",
            TextColor = Colors.White,
            FontSize = 16,
            HorizontalTextAlignment = TextAlignment.Center
        });

        _loadingOverlay.Children.Add(loadingStack);
        mainGrid.Children.Add(_loadingOverlay);

        Content = mainGrid;
    }

    private Label CreateSectionLabel(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            Margin = new Thickness(0, 16, 0, 8)
        };
    }

    private Button CreateButton(string text, Color bgColor, Color textColor, int height = 48, bool bold = true)
    {
        return new Button
        {
            Text = text,
            BackgroundColor = bgColor,
            TextColor = textColor,
            CornerRadius = 8,
            HeightRequest = height,
            FontSize = bold && height > 48 ? 18 : 16,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Auto-backup on login
        await _backup.AutoBackupAsync("login");

        _loadingOverlay.IsVisible = false;

        _lblWelcome.Text = $"Welcome, {_auth.CurrentUsername}";
        await LoadDataAsync();

        if (!_introChecked)
        {
            _introChecked = true;

            if (!await HasSeenIntroAsync())
            {
                bool goToDiet = await DisplayAlert(
                    "Welcome to Bannister",
                    "This is your Home screen.\n" +
                    "Later you'll choose between different EXP games like Diet, Exercise, and more.\n\n" +
                    "A Diet game has been created for you.\n" +
                    "Want to go in now?",
                    "Go to Diet",
                    "Skip");

                await MarkIntroSeenAsync();

                if (goToDiet)
                {
                    await Shell.Current.GoToAsync("activitygrid?gameId=diet");
                }
            }
        }

        // Check for expired activities
        await CheckExpiredActivitiesAsync();
        
        // Check for missing weekly task commitments (only on Saturday - last chance before week resets)
        await CheckWeeklyCommitmentsAsync();
        
        // Check for unfilled daily habit slots (only on Saturday)
        await CheckDailyHabitAllowanceAsync();
    }

    private async Task CheckDailyHabitAllowanceAsync()
    {
        try
        {
            // Only check on Saturday (last day to fill before week resets on Sunday)
            if (DateTime.Today.DayOfWeek != DayOfWeek.Saturday) return;

            var habitService = Application.Current?.Handler?.MauiContext?.Services
                .GetService<NewHabitService>();

            if (habitService == null) return;

            var (needsAlert, active, required) = await habitService.CheckDailyHabitAlertAsync(_auth.CurrentUsername);
            
            if (!needsAlert) return;

            // Only prompt once per day
            string lastPromptKey = $"daily_habit_allowance_prompt_{_auth.CurrentUsername}";
            string? lastPrompt = null;
            try { lastPrompt = await SecureStorage.GetAsync(lastPromptKey); } catch { }
            
            string today = DateTime.Today.ToString("yyyy-MM-dd");
            if (lastPrompt == today) return; // Already prompted today

            bool goToHabits = await DisplayAlert(
                "⚠️ Daily Habit Slots Unfilled",
                $"You have {active}/{required} daily habit slots filled this week.\n\n" +
                $"Today is your last chance! If you don't fill all {required} slot(s) by end of day, " +
                $"you'll lose 1 allowance (currently {required}).\n\n" +
                "Would you like to add daily habits now?",
                "Yes, Go to Daily Habits",
                "Later");

            // Mark as prompted today
            try { await SecureStorage.SetAsync(lastPromptKey, today); } catch { }

            if (goToHabits)
            {
                await Shell.Current.GoToAsync("newhabits?frequency=Daily");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking daily habit allowance: {ex.Message}");
        }
    }

    private async Task CheckWeeklyCommitmentsAsync()
    {
        try
        {
            // Only check on Saturday (last day to designate before Sunday reset)
            if (DateTime.Today.DayOfWeek != DayOfWeek.Saturday) return;

            var challengeService = Application.Current?.Handler?.MauiContext?.Services
                .GetService<WeeklyChallengeService>();

            if (challengeService == null) return;

            var challenge = await challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
            if (challenge == null) return;

            // Get current week's commitments
            var commitments = await challengeService.GetCurrentWeekCommitmentsAsync(challenge.Id);
            int committed = commitments.Count;
            int required = challenge.CurrentAllowance;

            // If we've already committed enough, no need to prompt
            if (committed >= required) return;

            // Only prompt once per day
            string lastPromptKey = $"weekly_commitment_prompt_{_auth.CurrentUsername}";
            string? lastPrompt = null;
            try { lastPrompt = await SecureStorage.GetAsync(lastPromptKey); } catch { }
            
            string today = DateTime.Today.ToString("yyyy-MM-dd");
            if (lastPrompt == today) return; // Already prompted today

            bool goToTasks = await DisplayAlert(
                "⚠️ Last Day to Designate Tasks",
                $"You have designated {committed}/{required} tasks for your focus '{challenge.FocusCategory}' this week.\n\n" +
                $"Today is your last chance! If you don't designate {required} task(s) by end of day, " +
                $"you'll lose allowance (currently {challenge.CurrentAllowance}).\n\n" +
                "Would you like to designate tasks now?",
                "Yes, Go to Tasks",
                "Later");

            // Mark as prompted today
            try { await SecureStorage.SetAsync(lastPromptKey, today); } catch { }

            if (goToTasks)
            {
                await Shell.Current.GoToAsync("tasks");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking weekly commitments: {ex.Message}");
        }
    }

    private async Task<bool> HasSeenIntroAsync()
    {
        try
        {
            var seen = await SecureStorage.GetAsync($"intro_seen_{_auth.CurrentUsername}");
            return seen == "true";
        }
        catch
        {
            return false;
        }
    }

    private async Task MarkIntroSeenAsync()
    {
        try
        {
            await SecureStorage.SetAsync($"intro_seen_{_auth.CurrentUsername}", "true");
        }
        catch { }
    }

    private async Task LoadDataAsync()
    {
        var games = await _games.GetGamesAsync(_auth.CurrentUsername);

        if (games.Count == 0)
        {
            // Create default games
            await _games.CreateGameAsync(_auth.CurrentUsername, "Diet");
            await _games.CreateGameAsync(_auth.CurrentUsername, "Learning");
            games = await _games.GetGamesAsync(_auth.CurrentUsername);
        }

        // Update games button with count
        _btnGames.Text = $"🎮 Games ({games.Count})";

        // Get all dragons count
        var activeDragons = await _dragons.GetActiveDragonsAsync(_auth.CurrentUsername);
        var slainDragons = await _dragons.GetSlainDragonsAsync(_auth.CurrentUsername);
        var irrelevantDragons = await _dragons.GetIrrelevantDragonsAsync(_auth.CurrentUsername);
        int totalDragons = activeDragons.Count + slainDragons.Count + irrelevantDragons.Count;

        _btnDragons.Text = $"🐉 Dragons ({activeDragons.Count} active)";

        // Count active streaks across all games
        int activeStreaksCount = 0;
        foreach (var game in games)
        {
            var streaks = await _streaks.GetActiveStreaksAsync(_auth.CurrentUsername, game.GameId);
            activeStreaksCount += streaks.Count;
        }
        _btnStreaks.Text = $"🔥 Streaks ({activeStreaksCount} active)";

        // Count active countdowns
        var activeCountdowns = await _countdowns.GetActiveCountdownsAsync(_auth.CurrentUsername);
        var expiredCountdowns = await _countdowns.GetExpiredCountdownsAsync(_auth.CurrentUsername);
        if (expiredCountdowns.Count > 0)
        {
            _btnCountdowns.Text = $"⏳ Countdowns ({activeCountdowns.Count} active, {expiredCountdowns.Count} need resolution!)";
            _btnCountdowns.BackgroundColor = Color.FromArgb("#FFF3E0");
            _btnCountdowns.TextColor = Color.FromArgb("#E65100");
        }
        else
        {
            _btnCountdowns.Text = $"⏳ Countdowns ({activeCountdowns.Count} active)";
        }

        // Update learning button with stats
        var (totalBooks, completedBooks, totalVideos, completedVideos) = await _learning.GetStatsAsync(_auth.CurrentUsername);
        int pendingBooks = totalBooks - completedBooks;
        int pendingVideos = totalVideos - completedVideos;

        if (totalBooks == 0 && totalVideos == 0)
        {
            _btnLearning.Text = "📚 Learning (Books & Videos)";
        }
        else
        {
            _btnLearning.Text = $"📚 Learning ({pendingBooks} books, {pendingVideos} videos to go)";
        }

        // Update prompts button with pack count
        var packNames = await _prompts.GetPackNamesAsync();
        _btnPrompts.Text = $"✨ Prompts ({packNames.Count} packs)";

        // Update story production button with project count (exclude drafts)
        var allProjects = await _storyProduction.GetActiveProjectsAsync(_auth.CurrentUsername);
        var originalProjects = allProjects.Where(p => p.ParentProjectId == null).ToList();
        if (originalProjects.Count > 0)
        {
            _btnStoryProduction.Text = $"🎬 Story Production ({originalProjects.Count} projects)";
        }
        else
        {
            _btnStoryProduction.Text = "🎬 Story Production";
        }
        
        // Update tasks button with count
        var (activeTasks, overdueTasks, dueTodayTasks, urgentTasks) = await _taskService.GetStatsAsync(_auth.CurrentUsername);
        if (urgentTasks > 0)
        {
            _btnTasks.Text = $"📋 Tasks ({activeTasks}, {urgentTasks} urgent!)";
            _btnTasks.BackgroundColor = Color.FromArgb("#F3E5F5");
            _btnTasks.TextColor = Color.FromArgb("#7B1FA2");
        }
        else if (overdueTasks > 0)
        {
            _btnTasks.Text = $"📋 Tasks ({activeTasks}, {overdueTasks} overdue!)";
            _btnTasks.BackgroundColor = Color.FromArgb("#FFEBEE");
            _btnTasks.TextColor = Color.FromArgb("#C62828");
        }
        else if (activeTasks > 0)
        {
            _btnTasks.Text = $"📋 Tasks ({activeTasks})";
            _btnTasks.BackgroundColor = Color.FromArgb("#E3F2FD");
            _btnTasks.TextColor = Color.FromArgb("#1565C0");
        }
        else
        {
            _btnTasks.Text = "📋 Tasks";
            _btnTasks.BackgroundColor = Color.FromArgb("#E3F2FD");
            _btnTasks.TextColor = Color.FromArgb("#1565C0");
        }
    }

    private async Task CheckExpiredActivitiesAsync()
    {
        try
        {
            var activityService = Application.Current?.Handler?.MauiContext?.Services
                .GetService<ActivityService>();

            if (activityService == null) return;

            var expired = await activityService.GetExpiredActivitiesAsync(_auth.CurrentUsername);

            if (expired.Count > 0)
            {
                bool handle = await DisplayAlert(
                    "Expired Activities",
                    $"You have {expired.Count} expired activity(ies). Would you like to review them?",
                    "Yes",
                    "Later");

                if (handle)
                {
                    await Shell.Current.GoToAsync("expiredactivities");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking expired activities: {ex.Message}");
        }
    }

    private async void OnGamesClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("gameslist");
    }

    private async void OnNewHabitsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("habits");
    }

    private async void OnChartsClicked(object? sender, EventArgs e)
    {
        var page = new ChartsHubPage(_auth, _games, _exp, _db);
        await Navigation.PushAsync(page);
    }

    private async void OnConversationPracticeClicked(object? sender, EventArgs e)
    {
        // Navigate to Conversation Practice scenarios page
        var page = new ConversationListPage(_conversationService, _auth.CurrentUsername);
        await Navigation.PushAsync(page);
    }

    private async void OnPromptsClicked(object? sender, EventArgs e)
    {
        var page = new PromptsPage(_auth, _prompts, _ideaLogger, _ideas);
        await Navigation.PushAsync(page);
    }

    private async void OnIdeasClicked(object? sender, EventArgs e)
    {
        var page = new IdeasPage(_auth, _ideas, _ideaLogger);
        await Navigation.PushAsync(page);
    }

    private async void OnStoryProductionClicked(object? sender, EventArgs e)
    {
        var page = new StoryProductionHubPage(_auth, _storyProduction, _ideas, _ideaLogger);
        await Navigation.PushAsync(page);
    }

    private async void OnImageProductionClicked(object? sender, EventArgs e)
    {
        var service = new ImageProductionService(_db);
        var page = new ImageProductionPage(_auth, service);
        await Navigation.PushAsync(page);
    }

    private async void OnCalendarClicked(object? sender, EventArgs e)
    {
        var page = new CalendarPage(_auth, _taskService, _ideas, _db);
        await Navigation.PushAsync(page);
    }

    private async void OnSubActivitiesClicked(object? sender, EventArgs e)
    {
        var page = new SubActivitiesPage(_auth, _subActivityService);
        await Navigation.PushAsync(page);
    }

    private async void OnLearningClicked(object? sender, EventArgs e)
    {
        var page = new LearningPage(_auth, _learning, _activities, _games);
        await Navigation.PushAsync(page);
    }

    private async void OnDatabasesClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("databases");
    }

    private async void OnDragonsClicked(object? sender, EventArgs e)
    {
        var page = new DragonsHubPage(_auth, _dragons, _attempts);
        await Navigation.PushAsync(page);
    }

    private async void OnStreaksClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("streakdashboard");
    }

    private async void OnTasksClicked(object? sender, EventArgs e)
    {
        var page = new TasksPage(_auth, _taskService, _challengeService);
        await Navigation.PushAsync(page);
    }

    private async void OnCountdownsClicked(object? sender, EventArgs e)
    {
        var page = new CountdownsHomePage(_auth, _countdowns);
        await Navigation.PushAsync(page);
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        await _backup.AutoBackupAsync("logout");

        _auth.Logout();

        // RESET STACK — ANDROID REQUIRED
        await Shell.Current.GoToAsync("//login");
    }
}
