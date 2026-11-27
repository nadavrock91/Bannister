using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Tasks page with compact data grid view and detail panel.
/// Includes weekly challenge widget with full functionality.
/// </summary>
public class TasksPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly TaskService _tasks;
    private readonly WeeklyChallengeService _challengeService;
    private readonly SequenceTaskService _sequenceTaskService;
    private readonly IdeasService? _ideasService;
    private readonly IdeaLoggerService? _ideaLogger;
    private readonly DatabaseService? _db;
    private readonly PrivacyModeService _privacyMode;
    
    // UI
    private Label _headerLabel;
    private Picker _categoryPicker;
    private Entry _searchEntry;
    private VerticalStackLayout _gridToolbarContainer;
    private VerticalStackLayout _gridContainer;
    private Frame _detailPanel;
    private Label _detailTitle;
    private Editor _detailNotes;
    private Label _detailMeta;
    private Button _showCompletedBtn;
    
    private Frame _challengeFrame;
    private Label _challengeFocusLabel;
    private Label _challengeProgressLabel;
    private Label _challengeStreakLabel;
    private Label _challengeAllowanceLabel;
    private VerticalStackLayout _allowanceChartContainer;
    private VerticalStackLayout _commitmentsList;
    private VerticalStackLayout _freeCommitmentsList;
    private VerticalStackLayout _topCandidatesList;
    private Button _addCommitmentBtn;
    private Button _addFreeCommitmentBtn;
    private Button _addFreeTopCandidateBtn;
    private Button _markFocusTopCandidateBtn;
    private Button _markFreeTopCandidateBtn;
    private Label _freeProgressLabel;
    private Button _ratioBtn;
    private Button _addTopCandidateBtn;
    private Button _consultLlmBtn;
    private Button _startChallengeBtn;
    
    // State
    private List<string> _categories = new();
    private string _selectedCategory = "All";
    private bool _showingCompleted = false;
    private bool _topCandidatesExpanded = false;
    private TaskItem? _selectedTask = null;
    private List<TaskItem> _currentTasks = new();
    private string _searchText = "";
    private bool _isLoading = false;
    private string _sortColumn = "CreatedAt";
    private bool _sortAscending = false;
    private DataGridView? _mainDataGrid;

    public TasksPage(
        AuthService auth,
        TaskService tasks,
        WeeklyChallengeService challengeService,
        SequenceTaskService sequenceTaskService,
        IdeasService? ideasService = null,
        DatabaseService? db = null,
        PrivacyModeService? privacyMode = null)
    {
        _auth = auth;
        _tasks = tasks;
        _challengeService = challengeService;
        _sequenceTaskService = sequenceTaskService;
        _ideasService = ideasService;
        _ideaLogger = Application.Current?.Handler?.MauiContext?.Services.GetService<IdeaLoggerService>();
        _db = db;
        _privacyMode = privacyMode
            ?? Application.Current?.Handler?.MauiContext?.Services
                .GetService<PrivacyModeService>()
            ?? throw new InvalidOperationException(
                "PrivacyModeService not available");
        
        Title = "Tasks";

        
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // One-time migration from old 0-3 inverted priority scale
        await _tasks.MigratePriorityValuesAsync(
            _auth.CurrentUsername);
        _isLoading = true;
        await LoadCategoriesAsync();
        _isLoading = false;
        await RefreshChallengeWidgetAsync();
        await RefreshTasksAsync();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout
        {
            Padding = 12,
            Spacing = 8
        };

        // Header row
        var headerRow = new HorizontalStackLayout { Spacing = 10 };

        headerRow.Children.Add(new Label
        {
            Text = "📋",
            FontSize = 20,
            VerticalOptions = LayoutOptions.Center
        });

        _headerLabel = (new Label
        {
            Text = "0 tasks",
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2);
        headerRow.Children.Add(_headerLabel);

        _categoryPicker = (new Picker
        {
            Title = "Category",
            WidthRequest = 120
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        _categoryPicker.SelectedIndexChanged += OnCategoryChanged;
        headerRow.Children.Add(_categoryPicker);

        _searchEntry = (new Entry
        {
            Placeholder = "🔍 Search...",
            WidthRequest = 140
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        _searchEntry.TextChanged += OnSearchChanged;
        headerRow.Children.Add(_searchEntry);

        var addBtn = (new Button
        {
            Text = "+ New",
            CornerRadius = 6,
            HeightRequest = 36,
            Padding = new Thickness(12, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        addBtn.Clicked += OnAddTaskClicked;
        headerRow.Children.Add(addBtn);

        _showCompletedBtn = (new Button
        {
            Text = "✓",
            CornerRadius = 6,
            WidthRequest = 36,
            HeightRequest = 36
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _showCompletedBtn.Clicked += OnToggleCompletedClicked;
        headerRow.Children.Add(_showCompletedBtn);

        mainStack.Children.Add(headerRow);

        BuildChallengeNavigation(mainStack);
        BuildChallengeWidget(mainStack);

        // Content area
        var contentGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(300))
            },
            ColumnSpacing = 12
        };

        // Data grid toolbar and horizontally scrollable grid.
        var gridArea = new VerticalStackLayout { Spacing = 4 };

        _gridToolbarContainer = new VerticalStackLayout
        {
            Padding = new Thickness(0, 0, 0, 4)
        };
        gridArea.Children.Add(_gridToolbarContainer);

        var gridScroll = new ScrollView { Orientation = ScrollOrientation.Horizontal };
        _gridContainer = new VerticalStackLayout { Spacing = 4 };
        gridScroll.Content = _gridContainer;
        gridArea.Children.Add(gridScroll);

        Grid.SetColumn(gridArea, 0);
        contentGrid.Children.Add(gridArea);

        // Detail panel
        _detailPanel = (new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            HasShadow = true,
            IsVisible = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.InfoText);

        var detailStack = new VerticalStackLayout { Spacing = 10 };

        var closeRow = new HorizontalStackLayout();
        closeRow.Children.Add((new Label
        {
            Text = "📄 Details",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.StartAndExpand,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText1));
        var closeBtn = (new Button
        {
            Text = "✕",
            BackgroundColor = Colors.Transparent,
            WidthRequest = 30,
            HeightRequest = 30,
            Padding = 0
        }).ThemeColor(Button.TextColorProperty, ThemeKey.CardText3);
        closeBtn.Clicked += (s, e) => { _detailPanel.IsVisible = false; _selectedTask = null; };
        closeRow.Children.Add(closeBtn);
        detailStack.Children.Add(closeRow);

        _detailTitle = (new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1);
        detailStack.Children.Add(_detailTitle);

        _detailMeta = (new Label
        {
            FontSize = 10,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3);
        detailStack.Children.Add(_detailMeta);

        _detailNotes = (new Editor
        {
            Placeholder = "Notes...",
            HeightRequest = 120,
            FontSize = 12
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        _detailNotes.Unfocused += OnDetailNotesSave;
        detailStack.Children.Add(_detailNotes);

        // Action buttons
        var actions = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        var btnEdit = MiniBtn("✏️", ThemeKey.InfoButtonBackground); btnEdit.Clicked += OnEditClicked;
        var btnComplete = MiniBtn("✅", ThemeKey.SuccessButtonBackground); btnComplete.Clicked += OnCompleteClicked;
        var btnPriority = MiniBtn("⚡", ThemeKey.WarningButtonBackground); btnPriority.Clicked += OnPriorityClicked;
        var btnMove = MiniBtn("📂", ThemeKey.HighlightVioletText); btnMove.Clicked += OnMoveClicked;
        var privateBtn = (new Button
        {
            Text = " Toggle Private",
            CornerRadius = 6,
            FontSize = 12,
            Padding = new Thickness(10, 6)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightViolet).ThemeColor(Button.TextColorProperty, ThemeKey.HighlightVioletText);
        privateBtn.Clicked += async (_, _) =>
        {
            if (_selectedTask == null) return;
            _selectedTask.IsPrivate = !_selectedTask.IsPrivate;
            await _tasks.UpdateTaskAsync(_selectedTask);
            ShowDetail(_selectedTask);
            await RefreshTasksAsync();
        };
        var btnDelete = MiniBtn("🗑️", ThemeKey.ErrorButtonBackground); btnDelete.Clicked += OnDeleteClicked;
        actions.Children.Add(btnEdit);
        actions.Children.Add(btnComplete);
        actions.Children.Add(btnPriority);
        actions.Children.Add(btnMove);
        actions.Children.Add(privateBtn);
        actions.Children.Add(btnDelete);
        detailStack.Children.Add(actions);

        _detailPanel.Content = detailStack;
        Grid.SetColumn(_detailPanel, 1);
        contentGrid.Children.Add(_detailPanel);

        mainStack.Children.Add(contentGrid);

        Content = new ScrollView { Content = mainStack };
    }

    private void BuildChallengeNavigation(VerticalStackLayout mainStack)
    {
        _allowanceChartContainer = new VerticalStackLayout
        {
            Spacing = 4,
            Margin = new Thickness(0, 0, 0, 8)
        };
        mainStack.Children.Add(_allowanceChartContainer);

        var challengeNavStack = new VerticalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(0, 8)
        };

        var focusBtn = (new Button
        {
            Text = "\U0001F3AF Focus Tasks",
            CornerRadius = 8,
            HeightRequest = 48,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        focusBtn.Clicked += async (_, _) =>
        {
            await Navigation.PushAsync(new ChallengeTasksPage(
                _auth, _tasks, _challengeService, _sequenceTaskService, _ideasService, true));
        };
        challengeNavStack.Children.Add(focusBtn);

        var freeBtn = (new Button
        {
            Text = "\U0001F30D Free Tasks",
            CornerRadius = 8,
            HeightRequest = 48,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ButtonPrimaryBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        freeBtn.Clicked += async (_, _) =>
        {
            await Navigation.PushAsync(new ChallengeTasksPage(
                _auth, _tasks, _challengeService, _sequenceTaskService, _ideasService, false));
        };
        challengeNavStack.Children.Add(freeBtn);

        var consultBtn = (new Button
        {
            Text = "\U0001F4AC Consult LLM",
            CornerRadius = 8,
            HeightRequest = 44,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightViolet).ThemeColor(Button.TextColorProperty, ThemeKey.HighlightVioletText);
        consultBtn.Clicked += async (_, _) =>
        {
            var database = _db ?? Handler?.MauiContext?.Services.GetService<DatabaseService>();
            if (database == null)
            {
                await DisplayAlert("Unavailable", "Database service is unavailable.", "OK");
                return;
            }

            var page = new FocusContextPage(_auth, _tasks, _challengeService, database);
            await Navigation.PushAsync(page);
        };
        challengeNavStack.Children.Add(consultBtn);

        var historyBtn = (new Button
        {
            Text = "\U0001F4C8 Focus History",
            CornerRadius = 8,
            HeightRequest = 44,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightViolet).ThemeColor(Button.TextColorProperty, ThemeKey.HighlightVioletText);
        historyBtn.Clicked += async (_, _) =>
        {
            var page = new FocusHistoryPage(_auth, _challengeService, _tasks);
            await Navigation.PushAsync(page);
        };
        challengeNavStack.Children.Add(historyBtn);

        var sequenceBtn = (new Button
        {
            Text = "Sequence Tasks",
            CornerRadius = 8,
            HeightRequest = 44,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningBackground).ThemeColor(Button.TextColorProperty, ThemeKey.WarningText);
        sequenceBtn.Clicked += async (_, _) =>
        {
            await Navigation.PushAsync(
                new SequenceTaskPage(
                    _sequenceTaskService,
                    _auth,
                    _tasks,
                    _ideasService));
        };
        challengeNavStack.Children.Add(sequenceBtn);

        mainStack.Children.Add(challengeNavStack);
    }

    private void BuildChallengeWidget(VerticalStackLayout mainStack)
    {
        var challengeRow = new VerticalStackLayout { Spacing = 8 };

        // Start challenge button
        _startChallengeBtn = (new Button
        {
            Text = "🎯 Start Weekly Challenge",
            CornerRadius = 6,
            HeightRequest = 36,
            IsVisible = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightViolet).ThemeColor(Button.TextColorProperty, ThemeKey.HighlightVioletText);
        _startChallengeBtn.Clicked += OnStartChallengeClicked;
        challengeRow.Children.Add(_startChallengeBtn);

        // Challenge frame (compact)
        _challengeFrame = (new Frame
        {
            Padding = 8,
            CornerRadius = 8,
            IsVisible = false,
            HorizontalOptions = LayoutOptions.Fill
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightViolet).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.HighlightVioletText);

        var challengeStack = new VerticalStackLayout { Spacing = 4 };

        var headerRow = new HorizontalStackLayout { Spacing = 8 };
        headerRow.Children.Add(new Label
        {
            Text = "🎯",
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center
        });
        _challengeFocusLabel = (new Label
        {
            Text = "Focus: Marketing",
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText3);
        headerRow.Children.Add(_challengeFocusLabel);
        
        _challengeAllowanceLabel = (new Label
        {
            Text = "📊 1/week",
            FontSize = 10,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3);
        headerRow.Children.Add(_challengeAllowanceLabel);
        
        _challengeStreakLabel = (new Label
        {
            Text = "🔥 0",
            FontSize = 10,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3);
        headerRow.Children.Add(_challengeStreakLabel);

        var settingsBtn = (new Button
        {
            Text = "⚙️",
            BackgroundColor = Colors.Transparent,
            WidthRequest = 30,
            HeightRequest = 30,
            Padding = 0
        }).ThemeColor(Button.TextColorProperty, ThemeKey.HighlightVioletText);
        settingsBtn.Clicked += OnChallengeSettingsClicked;
        headerRow.Children.Add(settingsBtn);

        _ratioBtn = (new Button
        {
            Text = "Ratio: 1 free per 3",
            FontSize = 9,
            CornerRadius = 4,
            HeightRequest = 24,
            Padding = new Thickness(6, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.CardText3);
        _ratioBtn.Clicked += async (_, _) => await ChangeFreeTaskRatioAsync();
        headerRow.Children.Add(_ratioBtn);

        challengeStack.Children.Add(headerRow);

        _challengeProgressLabel = (new Label
        {
            Text = "This week: 0/1",
            FontSize = 11
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText3);
        challengeStack.Children.Add(_challengeProgressLabel);

        _commitmentsList = new VerticalStackLayout { Spacing = 2 };
        challengeStack.Children.Add(_commitmentsList);

        _addCommitmentBtn = (new Button
        {
            Text = "+ Pick Task",
            FontSize = 11,
            CornerRadius = 4,
            HeightRequest = 28,
            Padding = new Thickness(8, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _addCommitmentBtn.Clicked += OnAddCommitmentClicked;
        challengeStack.Children.Add(_addCommitmentBtn);

        _freeProgressLabel = (new Label
        {
            Text = "Free Tasks: 0/0",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            IsVisible = false
        }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText1);
        challengeStack.Children.Add(_freeProgressLabel);

        _freeCommitmentsList = new VerticalStackLayout { Spacing = 4, IsVisible = false };
        challengeStack.Children.Add(_freeCommitmentsList);

        _addFreeCommitmentBtn = (new Button
        {
            Text = "+ Pick Free Task",
            FontSize = 11,
            CornerRadius = 4,
            HeightRequest = 28,
            Padding = new Thickness(8, 0),
            IsVisible = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        _addFreeCommitmentBtn.Clicked += async (_, _) => await OnAddFreeCommitmentClicked();
        challengeStack.Children.Add(_addFreeCommitmentBtn);

        _addFreeTopCandidateBtn = (new Button
        {
            Text = "+ Free Top Candidate",
            FontSize = 11,
            CornerRadius = 4,
            HeightRequest = 28,
            Padding = new Thickness(8, 0),
            IsVisible = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        _addFreeTopCandidateBtn.Clicked += async (_, _) => await AddFreeTopCandidateAsync();
        challengeStack.Children.Add(_addFreeTopCandidateBtn);

        _markFreeTopCandidateBtn = (new Button
        {
            Text = "\u2B50 From Existing",
            FontSize = 11,
            CornerRadius = 4,
            HeightRequest = 28,
            Padding = new Thickness(8, 0),
            IsVisible = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        _markFreeTopCandidateBtn.Clicked += async (_, _) => await MarkExistingAsTopCandidateAsync(false);
        challengeStack.Children.Add(_markFreeTopCandidateBtn);

        _topCandidatesList = new VerticalStackLayout { Spacing = 2 };
        challengeStack.Children.Add(_topCandidatesList);

        _addTopCandidateBtn = (new Button
        {
            Text = "+ Top Candidate",
            FontSize = 11,
            CornerRadius = 4,
            HeightRequest = 28,
            Padding = new Thickness(8, 0),
            IsVisible = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _addTopCandidateBtn.Clicked += async (_, _) => await AddTopCandidateAsync();
        challengeStack.Children.Add(_addTopCandidateBtn);

        _markFocusTopCandidateBtn = (new Button
        {
            Text = "\u2B50 From Existing",
            FontSize = 11,
            CornerRadius = 4,
            HeightRequest = 28,
            Padding = new Thickness(8, 0),
            IsVisible = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningBackground).ThemeColor(Button.TextColorProperty, ThemeKey.WarningText);
        _markFocusTopCandidateBtn.Clicked += async (_, _) => await MarkExistingAsTopCandidateAsync(true);
        challengeStack.Children.Add(_markFocusTopCandidateBtn);

        _consultLlmBtn = (new Button
        {
            Text = "Consult LLM",
            FontSize = 11,
            CornerRadius = 4,
            HeightRequest = 28,
            Padding = new Thickness(8, 0),
            IsVisible = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightViolet).ThemeColor(Button.TextColorProperty, ThemeKey.HighlightVioletText);
        _consultLlmBtn.Clicked += async (_, _) => await ConsultLlmForPrioritizationAsync();
        challengeStack.Children.Add(_consultLlmBtn);

        _challengeFrame.Content = challengeStack;

        mainStack.Children.Add(challengeRow);
    }

    private Button MiniBtn(string text, ThemeKey color) => (new Button
    {
        Text = text,
        CornerRadius = 4,
        WidthRequest = 36,
        HeightRequest = 32,
        Padding = 0,
        Margin = new Thickness(0, 0, 4, 4)
    }).ThemeColor(VisualElement.BackgroundColorProperty, color).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

    #region Data Loading

    private async Task LoadCategoriesAsync()
    {
        _categories = await _tasks.GetCategoriesAsync(_auth.CurrentUsername);
        
        _categoryPicker.Items.Clear();
        _categoryPicker.Items.Add("All");
        foreach (var cat in _categories)
            _categoryPicker.Items.Add(cat);
        
        if (!string.IsNullOrEmpty(_selectedCategory))
        {
            int idx = _categoryPicker.Items.IndexOf(_selectedCategory);
            _categoryPicker.SelectedIndex = idx >= 0 ? idx : 0;
        }
        else
        {
            _categoryPicker.SelectedIndex = 0;
        }
    }

    private async void OnCategoryChanged(object? sender, EventArgs e)
    {
        if (_isLoading || _categoryPicker.SelectedIndex < 0) return;
        _selectedCategory = _categoryPicker.Items[_categoryPicker.SelectedIndex];
        await RefreshTasksAsync();
    }

    private async void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _searchText = e.NewTextValue?.ToLower() ?? "";
        await RefreshTasksAsync();
    }

    private async void OnToggleCompletedClicked(object? sender, EventArgs e)
    {
        _showingCompleted = !_showingCompleted;
        _showCompletedBtn.ThemeColor(VisualElement.BackgroundColorProperty, _showingCompleted ? ThemeKey.SuccessButtonBackground : ThemeKey.DisabledButtonBackground);
        await RefreshTasksAsync();
    }

    private async Task RefreshTasksAsync()
    {
        List<TaskItem> tasks;

        if (_showingCompleted)
        {
            tasks = await _tasks.GetCompletedTasksAsync(_auth.CurrentUsername);
            if (_selectedCategory != "All")
                tasks = tasks.Where(t => t.Category == _selectedCategory).ToList();
        }
        else
        {
            tasks = _selectedCategory == "All"
                ? await _tasks.GetActiveTasksAsync(_auth.CurrentUsername)
                : await _tasks.GetTasksByCategoryAsync(_auth.CurrentUsername, _selectedCategory);
        }

        // Search filter
        if (!string.IsNullOrEmpty(_searchText))
        {
            tasks = tasks.Where(t =>
                t.Title.ToLower().Contains(_searchText) ||
                (t.Notes?.ToLower().Contains(_searchText) ?? false) ||
                t.Category.ToLower().Contains(_searchText)
            ).ToList();
        }

        tasks = _sortColumn switch
        {
            "Id" => _sortAscending ? tasks.OrderBy(t => t.Id).ToList() : tasks.OrderByDescending(t => t.Id).ToList(),
            "Status" => _sortAscending ? tasks.OrderBy(GetTaskStatusText).ToList() : tasks.OrderByDescending(GetTaskStatusText).ToList(),
            "Title" => _sortAscending ? tasks.OrderBy(t => t.Title).ToList() : tasks.OrderByDescending(t => t.Title).ToList(),
            "Category" => _sortAscending ? tasks.OrderBy(t => t.Category).ToList() : tasks.OrderByDescending(t => t.Category).ToList(),
            "Priority" => _sortAscending ? tasks.OrderByDescending(t => t.Priority).ToList() : tasks.OrderBy(t => t.Priority).ToList(),
            "DueDate" => _sortAscending ? tasks.OrderBy(t => t.DueDate).ToList() : tasks.OrderByDescending(t => t.DueDate).ToList(),
            "Notes" => _sortAscending ? tasks.OrderBy(t => t.Notes).ToList() : tasks.OrderByDescending(t => t.Notes).ToList(),
            "CreatedAt" => _sortAscending ? tasks.OrderBy(t => t.CreatedAt).ToList() : tasks.OrderByDescending(t => t.CreatedAt).ToList(),
            "CompletedAt" => _sortAscending ? tasks.OrderBy(t => t.CompletedAt).ToList() : tasks.OrderByDescending(t => t.CompletedAt).ToList(),
            _ => tasks.OrderByDescending(t => t.CreatedAt).ToList()
        };

        _currentTasks = tasks;

        // Update header
        var (active, overdue, dueToday, urgent) = await _tasks.GetStatsAsync(_auth.CurrentUsername);
        _headerLabel.Text = urgent > 0 ? $"{active} tasks ({urgent}🟣)"
            : overdue > 0 ? $"{active} tasks ({overdue} overdue)"
            : $"{active} tasks";

        BuildDataGrid(tasks);
    }

    private void BuildDataGrid(List<TaskItem> tasks)
    {
        _gridToolbarContainer.Children.Clear();
        _gridContainer.Children.Clear();

        if (tasks.Count == 0)
        {
            _gridContainer.Children.Add((new Label
            {
                Text = "No tasks. Click + New to add one.",
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(20)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2));
            return;
        }

        var displayMode = _privacyMode.GetDisplayMode(
            _auth.CurrentUsername);
        bool isPublicMode = displayMode == ActivityDisplayMode.PublicOnly;
        var columns = isPublicMode
            ? new List<string> { "Id", "Status", "Impact", "Category", "Priority", "DueDate", "CreatedAt", "CompletedAt" }
            : new List<string> { "Id", "Status", "Title", "Category", "Priority", "DueDate", "Impact", "Notes", "CreatedAt", "CompletedAt" };
        var headers = columns.ToList();
        var displayRows = new List<List<string>>();
        var fullRows = new List<List<string>>();

        foreach (var task in tasks)
        {
            var row = BuildTaskGridRow(task, isPublicMode);
            fullRows.Add(row);
            displayRows.Add(row.Select(v => v.Length > 50 ? v.Substring(0, 47) + "..." : v).ToList());
        }

        var dataGrid = DataGridView.Create(headers, displayRows)
            .WithHeaderStyle(AppColors.Get(ThemeKey.HighlightIndigoText), AppColors.Get(ThemeKey.ButtonPrimaryText))
            .WithHeaderTextProvider(index => GetHeaderText(columns[index]))
            .WithAlternateRowColor(AppColors.Get(ThemeKey.HighlightViolet))
            .WithColumnWidths(60, 220)
            .WithCellPadding(6)
            .WithFontSize(12, 12)
            .WithFullRows(fullRows)
            .WithIdColumn("Id")
            .OnHeaderTapped(async (_, e) =>
            {
                if (e.ColumnIndex >= 0 && e.ColumnIndex < columns.Count)
                    await SortByColumnAsync(columns[e.ColumnIndex]);
            })
            .OnCellTapped((s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _currentTasks.Count)
                    ShowDetail(_currentTasks[e.RowIndex]);
            })
            .WithUpdateCallback(UpdateTaskGridCellAsync)
            .Build();

        _mainDataGrid = dataGrid;

        _gridToolbarContainer.Children.Add(dataGrid.ToolbarView);
        _gridContainer.Children.Add(dataGrid.GridView);
    }

    private string GetHeaderText(string column)
    {
        if (_sortColumn != column) return column;
        return column + (_sortAscending ? " \u25B2" : " \u25BC");
    }

    private async Task SortByColumnAsync(string column)
    {
        if (_sortColumn == column)
            _sortAscending = !_sortAscending;
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }

        _mainDataGrid?.ResetToFirstPage();
        await RefreshTasksAsync();
    }

    private Frame BuildCell(TaskItem task)
    {
        ThemeKey bg = task.IsCompleted ? ThemeKey.SuccessBackground : task.IsOverdue ? ThemeKey.ErrorBackground : ThemeKey.CardBackground;

        var frame = (new Frame
        {
            Padding = 4,
            CornerRadius = 0,
            BorderColor = Colors.Transparent,
            HasShadow = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, bg);

        var stack = new VerticalStackLayout { Spacing = 1 };

        // Icons
        var icons = new HorizontalStackLayout { Spacing = 2 };
        if (task.Priority > 0)
            icons.Children.Add((new Label
            {
                Text = $"P{task.Priority}",
                FontSize = 9,
                FontAttributes = FontAttributes.Bold
            }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightIndigoText, ThemeKey.PageText3));
        if (task.IsCompleted) icons.Children.Add((new Label { Text = "✓", FontSize = 8 }).ThemeColor(Label.TextColorProperty, ThemeKey.SuccessButtonBackground, ThemeKey.PageText3));
        if (task.IsOverdue) icons.Children.Add((new Label { Text = "!", FontSize = 8, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.ErrorText, ThemeKey.PageText3));
        if (icons.Children.Count > 0) stack.Children.Add(icons);

        // Title
        var title = task.Title.Length > 30 ? task.Title.Substring(0, 30) + "…" : task.Title;
        stack.Children.Add((new Label
        {
            Text = title,
            FontSize = 10,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        }).ThemeColor(Label.TextColorProperty, task.IsCompleted ? ThemeKey.CardText3 : ThemeKey.CardText1, ThemeKey.PageText3));

        // Category
        stack.Children.Add((new Label
        {
            Text = task.Category,
            FontSize = 8
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3));

        frame.Content = stack;

        var tap = new TapGestureRecognizer();
        var captured = task;
        tap.Tapped += (s, e) => ShowDetail(captured);
        frame.GestureRecognizers.Add(tap);

        return frame;
    }

    private static List<string> BuildTaskGridRow(
        TaskItem task,
        bool isPublicMode)
    {
        if (isPublicMode)
        {
            return new List<string>
            {
                task.Id.ToString(),
                GetTaskStatusText(task),
                task.Impact,
                task.Category,
                task.Priority.ToString(),
                task.DueDate?.ToString("yyyy-MM-dd") ?? "",
                task.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                task.CompletedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? ""
            };
        }

        return new List<string>
        {
            task.Id.ToString(),
            GetTaskStatusText(task),
            task.Title,
            task.Category,
            task.Priority.ToString(),
            task.DueDate?.ToString("yyyy-MM-dd") ?? "",
            task.Impact,
            task.Notes ?? "",
            task.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            task.CompletedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? ""
        };
    }

    private static string GetTaskStatusText(TaskItem task) =>
        task.IsCompleted ? "Done" : task.IsOverdue ? "Overdue" : task.IsDueToday ? "Today" : "Open";

    private async Task<bool> UpdateTaskGridCellAsync(string idValue, string columnName, string newValue)
    {
        if (!int.TryParse(idValue, out int id))
        {
            return false;
        }

        var task = _currentTasks.FirstOrDefault(t => t.Id == id);
        if (task == null)
        {
            task = (await _tasks.GetActiveTasksAsync(_auth.CurrentUsername))
                .Concat(await _tasks.GetCompletedTasksAsync(_auth.CurrentUsername))
                .FirstOrDefault(t => t.Id == id);
        }

        if (task == null)
        {
            return false;
        }

        switch (columnName)
        {
            case "Title":
                if (string.IsNullOrWhiteSpace(newValue)) return false;
                task.Title = newValue.Trim();
                break;
            case "Category":
                task.Category = string.IsNullOrWhiteSpace(newValue) ? "General" : newValue.Trim();
                break;
            case "Priority":
                task.Priority = ParsePriority(newValue);
                break;
            case "DueDate":
                if (string.IsNullOrWhiteSpace(newValue) || newValue.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                {
                    task.DueDate = null;
                }
                else if (DateTime.TryParse(newValue, out var dueDate))
                {
                    task.DueDate = dueDate.Date;
                }
                else
                {
                    return false;
                }
                break;
            case "Notes":
                task.Notes = newValue == "NULL" ? "" : newValue;
                break;
            case "Impact":
                task.Impact = newValue == "NULL" ? "" : newValue;
                break;
            case "Status":
                if (newValue.Equals("Done", StringComparison.OrdinalIgnoreCase) ||
                    newValue.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
                    newValue.Equals("True", StringComparison.OrdinalIgnoreCase))
                {
                    task.IsCompleted = true;
                    task.CompletedAt ??= DateTime.UtcNow;
                }
                else if (newValue.Equals("Open", StringComparison.OrdinalIgnoreCase) ||
                         newValue.Equals("False", StringComparison.OrdinalIgnoreCase))
                {
                    task.IsCompleted = false;
                    task.CompletedAt = null;
                }
                else
                {
                    return false;
                }
                break;
            case "IsPrivate":
                task.IsPrivate = newValue.Equals("true", StringComparison.OrdinalIgnoreCase)
                    || newValue == "1";
                break;
            case "CreatedAt":
            case "CompletedAt":
                return false;
            default:
                return false;
        }

        await _tasks.UpdateTaskAsync(task);
        ShowDetail(task);
        await RefreshChallengeWidgetAsync();
        return true;
    }

    private static int ParsePriority(string value)
    {
        if (int.TryParse(value.Trim(), out int numeric))
            return Math.Max(0, numeric);
        return 0;
    }

    private void ShowDetail(TaskItem task)
    {
        _selectedTask = task;
        _detailPanel.IsVisible = true;

        var displayMode = _privacyMode.GetDisplayMode(
            _auth.CurrentUsername);
        bool isPublicMode = displayMode == ActivityDisplayMode.PublicOnly;
        _detailTitle.Text = isPublicMode ? task.Impact : task.Title;
        _detailNotes.Text = isPublicMode ? "" : task.Notes ?? "";

        var meta = $"📁 {task.Category}";
        if (task.IsPrivate) meta += " •  Private";
        meta += $" • Priority: {task.Priority}";
        if (task.DueDate.HasValue) meta += $" • Due: {task.DueDate.Value:MMM d}";
        if (task.IsCompleted) meta += " • ✅ Done";
        _detailMeta.Text = meta;
    }

    private async void OnDetailNotesSave(object? sender, FocusEventArgs e)
    {
        if (_selectedTask == null) return;
        _selectedTask.Notes = _detailNotes.Text;
        await _tasks.UpdateTaskAsync(_selectedTask);
    }

    #endregion

    #region Task Actions

    private async void OnAddTaskClicked(object? sender, EventArgs e)
    {
        var newTask = await TaskCreationHelper.ShowCreateTaskAsync(
            this, _auth, _tasks, _ideasService);
        if (newTask != null)
        {
            await LoadCategoriesAsync();
            await RefreshTasksAsync();
        }
    }

    private async Task<string?> AskForCategoryAsync()
    {
        var options = new List<string> { "General" };
        options.AddRange(_categories.Where(c => c != "General"));
        options.Add("+ New Category");

        string? choice = await DisplayActionSheet("Category", "Cancel", null, options.Distinct().ToArray());
        if (choice == "Cancel" || string.IsNullOrEmpty(choice)) return null;

        if (choice == "+ New Category")
        {
            string? newCat = await DisplayPromptAsync("New Category", "Enter category name:");
            return string.IsNullOrWhiteSpace(newCat) ? null : newCat.Trim();
        }

        return choice;
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_selectedTask == null) return;
        string originalTitle = _selectedTask.Title;
        string? t = await DisplayPromptAsync("Edit", "Update task:", "Save", "Cancel", initialValue: _selectedTask.Title);
        if (!string.IsNullOrWhiteSpace(t) && t != _selectedTask.Title)
        {
            _selectedTask.Title = t.Trim();
            await _tasks.UpdateTaskAsync(_selectedTask);
            _detailTitle.Text = _selectedTask.Title;

            string ideaText = $"[Edited] \"{originalTitle}\" → \"{t.Trim()}\"";
            if (_ideaLogger != null)
                await _ideaLogger.LogSilentlyAsync(new IdeaLogRequest
                {
                    Username = _auth.CurrentUsername,
                    Title = ideaText,
                    Body = ideaText,
                    Category = "tasks_ideas"
                });

            await RefreshTasksAsync();
        }
    }

    private async void OnCompleteClicked(object? sender, EventArgs e)
    {
        if (_selectedTask == null) return;

        if (_selectedTask.IsCompleted)
            await _tasks.UncompleteTaskAsync(_selectedTask);
        else
            await _tasks.CompleteTaskAsync(_selectedTask);

        ShowDetail(_selectedTask);
        await RefreshTasksAsync();
        await RefreshChallengeWidgetAsync();
    }

    private async void OnPriorityClicked(object? sender, EventArgs e)
    {
        if (_selectedTask == null) return;

        string? input = await DisplayPromptAsync(
            "Priority",
            "Enter priority (higher number = higher priority):",
            "Set",
            "Cancel",
            initialValue: _selectedTask.Priority.ToString(),
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(input)) return;

        if (!int.TryParse(input.Trim(), out int priority)
            || priority < 0)
        {
            await DisplayAlert("Invalid",
                "Enter a non-negative whole number.", "OK");
            return;
        }

        _selectedTask.Priority = priority;
        await _tasks.UpdateTaskAsync(_selectedTask);
        ShowDetail(_selectedTask);
        await RefreshTasksAsync();
    }

    private async void OnMoveClicked(object? sender, EventArgs e)
    {
        if (_selectedTask == null) return;
        string? category = await AskForCategoryAsync();
        if (category == null) return;

        _selectedTask.Category = category;
        await _tasks.UpdateTaskAsync(_selectedTask);
        ShowDetail(_selectedTask);
        await LoadCategoriesAsync();
        await RefreshTasksAsync();
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_selectedTask == null) return;
        if (await DisplayAlert("Delete?", "Permanently delete?", "Delete", "Cancel"))
        {
            await _tasks.DeleteTaskAsync(_selectedTask);
            _detailPanel.IsVisible = false;
            _selectedTask = null;
            await LoadCategoriesAsync();
            await RefreshTasksAsync();
        }
    }

    #endregion

    #region Weekly Challenge

    private async Task RefreshChallengeWidgetAsync(bool processWeekEnd = false)
    {
        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);

        if (challenge == null)
        {
            _challengeFrame.IsVisible = false;
            _startChallengeBtn.IsVisible = true;
            _allowanceChartContainer.IsVisible = false;
            _topCandidatesList.Children.Clear();
            _addTopCandidateBtn.IsVisible = false;
            _consultLlmBtn.IsVisible = false;
            _freeProgressLabel.IsVisible = false;
            _freeCommitmentsList.IsVisible = false;
            _addFreeCommitmentBtn.IsVisible = false;
            _addFreeTopCandidateBtn.IsVisible = false;
            _markFocusTopCandidateBtn.IsVisible = false;
            _markFreeTopCandidateBtn.IsVisible = false;
            return;
        }

        _challengeFrame.IsVisible = true;
        _startChallengeBtn.IsVisible = false;

        _challengeFocusLabel.Text = $"Focus: {challenge.FocusCategory} ({challenge.RemainingFocusTasks})";
        _challengeAllowanceLabel.Text = $"📊 {challenge.CurrentAllowance}/wk";
        _challengeStreakLabel.Text = $"🔥 {challenge.SuccessStreak}";
        _ratioBtn.Text = $"Ratio: 1 free per {challenge.FreeTaskRatio}";

        var commitments = await _challengeService.GetCurrentWeekCommitmentsAsync(challenge.Id);
        var focusCommitments = commitments.Where(c => c.IsFocusTask).ToList();
        var freeCommitments = commitments.Where(c => !c.IsFocusTask).ToList();
        var (focusTarget, freeTarget) = WeeklyChallengeService.CalculateTaskSplit(challenge.CurrentAllowance, challenge.FreeTaskRatio);
        int completedFocus = focusCommitments.Count(c => c.IsCompleted);
        int completedFree = freeCommitments.Count(c => c.IsCompleted);
        int designatedFocus = focusCommitments.Count;
        int designatedFree = freeCommitments.Count;

        await RefreshAllowanceChartAsync(
            completedFocus,
            focusTarget,
            designatedFocus,
            completedFree,
            freeTarget,
            designatedFree);

        _challengeProgressLabel.Text = $"Focus: {completedFocus}/{focusTarget}";

        var availableTaskLookup = (await _tasks.GetActiveTasksAsync(_auth.CurrentUsername))
            .Concat(await _tasks.GetCompletedTasksAsync(_auth.CurrentUsername))
            .GroupBy(t => t.Id)
            .ToDictionary(g => g.Key, g => g.First());

        _commitmentsList.Children.Clear();
        foreach (var commitment in focusCommitments)
        {
            if (availableTaskLookup.TryGetValue(commitment.TaskId, out var task))
                _commitmentsList.Children.Add(BuildCommitmentRow(commitment, task));
        }

        _addCommitmentBtn.IsVisible = focusCommitments.Count < focusTarget;

        _freeCommitmentsList.Children.Clear();
        _freeProgressLabel.Text = freeTarget > 0
            ? $"Free Tasks: {freeCommitments.Count(c => c.IsCompleted)}/{freeTarget}"
            : "Free Tasks: unlocks at allowance 3";
        _freeProgressLabel.IsVisible = true;
        _freeCommitmentsList.IsVisible = true;
        _addFreeCommitmentBtn.IsVisible = freeTarget > 0;
        _addFreeTopCandidateBtn.IsVisible = true;
        _markFreeTopCandidateBtn.IsVisible = true;

        if (freeTarget > 0)
        {
            foreach (var commitment in freeCommitments)
            {
                if (availableTaskLookup.TryGetValue(commitment.TaskId, out var task))
                    _freeCommitmentsList.Children.Add(BuildCommitmentRow(commitment, task));
            }

            int freeSlots = freeTarget - freeCommitments.Count;
            _addFreeCommitmentBtn.IsVisible = freeSlots > 0;
        }

        // Show top candidates
        _topCandidatesList.Children.Clear();

        var committedTaskIds = commitments.Select(c => c.TaskId).ToHashSet();
        var topCandidates = (await _tasks.GetActiveTasksAsync(_auth.CurrentUsername))
            .Where(t => t.IsTopCandidate &&
                   string.Equals(t.Category, challenge.FocusCategory, StringComparison.OrdinalIgnoreCase) &&
                   !committedTaskIds.Contains(t.Id))
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (topCandidates.Count > 0)
        {
            var headerLabel = (new Label
            {
                Text = _topCandidatesExpanded
                    ? $"\u25BC \u2B50 Top Candidates ({topCandidates.Count})"
                    : $"\u25B6 \u2B50 Top Candidates ({topCandidates.Count})",
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 4, 0, 0)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText3);

            var headerTap = new TapGestureRecognizer();
            headerTap.Tapped += async (_, _) =>
            {
                _topCandidatesExpanded = !_topCandidatesExpanded;
                await RefreshChallengeWidgetAsync(processWeekEnd: false);
            };
            headerLabel.GestureRecognizers.Add(headerTap);

            _topCandidatesList.Children.Add(headerLabel);

            if (_topCandidatesExpanded)
            {
                foreach (var task in topCandidates)
                {
                    var row = (new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        Padding = new Thickness(4, 2)
                    }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningBackground);

                    var lbl = (new Label
                    {
                        Text = task.Priority > 0
                            ? $"[{task.Priority}] {task.Title}"
                            : task.Title,
                        FontSize = 10,
                        VerticalOptions = LayoutOptions.Center,
                        LineBreakMode = LineBreakMode.WordWrap
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText3);
                    Grid.SetColumn(lbl, 0);
                    row.Children.Add(lbl);

                    var actionsStack = new HorizontalStackLayout { Spacing = 0 };

                    // Remove top candidate flag
                    var unstarBtn = (new Button
                    {
                        Text = "\u2B50",
                        BackgroundColor = Colors.Transparent,
                        WidthRequest = 26,
                        HeightRequest = 26,
                        Padding = 0,
                        FontSize = 10
                    }).ThemeColor(Button.TextColorProperty, ThemeKey.WarningText);
                    var capturedTask = task;
                    unstarBtn.Clicked += async (_, _) =>
                    {
                        capturedTask.IsTopCandidate = false;
                        await _tasks.UpdateTaskAsync(capturedTask);
                        await RefreshChallengeWidgetAsync(processWeekEnd: false);
                    };
                    actionsStack.Children.Add(unstarBtn);

                    Grid.SetColumn(actionsStack, 1);
                    row.Children.Add(actionsStack);

                    _topCandidatesList.Children.Add(row);
                }
            }
        }

        _addTopCandidateBtn.IsVisible = true;
        _markFocusTopCandidateBtn.IsVisible = true;
        _consultLlmBtn.IsVisible = true;
    }

    private View BuildCommitmentRow(WeeklyCommitment commitment, TaskItem task)
    {
        var row = (new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Padding = new Thickness(4, 2)
        }).ThemeColor(VisualElement.BackgroundColorProperty, commitment.IsCompleted ? ThemeKey.SuccessBackground : ThemeKey.CardBackground);

        var cb = (new CheckBox
        {
            IsChecked = commitment.IsCompleted,
            IsEnabled = !commitment.IsCompleted,
            Scale = 0.8
        }).ThemeColor(CheckBox.ColorProperty, ThemeKey.SuccessButtonBackground);
        var capturedTask = task;
        var capturedCommitment = commitment;
        cb.CheckedChanged += async (_, _) =>
        {
            if (cb.IsChecked && !capturedCommitment.IsCompleted)
            {
                await _tasks.CompleteTaskAsync(capturedTask);
                await _challengeService.MarkCommitmentCompletedAsync(capturedTask.Id);
                await RefreshChallengeWidgetAsync();
                await RefreshTasksAsync();
            }
        };
        Grid.SetColumn(cb, 0);
        row.Children.Add(cb);

        var lbl = (new Label
        {
            Text = task.Title + (commitment.IsFocusTask ? "" : " 🌈"),
            FontSize = 10,
            TextDecorations = commitment.IsCompleted ? TextDecorations.Strikethrough : TextDecorations.None,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, commitment.IsCompleted ? ThemeKey.CardText3 : ThemeKey.CardText1, ThemeKey.PageText3);

        if (!commitment.IsCompleted)
        {
            var editTap = new TapGestureRecognizer();
            editTap.Tapped += async (_, _) => await EditCommitmentTaskAsync(capturedTask);
            lbl.GestureRecognizers.Add(editTap);
        }

        Grid.SetColumn(lbl, 1);
        row.Children.Add(lbl);

        if (!commitment.IsCompleted)
        {
            var actionsStack = new HorizontalStackLayout { Spacing = 0 };
            var editBtn = (new Button
            {
                Text = "✏️",
                BackgroundColor = Colors.Transparent,
                WidthRequest = 26,
                HeightRequest = 26,
                Padding = 0,
                FontSize = 10
            }).ThemeColor(Button.TextColorProperty, ThemeKey.HighlightIndigoText);
            editBtn.Clicked += async (_, _) => await EditCommitmentTaskAsync(capturedTask);
            actionsStack.Children.Add(editBtn);

            var removeBtn = (new Button
            {
                Text = "✕",
                BackgroundColor = Colors.Transparent,
                WidthRequest = 26,
                HeightRequest = 26,
                Padding = 0,
                FontSize = 10
            }).ThemeColor(Button.TextColorProperty, ThemeKey.CardText3);
            var capturedId = commitment.Id;
            removeBtn.Clicked += async (_, _) =>
            {
                await _challengeService.RemoveCommitmentAsync(capturedId);
                await RefreshChallengeWidgetAsync();
            };
            actionsStack.Children.Add(removeBtn);

            Grid.SetColumn(actionsStack, 2);
            row.Children.Add(actionsStack);
        }

        return row;
    }

    private async Task RefreshAllowanceChartAsync(int completedFocus, int focusTarget, int designatedFocus, int completedFree, int freeTarget, int designatedFree)
    {
        _allowanceChartContainer.Children.Clear();
        _allowanceChartContainer.IsVisible = true;

        var active = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (active == null) return;

        // ---- Chart: allowance over time (snapshots + current week) ----
        var snapshots = await _challengeService.GetWeekSnapshotsAsync(_auth.CurrentUsername, 12);
        snapshots.Reverse(); // oldest on the left

        _allowanceChartContainer.Children.Add((new Label
        {
            Text = "Focus Allowance History",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText1));

        var chartFrame = (new Frame
        {
            Padding = 12,
            CornerRadius = 10,
            BorderColor = Colors.Transparent,
            HasShadow = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);

        var chartGrid = new Grid
        {
            ColumnSpacing = 4,
            RowSpacing = 2,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            }
        };

        int maxAllowance = Math.Max(active.CurrentAllowance, snapshots.Count > 0 ? snapshots.Max(s => s.Allowance) : 1);
        if (maxAllowance < 1) maxAllowance = 1;
        double barMaxHeight = 80;

        void AddBar(int column, int allowance, ThemeKey color, string label)
        {
            chartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            double barHeight = Math.Max(8, (allowance / (double)maxAllowance) * barMaxHeight);

            var barStack = new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.End,
                HorizontalOptions = LayoutOptions.Center
            };
            barStack.Children.Add((new Label
            {
                Text = allowance.ToString(),
                FontSize = 9,
                HorizontalTextAlignment = TextAlignment.Center
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));
            barStack.Children.Add((new BoxView
            {
                HeightRequest = barHeight,
                WidthRequest = 20,
                CornerRadius = 4,
                HorizontalOptions = LayoutOptions.Center
            }).ThemeColor(BoxView.ColorProperty, color));
            Grid.SetColumn(barStack, column);
            Grid.SetRow(barStack, 0);
            chartGrid.Children.Add(barStack);

            var weekLabel = (new Label
            {
                Text = label,
                FontSize = 8,
                HorizontalTextAlignment = TextAlignment.Center
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3);
            Grid.SetColumn(weekLabel, column);
            Grid.SetRow(weekLabel, 1);
            chartGrid.Children.Add(weekLabel);
        }

        for (int i = 0; i < snapshots.Count; i++)
        {
            var s = snapshots[i];
            AddBar(i, s.Allowance,
                s.WasSuccessful ? ThemeKey.HighlightVioletText : ThemeKey.HighlightVioletText,
                s.WeekStart.ToString("M/d"));
        }
        AddBar(snapshots.Count, active.CurrentAllowance, ThemeKey.SuccessButtonBackground, "Now");

        chartFrame.Content = chartGrid;
        _allowanceChartContainer.Children.Add(chartFrame);

        // ---- Text summary ----
        string freeLine = freeTarget > 0
            ? $"Free tasks allowed: {freeTarget}  •  Designated: {designatedFree}  •  Completed: {completedFree}/{designatedFree}"
            : "Free tasks allowed: 0 (unlocks at allowance 3)";

        _allowanceChartContainer.Children.Add((new Label
        {
            Text =
                $"Total tasks allowed per week: {active.CurrentAllowance}\n" +
                $"Weeks completed in a row: {active.SuccessStreak}\n" +
                $"Focus tasks allowed: {focusTarget}  •  Designated: {designatedFocus}  •  Completed: {completedFocus}/{designatedFocus}\n" +
                freeLine,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1));
    }

    private async void OnStartChallengeClicked(object? sender, EventArgs e)
    {
        if (_categories.Count == 0)
        {
            await DisplayAlert("No Categories", "Create some tasks with categories first.", "OK");
            return;
        }

        string? focusCategory = await DisplayActionSheet(
            "Pick Focus Category",
            "Cancel",
            null,
            _categories.ToArray());

        if (string.IsNullOrEmpty(focusCategory) || focusCategory == "Cancel") return;

        await PickTargetAndStartChallengeAsync(focusCategory);
    }

    private async Task PickTargetAndStartChallengeAsync(string focusCategory)
    {
        string? targetStr = await DisplayActionSheet("How many tasks to complete?", "Cancel", null,
            "25 tasks", "50 tasks", "100 tasks", "200 tasks", "500 tasks");
        if (string.IsNullOrEmpty(targetStr) || targetStr == "Cancel") return;

        int target = int.Parse(targetStr.Split(' ')[0]);
        await _challengeService.StartChallengeAsync(_auth.CurrentUsername, focusCategory, target);
        await RefreshChallengeWidgetAsync();
    }

    private async void OnChallengeSettingsClicked(object? sender, EventArgs e)
    {
        string action = await DisplayActionSheet("Challenge Settings", "Cancel", "End Challenge", "View Stats", "Edit Streak", "Edit Allowance");

        if (action == "End Challenge")
        {
            if (await DisplayAlert("End Challenge?", "Are you sure?", "End", "Cancel"))
            {
                await _challengeService.EndChallengeAsync(_auth.CurrentUsername);
                await RefreshChallengeWidgetAsync();
            }
        }
        else if (action == "View Stats")
        {
            var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
            if (challenge != null)
            {
                await DisplayAlert("Challenge Stats",
                    $"Focus: {challenge.FocusCategory}\n" +
                    $"Progress: {challenge.CompletedFocusTaskCount}/{challenge.TargetTaskCount}\n" +
                    $"Allowance: {challenge.CurrentAllowance}/week\n" +
                    $"Streak: {challenge.SuccessStreak} weeks",
                    "OK");
            }
        }
        else if (action == "Edit Streak")
        {
            var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
            if (challenge == null) return;

            string? streakText = await DisplayPromptAsync(
                "Edit Streak",
                "Set the focus streak (weeks).",
                "Save",
                "Cancel",
                initialValue: challenge.SuccessStreak.ToString(),
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(streakText)) return;
            if (!int.TryParse(streakText.Trim(), out int streak)) return;

            await _challengeService.SetSuccessStreakAsync(_auth.CurrentUsername, streak);
            await RefreshChallengeWidgetAsync(processWeekEnd: false);
        }
        else if (action == "Edit Allowance")
        {
            var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
            if (challenge == null) return;

            string? allowanceText = await DisplayPromptAsync(
                "Edit Allowance",
                "Set the weekly task allowance.",
                "Save",
                "Cancel",
                initialValue: challenge.CurrentAllowance.ToString(),
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(allowanceText)) return;
            if (!int.TryParse(allowanceText.Trim(), out int allowance)) return;

            await _challengeService.SetCurrentAllowanceAsync(_auth.CurrentUsername, allowance);
            await RefreshChallengeWidgetAsync(processWeekEnd: false);
        }
    }

    private async void OnAddCommitmentClicked(object? sender, EventArgs e)
    {
        var dayOfWeek = DateTime.Today.DayOfWeek;
        if (dayOfWeek == DayOfWeek.Saturday)
        {
            await DisplayAlert("Deadline Passed",
                "The designation deadline was Friday. You can designate tasks again starting tomorrow (Sunday) for the new week.",
                "OK");
            return;
        }

        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (challenge == null) return;

        var commitments = await _challengeService.GetCurrentWeekCommitmentsAsync(challenge.Id);
        var (focusTarget, _) = WeeklyChallengeService.CalculateTaskSplit(challenge.CurrentAllowance, challenge.FreeTaskRatio);
        if (commitments.Count(c => c.IsFocusTask) >= focusTarget)
        {
            await DisplayAlert("Full", "You've already picked all focus tasks for this week.", "OK");
            return;
        }

        var availableTasks = await _challengeService.GetAvailableFocusTasksAsync(
            _auth.CurrentUsername, challenge.FocusCategory);

        if (availableTasks.Count == 0)
        {
            await DisplayAlert("No Tasks", $"No available tasks in {challenge.FocusCategory}.", "OK");
            return;
        }

        var selectedTask = await ShowTaskPickerAsync(availableTasks, challenge.FocusCategory);
        if (selectedTask == null) return;

        await _challengeService.AddCommitmentAsync(challenge.Id, selectedTask.Id, isFocusTask: true);
        await RefreshChallengeWidgetAsync();
    }

    private async Task OnAddFreeCommitmentClicked()
    {
        if (DateTime.Today.DayOfWeek == DayOfWeek.Saturday)
        {
            await DisplayAlert("Deadline Passed",
                "The designation deadline was Friday. You can designate tasks again starting tomorrow (Sunday) for the new week.",
                "OK");
            return;
        }

        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (challenge == null) return;

        var availableTasks = await _challengeService.GetAvailableNonFocusTasksAsync(
            _auth.CurrentUsername, challenge.FocusCategory);

        if (availableTasks.Count == 0)
        {
            await DisplayAlert("No Tasks", "No available non-focus tasks.", "OK");
            return;
        }

        var selected = await ShowTaskPickerAsync(availableTasks, "Any Category");
        if (selected == null) return;

        await _challengeService.AddCommitmentAsync(challenge.Id, selected.Id, isFocusTask: false);
        await RefreshChallengeWidgetAsync();
    }

    private async Task ChangeFreeTaskRatioAsync()
    {
        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (challenge == null) return;

        string? input = await DisplayPromptAsync(
            "Free Task Ratio",
            "1 free task per how many total?\n\nE.g. 3 means: allowance 6 = 4 focus + 2 free",
            "Save",
            "Cancel",
            initialValue: challenge.FreeTaskRatio.ToString(),
            maxLength: 2,
            keyboard: Keyboard.Numeric);

        if (!string.IsNullOrWhiteSpace(input) && int.TryParse(input, out int ratio) && ratio >= 2)
        {
            challenge.FreeTaskRatio = ratio;
            var (_, freeTasks) = WeeklyChallengeService.CalculateTaskSplit(challenge.CurrentAllowance, ratio);
            challenge.RequiredFreeTasks = freeTasks;
            await _challengeService.UpdateChallengeAsync(challenge);
            await RefreshChallengeWidgetAsync();
        }
    }

    private async Task AddTopCandidateAsync()
    {
        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (challenge == null) return;

        var newTask = await TaskCreationHelper.ShowCreateTaskAsync(
            this, _auth, _tasks, _ideasService,
            fixedCategory: challenge.FocusCategory,
            markAsTopCandidate: true);
        if (newTask != null)
        {
            await RefreshChallengeWidgetAsync();
            await RefreshTasksAsync();
        }
    }

    private async Task AddFreeTopCandidateAsync()
    {
        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (challenge == null) return;

        var newTask = await TaskCreationHelper.ShowCreateTaskAsync(
            this, _auth, _tasks, _ideasService,
            markAsTopCandidate: true,
            excludedCategory: challenge.FocusCategory);
        if (newTask != null)
        {
            await RefreshChallengeWidgetAsync(processWeekEnd: false);
            await RefreshTasksAsync();
        }
    }

    private async Task MarkExistingAsTopCandidateAsync(bool focusOnly)
    {
        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (challenge == null) return;

        List<TaskItem> tasks;
        string label;

        if (focusOnly)
        {
            tasks = (await _tasks.GetActiveTasksAsync(_auth.CurrentUsername))
                .Where(t => string.Equals(t.Category, challenge.FocusCategory, StringComparison.OrdinalIgnoreCase) &&
                            !t.IsTopCandidate)
                .ToList();
            label = challenge.FocusCategory;
        }
        else
        {
            tasks = (await _tasks.GetActiveTasksAsync(_auth.CurrentUsername))
                .Where(t => !string.Equals(t.Category, challenge.FocusCategory, StringComparison.OrdinalIgnoreCase) &&
                            !t.IsTopCandidate)
                .ToList();
            label = "Non-focus";
        }

        if (tasks.Count == 0)
        {
            await DisplayAlert("No Tasks", "No available tasks to mark.", "OK");
            return;
        }

        var selected = await ShowTaskPickerAsync(tasks, label);
        if (selected == null) return;

        selected.IsTopCandidate = true;
        await _tasks.UpdateTaskAsync(selected);

        await RefreshChallengeWidgetAsync(processWeekEnd: false);
        await RefreshTasksAsync();
    }

    private async Task ConsultLlmForPrioritizationAsync()
    {
        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (challenge == null)
        {
            await DisplayAlert("No Challenge", "Start a weekly challenge first.", "OK");
            return;
        }

        var focusTasks = await _challengeService.GetAvailableFocusTasksAsync(
            _auth.CurrentUsername, challenge.FocusCategory);

        // Also include already-committed-but-uncompleted tasks for full picture
        var commitments = await _challengeService.GetCurrentWeekCommitmentsAsync(challenge.Id);
        var committedTaskIds = commitments.Where(c => !c.IsCompleted).Select(c => c.TaskId).ToHashSet();

        var allActive = await _tasks.GetActiveTasksAsync(_auth.CurrentUsername);
        var committedTasks = allActive.Where(t => committedTaskIds.Contains(t.Id)).ToList();

        var allFocusTasks = new List<TaskItem>();
        allFocusTasks.AddRange(committedTasks);
        allFocusTasks.AddRange(focusTasks);

        // Dedupe by Id
        allFocusTasks = allFocusTasks
            .GroupBy(t => t.Id)
            .Select(g => g.First())
            .ToList();

        if (allFocusTasks.Count == 0)
        {
            await DisplayAlert("No Tasks", $"No tasks in {challenge.FocusCategory} to prioritize.", "OK");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("I need help prioritizing my tasks for a weekly focus challenge.");
        sb.AppendLine();
        sb.AppendLine($"FOCUS CATEGORY: {challenge.FocusCategory}");
        sb.AppendLine($"WEEKLY ALLOWANCE: {challenge.CurrentAllowance} tasks per week");
        sb.AppendLine($"REMAINING TO COMPLETE CHALLENGE: {challenge.RemainingFocusTasks} tasks");
        sb.AppendLine($"SUCCESS STREAK: {challenge.SuccessStreak} weeks");
        sb.AppendLine();
        sb.AppendLine("TASKS:");
        sb.AppendLine();

        foreach (var task in allFocusTasks)
        {
            string committed = committedTaskIds.Contains(task.Id) ? " [COMMITTED THIS WEEK]" : "";
            string notes = string.IsNullOrWhiteSpace(task.Notes) ? "" : $" | Notes: {task.Notes.Trim()}";

            sb.AppendLine($"- ID:{task.Id} | {task.Title} | Priority: {task.Priority}{committed}{notes}");
        }

        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("1. Analyze these tasks and recommend a priority ordering.");
        sb.AppendLine("2. Consider which tasks are most impactful, have dependencies, or are quick wins.");
        sb.AppendLine($"3. I can only commit {challenge.CurrentAllowance} tasks per week — recommend which to pick this week.");
        sb.AppendLine("4. Return a table in this EXACT parseable format (one line per task, sorted by recommended priority):");
        sb.AppendLine();
        sb.AppendLine("PRIORITY TABLE:");
        sb.AppendLine("ID|PRIORITY|PICK_THIS_WEEK|REASON");
        sb.AppendLine("<task_id>|<non-negative numeric priority>|YES or NO|<brief reason>");
        sb.AppendLine();
        sb.AppendLine("Example:");
        sb.AppendLine("42|100|YES|Quick win that unblocks other tasks");
        sb.AppendLine("17|50|NO|Important but lower priority this week");
        sb.AppendLine();
        sb.AppendLine("After the table, add a brief SUMMARY paragraph explaining your prioritization strategy.");

        await Clipboard.SetTextAsync(sb.ToString());

        bool paste = await DisplayAlert(
            "Prompt Copied",
            $"Exported {allFocusTasks.Count} tasks from {challenge.FocusCategory}.\n\nPaste into Claude/ChatGPT, then copy the response and come back to apply priorities.",
            "Paste Result",
            "Done");

        if (!paste) return;

        string? pastedText = null;

        var tcs = new TaskCompletionSource<string?>();
        var overlay = (new Grid { }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);

        var editor = (new Editor
        {
            AutoSize = EditorAutoSizeOption.Disabled,
            HeightRequest = 300,
            Placeholder = "Paste the LLM response here..."
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText3);

        var applyBtn = (new Button
        {
            Text = "Apply Priorities",
            CornerRadius = 8
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

        var cancelBtn = (new Button
        {
            Text = "Cancel",
            CornerRadius = 8
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

        applyBtn.Clicked += (_, _) =>
        {
            if (Content is Grid g) g.Children.Remove(overlay);
            tcs.TrySetResult(editor.Text);
        };

        cancelBtn.Clicked += (_, _) =>
        {
            if (Content is Grid g) g.Children.Remove(overlay);
            tcs.TrySetResult(null);
        };

        var btnRow = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.End,
            Children = { cancelBtn, applyBtn }
        };

        var card = (new Frame
        {
            CornerRadius = 12,
            Padding = 20,
            WidthRequest = 600,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    (new Label
                    {
                        Text = "Paste LLM Prioritization Result",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1),
                    (new Label
                    {
                        Text = "Paste the full response. Bannister will parse the PRIORITY TABLE and update task priorities.",
                        FontSize = 12,
                        FontAttributes = FontAttributes.Italic
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3),
                    editor,
                    btnRow
                }
            }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        overlay.Children.Add(card);

        if (Content is Grid mainGrid)
            mainGrid.Children.Add(overlay);

        pastedText = await tcs.Task;

        if (string.IsNullOrWhiteSpace(pastedText)) return;

        await ApplyPrioritizationResultAsync(pastedText, allFocusTasks);
    }

    private async Task ApplyPrioritizationResultAsync(string llmResponse, List<TaskItem> focusTasks)
    {
        var taskLookup = focusTasks.ToDictionary(t => t.Id);
        var lines = llmResponse.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Find lines matching the ID|PRIORITY|PICK|REASON format
        int updated = 0;
        var recommendations = new List<(int Id, string Title, int Priority, bool PickThisWeek, string Reason)>();

        foreach (var line in lines)
        {
            var parts = line.Split('|');
            if (parts.Length < 3) continue;

            if (!int.TryParse(parts[0].Trim(), out int taskId)) continue;
            if (!taskLookup.ContainsKey(taskId)) continue;

            int newPriority = ParsePriority(parts[1]);

            bool pickThisWeek = parts.Length > 2 &&
                parts[2].Trim().Equals("YES", StringComparison.OrdinalIgnoreCase);

            string reason = parts.Length > 3 ? parts[3].Trim() : "";

            var task = taskLookup[taskId];
            recommendations.Add((taskId, task.Title, newPriority, pickThisWeek, reason));

            if (task.Priority != newPriority)
            {
                task.Priority = newPriority;
                await _tasks.UpdateTaskAsync(task);
                updated++;
            }
        }

        if (recommendations.Count == 0)
        {
            await DisplayAlert("Parse Error",
                "Could not find any lines matching the expected format:\nID|PRIORITY|PICK_THIS_WEEK|REASON\n\nMake sure the LLM included the PRIORITY TABLE.",
                "OK");
            return;
        }

        var pickTasks = recommendations.Where(r => r.PickThisWeek).ToList();
        var summaryLines = new List<string>
        {
            $"Parsed {recommendations.Count} task(s), updated {updated} priority value(s).",
            ""
        };

        if (pickTasks.Count > 0)
        {
            summaryLines.Add($"Recommended for this week ({pickTasks.Count}):");
            foreach (var pick in pickTasks)
            {
                summaryLines.Add($"  [P{pick.Priority}] {pick.Title}");
                if (!string.IsNullOrWhiteSpace(pick.Reason))
                    summaryLines.Add($"        {pick.Reason}");
            }
        }

        await DisplayAlert("Priorities Updated", string.Join("\n", summaryLines), "OK");
        await RefreshChallengeWidgetAsync();
        await RefreshTasksAsync();
    }

    private async Task EditCommitmentTaskAsync(TaskItem task)
    {
        string? newTitle = await DisplayPromptAsync(
            "Edit Task",
            "Update the task text:",
            "Save",
            "Cancel",
            initialValue: task.Title);

        if (string.IsNullOrWhiteSpace(newTitle) || newTitle == task.Title) return;

        task.Title = newTitle.Trim();
        await _tasks.UpdateTaskAsync(task);
        await RefreshChallengeWidgetAsync();
        await RefreshTasksAsync();
    }

    private async Task<TaskItem?> ShowTaskPickerAsync(List<TaskItem> tasks, string categoryLabel)
    {
        var tcs = new TaskCompletionSource<TaskItem?>();
        var allTasks = new List<TaskItem>(tasks);
        var filteredTasks = new List<TaskItem>(allTasks);
        string currentSort = "date_desc";
        string currentSearch = "";

        var overlay = (new Grid { }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);

        var card = (new Frame
        {
            CornerRadius = 12,
            Padding = 0,
            HasShadow = true,
            WidthRequest = 480,
            MaximumHeightRequest = 600,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var cardStack = new VerticalStackLayout();

        // Header
        var header = (new Frame
        {
            Padding = 16,
            CornerRadius = 0,
            BorderColor = Colors.Transparent
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText);

        var headerStack = new VerticalStackLayout { Spacing = 4 };
        headerStack.Children.Add((new Label
        {
            Text = "Pick Task for This Week",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1));
        headerStack.Children.Add((new Label
        {
            Text = $"From: {categoryLabel}",
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightViolet, ThemeKey.PageText1));
        header.Content = headerStack;
        cardStack.Children.Add(header);

        // Search box
        var searchEntry = (new Entry
        {
            Placeholder = "Search tasks...",
            FontSize = 13,
            Margin = new Thickness(12, 8, 12, 0),
            HeightRequest = 38
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText3);
        cardStack.Children.Add(searchEntry);

        // Sort row
        var sortRow = new HorizontalStackLayout
        {
            Spacing = 6,
            Margin = new Thickness(12, 4, 12, 4)
        };

        var sortLabel = (new Label
        {
            Text = "Sort:",
            FontSize = 11,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3);
        sortRow.Children.Add(sortLabel);

        var sortDateBtn = (new Button
        {
            Text = "Newest ▼",
            FontSize = 10,
            HeightRequest = 28,
            Padding = new Thickness(8, 0),
            CornerRadius = 4
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

        var sortAlphaBtn = (new Button
        {
            Text = "A-Z",
            FontSize = 10,
            HeightRequest = 28,
            Padding = new Thickness(8, 0),
            CornerRadius = 4
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightViolet).ThemeColor(Button.TextColorProperty, ThemeKey.HighlightVioletText);

        var sortPriorityBtn = (new Button
        {
            Text = "Priority",
            FontSize = 10,
            HeightRequest = 28,
            Padding = new Thickness(8, 0),
            CornerRadius = 4
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightViolet).ThemeColor(Button.TextColorProperty, ThemeKey.HighlightVioletText);

        sortRow.Children.Add(sortDateBtn);
        sortRow.Children.Add(sortAlphaBtn);
        sortRow.Children.Add(sortPriorityBtn);

        var resultCountLabel = (new Label
        {
            Text = $"{filteredTasks.Count} tasks",
            FontSize = 10,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(8, 0, 0, 0)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3);
        sortRow.Children.Add(resultCountLabel);

        cardStack.Children.Add(sortRow);

        // Task list
        var taskList = new VerticalStackLayout { Padding = 8, Spacing = 4 };
        var scrollView = new ScrollView { MaximumHeightRequest = 350, Content = taskList };
        cardStack.Children.Add(scrollView);

        // Helper: update sort button styles
        void UpdateSortButtonStyles()
        {
            var active = ThemeKey.HighlightVioletText;
            var activeText = ThemeKey.ButtonPrimaryText;
            var inactive = ThemeKey.HighlightViolet;
            var inactiveText = ThemeKey.HighlightVioletText;

            sortDateBtn.ThemeColor(VisualElement.BackgroundColorProperty, currentSort.StartsWith("date") ? active : inactive);
            sortDateBtn.ThemeColor(Button.TextColorProperty, currentSort.StartsWith("date") ? activeText : inactiveText);
            sortDateBtn.Text = currentSort == "date_desc" ? "Newest ▼" : "Oldest ▲";

            sortAlphaBtn.ThemeColor(VisualElement.BackgroundColorProperty, currentSort.StartsWith("alpha") ? active : inactive);
            sortAlphaBtn.ThemeColor(Button.TextColorProperty, currentSort.StartsWith("alpha") ? activeText : inactiveText);
            sortAlphaBtn.Text = currentSort == "alpha_asc" ? "A-Z ▲" : currentSort == "alpha_desc" ? "Z-A ▼" : "A-Z";

            sortPriorityBtn.ThemeColor(VisualElement.BackgroundColorProperty, currentSort.StartsWith("priority") ? active : inactive);
            sortPriorityBtn.ThemeColor(Button.TextColorProperty, currentSort.StartsWith("priority") ? activeText : inactiveText);
            sortPriorityBtn.Text = currentSort == "priority_asc" ? "Priority ▲" : currentSort == "priority_desc" ? "Priority ▼" : "Priority";
        }

        // Helper: apply filter and sort
        void ApplyFilterAndSort()
        {
            var search = currentSearch.Trim();
            filteredTasks = string.IsNullOrEmpty(search)
                ? new List<TaskItem>(allTasks)
                : allTasks.Where(t =>
                    t.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    t.Notes.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            filteredTasks = currentSort switch
            {
                "date_desc" => filteredTasks.OrderByDescending(t => t.CreatedAt).ToList(),
                "date_asc" => filteredTasks.OrderBy(t => t.CreatedAt).ToList(),
                "alpha_asc" => filteredTasks.OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList(),
                "alpha_desc" => filteredTasks.OrderByDescending(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList(),
                "priority_asc" => filteredTasks.OrderByDescending(t => t.Priority).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList(),
                "priority_desc" => filteredTasks.OrderBy(t => t.Priority).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList(),
                _ => filteredTasks
            };

            resultCountLabel.Text = string.IsNullOrEmpty(search)
                ? $"{allTasks.Count} tasks"
                : $"{filteredTasks.Count} of {allTasks.Count} tasks";
        }

        // Helper: rebuild task list UI
        void RebuildTaskList()
        {
            taskList.Children.Clear();

            if (filteredTasks.Count == 0)
            {
                taskList.Children.Add((new Label
                {
                    Text = string.IsNullOrEmpty(currentSearch)
                        ? "No tasks available."
                        : "No tasks match your search.",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Italic,
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 20)
                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2));
                return;
            }

            // Split into top candidates and regular tasks
            var topCandidates = filteredTasks.Where(t => t.IsTopCandidate).ToList();
            var regularTasks = filteredTasks.Where(t => !t.IsTopCandidate).ToList();

            if (topCandidates.Count > 0)
            {
                taskList.Children.Add((new Label
                {
                    Text = "\u2B50 Top Candidates",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    Margin = new Thickness(4, 4, 0, 2)
                }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText1));

                foreach (var task in topCandidates)
                {
                    taskList.Children.Add(BuildPickerTaskCard(task, categoryLabel, overlay, tcs));
                }

                if (regularTasks.Count > 0)
                {
                    taskList.Children.Add((new BoxView
                    {
                        HeightRequest = 1,
                        Margin = new Thickness(0, 8)
                    }).ThemeColor(BoxView.ColorProperty, ThemeKey.Separator));

                    taskList.Children.Add((new Label
                    {
                        Text = "All Tasks",
                        FontSize = 13,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new Thickness(4, 0, 0, 2)
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText1));
                }
            }

            foreach (var task in regularTasks)
            {
                taskList.Children.Add(BuildPickerTaskCard(task, categoryLabel, overlay, tcs));
            }
        }

        searchEntry.TextChanged += (s, e) =>
        {
            currentSearch = e.NewTextValue ?? "";
            ApplyFilterAndSort();
            RebuildTaskList();
        };

        sortDateBtn.Clicked += (s, e) =>
        {
            currentSort = currentSort == "date_desc" ? "date_asc" : "date_desc";
            UpdateSortButtonStyles();
            ApplyFilterAndSort();
            RebuildTaskList();
        };

        sortAlphaBtn.Clicked += (s, e) =>
        {
            currentSort = currentSort == "alpha_asc" ? "alpha_desc" : "alpha_asc";
            UpdateSortButtonStyles();
            ApplyFilterAndSort();
            RebuildTaskList();
        };

        sortPriorityBtn.Clicked += (s, e) =>
        {
            currentSort = currentSort == "priority_asc" ? "priority_desc" : "priority_asc";
            UpdateSortButtonStyles();
            ApplyFilterAndSort();
            RebuildTaskList();
        };

        // Bottom buttons row
        var bottomRow = new HorizontalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(12, 4, 12, 12),
            HorizontalOptions = LayoutOptions.Fill
        };

        var createBtn = (new Button
        {
            Text = "+ New Task",
            FontSize = 12,
            CornerRadius = 6,
            HeightRequest = 36,
            Padding = new Thickness(12, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

        createBtn.Clicked += async (s, e) =>
        {
            string? title = await DisplayPromptAsync(
                "New Task",
                $"Create a new task in {(categoryLabel == "Any Category" ? "General" : categoryLabel)}:",
                "Create",
                "Cancel",
                maxLength: 200);

            if (string.IsNullOrWhiteSpace(title)) return;

            string category = categoryLabel == "Any Category" ? "General" : categoryLabel;

            string? priorityInput = await DisplayPromptAsync(
                "Priority",
                "Enter priority (higher = higher priority, 0 = lowest):",
                "Set", "Cancel",
                initialValue: "0",
                keyboard: Keyboard.Numeric);
            if (string.IsNullOrWhiteSpace(priorityInput)) return;
            if (!int.TryParse(priorityInput.Trim(), out int priority)
                || priority < 0)
                priority = 0;

            var newTask = await _tasks.CreateTaskAsync(
                _auth.CurrentUsername,
                title.Trim(),
                category,
                priority);

            bool markTop = await DisplayAlert(
                "Top Candidate?",
                "Mark this as a top candidate for focus picks?",
                "Yes",
                "No");

            if (markTop)
            {
                newTask.IsTopCandidate = true;
                await _tasks.UpdateTaskAsync(newTask);
            }

            string ideaText = title.Trim();
            if (_ideaLogger != null)
                await _ideaLogger.LogSilentlyAsync(new IdeaLogRequest
                {
                    Username = _auth.CurrentUsername,
                    Title = ideaText,
                    Body = ideaText,
                    Category = "all_tasks"
                });

            allTasks.Add(newTask);
            ApplyFilterAndSort();
            RebuildTaskList();
        };

        var cancelBtn = (new Button
        {
            Text = "Cancel",
            BackgroundColor = Colors.Transparent,
            FontSize = 14,
            HeightRequest = 36
        }).ThemeColor(Button.TextColorProperty, ThemeKey.HighlightVioletText);
        cancelBtn.Clicked += (s, e) =>
        {
            if (Content is Grid mainGrid)
                mainGrid.Children.Remove(overlay);
            tcs.TrySetResult(null);
        };

        bottomRow.Children.Add(createBtn);
        bottomRow.Children.Add(cancelBtn);
        cardStack.Children.Add(bottomRow);

        card.Content = cardStack;
        overlay.Children.Add(card);

        // Initial render
        ApplyFilterAndSort();
        UpdateSortButtonStyles();
        RebuildTaskList();

        if (Content is Grid grid)
        {
            grid.Children.Add(overlay);
        }
        else
        {
            var existingContent = Content;
            var newGrid = new Grid();
            newGrid.Children.Add(existingContent);
            newGrid.Children.Add(overlay);
            Content = newGrid;
        }

        return await tcs.Task;
    }

    private Frame BuildPickerTaskCard(
        TaskItem task,
        string categoryLabel,
        Grid overlay,
        TaskCompletionSource<TaskItem?> tcs)
    {
        var taskFrame = (new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            BorderColor = Colors.Transparent,
            HasShadow = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, task.IsTopCandidate ? ThemeKey.WarningBackground : ThemeKey.SecondarySurface);
        if (task.IsTopCandidate)
            taskFrame.ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.WarningButtonBackground);

        var taskStack = new HorizontalStackLayout { Spacing = 8 };

        taskStack.Children.Add((new Label
        {
            Text = task.Priority > 0 ? $"P{task.Priority}" : "P0",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightIndigoText, ThemeKey.PageText3));

        var textStack = new VerticalStackLayout { Spacing = 2 };
        textStack.Children.Add((new Label
        {
            Text = task.IsTopCandidate ? $"\u2B50 {task.Title}" : task.Title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1));

        if (categoryLabel == "Any Category")
        {
            textStack.Children.Add((new Label
            {
                Text = task.Category,
                FontSize = 12
            }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText3));
        }

        if (!string.IsNullOrWhiteSpace(task.Notes))
        {
            textStack.Children.Add((new Label
            {
                Text = task.Notes.Length > 60 ? task.Notes[..60] + "..." : task.Notes,
                FontSize = 11,
                LineBreakMode = LineBreakMode.TailTruncation
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3));
        }

        taskStack.Children.Add(textStack);
        taskFrame.Content = taskStack;

        var capturedTask = task;
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) =>
        {
            if (Content is Grid mainGrid)
                mainGrid.Children.Remove(overlay);
            tcs.TrySetResult(capturedTask);
        };
        taskFrame.GestureRecognizers.Add(tapGesture);

        return taskFrame;
    }

    #endregion
}
