using Bannister.Services;
using Bannister.Models;
using ConversationPractice.Services;
using ConversationPractice.Views;
using System.Globalization;

namespace Bannister.Views;

public class HomePage : ContentPage
{
    private sealed record HomeNavButton(string Id, Button SourceButton);

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
    private readonly CustomGameService _customGames;
    private readonly OperationApplierService _applier;
    private readonly PendingActivityIdeaService _pendingIdeas;
    private readonly OpenAIKeyService _openAIKeyService;
    private readonly OpenAIImageService _openAIImageService;
    private readonly OwnerModeService _ownerMode;
    private readonly WebsiteProjectService _websiteProjects;
    private readonly WebsiteIdeaService _websiteIdeas;
    private bool _introChecked = false;
    private bool _queueCheckCompleted = false;
    private bool _expiredActivitiesPromptChecked = false;
    private bool _dailyHabitAllowancePromptChecked = false;
    private bool _weeklyCommitmentsPromptChecked = false;
    private bool _subActivityDailyPromptChecked = false;
    private bool _deadlineCheckInChecked = false;
    private bool _allowanceDailyPromptChecked = false;
    private bool _isHomeVisible = false;
    private int _homePromptRunId = 0;
    private bool _homePromptSequenceRunning = false;
    private const string QueuePromptSnoozedUntilKey = "queue_prompt_snoozed_until";

    // UI Controls
    private Label _lblWelcome;
    private Button _btnGames;
    private Button _btnNewHabits;
    private Button _btnCharts;
    private Button _btnAllowances;
    private Button _btnCommandsCasino;
    private Button _btnConversationPractice;
    private Button _btnCustomGames;
    private Button _btnPrompts;
    private Button _btnIdeas;
    private Button _btnImageEdit;
    private Button _btnImageGeneration;
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
    private Button _btnToBeTested;
    private Button _btnWebsiteBuilder;
    private Button _btnZeroCounts;
    private VerticalStackLayout _buttonSectionsStack;
    private List<HomeNavButton> _allHomeNavButtons = new();
    private List<HomeNavButton> _homeNavButtons = new();
    private Grid _loadingOverlay;
    private Label _loadingOverlayLabel;
    private Label _ownerModeStatusLabel;
    private Button _lockOwnerModeButton;
    private bool _isOwnerModeUnlocked;
    private int _ownerModeTapCount;
    private DateTime _ownerModeFirstTapAt = DateTime.MinValue;

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
        AllowanceService allowanceService, CustomGameService customGames, OpenAIKeyService openAIKeyService,
        OpenAIImageService openAIImageService, OwnerModeService ownerMode, WebsiteProjectService websiteProjects,
        WebsiteIdeaService websiteIdeas)
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
        _customGames = customGames;
        _openAIKeyService = openAIKeyService;
        _openAIImageService = openAIImageService;
        _ownerMode = ownerMode;
        _websiteProjects = websiteProjects;
        _websiteIdeas = websiteIdeas;
        _ownerMode.StateChanged += OnOwnerModeStateChanged;

        Title = "Bannister";
        BackgroundColor = Color.FromArgb("#6B73FF");

        BuildUI();
        _ = RefreshOwnerModeUiAsync();
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

        _btnCustomGames = CreateButton("🎲 Custom Games", Color.FromArgb("#EDE7F6"), Color.FromArgb("#512DA8"));
        _btnCustomGames.Clicked += OnCustomGamesClicked;
        navButtons.Add(("Custom Games", _btnCustomGames));

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

        _btnImageGeneration = CreateButton("Image Generation", Color.FromArgb("#F8BBD0"), Color.FromArgb("#C2185B"));
        _btnImageGeneration.Clicked += OnImageGenerationClicked;
        navButtons.Add(("Image Generation", _btnImageGeneration));

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

        _btnToBeTested = CreateButton("To Be Tested", Color.FromArgb("#FFF9C4"), Color.FromArgb("#F57C00"));
        _btnToBeTested.Clicked += OnToBeTestedClicked;
        navButtons.Add(("To Be Tested", _btnToBeTested));

        _btnAudioLibrary = CreateButton("🔊 Audio Library", Color.FromArgb("#EDE7F6"), Color.FromArgb("#4527A0"));
        _btnAudioLibrary.Clicked += OnAudioLibraryClicked;
        navButtons.Add(("Audio Library", _btnAudioLibrary));

        _btnWebsiteBuilder = CreateButton("Website Builder", Color.FromArgb("#B3E5FC"), Color.FromArgb("#01579B"));
        _btnWebsiteBuilder.Clicked += OnWebsiteBuilderClicked;
        navButtons.Add(("Website Builder", _btnWebsiteBuilder));

        _btnZeroCounts = CreateButton("Zero Counts", Color.FromArgb("#D1FAE5"), Color.FromArgb("#065F46"));
        _btnZeroCounts.Clicked += OnZeroCountsClicked;
        navButtons.Add(("Zero Counts", _btnZeroCounts));

        _allHomeNavButtons = navButtons
            .Select(item => new HomeNavButton(item.sortKey, item.btn))
            .ToList();
        _homeNavButtons = GetVisibleHomeNavButtons();

        _buttonSectionsStack = new VerticalStackLayout
        {
            Spacing = 10,
            Margin = new Thickness(0, 16, 0, 0)
        };
        mainStack.Children.Add(_buttonSectionsStack);
        RefreshButtonsLayout();

        // Logout (always last)
        var btnLogout = CreateButton("Logout", Colors.White, Color.FromArgb("#333333"));
        btnLogout.Margin = new Thickness(0, 16, 0, 0);
        btnLogout.Clicked += OnLogoutClicked;
        mainStack.Children.Add(btnLogout);
        mainStack.Children.Add(CreateOwnerModeSection());

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

    private View CreateOwnerModeSection()
    {
        _ownerModeStatusLabel = new Label
        {
            Text = "Owner Mode: Locked",
            FontSize = 13,
            TextColor = Color.FromArgb("#ECEFF1"),
            VerticalOptions = LayoutOptions.Center
        };

        _lockOwnerModeButton = new Button
        {
            Text = "Lock Owner Mode",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333333"),
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12,
            IsVisible = false
        };
        _lockOwnerModeButton.Clicked += async (_, _) => await LockOwnerModeAsync();

        var statusGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 10
        };
        statusGrid.Add(_ownerModeStatusLabel, 0, 0);
        statusGrid.Add(_lockOwnerModeButton, 1, 0);

        var versionLabel = new Label
        {
            Text = $"Bannister v{AppInfo.Current.VersionString}",
            FontSize = 12,
            TextColor = Color.FromArgb("#DDE1FF"),
            HorizontalTextAlignment = TextAlignment.Center
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await HandleOwnerModeVersionTapAsync();
        versionLabel.GestureRecognizers.Add(tap);

        return new VerticalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 0),
            Children =
            {
                statusGrid,
                versionLabel
            }
        };
    }

    private List<HomeNavButton> GetVisibleHomeNavButtons()
    {
        return _allHomeNavButtons
            .Where(button => _isOwnerModeUnlocked || button.Id != "Image Generation")
            .ToList();
    }

    private async Task RefreshOwnerModeUiAsync()
    {
        _isOwnerModeUnlocked = await _ownerMode.IsUnlockedAsync();
        _homeNavButtons = GetVisibleHomeNavButtons();

        if (_ownerModeStatusLabel != null)
        {
            _ownerModeStatusLabel.Text = _isOwnerModeUnlocked
                ? "Owner Mode: Unlocked"
                : "Owner Mode: Locked";
            _ownerModeStatusLabel.TextColor = _isOwnerModeUnlocked
                ? Color.FromArgb("#A5D6A7")
                : Color.FromArgb("#ECEFF1");
        }

        if (_lockOwnerModeButton != null)
            _lockOwnerModeButton.IsVisible = _isOwnerModeUnlocked;

        RefreshButtonsLayout();
    }

    private void OnOwnerModeStateChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () => await RefreshOwnerModeUiAsync());
    }

    private async Task HandleOwnerModeVersionTapAsync()
    {
        var now = DateTime.UtcNow;
        if (_ownerModeTapCount == 0 || (now - _ownerModeFirstTapAt).TotalSeconds > 3)
        {
            _ownerModeFirstTapAt = now;
            _ownerModeTapCount = 1;
            return;
        }

        _ownerModeTapCount++;
        if (_ownerModeTapCount < 7)
            return;

        _ownerModeTapCount = 0;
        _ownerModeFirstTapAt = DateTime.MinValue;
        await ShowOwnerModeUnlockPromptAsync();
    }

    private async Task ShowOwnerModeUnlockPromptAsync()
    {
        var passphrase = await OwnerPassphrasePromptPage.ShowAsync(Navigation);
        if (passphrase == null)
            return;

        if (await _ownerMode.TryUnlockAsync(passphrase))
        {
            await DisplayAlert("Owner Mode", "Owner Mode unlocked.", "OK");
            await RefreshOwnerModeUiAsync();
        }
        else
        {
            await DisplayAlert("Owner Mode", "Incorrect passphrase.", "OK");
        }
    }

    private async Task LockOwnerModeAsync()
    {
        await _ownerMode.LockAsync();
        await RefreshOwnerModeUiAsync();
    }

    private string GetQuickAccessPreferencesKey() => $"home_quick_access_{_auth.CurrentUsername}";

    private HashSet<string> GetQuickAccessButtons()
    {
        string stored = Preferences.Default.Get(GetQuickAccessPreferencesKey(), "");
        return stored
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private void ToggleQuickAccess(string buttonId)
    {
        var quickAccess = GetQuickAccessButtons();
        if (!quickAccess.Add(buttonId))
        {
            quickAccess.Remove(buttonId);
        }

        string stored = string.Join(",", quickAccess.OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
        Preferences.Default.Set(GetQuickAccessPreferencesKey(), stored);
        RefreshButtonsLayout();
    }

    private void RefreshButtonsLayout()
    {
        if (_buttonSectionsStack == null || _homeNavButtons.Count == 0)
        {
            return;
        }

        _buttonSectionsStack.Children.Clear();
        var quickAccess = GetQuickAccessButtons();
        var sortedButtons = _homeNavButtons
            .OrderBy(button => button.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _buttonSectionsStack.Children.Add(CreateButtonSectionHeader("Quick Access"));

        var quickButtons = sortedButtons
            .Where(button => quickAccess.Contains(button.Id))
            .ToList();

        if (quickButtons.Count == 0)
        {
            _buttonSectionsStack.Children.Add(new Label
            {
                Text = "Use the three-dot menu (⋮) on any button below to add it here.",
                FontSize = 13,
                TextColor = Color.FromArgb("#E8EAF6"),
                Margin = new Thickness(0, -4, 0, 8)
            });
        }
        else
        {
            foreach (var button in quickButtons)
            {
                _buttonSectionsStack.Children.Add(CreateNavButtonWrapper(button, quickAccess.Contains(button.Id)));
            }
        }

        _buttonSectionsStack.Children.Add(CreateButtonSectionHeader("All Buttons"));

        foreach (var range in new[] { "A-E", "F-J", "K-O", "P-T", "U-Z" })
        {
            var groupButtons = sortedButtons
                .Where(button => GetButtonRange(button.Id) == range)
                .ToList();

            if (groupButtons.Count == 0)
            {
                continue;
            }

            _buttonSectionsStack.Children.Add(CreateButtonRangeHeader(range));
            foreach (var button in groupButtons)
            {
                _buttonSectionsStack.Children.Add(CreateNavButtonWrapper(button, quickAccess.Contains(button.Id)));
            }
        }
    }

    private Label CreateButtonSectionHeader(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            Margin = new Thickness(0, 10, 0, 2)
        };
    }

    private Label CreateButtonRangeHeader(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#E8EAF6"),
            Margin = new Thickness(2, 8, 0, 0)
        };
    }

    private View CreateNavButtonWrapper(HomeNavButton navButton, bool isQuickAccess)
    {
        var source = navButton.SourceButton;
        var button = CreateButton(
            source.Text,
            source.BackgroundColor,
            source.TextColor,
            (int)source.HeightRequest,
            source.FontAttributes.HasFlag(FontAttributes.Bold));
        button.Clicked += GetHomeNavClickedHandler(navButton.Id);

        var menuButton = new Button
        {
            Text = "⋮",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            WidthRequest = 34,
            HeightRequest = 34,
            Padding = new Thickness(0),
            CornerRadius = 17,
            BackgroundColor = Color.FromRgba(255, 255, 255, 210),
            TextColor = Color.FromArgb("#333333"),
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 7, 7, 0)
        };
        menuButton.Clicked += async (_, _) => await ShowQuickAccessMenuAsync(navButton.Id, isQuickAccess);

        var wrapper = new Grid();
        wrapper.Children.Add(button);
        wrapper.Children.Add(menuButton);
        return wrapper;
    }

    private async Task ShowQuickAccessMenuAsync(string buttonId, bool isQuickAccess)
    {
        string option = isQuickAccess ? "Remove from Quick Access" : "Add to Quick Access";
        string action = await DisplayActionSheet(buttonId, "Cancel", null, option);
        if (action == option)
        {
            ToggleQuickAccess(buttonId);
        }
    }

    private string GetButtonRange(string buttonId)
    {
        char first = buttonId.FirstOrDefault(c => char.IsLetter(c));
        if (first == default)
        {
            return "U-Z";
        }

        first = char.ToUpperInvariant(first);
        if (first <= 'E') return "A-E";
        if (first <= 'J') return "F-J";
        if (first <= 'O') return "K-O";
        if (first <= 'T') return "P-T";
        return "U-Z";
    }

    private EventHandler GetHomeNavClickedHandler(string buttonId)
    {
        return buttonId switch
        {
            "Allowances" => OnAllowancesClicked,
            "Audio Library" => OnAudioLibraryClicked,
            "Calendar" => OnCalendarClicked,
            "Charts" => OnChartsClicked,
            "Commands Casino" => OnCommandsCasinoClicked,
            "Conversation Practice" => OnConversationPracticeClicked,
            "Custom Games" => OnCustomGamesClicked,
            "Countdowns" => OnCountdownsClicked,
            "Databases" => OnDatabasesClicked,
            "Deadlines" => OnDeadlinesClicked,
            "Designations" => OnDesignationsClicked,
            "Dragons" => OnDragonsClicked,
            "Games" => OnGamesClicked,
            "Habits" => OnNewHabitsClicked,
            "Ideas" => OnIdeasClicked,
            "Image Edit" => OnImageEditClicked,
            "Image Generation" => OnImageGenerationClicked,
            "Image Production" => OnImageProductionClicked,
            "Learning" => OnLearningClicked,
            "Lists" => OnListsClicked,
            "Money Management" => OnMoneyManagementClicked,
            "Music Production" => OnMusicProductionClicked,
            "Prompts" => OnPromptsClicked,
            "Settings" => OnSettingsClicked,
            "Story Production" => OnStoryProductionClicked,
            "Streaks" => OnStreaksClicked,
            "SubActivities" => OnSubActivitiesClicked,
            "Tasks" => OnTasksClicked,
            "To Be Tested" => OnToBeTestedClicked,
            "Website Builder" => OnWebsiteBuilderClicked,
            "Zero Counts" => OnZeroCountsClicked,
            _ => (_, _) => { }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        System.Diagnostics.Debug.WriteLine($"[HomePage] OnAppearing fired, sequence-running={_homePromptSequenceRunning}, run-id-before={_homePromptRunId}, is-visible-before={_isHomeVisible}");
        _isHomeVisible = true;
        if (_homePromptSequenceRunning)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] OnAppearing returning because prompt sequence is already running, run-id={_homePromptRunId}");
            return;
        }

        _homePromptSequenceRunning = true;
        int promptRunId = ++_homePromptRunId;
        System.Diagnostics.Debug.WriteLine($"[HomePage] OnAppearing started prompt sequence, promptRunId={promptRunId}");

        try
        {
            // Auto-backup on login
            await _backup.AutoBackupAsync("login");
            if (!IsHomePromptRunActive(promptRunId)) return;

            _loadingOverlay.IsVisible = false;

            _lblWelcome.Text = $"Welcome, {_auth.CurrentUsername}";
            await LoadDataAsync();
            if (!IsHomePromptRunActive(promptRunId)) return;
            await ShowHomePromptManagerIfNeededAsync(promptRunId);
            if (!IsHomePromptRunActive(promptRunId)) return;

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
                        await NavigateToGameWithCatchUpAsync("diet");
                    }
                }
            }

            // Check if unencrypted legacy database file still exists
            await CheckLegacyUnencryptedDbAsync();

            _ = CheckQueuedOperationsPromptAsync();
        }
        finally
        {
            _homePromptSequenceRunning = false;
            System.Diagnostics.Debug.WriteLine($"[HomePage] OnAppearing prompt sequence finished/reset, promptRunId={promptRunId}, current-run-id={_homePromptRunId}, is-visible={_isHomeVisible}");
        }
    }

    protected override void OnDisappearing()
    {
        System.Diagnostics.Debug.WriteLine($"[HomePage] OnDisappearing fired, sequence-running={_homePromptSequenceRunning}, run-id={_homePromptRunId}, is-visible-before={_isHomeVisible}");
        _isHomeVisible = false;
        base.OnDisappearing();
    }

    private bool IsHomePromptRunActive(int promptRunId)
    {
        return _isHomeVisible && promptRunId == _homePromptRunId;
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

    private async Task ShowHomePromptManagerIfNeededAsync(int promptRunId)
    {
        while (IsHomePromptRunActive(promptRunId))
        {
            var pendingPrompts = await GetPendingHomePromptDefinitionsAsync(promptRunId);
            if (pendingPrompts.Count == 0)
                return;

            var result = await HomePromptManagerPage.ShowAsync(Navigation, pendingPrompts);
            if (result == null)
                return;

            if (!IsHomePromptRunActive(promptRunId))
                return;

            if (result.Action == HomePromptManagerResult.AddressAll)
            {
                foreach (var queuedPrompt in pendingPrompts)
                {
                    if (!IsHomePromptRunActive(promptRunId))
                        return;

                    var currentPrompt = (await GetPendingHomePromptDefinitionsAsync(promptRunId))
                        .FirstOrDefault(p => p.Id == queuedPrompt.Id);
                    if (currentPrompt == null)
                        continue;

                    await currentPrompt.AddressAsync();
                }

                continue;
            }

            if (result.Action == HomePromptManagerResult.Address && !string.IsNullOrWhiteSpace(result.PromptId))
            {
                var currentPrompt = (await GetPendingHomePromptDefinitionsAsync(promptRunId))
                    .FirstOrDefault(p => p.Id == result.PromptId);
                if (currentPrompt != null)
                {
                    await currentPrompt.AddressAsync();
                }
            }
        }
    }

    private async Task<List<HomePromptDefinition>> GetPendingHomePromptDefinitionsAsync(int promptRunId)
    {
        var definitions = CreateHomePromptDefinitions(promptRunId);
        var pending = new List<HomePromptDefinition>();

        foreach (var definition in definitions)
        {
            try
            {
                if (await definition.IsPendingAsync())
                {
                    pending.Add(definition);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking prompt '{definition.Id}': {ex.Message}");
            }
        }

        return pending;
    }

    private List<HomePromptDefinition> CreateHomePromptDefinitions(int promptRunId)
    {
        return new List<HomePromptDefinition>
        {
            new(
                "daily_login",
                "Daily Login Prompts",
                "Review your configured daily login reminders.",
                IsDailyLoginPromptPendingAsync,
                () => ShowDailyLoginPromptsIfNeededAsync(promptRunId),
                SkipDailyLoginPromptsTodayAsync),
            new(
                "subactivity",
                "Sub-Activities Check-In",
                "Check off any sub-activity steps you completed today.",
                IsSubActivityDailyPromptPendingAsync,
                () => CheckSubActivityDailyPromptAsync(promptRunId),
                SkipSubActivityDailyPromptTodayAsync),
            new(
                "allowance",
                "Allowance Check-In",
                "Review daily allowance items and mark completed work.",
                IsAllowanceDailyPromptPendingAsync,
                () => CheckAllowanceDailyPromptAsync(promptRunId),
                SkipAllowanceDailyPromptTodayAsync),
            new(
                "deadline",
                "Deadline Check-In",
                "Resolve overdue deadlines that need a completion decision.",
                IsDeadlineCheckInPendingAsync,
                () => CheckDeadlineCheckInAsync(promptRunId),
                SkipDeadlineCheckInTodayAsync),
            new(
                "expired",
                "Expired Activities",
                "Review activities whose end date has passed.",
                IsExpiredActivitiesPromptPendingAsync,
                async () =>
                {
                    await CheckExpiredActivitiesAsync(promptRunId);
                    await SkipExpiredActivitiesPromptTodayAsync();
                },
                SkipExpiredActivitiesPromptTodayAsync),
            new(
                "weekly_commitments",
                "Weekly Commitments",
                "Designate enough focus tasks before the week resets.",
                IsWeeklyCommitmentsPromptPendingAsync,
                () => CheckWeeklyCommitmentsAsync(promptRunId),
                SkipWeeklyCommitmentsPromptTodayAsync),
            new(
                "daily_habit_allowance",
                "Daily Habit Slots",
                "Fill required daily habit slots before the week resets.",
                IsDailyHabitAllowancePromptPendingAsync,
                () => CheckDailyHabitAllowanceAsync(promptRunId),
                SkipDailyHabitAllowancePromptTodayAsync),
            new(
                "weekly_habit_allowance",
                "Weekly Habit Slots",
                "Fill required weekly habit slots for the month.",
                IsWeeklyHabitAllowancePromptPendingAsync,
                () => CheckWeeklyHabitAllowanceAsync(promptRunId),
                SkipWeeklyHabitAllowancePromptThisMonthAsync),
            new(
                "monthly_habit_allowance",
                "Monthly Habit Slots",
                "Fill required monthly habit slots for the month.",
                IsMonthlyHabitAllowancePromptPendingAsync,
                () => CheckMonthlyHabitAllowanceAsync(promptRunId),
                SkipMonthlyHabitAllowancePromptThisMonthAsync)
        };
    }

    private async Task<bool> IsDailyLoginPromptPendingAsync()
    {
        string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string shownKey = $"daily_login_prompts_shown_{_auth.CurrentUsername}";
        if (await ReadSecureStorageAsync(shownKey) == today)
            return false;

        var prompts = await _dailyLoginPrompts.GetPromptsAsync(_auth.CurrentUsername, activeOnly: true);
        return prompts.Count > 0;
    }

    private async Task<bool> IsSubActivityDailyPromptPendingAsync()
    {
        if (_db.IsReadOnly)
            return false;

        string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (await ReadSecureStorageAsync($"subactivity_daily_prompt_{_auth.CurrentUsername}") == today)
            return false;

        var processes = await _subActivityService.GetDailyPromptProcessesAsync(_auth.CurrentUsername);
        return processes.Count > 0;
    }

    private async Task<bool> IsAllowanceDailyPromptPendingAsync()
    {
        if (_db.IsReadOnly)
            return false;

        string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (await ReadSecureStorageAsync($"allowance_daily_prompt_{_auth.CurrentUsername}") == today)
            return false;

        var allowances = await _allowanceService.GetDailyPromptAllowancesAsync(_auth.CurrentUsername);
        return allowances.Count > 0;
    }

    private async Task<bool> IsDeadlineCheckInPendingAsync()
    {
        if (_db.IsReadOnly)
            return false;

        string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (await ReadSecureStorageAsync($"deadlines_checkin_{_auth.CurrentUsername}") == today)
            return false;

        var overdue = await _deadlineService.GetOverdueActiveDeadlinesAsync(_auth.CurrentUsername);
        return overdue.Count > 0;
    }

    private async Task<bool> IsExpiredActivitiesPromptPendingAsync()
    {
        string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (await ReadSecureStorageAsync($"expired_activities_prompt_{_auth.CurrentUsername}") == today)
            return false;

        var expired = await _activities.GetExpiredActivitiesAsync(_auth.CurrentUsername);
        return expired.Count > 0;
    }

    private async Task<bool> IsDailyHabitAllowancePromptPendingAsync()
    {
        if (DateTime.Today.DayOfWeek != DayOfWeek.Saturday)
            return false;

        var habitService = Application.Current?.Handler?.MauiContext?.Services
            .GetService<NewHabitService>();
        if (habitService == null)
            return false;

        var (needsAlert, _, _) = await habitService.CheckDailyHabitAlertAsync(_auth.CurrentUsername);
        if (!needsAlert)
            return false;

        string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return await ReadSecureStorageAsync($"daily_habit_allowance_prompt_{_auth.CurrentUsername}") != today;
    }

    private async Task<bool> IsWeeklyHabitAllowancePromptPendingAsync()
    {
        if (DateTime.Today.Day != 1)
            return false;

        var habitService = Application.Current?.Handler?.MauiContext?.Services
            .GetService<NewHabitService>();
        if (habitService == null)
            return false;

        var (needsAlert, _, _) = await habitService.CheckWeeklyHabitAlertAsync(_auth.CurrentUsername);
        if (!needsAlert)
            return false;

        string thisMonth = DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        return await ReadSecureStorageAsync($"weekly_habit_allowance_prompt_{_auth.CurrentUsername}") != thisMonth;
    }

    private async Task<bool> IsMonthlyHabitAllowancePromptPendingAsync()
    {
        if (DateTime.Today.Day != 1)
            return false;

        var habitService = Application.Current?.Handler?.MauiContext?.Services
            .GetService<NewHabitService>();
        if (habitService == null)
            return false;

        var (needsAlert, _, _) = await habitService.CheckMonthlyHabitAlertAsync(_auth.CurrentUsername);
        if (!needsAlert)
            return false;

        string thisMonth = DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        return await ReadSecureStorageAsync($"monthly_habit_allowance_prompt_{_auth.CurrentUsername}") != thisMonth;
    }

    private async Task<bool> IsWeeklyCommitmentsPromptPendingAsync()
    {
        if (DateTime.Today.DayOfWeek != DayOfWeek.Saturday)
            return false;

        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (challenge == null)
            return false;

        var commitments = await _challengeService.GetCurrentWeekCommitmentsAsync(challenge.Id);
        if (commitments.Count >= challenge.CurrentAllowance)
            return false;

        string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return await ReadSecureStorageAsync($"weekly_commitment_prompt_{_auth.CurrentUsername}") != today;
    }

    private Task SkipDailyLoginPromptsTodayAsync() =>
        WriteSecureStorageAsync($"daily_login_prompts_shown_{_auth.CurrentUsername}", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private Task SkipSubActivityDailyPromptTodayAsync() =>
        WriteSecureStorageAsync($"subactivity_daily_prompt_{_auth.CurrentUsername}", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private Task SkipAllowanceDailyPromptTodayAsync() =>
        WriteSecureStorageAsync($"allowance_daily_prompt_{_auth.CurrentUsername}", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private Task SkipDeadlineCheckInTodayAsync() =>
        WriteSecureStorageAsync($"deadlines_checkin_{_auth.CurrentUsername}", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private Task SkipExpiredActivitiesPromptTodayAsync() =>
        WriteSecureStorageAsync($"expired_activities_prompt_{_auth.CurrentUsername}", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private Task SkipDailyHabitAllowancePromptTodayAsync() =>
        WriteSecureStorageAsync($"daily_habit_allowance_prompt_{_auth.CurrentUsername}", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private Task SkipWeeklyCommitmentsPromptTodayAsync() =>
        WriteSecureStorageAsync($"weekly_commitment_prompt_{_auth.CurrentUsername}", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private Task SkipWeeklyHabitAllowancePromptThisMonthAsync() =>
        WriteSecureStorageAsync($"weekly_habit_allowance_prompt_{_auth.CurrentUsername}", DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture));

    private Task SkipMonthlyHabitAllowancePromptThisMonthAsync() =>
        WriteSecureStorageAsync($"monthly_habit_allowance_prompt_{_auth.CurrentUsername}", DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture));

    private static async Task<string?> ReadSecureStorageAsync(string key)
    {
        try
        {
            return await SecureStorage.GetAsync(key);
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteSecureStorageAsync(string key, string value)
    {
        try
        {
            await SecureStorage.SetAsync(key, value);
        }
        catch { }
    }

    private async Task ShowDailyLoginPromptsIfNeededAsync(int promptRunId)
    {
        System.Diagnostics.Debug.WriteLine($"[DailyLogin] Entry, promptRunId={promptRunId}, current-run-id={_homePromptRunId}, is-visible={_isHomeVisible}, sequence-running={_homePromptSequenceRunning}");
        string today = DateTime.Today.ToString("yyyy-MM-dd");
        string shownKey = $"daily_login_prompts_shown_{_auth.CurrentUsername}";
        System.Diagnostics.Debug.WriteLine($"[DailyLogin] Today={today}, shownKey={shownKey}");
        string? lastShown = null;
        try
        {
            lastShown = await SecureStorage.GetAsync(shownKey);
            System.Diagnostics.Debug.WriteLine($"[DailyLogin] SecureStorage read succeeded, stored marker={(lastShown ?? "null")}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DailyLogin] SecureStorage read failed: {ex.GetType().Name}: {ex.Message}");
        }

        if (lastShown == today)
        {
            System.Diagnostics.Debug.WriteLine("[DailyLogin] Stored marker matches today; skipping prompts.");
            return;
        }

        var prompts = await _dailyLoginPrompts.GetPromptsAsync(_auth.CurrentUsername, activeOnly: true);
        System.Diagnostics.Debug.WriteLine($"[DailyLogin] Retrieved {prompts.Count} active prompts.");
        if (prompts.Count == 0)
        {
            bool active = IsHomePromptRunActive(promptRunId);
            System.Diagnostics.Debug.WriteLine($"[DailyLogin] No prompts. Active-run before marker write={active}, promptRunId={promptRunId}, current-run-id={_homePromptRunId}, is-visible={_isHomeVisible}");
            if (!active) return;
            try
            {
                System.Diagnostics.Debug.WriteLine($"[DailyLogin] Writing marker for no-prompt case: {shownKey}={today}");
                await SecureStorage.SetAsync(shownKey, today);
                System.Diagnostics.Debug.WriteLine("[DailyLogin] SecureStorage marker write succeeded for no-prompt case.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DailyLogin] SecureStorage marker write failed for no-prompt case: {ex.GetType().Name}: {ex.Message}");
            }
            return;
        }

        for (int i = 0; i < prompts.Count; i++)
        {
            bool activeBeforePrompt = IsHomePromptRunActive(promptRunId);
            System.Diagnostics.Debug.WriteLine($"[DailyLogin] Prompt {i + 1}/{prompts.Count} about to display, id={prompts[i].Id}, sort={prompts[i].SortOrder}, active-before={activeBeforePrompt}, promptRunId={promptRunId}, current-run-id={_homePromptRunId}, is-visible={_isHomeVisible}");
            if (!activeBeforePrompt) return;
            await DailyLoginPromptDisplayPage.ShowAsync(Navigation, prompts[i], i + 1, prompts.Count);
            bool activeAfterPrompt = IsHomePromptRunActive(promptRunId);
            System.Diagnostics.Debug.WriteLine($"[DailyLogin] Prompt {i + 1}/{prompts.Count} returned, active-after={activeAfterPrompt}, promptRunId={promptRunId}, current-run-id={_homePromptRunId}, is-visible={_isHomeVisible}, sequence-running={_homePromptSequenceRunning}");
            if (!activeAfterPrompt) return;
        }

        System.Diagnostics.Debug.WriteLine($"[DailyLogin] All prompts returned. Writing marker: {shownKey}={today}");
        try
        {
            await SecureStorage.SetAsync(shownKey, today);
            System.Diagnostics.Debug.WriteLine("[DailyLogin] SecureStorage marker write succeeded after prompts.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DailyLogin] SecureStorage marker write failed after prompts: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private async Task CheckSubActivityDailyPromptAsync(int promptRunId)
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

            while (IsHomePromptRunActive(promptRunId))
            {
                var processes = await _subActivityService.GetDailyPromptProcessesAsync(_auth.CurrentUsername);
                if (processes.Count == 0) break;

                var currentProcess = await _subActivityService.GetByIdAsync(processes[0].Id);
                if (currentProcess == null) continue;

                if (!IsHomePromptRunActive(promptRunId)) return;
                await SubActivityDailyPromptPage.ShowAsync(Navigation, _subActivityService, currentProcess);
                if (!IsHomePromptRunActive(promptRunId)) return;

                var refreshed = await _subActivityService.GetByIdAsync(currentProcess.Id);
                if (refreshed?.LastSubmissionDate?.Date != DateTime.Today)
                    break;
            }

            var remaining = await _subActivityService.GetDailyPromptProcessesAsync(_auth.CurrentUsername);
            if (remaining.Count == 0)
            {
                try { await SecureStorage.SetAsync(lastPromptKey, today); } catch { }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking sub-activity daily prompts: {ex.Message}");
        }
        finally
        {
            _subActivityDailyPromptChecked = false;
        }
    }

    private async Task CheckDeadlineCheckInAsync(int promptRunId)
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
                if (!IsHomePromptRunActive(promptRunId)) return;
                bool completed = await DisplayAlert(
                    "Deadline Check-In",
                    $"Did you complete \"{deadline.Title}\"? ({DeadlineService.BucketName(deadline.Bucket)})",
                    "Yes",
                    "No");
                if (!IsHomePromptRunActive(promptRunId)) return;

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
        finally
        {
            _deadlineCheckInChecked = false;
        }
    }

    private async Task CheckAllowanceDailyPromptAsync(int promptRunId)
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
                if (!IsHomePromptRunActive(promptRunId)) return;
                bool completed = await DisplayAlert(
                    "Daily Allowance Check-In",
                    $"Did you complete \"{allowance.Title}\" today?",
                    "Yes",
                    "No");
                if (!IsHomePromptRunActive(promptRunId)) return;

                if (completed)
                    await _allowanceService.IncrementAsync(allowance.Id);
            }

            try { await SecureStorage.SetAsync(lastPromptKey, today); } catch { }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking allowance daily prompts: {ex.Message}");
        }
        finally
        {
            _allowanceDailyPromptChecked = false;
        }
    }

    private async Task CheckDailyHabitAllowanceAsync(int promptRunId)
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

            if (!IsHomePromptRunActive(promptRunId)) return;
            bool goToHabits = await DisplayAlert(
                "⚠️ Daily Habit Slots Unfilled",
                $"You have {active}/{required} daily habit slots filled this week.\n\n" +
                $"Today is your last chance! If you don't fill all {required} slot(s) by end of day, " +
                $"you'll lose 1 allowance (currently {required}).\n\n" +
                "Would you like to add daily habits now?",
                "Yes, Go to Daily Habits",
                "Later");
            if (!IsHomePromptRunActive(promptRunId)) return;

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
        finally
        {
            _dailyHabitAllowancePromptChecked = false;
        }
    }

    private async Task CheckWeeklyHabitAllowanceAsync(int promptRunId)
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

            if (!IsHomePromptRunActive(promptRunId)) return;
            bool goToHabits = await DisplayAlert(
                "Weekly Habit Slots Unfilled",
                $"You have {active}/{required} weekly habit slots filled this month. Go to Weekly Habits to designate which weekly habits you'll pursue?",
                "Yes, Go to Weekly Habits",
                "Later");
            if (!IsHomePromptRunActive(promptRunId)) return;

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

    private async Task CheckMonthlyHabitAllowanceAsync(int promptRunId)
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

            if (!IsHomePromptRunActive(promptRunId)) return;
            bool goToHabits = await DisplayAlert(
                "Monthly Habit Slots Unfilled",
                $"You have {active}/{required} monthly habit slots filled this month. Go to Monthly Habits to designate which monthly habits you'll pursue?",
                "Yes, Go to Monthly Habits",
                "Later");
            if (!IsHomePromptRunActive(promptRunId)) return;

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

    private async Task CheckWeeklyCommitmentsAsync(int promptRunId)
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

            if (!IsHomePromptRunActive(promptRunId)) return;
            bool goToTasks = await DisplayAlert(
                "⚠️ Last Day to Designate Tasks",
                $"You have designated {committed}/{required} tasks for your focus '{challenge.FocusCategory}' this week.\n\n" +
                $"Today is your last chance! If you don't designate {required} task(s) by end of day, " +
                $"you'll lose allowance (currently {challenge.CurrentAllowance}).\n\n" +
                "Would you like to designate tasks now?",
                "Yes, Go to Tasks",
                "Later");
            if (!IsHomePromptRunActive(promptRunId)) return;

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
        finally
        {
            _weeklyCommitmentsPromptChecked = false;
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

        RefreshButtonsLayout();
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

    private async Task CheckExpiredActivitiesAsync(int promptRunId)
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
                if (!IsHomePromptRunActive(promptRunId)) return;
                bool handle = await DisplayAlert(
                    "Expired Activities",
                    $"You have {expired.Count} expired activity(ies). Would you like to review them?",
                    "Yes",
                    "Later");
                if (!IsHomePromptRunActive(promptRunId)) return;

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
        finally
        {
            _expiredActivitiesPromptChecked = false;
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

        if (await ShowWebsiteBuilderDailyInterruptAsync())
            return;

        await Shell.Current.GoToAsync("gameslist");
    }

    private async Task<bool> ShowWebsiteBuilderDailyInterruptAsync()
    {
        if (!await GetWebsiteBuilderInterruptEnabledAsync())
            return false;

        string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string tokenKey = GetWebsiteBuilderInterruptShownKey(today);
        string? token = null;
        try { token = await SecureStorage.GetAsync(tokenKey); } catch { }
        if (!string.IsNullOrWhiteSpace(token))
            return false;

        var game = await GetWebsiteBuildingGameAsync();
        if (game == null)
            return false;

        var activities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, game.GameId);
        var activity = activities.FirstOrDefault(a =>
            a.Name.Equals("Daily Website Task till 1000", StringComparison.OrdinalIgnoreCase));

        if (activity == null || IsActivityCompletedToday(activity))
            return false;

        bool goNow = await DisplayAlert(
            "Daily Website Task",
            "Your daily website task isn't done yet. Go to Website Builder first?",
            "Go now",
            "Later");

        try { await SecureStorage.SetAsync(tokenKey, "1"); } catch { }

        if (!goNow)
            return false;

        var page = new WebsiteBuilderPage(_auth, _websiteProjects, _websiteIdeas, _games);
        await Navigation.PushAsync(page);
        return true;
    }

    private async Task<Game?> GetWebsiteBuildingGameAsync()
    {
        var games = await _games.GetGamesAsync(_auth.CurrentUsername);
        return games.FirstOrDefault(g => g.DisplayName.Equals("Website Building", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsActivityCompletedToday(Activity activity)
    {
        var today = DateTime.Today;
        return (activity.LastHabitDate.HasValue && activity.LastHabitDate.Value.Date == today)
            || (activity.LastDisplayDayUsed.HasValue && activity.LastDisplayDayUsed.Value.Date == today);
    }

    private async Task NavigateToGameWithCatchUpAsync(string gameId)
    {
        var game = await _games.GetGameAsync(_auth.CurrentUsername, gameId);
        string encodedGameId = Uri.EscapeDataString(gameId);

        if (game != null && GameService.ShouldShowCatchUp(game, DateTime.Today, out _))
        {
            bool hasEligibleRows = await GameCatchUpPage.HasEligibleCatchUpActivitiesAsync(
                _auth.CurrentUsername,
                game,
                _activities,
                _exp);

            if (hasEligibleRows)
            {
                await Shell.Current.GoToAsync($"gamecatchup?gameId={encodedGameId}");
                return;
            }
        }

        if (game != null)
            await _games.UpdateLastVisitedAtAsync(_auth.CurrentUsername, game.GameId, DateTime.Now);

        await Shell.Current.GoToAsync($"activitygrid?gameId={encodedGameId}");
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

    private async void OnCustomGamesClicked(object? sender, EventArgs e)
    {
        var page = new CustomGamesListPage(_auth, _customGames);
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

    private async void OnImageGenerationClicked(object? sender, EventArgs e)
    {
        if (!await _ownerMode.IsUnlockedAsync())
        {
            await DisplayAlert("Owner Mode Locked", "This feature is available only when Owner Mode is unlocked.", "OK");
            return;
        }

        await Navigation.PushAsync(new ImageGenerationHubPage(_openAIKeyService, _openAIImageService, _ownerMode));
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

    private async void OnToBeTestedClicked(object? sender, EventArgs e)
    {
        var page = new ToBeTestedPage(_auth, _activities, _games, _exp);
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

    private async void OnWebsiteBuilderClicked(object? sender, EventArgs e)
    {
        var page = new WebsiteBuilderPage(_auth, _websiteProjects, _websiteIdeas, _games);
        await Navigation.PushAsync(page);
    }

    private async void OnZeroCountsClicked(object? sender, EventArgs e)
    {
        var page = new ZeroCountsPage(_auth, _activities, _games);
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

    private async Task<bool> GetWebsiteBuilderInterruptEnabledAsync()
    {
        string? value = null;
        try { value = await SecureStorage.GetAsync(GetWebsiteBuilderInterruptEnabledKey()); } catch { }
        return value != "0";
    }

    private string GetWebsiteBuilderInterruptEnabledKey() => $"website_builder_interrupt_enabled_{_auth.CurrentUsername}";

    private string GetWebsiteBuilderInterruptShownKey(string date) => $"website_builder_interrupt_shown_{_auth.CurrentUsername}_{date}";

    private async Task LogoutAsync()
    {
        await _backup.AutoBackupAsync("logout");

        _auth.Logout();

        // RESET STACK — ANDROID REQUIRED
        await Shell.Current.GoToAsync("//login");
    }

    private sealed class OwnerPassphrasePromptPage : ContentPage
    {
        private readonly TaskCompletionSource<string?> _completion = new();
        private readonly Entry _entry;
        private bool _isClosing;

        private OwnerPassphrasePromptPage()
        {
            Title = "Owner Mode";
            BackgroundColor = Color.FromRgba(0, 0, 0, 0.45);

            _entry = new Entry
            {
                Placeholder = "Passphrase",
                IsPassword = true,
                BackgroundColor = Colors.White,
                TextColor = Color.FromArgb("#222"),
                PlaceholderColor = Color.FromArgb("#777")
            };

            var saveButton = new Button
            {
                Text = "Unlock",
                BackgroundColor = Color.FromArgb("#5B63EE"),
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 44
            };
            saveButton.Clicked += async (_, _) => await CloseAsync(_entry.Text ?? string.Empty);

            var cancelButton = new Button
            {
                Text = "Cancel",
                BackgroundColor = Color.FromArgb("#ECEFF1"),
                TextColor = Color.FromArgb("#333"),
                CornerRadius = 8,
                HeightRequest = 44
            };
            cancelButton.Clicked += async (_, _) => await CloseAsync(null);

            var buttons = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 10
            };
            buttons.Add(cancelButton, 0, 0);
            buttons.Add(saveButton, 1, 0);

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
                            Spacing = 14,
                            Children =
                            {
                                new Label
                                {
                                    Text = "Enter Owner Passphrase",
                                    FontSize = 20,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Color.FromArgb("#222")
                                },
                                _entry,
                                buttons
                            }
                        }
                    }
                }
            };
        }

        protected override bool OnBackButtonPressed()
        {
            _ = CloseAsync(null);
            return true;
        }

        private async Task CloseAsync(string? result)
        {
            if (_isClosing)
                return;

            _isClosing = true;
            await Navigation.PopModalAsync();
            _completion.TrySetResult(result);
        }

        public static async Task<string?> ShowAsync(INavigation navigation)
        {
            var page = new OwnerPassphrasePromptPage();
            await navigation.PushModalAsync(page);
            return await page._completion.Task;
        }
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
