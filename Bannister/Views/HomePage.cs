using Bannister.Services;
using Bannister.Models;
using ConversationPractice.Services;
using ConversationPractice.Views;
using System.Globalization;

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
    private readonly MusicProductionService _musicProduction;
    private readonly CustomPromptService _customPrompts;
    private readonly TaskService _taskService;
    private readonly WeeklyChallengeService _challengeService;
    private readonly IdeasService _ideas;
    private readonly IdeaLoggerService _ideaLogger;
    private readonly OperationQueueService _operationQueue;
    private readonly SyncService _sync;
    private readonly ConversationService _conversationService;
    private readonly SubActivityService _subActivityService;
    private readonly AudioLibraryService _audioLibService;
    private readonly DailyLoginPromptService _dailyLoginPrompts;
    private readonly MoneyManagementService _moneyManagement;
    private readonly DesignationService _designationService;
    private readonly ListsService _listsService;
    private readonly CommandsCasinoService _commandsCasino;
    private readonly RoutineService _routineService;
    private readonly DeadlineService _deadlineService;
    private readonly AllowanceService _allowanceService;
    private readonly OperationApplierService _applier;
    private readonly PendingActivityIdeaService _pendingIdeas;
    private bool _introChecked = false;
    private bool _queueCheckCompleted = false;
    private bool _expiredActivitiesPromptChecked = false;
    private bool _dailyHabitAllowancePromptChecked = false;
    private bool _weeklyCommitmentsPromptChecked = false;
    private bool _subActivityDailyPromptChecked = false;
    private bool _deadlineCheckInChecked = false;
    private bool _allowanceDailyPromptChecked = false;
    private const string QueuePromptSnoozedUntilKey = "queue_prompt_snoozed_until";

    // UI Controls
    private Label _lblWelcome;
    private Button _btnGames;
    private Button _btnNewHabits;
    private Button _btnCharts;
    private Button _btnAllowances;
    private Button _btnCommandsCasino;
    private Button _btnConversationPractice;
    private Button _btnPrompts;
    private Button _btnIdeas;
    private Button _btnImageEdit;
    private Button _btnStoryProduction;
    private Button _btnMusicProduction;
    private Button _btnImageProduction;
    private Button _btnSubActivities;
    private Button _btnTasks;
    private Button _btnLearning;
    private Button _btnDatabases;
    private Button _btnDeadlines;
    private Button _btnDesignations;
    private Button _btnDragons;
    private Button _btnStreaks;
    private Button _btnCountdowns;
    private Button _btnCalendar;
    private Button _btnSettings;
    private Button _btnAudioLibrary;
    private Button _btnMoneyManagement;
    private Button _btnLists;
    private Grid _loadingOverlay;
    private Label _loadingOverlayLabel;

    public HomePage(AuthService auth, GameService games, DragonService dragons,
        BackupService backup, AttemptService attempts, StreakService streaks, DatabaseService db, ExpService exp,
        CountdownService countdowns, LearningService learning, ActivityService activities, PromptService prompts,
        StoryProductionService storyProduction, MusicProductionService musicProduction, TaskService taskService, WeeklyChallengeService challengeService,
        IdeasService ideas, IdeaLoggerService ideaLogger, ConversationService conversationService,
        SubActivityService subActivityService, AudioLibraryService audioLibService,
        DailyLoginPromptService dailyLoginPrompts, MoneyManagementService moneyManagement, ListsService listsService,
        OperationQueueService operationQueue, SyncService sync, OperationApplierService applier,
        PendingActivityIdeaService pendingIdeas, CustomPromptService customPrompts, DesignationService designationService,
        CommandsCasinoService commandsCasino, RoutineService routineService, DeadlineService deadlineService,
        AllowanceService allowanceService)
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
        _musicProduction = musicProduction;
        _customPrompts = customPrompts;
        _taskService = taskService;
        _challengeService = challengeService;
        _ideas = ideas;
        _ideaLogger = ideaLogger;
        _operationQueue = operationQueue;
        _sync = sync;
        _applier = applier;
        _pendingIdeas = pendingIdeas;
        _conversationService = conversationService;
        _subActivityService = subActivityService;
        _audioLibService = audioLibService;
        _dailyLoginPrompts = dailyLoginPrompts;
        _moneyManagement = moneyManagement;
        _designationService = designationService;
        _listsService = listsService;
        _commandsCasino = commandsCasino;
        _routineService = routineService;
        _deadlineService = deadlineService;
        _allowanceService = allowanceService;

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
        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };

        var headerLabel = new Label
        {
            Text = "Home",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(headerLabel, 0);
        headerGrid.Children.Add(headerLabel);

        var btnHomeMenu = new Button
        {
            Text = "⋮",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Color.FromArgb("#FBC02D"),
            TextColor = Colors.White,
            CornerRadius = 22,
            WidthRequest = 44,
            HeightRequest = 44,
            Padding = new Thickness(0),
            VerticalOptions = LayoutOptions.Center
        };
        btnHomeMenu.Clicked += OnHomeMenuClicked;
        Grid.SetColumn(btnHomeMenu, 1);
        headerGrid.Children.Add(btnHomeMenu);

        mainStack.Children.Add(headerGrid);

        _lblWelcome = new Label
        {
            Text = "Welcome, user",
            TextColor = Colors.White
        };
        mainStack.Children.Add(_lblWelcome);

        // Build all navigation buttons, then sort alphabetically before adding
        var navButtons = new List<(string sortKey, Button btn)>();

        _btnAllowances = CreateButton("Allowances", Color.FromArgb("#E0F2F1"), Color.FromArgb("#00695C"));
        _btnAllowances.Clicked += OnAllowancesClicked;
        navButtons.Add(("Allowances", _btnAllowances));

        _btnCalendar = CreateButton("📅 Calendar", Color.FromArgb("#E8EAF6"), Color.FromArgb("#283593"));
        _btnCalendar.Clicked += OnCalendarClicked;
        navButtons.Add(("Calendar", _btnCalendar));

        _btnCharts = CreateButton("📊 Charts", Color.FromArgb("#E3F2FD"), Color.FromArgb("#1565C0"));
        _btnCharts.Clicked += OnChartsClicked;
        navButtons.Add(("Charts", _btnCharts));

        _btnCommandsCasino = CreateButton("Commands Casino", Color.FromArgb("#FFF3E0"), Color.FromArgb("#BF360C"));
        _btnCommandsCasino.Clicked += OnCommandsCasinoClicked;
        navButtons.Add(("Commands Casino", _btnCommandsCasino));

        _btnConversationPractice = CreateButton("💬 Conversation Practice", Color.FromArgb("#E8EAF6"), Color.FromArgb("#3F51B5"));
        _btnConversationPractice.Clicked += OnConversationPracticeClicked;
        navButtons.Add(("Conversation Practice", _btnConversationPractice));

        _btnCountdowns = CreateButton("⏳ Countdowns (0)", Color.FromArgb("#E1F5FE"), Color.FromArgb("#0277BD"));
        _btnCountdowns.Clicked += OnCountdownsClicked;
        navButtons.Add(("Countdowns", _btnCountdowns));

        _btnDatabases = CreateButton("🗄️ Databases", Color.FromArgb("#ECEFF1"), Color.FromArgb("#455A64"));
        _btnDatabases.Clicked += OnDatabasesClicked;
        navButtons.Add(("Databases", _btnDatabases));

        _btnDeadlines = CreateButton("Deadlines", Color.FromArgb("#F3E5F5"), Color.FromArgb("#6A1B9A"));
        _btnDeadlines.Clicked += OnDeadlinesClicked;
        navButtons.Add(("Deadlines", _btnDeadlines));

        _btnDesignations = CreateButton("Designations", Color.FromArgb("#E8EAF6"), Color.FromArgb("#283593"));
        _btnDesignations.Clicked += OnDesignationsClicked;
        navButtons.Add(("Designations", _btnDesignations));

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

        _btnImageEdit = CreateButton("Image Edit", Color.FromArgb("#E0F7FA"), Color.FromArgb("#006064"));
        _btnImageEdit.Clicked += OnImageEditClicked;
        navButtons.Add(("Image Edit", _btnImageEdit));

        _btnLearning = CreateButton("📚 Learning (Books & Videos)", Color.FromArgb("#FCE4EC"), Color.FromArgb("#C2185B"));
        _btnLearning.Clicked += OnLearningClicked;
        navButtons.Add(("Learning", _btnLearning));

        _btnLists = CreateButton("Lists", Color.FromArgb("#E8EAF6"), Color.FromArgb("#283593"));
        _btnLists.Clicked += OnListsClicked;
        navButtons.Add(("Lists", _btnLists));

        _btnMoneyManagement = CreateButton("Money Management", Color.FromArgb("#E8F5E9"), Color.FromArgb("#1B5E20"));
        _btnMoneyManagement.Clicked += OnMoneyManagementClicked;
        navButtons.Add(("Money Management", _btnMoneyManagement));

        _btnMusicProduction = CreateButton("Music Production", Color.FromArgb("#E8EAF6"), Color.FromArgb("#3949AB"));
        _btnMusicProduction.Clicked += OnMusicProductionClicked;
        navButtons.Add(("Music Production", _btnMusicProduction));

        _btnPrompts = CreateButton("✨ Prompts", Color.FromArgb("#F3E5F5"), Color.FromArgb("#7B1FA2"));
        _btnPrompts.Clicked += OnPromptsClicked;
        navButtons.Add(("Prompts", _btnPrompts));

        _btnSettings = CreateButton("⚙️ Settings", Color.FromArgb("#ECEFF1"), Color.FromArgb("#37474F"));
        _btnSettings.Clicked += OnSettingsClicked;
        navButtons.Add(("Settings", _btnSettings));

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

        _btnAudioLibrary = CreateButton("🔊 Audio Library", Color.FromArgb("#EDE7F6"), Color.FromArgb("#4527A0"));
        _btnAudioLibrary.Clicked += OnAudioLibraryClicked;
        navButtons.Add(("Audio Library", _btnAudioLibrary));

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
            BackgroundColor = Color.FromRgba(0, 0, 0, 0.6),
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
            Color = Color.FromArgb("#5B63EE"),
            WidthRequest = 50,
            HeightRequest = 50
        });

        _loadingOverlayLabel = new Label
        {
            Text = "Loading...",
            TextColor = Color.FromArgb("#333"),
            FontSize = 16,
            HorizontalTextAlignment = TextAlignment.Center
        };
        loadingStack.Children.Add(_loadingOverlayLabel);

        _loadingOverlay.Children.Add(new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 12,
            Padding = 24,
            WidthRequest = 280,
            HasShadow = true,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Content = loadingStack
        });
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
        await ShowDailyLoginPromptsIfNeededAsync();
        await CheckSubActivityDailyPromptAsync();
        await CheckAllowanceDailyPromptAsync();
        await CheckDeadlineCheckInAsync();

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

        // Check for unfilled weekly/monthly habit slots (only on the 1st of the month)
        await CheckWeeklyHabitAllowanceAsync();
        await CheckMonthlyHabitAllowanceAsync();

        // Check if unencrypted legacy database file still exists
        await CheckLegacyUnencryptedDbAsync();

        _ = CheckQueuedOperationsPromptAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        return _loadingOverlay.IsVisible || base.OnBackButtonPressed();
    }

    private async Task CheckQueuedOperationsPromptAsync()
    {
        if (_db.IsReadOnly) return;
        if (_queueCheckCompleted) return;
        if (IsQueuePromptSnoozed()) return;

        _queueCheckCompleted = true;

        List<QueuedOperation> operations;
        try
        {
            operations = await _sync.DownloadQueueAsync();
        }
        catch
        {
            return;
        }

        if (operations.Count == 0) return;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var action = await DisplayActionSheet(
                $"Apply {operations.Count} queued operations?",
                null,
                null,
                "Apply now",
                "Remind me later",
                "Don't remind me until...");

            switch (action)
            {
                case "Apply now":
                    await Navigation.PushAsync(new QueuedOperationsPage(
                        _sync,
                        _applier,
                        _db,
                        _auth,
                        _activities,
                        _games,
                        _pendingIdeas));
                    break;
                case "Don't remind me until...":
                    var selectedDate = await QueueSnoozeDatePage.ShowAsync(Navigation);
                    if (selectedDate.HasValue)
                    {
                        Preferences.Default.Set(
                            QueuePromptSnoozedUntilKey,
                            selectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    }
                    break;
            }
        });
    }

    private static bool IsQueuePromptSnoozed()
    {
        if (!Preferences.Default.ContainsKey(QueuePromptSnoozedUntilKey))
            return false;

        var raw = Preferences.Default.Get(QueuePromptSnoozedUntilKey, "");
        return DateTime.TryParseExact(
                raw,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var snoozeDate) &&
            DateTime.Today < snoozeDate;
    }

    private async Task ApplyQueuedOperationsFromHomeAsync(List<QueuedOperation> operations)
    {
        try
        {
            ShowHomeOverlay("Applying queued operations...");
            var result = await _applier.ApplyAllAsync(operations);

            _loadingOverlayLabel.Text = "Clearing server...";
            var clearResult = await _sync.ClearAppliedFromServerAsync(result.UuidsToClear);
            if (!clearResult.Success)
                throw new InvalidOperationException(clearResult.Message);

            Preferences.Default.Remove(QueuePromptSnoozedUntilKey);
            HideHomeOverlay();

            await DisplayAlert(
                "Queue Applied",
                $"Applied {result.AppliedCount}, skipped {result.SkippedCount} (already applied), failed {result.FailedCount}. Cleared from server.",
                "OK");
        }
        catch (Exception ex)
        {
            HideHomeOverlay();
            await DisplayAlert("Apply Failed", ex.Message, "OK");
        }
    }

    private void ShowHomeOverlay(string status)
    {
        _loadingOverlayLabel.Text = status;
        _loadingOverlay.IsVisible = true;
    }

    private void HideHomeOverlay()
    {
        _loadingOverlay.IsVisible = false;
    }

    private async Task ShowDailyLoginPromptsIfNeededAsync()
    {
        string today = DateTime.Today.ToString("yyyy-MM-dd");
        string shownKey = $"daily_login_prompts_shown_{_auth.CurrentUsername}";
        string? lastShown = null;
        try { lastShown = await SecureStorage.GetAsync(shownKey); } catch { }

        if (lastShown == today) return;

        var prompts = await _dailyLoginPrompts.GetPromptsAsync(_auth.CurrentUsername, activeOnly: true);
        if (prompts.Count == 0)
        {
            try { await SecureStorage.SetAsync(shownKey, today); } catch { }
            return;
        }

        try { await SecureStorage.SetAsync(shownKey, today); } catch { }

        for (int i = 0; i < prompts.Count; i++)
        {
            await DailyLoginPromptDisplayPage.ShowAsync(Navigation, prompts[i], i + 1, prompts.Count);
        }
    }

    private async Task CheckSubActivityDailyPromptAsync()
    {
        if (_subActivityDailyPromptChecked) return;
        _subActivityDailyPromptChecked = true;

        try
        {
            if (_db.IsReadOnly) return;

            string today = DateTime.Today.ToString("yyyy-MM-dd");
            string lastPromptKey = $"subactivity_daily_prompt_{_auth.CurrentUsername}";
            string? lastPrompt = null;
            try { lastPrompt = await SecureStorage.GetAsync(lastPromptKey); } catch { }

            if (lastPrompt == today) return;

            var processes = await _subActivityService.GetDailyPromptProcessesAsync(_auth.CurrentUsername);
            if (processes.Count == 0) return;

            foreach (var process in processes)
            {
                string action = await DisplayActionSheet(
                    $"Did you complete all sub-activities for \"{process.Name}\"?",
                    "Cancel",
                    null,
                    "Mark All Done",
                    "Not Yet");

                if (action == "Mark All Done")
                {
                    await _subActivityService.CompleteAllStepsAsync(process);
                }
            }

            try { await SecureStorage.SetAsync(lastPromptKey, today); } catch { }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking sub-activity daily prompts: {ex.Message}");
        }
    }

    private async Task CheckDeadlineCheckInAsync()
    {
        if (_deadlineCheckInChecked) return;
        _deadlineCheckInChecked = true;

        try
        {
            if (_db.IsReadOnly) return;

            string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string lastPromptKey = $"deadlines_checkin_{_auth.CurrentUsername}";
            string? lastPrompt = null;
            try { lastPrompt = await SecureStorage.GetAsync(lastPromptKey); } catch { }

            if (lastPrompt == today) return;

            var overdue = await _deadlineService.GetOverdueActiveDeadlinesAsync(_auth.CurrentUsername);
            if (overdue.Count == 0) return;

            foreach (var deadline in overdue)
            {
                bool completed = await DisplayAlert(
                    "Deadline Check-In",
                    $"Did you complete \"{deadline.Title}\"? ({DeadlineService.BucketName(deadline.Bucket)})",
                    "Yes",
                    "No");

                await _deadlineService.SetStateAsync(
                    deadline.Id,
                    completed ? DeadlineService.StateArchived : DeadlineService.StateFailed);
            }

            try { await SecureStorage.SetAsync(lastPromptKey, today); } catch { }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking deadline prompts: {ex.Message}");
        }
    }

    private async Task CheckAllowanceDailyPromptAsync()
    {
        if (_allowanceDailyPromptChecked) return;
        _allowanceDailyPromptChecked = true;

        try
        {
            if (_db.IsReadOnly) return;

            string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string lastPromptKey = $"allowance_daily_prompt_{_auth.CurrentUsername}";
            string? lastPrompt = null;
            try { lastPrompt = await SecureStorage.GetAsync(lastPromptKey); } catch { }

            if (lastPrompt == today) return;

            var allowances = await _allowanceService.GetDailyPromptAllowancesAsync(_auth.CurrentUsername);
            if (allowances.Count == 0) return;

            foreach (var allowance in allowances)
            {
                bool completed = await DisplayAlert(
                    "Daily Allowance Check-In",
                    $"Did you complete \"{allowance.Title}\" today?",
                    "Yes",
                    "No");

                if (completed)
                    await _allowanceService.IncrementAsync(allowance.Id);
            }

            try { await SecureStorage.SetAsync(lastPromptKey, today); } catch { }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking allowance daily prompts: {ex.Message}");
        }
    }

    private async Task CheckDailyHabitAllowanceAsync()
    {
        if (_dailyHabitAllowancePromptChecked) return;
        _dailyHabitAllowancePromptChecked = true;

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

    private async Task CheckWeeklyHabitAllowanceAsync()
    {
        try
        {
            if (DateTime.Today.Day != 1) return;

            var habitService = Application.Current?.Handler?.MauiContext?.Services
                .GetService<NewHabitService>();

            if (habitService == null) return;

            var (needsAlert, active, required) = await habitService.CheckWeeklyHabitAlertAsync(_auth.CurrentUsername);

            if (!needsAlert) return;

            string lastPromptKey = $"weekly_habit_allowance_prompt_{_auth.CurrentUsername}";
            string? lastPrompt = null;
            try { lastPrompt = await SecureStorage.GetAsync(lastPromptKey); } catch { }

            string thisMonth = DateTime.Today.ToString("yyyy-MM");
            if (lastPrompt == thisMonth) return;

            bool goToHabits = await DisplayAlert(
                "Weekly Habit Slots Unfilled",
                $"You have {active}/{required} weekly habit slots filled this month. Go to Weekly Habits to designate which weekly habits you'll pursue?",
                "Yes, Go to Weekly Habits",
                "Later");

            try { await SecureStorage.SetAsync(lastPromptKey, thisMonth); } catch { }

            if (goToHabits)
            {
                await Shell.Current.GoToAsync("newhabits?frequency=Weekly");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking weekly habit allowance: {ex.Message}");
        }
    }

    private async Task CheckMonthlyHabitAllowanceAsync()
    {
        try
        {
            if (DateTime.Today.Day != 1) return;

            var habitService = Application.Current?.Handler?.MauiContext?.Services
                .GetService<NewHabitService>();

            if (habitService == null) return;

            var (needsAlert, active, required) = await habitService.CheckMonthlyHabitAlertAsync(_auth.CurrentUsername);

            if (!needsAlert) return;

            string lastPromptKey = $"monthly_habit_allowance_prompt_{_auth.CurrentUsername}";
            string? lastPrompt = null;
            try { lastPrompt = await SecureStorage.GetAsync(lastPromptKey); } catch { }

            string thisMonth = DateTime.Today.ToString("yyyy-MM");
            if (lastPrompt == thisMonth) return;

            bool goToHabits = await DisplayAlert(
                "Monthly Habit Slots Unfilled",
                $"You have {active}/{required} monthly habit slots filled this month. Go to Monthly Habits to designate which monthly habits you'll pursue?",
                "Yes, Go to Monthly Habits",
                "Later");

            try { await SecureStorage.SetAsync(lastPromptKey, thisMonth); } catch { }

            if (goToHabits)
            {
                await Shell.Current.GoToAsync("newhabits?frequency=Monthly");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking monthly habit allowance: {ex.Message}");
        }
    }

    private async Task CheckWeeklyCommitmentsAsync()
    {
        if (_weeklyCommitmentsPromptChecked) return;
        _weeklyCommitmentsPromptChecked = true;

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
        
        var musicProjects = await _musicProduction.GetActiveProjectsAsync(_auth.CurrentUsername);
        var originalMusicProjects = musicProjects.Where(p => p.ParentProjectId == null).ToList();
        _btnMusicProduction.Text = originalMusicProjects.Count > 0
            ? $"Music Production ({originalMusicProjects.Count} projects)"
            : "Music Production";

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

    private async Task CheckLegacyUnencryptedDbAsync()
    {
        try
        {
            if (!DatabaseService.LegacyUnencryptedFileExists)
                return;

            // Only prompt once per day
            string promptKey = $"legacy_db_prompted_{_auth.CurrentUsername}";
            string? prompted = null;
            try { prompted = await SecureStorage.GetAsync(promptKey); } catch { }
            
            string today = DateTime.Today.ToString("yyyy-MM-dd");
            if (prompted == today) return;

            bool delete = await DisplayAlert(
                "🔒 Unencrypted Database Found",
                "An old unencrypted copy of your database still exists on disk " +
                "(bannister_unencrypted.db).\n\n" +
                "Your data has been migrated to an encrypted database. " +
                "It is recommended to delete the old unencrypted file for security.\n\n" +
                "Delete the unencrypted copy now?",
                "Delete It",
                "Keep For Now");

            try { await SecureStorage.SetAsync(promptKey, today); } catch { }

            if (delete)
            {
                bool deleted = DatabaseService.DeleteLegacyUnencryptedFile();
                if (deleted)
                    await DisplayAlert("Deleted", "The unencrypted database copy has been removed.", "OK");
                else
                    await DisplayAlert("Error", "Could not delete the file. You can manually delete 'bannister_unencrypted.db' from the app data folder.", "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking legacy DB: {ex.Message}");
        }
    }

    private async Task CheckExpiredActivitiesAsync()
    {
        if (_expiredActivitiesPromptChecked) return;
        _expiredActivitiesPromptChecked = true;

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
        if (await ShouldBlockGamesUntilCalendarVisitAsync())
        {
            string action = await DisplayActionSheet(
                "Visit Calendar First",
                "Cancel",
                null,
                "Open Calendar",
                "Disable Block And Open Games");

            if (action == "Open Calendar")
            {
                await OpenCalendarFromHomeAsync();
                return;
            }

            if (action == "Disable Block And Open Games")
            {
                await SetCalendarBeforeGamesBlockEnabledAsync(false);
            }
            else
            {
                return;
            }
        }

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

    private async void OnCommandsCasinoClicked(object? sender, EventArgs e)
    {
        var page = new CommandsCasinoPage(_auth, _commandsCasino);
        await Navigation.PushAsync(page);
    }

    private async void OnPromptsClicked(object? sender, EventArgs e)
    {
        var page = new PromptsPage(_auth, _prompts, _ideaLogger, _ideas);
        await Navigation.PushAsync(page);
    }

    private async void OnIdeasClicked(object? sender, EventArgs e)
    {
        var page = new IdeasPage(_auth, _ideas, _ideaLogger, _db, _operationQueue, _sync);
        await Navigation.PushAsync(page);
    }

    private async void OnStoryProductionClicked(object? sender, EventArgs e)
    {
        var page = new StoryProductionHubPage(_auth, _storyProduction, _ideas, _ideaLogger, _subActivityService, _customPrompts);
        await Navigation.PushAsync(page);
    }

    private async void OnMusicProductionClicked(object? sender, EventArgs e)
    {
        var page = new MusicProductionHubPage(_auth, _musicProduction, _db, _ideas, _customPrompts);
        await Navigation.PushAsync(page);
    }

    private async void OnImageProductionClicked(object? sender, EventArgs e)
    {
        var service = new ImageProductionService(_db);
        var page = new ImageProductionPage(_auth, service);
        await Navigation.PushAsync(page);
    }

    private async void OnImageEditClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new ImageEditPage());
    }

    private async void OnCalendarClicked(object? sender, EventArgs e)
    {
        await OpenCalendarFromHomeAsync();
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

    private async void OnAllowancesClicked(object? sender, EventArgs e)
    {
        var page = new AllowancesPage(_auth, _allowanceService);
        await Navigation.PushAsync(page);
    }

    private async void OnDeadlinesClicked(object? sender, EventArgs e)
    {
        var page = new DeadlinesHubPage(_auth, _deadlineService);
        await Navigation.PushAsync(page);
    }

    private async void OnDesignationsClicked(object? sender, EventArgs e)
    {
        var page = new DesignationsPage(_auth, _designationService);
        await Navigation.PushAsync(page);
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
        var page = new TasksPage(_auth, _taskService, _challengeService, _ideas);
        await Navigation.PushAsync(page);
    }

    private async void OnCountdownsClicked(object? sender, EventArgs e)
    {
        var page = new CountdownsHomePage(_auth, _countdowns);
        await Navigation.PushAsync(page);
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        await NavigateToSettingsAsync();
    }

    private async void OnAudioLibraryClicked(object? sender, EventArgs e)
    {
        var page = new AudioLibraryPage(_auth, _audioLibService);
        await Navigation.PushAsync(page);
    }

    private async void OnMoneyManagementClicked(object? sender, EventArgs e)
    {
        var page = new MoneyManagementHubPage(_auth, _moneyManagement);
        await Navigation.PushAsync(page);
    }

    private async void OnListsClicked(object? sender, EventArgs e)
    {
        var page = new ListsPage(_auth, _listsService);
        await Navigation.PushAsync(page);
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        await LogoutAsync();
    }

    private async void OnHomeMenuClicked(object? sender, EventArgs e)
    {
        string action = await DisplayActionSheet(
            "Home",
            "Cancel",
            null,
            "Refresh",
            "Manual Backup",
            "Daily Login Prompts",
            "Settings",
            "Logout");

        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        switch (action)
        {
            case "Refresh":
                await LoadDataAsync();
                break;
            case "Manual Backup":
                var result = await _backup.ManualBackupAsync();
                await DisplayAlert(result.success ? "Backup Created" : "Backup Failed", result.message, "OK");
                break;
            case "Daily Login Prompts":
                await Navigation.PushAsync(new DailyLoginPromptsPage(_auth, _dailyLoginPrompts));
                break;
            case "Settings":
                await NavigateToSettingsAsync();
                break;
            case "Logout":
                await LogoutAsync();
                break;
        }
    }

    private async Task NavigateToSettingsAsync()
    {
        var page = new SettingsPage(_auth, _db, _backup);
        await Navigation.PushAsync(page);
    }

    private async Task OpenCalendarFromHomeAsync()
    {
        await MarkCalendarVisitedTodayAsync();
        var page = new CalendarPage(_auth, _taskService, _ideas, _db, routineService: _routineService);
        await Navigation.PushAsync(page);
    }

    private async Task<bool> ShouldBlockGamesUntilCalendarVisitAsync()
    {
        if (!await GetCalendarBeforeGamesBlockEnabledAsync())
            return false;

        string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string? lastVisited = null;
        try { lastVisited = await SecureStorage.GetAsync(GetCalendarVisitedStorageKey()); } catch { }
        return lastVisited != today;
    }

    private async Task MarkCalendarVisitedTodayAsync()
    {
        string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        try { await SecureStorage.SetAsync(GetCalendarVisitedStorageKey(), today); } catch { }
    }

    private async Task<bool> GetCalendarBeforeGamesBlockEnabledAsync()
    {
        string? value = null;
        try { value = await SecureStorage.GetAsync(GetCalendarBeforeGamesBlockStorageKey()); } catch { }
        return value != "false";
    }

    private async Task SetCalendarBeforeGamesBlockEnabledAsync(bool enabled)
    {
        try { await SecureStorage.SetAsync(GetCalendarBeforeGamesBlockStorageKey(), enabled ? "true" : "false"); } catch { }
    }

    private string GetCalendarVisitedStorageKey() => $"home_calendar_visited_{_auth.CurrentUsername}";

    private string GetCalendarBeforeGamesBlockStorageKey() => $"home_block_games_until_calendar_{_auth.CurrentUsername}";

    private async Task LogoutAsync()
    {
        await _backup.AutoBackupAsync("logout");

        _auth.Logout();

        // RESET STACK — ANDROID REQUIRED
        await Shell.Current.GoToAsync("//login");
    }

    private sealed class QueueSnoozeDatePage : ContentPage
    {
        private readonly TaskCompletionSource<DateTime?> _completion = new();
        private readonly DatePicker _datePicker;

        private QueueSnoozeDatePage()
        {
            Title = "Queue Reminder";
            BackgroundColor = Color.FromRgba(0, 0, 0, 0.45);

            _datePicker = new DatePicker
            {
                Date = DateTime.Today.AddDays(7),
                MinimumDate = DateTime.Today.AddDays(1),
                MaximumDate = DateTime.Today.AddDays(90),
                TextColor = Color.FromArgb("#222"),
                BackgroundColor = Colors.White,
                HorizontalOptions = LayoutOptions.Fill
            };

            var okButton = new Button
            {
                Text = "OK",
                BackgroundColor = Color.FromArgb("#5B63EE"),
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 44
            };
            okButton.Clicked += async (_, _) =>
            {
                _completion.TrySetResult(_datePicker.Date);
                await Navigation.PopModalAsync();
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                BackgroundColor = Color.FromArgb("#ECEFF1"),
                TextColor = Color.FromArgb("#333"),
                CornerRadius = 8,
                HeightRequest = 44
            };
            cancelButton.Clicked += async (_, _) =>
            {
                _completion.TrySetResult(null);
                await Navigation.PopModalAsync();
            };

            var buttonGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 10
            };
            buttonGrid.Add(cancelButton, 0, 0);
            buttonGrid.Add(okButton, 1, 0);

            Content = new Grid
            {
                Padding = 24,
                Children =
                {
                    new Frame
                    {
                        BackgroundColor = Colors.White,
                        CornerRadius = 12,
                        Padding = 20,
                        HasShadow = true,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Fill,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 16,
                            Children =
                            {
                                new Label
                                {
                                    Text = "Don't remind me until",
                                    FontSize = 20,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Color.FromArgb("#333")
                                },
                                _datePicker,
                                buttonGrid
                            }
                        }
                    }
                }
            };
        }

        protected override bool OnBackButtonPressed()
        {
            _completion.TrySetResult(null);
            return base.OnBackButtonPressed();
        }

        public static async Task<DateTime?> ShowAsync(INavigation navigation)
        {
            var page = new QueueSnoozeDatePage();
            await navigation.PushModalAsync(page);
            return await page._completion.Task;
        }
    }
}
