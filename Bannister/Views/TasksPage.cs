using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Tasks page with compact data grid view and detail panel.
/// Includes weekly challenge widget with full functionality.
/// </summary>
public class TasksPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly TaskService _tasks;
    private readonly WeeklyChallengeService _challengeService;
    
    // UI
    private Label _headerLabel;
    private Picker _categoryPicker;
    private Entry _searchEntry;
    private Grid _dataGrid;
    private Frame _detailPanel;
    private Label _detailTitle;
    private Editor _detailNotes;
    private Label _detailMeta;
    private Button _showCompletedBtn;
    
    // Challenge UI
    private Frame _challengeFrame;
    private Label _challengeFocusLabel;
    private Label _challengeProgressLabel;
    private Label _challengeStreakLabel;
    private Label _challengeAllowanceLabel;
    private VerticalStackLayout _commitmentsList;
    private Button _addCommitmentBtn;
    private Button _startChallengeBtn;
    
    // State
    private List<string> _categories = new();
    private string _selectedCategory = "All";
    private bool _showingCompleted = false;
    private TaskItem? _selectedTask = null;
    private List<TaskItem> _currentTasks = new();
    private string _searchText = "";
    private bool _isLoading = false;

    // Grid config
    private const int ColumnCount = 4;
    private const double CellWidth = 180;
    private const double CellHeight = 50;

    public TasksPage(AuthService auth, TaskService tasks, WeeklyChallengeService challengeService)
    {
        _auth = auth;
        _tasks = tasks;
        _challengeService = challengeService;
        
        Title = "Tasks";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isLoading = true;
        await LoadCategoriesAsync();
        _isLoading = false;
        await RefreshChallengeWidgetAsync();
        await RefreshTasksAsync();
    }

    private void BuildUI()
    {
        var rootGrid = new Grid();

        var mainGrid = new Grid
        {
            Padding = 12,
            RowSpacing = 8,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),  // Header + Controls
                new RowDefinition(GridLength.Auto),  // Challenge widget
                new RowDefinition(GridLength.Star)   // Content
            }
        };

        // Header row
        var headerRow = new HorizontalStackLayout { Spacing = 10 };

        headerRow.Children.Add(new Label
        {
            Text = "📋",
            FontSize = 20,
            VerticalOptions = LayoutOptions.Center
        });

        _headerLabel = new Label
        {
            Text = "0 tasks",
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        };
        headerRow.Children.Add(_headerLabel);

        _categoryPicker = new Picker
        {
            Title = "Category",
            BackgroundColor = Colors.White,
            WidthRequest = 120
        };
        _categoryPicker.SelectedIndexChanged += OnCategoryChanged;
        headerRow.Children.Add(_categoryPicker);

        _searchEntry = new Entry
        {
            Placeholder = "🔍 Search...",
            BackgroundColor = Colors.White,
            WidthRequest = 140
        };
        _searchEntry.TextChanged += OnSearchChanged;
        headerRow.Children.Add(_searchEntry);

        var addBtn = new Button
        {
            Text = "+ New",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 6,
            HeightRequest = 36,
            Padding = new Thickness(12, 0)
        };
        addBtn.Clicked += OnAddTaskClicked;
        headerRow.Children.Add(addBtn);

        _showCompletedBtn = new Button
        {
            Text = "✓",
            BackgroundColor = Color.FromArgb("#9E9E9E"),
            TextColor = Colors.White,
            CornerRadius = 6,
            WidthRequest = 36,
            HeightRequest = 36
        };
        _showCompletedBtn.Clicked += OnToggleCompletedClicked;
        headerRow.Children.Add(_showCompletedBtn);

        Grid.SetRow(headerRow, 0);
        mainGrid.Children.Add(headerRow);

        // Challenge widget row
        BuildChallengeWidget(mainGrid);

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

        // Data grid
        var gridScroll = new ScrollView { Orientation = ScrollOrientation.Both };
        _dataGrid = new Grid
        {
            ColumnSpacing = 1,
            RowSpacing = 1,
            BackgroundColor = Color.FromArgb("#DDD"),
            Padding = 1
        };
        gridScroll.Content = _dataGrid;
        Grid.SetColumn(gridScroll, 0);
        contentGrid.Children.Add(gridScroll);

        // Detail panel
        _detailPanel = new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#1976D2"),
            HasShadow = true,
            IsVisible = false
        };

        var detailStack = new VerticalStackLayout { Spacing = 10 };

        var closeRow = new HorizontalStackLayout();
        closeRow.Children.Add(new Label
        {
            Text = "📄 Details",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1976D2"),
            HorizontalOptions = LayoutOptions.StartAndExpand,
            VerticalOptions = LayoutOptions.Center
        });
        var closeBtn = new Button
        {
            Text = "✕",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#999"),
            WidthRequest = 30,
            HeightRequest = 30,
            Padding = 0
        };
        closeBtn.Clicked += (s, e) => { _detailPanel.IsVisible = false; _selectedTask = null; };
        closeRow.Children.Add(closeBtn);
        detailStack.Children.Add(closeRow);

        _detailTitle = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        detailStack.Children.Add(_detailTitle);

        _detailMeta = new Label
        {
            FontSize = 10,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        detailStack.Children.Add(_detailMeta);

        _detailNotes = new Editor
        {
            Placeholder = "Notes...",
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            HeightRequest = 120,
            FontSize = 12
        };
        _detailNotes.Unfocused += OnDetailNotesSave;
        detailStack.Children.Add(_detailNotes);

        // Action buttons
        var actions = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        var btnEdit = MiniBtn("✏️", "#2196F3"); btnEdit.Clicked += OnEditClicked;
        var btnComplete = MiniBtn("✅", "#4CAF50"); btnComplete.Clicked += OnCompleteClicked;
        var btnPriority = MiniBtn("⚡", "#FF9800"); btnPriority.Clicked += OnPriorityClicked;
        var btnMove = MiniBtn("📂", "#9C27B0"); btnMove.Clicked += OnMoveClicked;
        var btnDelete = MiniBtn("🗑️", "#F44336"); btnDelete.Clicked += OnDeleteClicked;
        actions.Children.Add(btnEdit);
        actions.Children.Add(btnComplete);
        actions.Children.Add(btnPriority);
        actions.Children.Add(btnMove);
        actions.Children.Add(btnDelete);
        detailStack.Children.Add(actions);

        _detailPanel.Content = detailStack;
        Grid.SetColumn(_detailPanel, 1);
        contentGrid.Children.Add(_detailPanel);

        Grid.SetRow(contentGrid, 2);
        mainGrid.Children.Add(contentGrid);

        rootGrid.Children.Add(mainGrid);
        Content = rootGrid;
    }

    private void BuildChallengeWidget(Grid mainGrid)
    {
        var challengeRow = new HorizontalStackLayout { Spacing = 8 };

        // Start challenge button
        _startChallengeBtn = new Button
        {
            Text = "🎯 Start Weekly Challenge",
            BackgroundColor = Color.FromArgb("#E1BEE7"),
            TextColor = Color.FromArgb("#7B1FA2"),
            CornerRadius = 6,
            HeightRequest = 36,
            IsVisible = true
        };
        _startChallengeBtn.Clicked += OnStartChallengeClicked;
        challengeRow.Children.Add(_startChallengeBtn);

        // Challenge frame (compact)
        _challengeFrame = new Frame
        {
            Padding = 8,
            CornerRadius = 8,
            BackgroundColor = Color.FromArgb("#F3E5F5"),
            BorderColor = Color.FromArgb("#7B1FA2"),
            IsVisible = false,
            HorizontalOptions = LayoutOptions.Fill
        };

        var challengeStack = new VerticalStackLayout { Spacing = 4 };

        var headerRow = new HorizontalStackLayout { Spacing = 8 };
        headerRow.Children.Add(new Label
        {
            Text = "🎯",
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center
        });
        _challengeFocusLabel = new Label
        {
            Text = "Focus: Marketing",
            FontSize = 12,
            TextColor = Color.FromArgb("#7B1FA2"),
            VerticalOptions = LayoutOptions.Center
        };
        headerRow.Children.Add(_challengeFocusLabel);
        
        _challengeAllowanceLabel = new Label
        {
            Text = "📊 1/week",
            FontSize = 10,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        };
        headerRow.Children.Add(_challengeAllowanceLabel);
        
        _challengeStreakLabel = new Label
        {
            Text = "🔥 0",
            FontSize = 10,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        };
        headerRow.Children.Add(_challengeStreakLabel);

        var settingsBtn = new Button
        {
            Text = "⚙️",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#7B1FA2"),
            WidthRequest = 30,
            HeightRequest = 30,
            Padding = 0
        };
        settingsBtn.Clicked += OnChallengeSettingsClicked;
        headerRow.Children.Add(settingsBtn);

        challengeStack.Children.Add(headerRow);

        _challengeProgressLabel = new Label
        {
            Text = "This week: 0/1",
            FontSize = 11,
            TextColor = Color.FromArgb("#333")
        };
        challengeStack.Children.Add(_challengeProgressLabel);

        _commitmentsList = new VerticalStackLayout { Spacing = 2 };
        challengeStack.Children.Add(_commitmentsList);

        _addCommitmentBtn = new Button
        {
            Text = "+ Pick Task",
            BackgroundColor = Color.FromArgb("#7B1FA2"),
            TextColor = Colors.White,
            FontSize = 11,
            CornerRadius = 4,
            HeightRequest = 28,
            Padding = new Thickness(8, 0)
        };
        _addCommitmentBtn.Clicked += OnAddCommitmentClicked;
        challengeStack.Children.Add(_addCommitmentBtn);

        _challengeFrame.Content = challengeStack;
        challengeRow.Children.Add(_challengeFrame);

        Grid.SetRow(challengeRow, 1);
        mainGrid.Children.Add(challengeRow);
    }

    private Button MiniBtn(string text, string color) => new Button
    {
        Text = text,
        BackgroundColor = Color.FromArgb(color),
        TextColor = Colors.White,
        CornerRadius = 4,
        WidthRequest = 36,
        HeightRequest = 32,
        Padding = 0,
        Margin = new Thickness(0, 0, 4, 4)
    };

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
        _showCompletedBtn.BackgroundColor = _showingCompleted ? Color.FromArgb("#4CAF50") : Color.FromArgb("#9E9E9E");
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
        _dataGrid.Children.Clear();
        _dataGrid.RowDefinitions.Clear();
        _dataGrid.ColumnDefinitions.Clear();

        if (tasks.Count == 0)
        {
            _dataGrid.BackgroundColor = Colors.Transparent;
            _dataGrid.Children.Add(new Label
            {
                Text = "No tasks. Click + New to add one.",
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(20)
            });
            return;
        }

        _dataGrid.BackgroundColor = Color.FromArgb("#DDD");

        int cols = ColumnCount;
        int rows = (int)Math.Ceiling((double)tasks.Count / cols);

        for (int c = 0; c < cols; c++)
            _dataGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(CellWidth)));
        for (int r = 0; r < rows; r++)
            _dataGrid.RowDefinitions.Add(new RowDefinition(new GridLength(CellHeight)));

        for (int i = 0; i < tasks.Count; i++)
        {
            int row = i / cols;
            int col = i % cols;
            var cell = BuildCell(tasks[i]);
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, col);
            _dataGrid.Children.Add(cell);
        }
    }

    private Frame BuildCell(TaskItem task)
    {
        Color bg = task.IsCompleted ? Color.FromArgb("#E8F5E9")
            : task.IsOverdue ? Color.FromArgb("#FFEBEE")
            : task.Priority == 0 ? Color.FromArgb("#F3E5F5")
            : Colors.White;

        var frame = new Frame
        {
            Padding = 4,
            CornerRadius = 0,
            BackgroundColor = bg,
            BorderColor = Colors.Transparent,
            HasShadow = false
        };

        var stack = new VerticalStackLayout { Spacing = 1 };

        // Icons
        var icons = new HorizontalStackLayout { Spacing = 2 };
        if (task.Priority == 0) icons.Children.Add(new Label { Text = "🟣", FontSize = 8 });
        else if (task.Priority == 1) icons.Children.Add(new Label { Text = "🔴", FontSize = 8 });
        if (task.IsCompleted) icons.Children.Add(new Label { Text = "✓", FontSize = 8, TextColor = Color.FromArgb("#4CAF50") });
        if (task.IsOverdue) icons.Children.Add(new Label { Text = "!", FontSize = 8, TextColor = Colors.Red, FontAttributes = FontAttributes.Bold });
        if (icons.Children.Count > 0) stack.Children.Add(icons);

        // Title
        var title = task.Title.Length > 30 ? task.Title.Substring(0, 30) + "…" : task.Title;
        stack.Children.Add(new Label
        {
            Text = title,
            FontSize = 10,
            TextColor = task.IsCompleted ? Color.FromArgb("#999") : Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        });

        // Category
        stack.Children.Add(new Label
        {
            Text = task.Category,
            FontSize = 8,
            TextColor = Color.FromArgb("#999")
        });

        frame.Content = stack;

        var tap = new TapGestureRecognizer();
        var captured = task;
        tap.Tapped += (s, e) => ShowDetail(captured);
        frame.GestureRecognizers.Add(tap);

        return frame;
    }

    private void ShowDetail(TaskItem task)
    {
        _selectedTask = task;
        _detailPanel.IsVisible = true;

        _detailTitle.Text = task.Title;
        _detailNotes.Text = task.Notes ?? "";

        var meta = $"📁 {task.Category}";
        string priorityIcon = task.Priority == 0 ? "🟣 Urgent" : task.Priority == 1 ? "🔴 High" : task.Priority == 3 ? "🟢 Low" : "🟡 Medium";
        meta += $" • {priorityIcon}";
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
        string? title = await DisplayPromptAsync("New Task", "What do you need to do?", "Next", "Cancel");
        if (string.IsNullOrWhiteSpace(title)) return;

        string? category = await AskForCategoryAsync();
        if (category == null) return;

        string? priorityChoice = await DisplayActionSheet("Priority", "Cancel", null,
            "🟣 Urgent", "🔴 High", "🟡 Medium", "🟢 Low");
        if (priorityChoice == "Cancel" || string.IsNullOrEmpty(priorityChoice)) return;

        int priority = priorityChoice.Contains("Urgent") ? 0 : priorityChoice.Contains("High") ? 1 : priorityChoice.Contains("Low") ? 3 : 2;

        await _tasks.CreateTaskAsync(_auth.CurrentUsername, title.Trim(), category, priority);
        await LoadCategoriesAsync();
        await RefreshTasksAsync();
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
        string? t = await DisplayPromptAsync("Edit", "Update task:", "Save", "Cancel", initialValue: _selectedTask.Title);
        if (!string.IsNullOrWhiteSpace(t) && t != _selectedTask.Title)
        {
            _selectedTask.Title = t.Trim();
            await _tasks.UpdateTaskAsync(_selectedTask);
            _detailTitle.Text = _selectedTask.Title;
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
        string? choice = await DisplayActionSheet("Priority", "Cancel", null,
            "🟣 Urgent", "🔴 High", "🟡 Medium", "🟢 Low");
        if (choice == "Cancel" || string.IsNullOrEmpty(choice)) return;

        _selectedTask.Priority = choice.Contains("Urgent") ? 0 : choice.Contains("High") ? 1 : choice.Contains("Low") ? 3 : 2;
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

    private async Task RefreshChallengeWidgetAsync()
    {
        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);

        if (challenge == null)
        {
            _challengeFrame.IsVisible = false;
            _startChallengeBtn.IsVisible = true;
            return;
        }

        await _challengeService.ProcessWeekEndAsync(_auth.CurrentUsername);
        challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (challenge == null) return;

        _challengeFrame.IsVisible = true;
        _startChallengeBtn.IsVisible = false;

        _challengeFocusLabel.Text = $"Focus: {challenge.FocusCategory} ({challenge.RemainingFocusTasks})";
        _challengeAllowanceLabel.Text = $"📊 {challenge.CurrentAllowance}/wk";
        _challengeStreakLabel.Text = $"🔥 {challenge.SuccessStreak}";

        var commitments = await _challengeService.GetCurrentWeekCommitmentsAsync(challenge.Id);
        int completed = commitments.Count(c => c.IsCompleted);
        _challengeProgressLabel.Text = $"This week: {completed}/{challenge.CurrentAllowance}";

        _commitmentsList.Children.Clear();
        foreach (var commitment in commitments)
        {
            var task = (await _tasks.GetActiveTasksAsync(_auth.CurrentUsername))
                .Concat(await _tasks.GetCompletedTasksAsync(_auth.CurrentUsername))
                .FirstOrDefault(t => t.Id == commitment.TaskId);

            if (task != null)
            {
                var row = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    Padding = new Thickness(4, 2),
                    BackgroundColor = commitment.IsCompleted ? Color.FromArgb("#E8F5E9") : Colors.White
                };

                var cb = new CheckBox
                {
                    IsChecked = commitment.IsCompleted,
                    Color = Color.FromArgb("#4CAF50"),
                    IsEnabled = !commitment.IsCompleted,
                    Scale = 0.8
                };
                var capturedTask = task;
                var capturedCommitment = commitment;
                cb.CheckedChanged += async (s, e) =>
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

                var lbl = new Label
                {
                    Text = task.Title + (commitment.IsFocusTask ? "" : " 🌈"),
                    FontSize = 10,
                    TextColor = commitment.IsCompleted ? Color.FromArgb("#999") : Color.FromArgb("#333"),
                    TextDecorations = commitment.IsCompleted ? TextDecorations.Strikethrough : TextDecorations.None,
                    VerticalOptions = LayoutOptions.Center,
                    LineBreakMode = LineBreakMode.WordWrap
                };
                
                // Add tap to edit
                if (!commitment.IsCompleted)
                {
                    var editTap = new TapGestureRecognizer();
                    editTap.Tapped += async (s, e) =>
                    {
                        await EditCommitmentTaskAsync(capturedTask);
                    };
                    lbl.GestureRecognizers.Add(editTap);
                }
                
                Grid.SetColumn(lbl, 1);
                row.Children.Add(lbl);

                if (!commitment.IsCompleted)
                {
                    var actionsStack = new HorizontalStackLayout { Spacing = 0 };
                    
                    var editBtn = new Button
                    {
                        Text = "✏️",
                        BackgroundColor = Colors.Transparent,
                        TextColor = Color.FromArgb("#5B63EE"),
                        WidthRequest = 26,
                        HeightRequest = 26,
                        Padding = 0,
                        FontSize = 10
                    };
                    editBtn.Clicked += async (s, e) =>
                    {
                        await EditCommitmentTaskAsync(capturedTask);
                    };
                    actionsStack.Children.Add(editBtn);
                    
                    var removeBtn = new Button
                    {
                        Text = "✕",
                        BackgroundColor = Colors.Transparent,
                        TextColor = Color.FromArgb("#999"),
                        WidthRequest = 26,
                        HeightRequest = 26,
                        Padding = 0,
                        FontSize = 10
                    };
                    var capturedId = commitment.Id;
                    removeBtn.Clicked += async (s, e) =>
                    {
                        await _challengeService.RemoveCommitmentAsync(capturedId);
                        await RefreshChallengeWidgetAsync();
                    };
                    actionsStack.Children.Add(removeBtn);
                    
                    Grid.SetColumn(actionsStack, 2);
                    row.Children.Add(actionsStack);
                }

                _commitmentsList.Children.Add(row);
            }
        }

        _addCommitmentBtn.IsVisible = commitments.Count < challenge.CurrentAllowance;
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
        string action = await DisplayActionSheet("Challenge Settings", "Cancel", "End Challenge", "View Stats");

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
    }

    private async void OnAddCommitmentClicked(object? sender, EventArgs e)
    {
        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (challenge == null) return;

        var commitments = await _challengeService.GetCurrentWeekCommitmentsAsync(challenge.Id);
        if (commitments.Count >= challenge.CurrentAllowance)
        {
            await DisplayAlert("Full", "You've already picked all tasks for this week.", "OK");
            return;
        }

        int nextSlot = commitments.Count + 1;
        bool canBeNonFocus = nextSlot % 3 == 0;

        List<string> options = new() { "Focus: " + challenge.FocusCategory };
        if (canBeNonFocus) options.Add("Any Category 🌈");

        string? choice = options.Count == 1 ? options[0]
            : await DisplayActionSheet("Pick from", "Cancel", null, options.ToArray());
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        bool pickingFocus = choice.StartsWith("Focus:");

        List<TaskItem> availableTasks = pickingFocus
            ? await _challengeService.GetAvailableFocusTasksAsync(_auth.CurrentUsername, challenge.FocusCategory)
            : await _challengeService.GetAvailableNonFocusTasksAsync(_auth.CurrentUsername, challenge.FocusCategory);

        if (availableTasks.Count == 0)
        {
            await DisplayAlert("No Tasks", pickingFocus
                ? $"No available tasks in {challenge.FocusCategory}."
                : "No available tasks in other categories.", "OK");
            return;
        }

        var selectedTask = await ShowTaskPickerAsync(availableTasks, pickingFocus ? challenge.FocusCategory : "Any Category");
        if (selectedTask == null) return;

        await _challengeService.AddCommitmentAsync(challenge.Id, selectedTask.Id, pickingFocus);
        await RefreshChallengeWidgetAsync();
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
        
        // Create modal overlay
        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000")
        };
        
        var card = new Frame
        {
            CornerRadius = 12,
            Padding = 0,
            BackgroundColor = Colors.White,
            HasShadow = true,
            WidthRequest = 400,
            MaximumHeightRequest = 500,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        
        var cardStack = new VerticalStackLayout();
        
        // Header
        var header = new Frame
        {
            Padding = 16,
            CornerRadius = 0,
            BackgroundColor = Color.FromArgb("#7B1FA2"),
            BorderColor = Colors.Transparent
        };
        
        var headerStack = new VerticalStackLayout { Spacing = 4 };
        headerStack.Children.Add(new Label
        {
            Text = "Pick Task for This Week",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });
        headerStack.Children.Add(new Label
        {
            Text = $"From: {categoryLabel}",
            FontSize = 13,
            TextColor = Color.FromArgb("#E1BEE7")
        });
        header.Content = headerStack;
        cardStack.Children.Add(header);
        
        // Scrollable task list
        var scrollView = new ScrollView
        {
            MaximumHeightRequest = 350
        };
        
        var taskList = new VerticalStackLayout
        {
            Padding = 8,
            Spacing = 4
        };
        
        foreach (var task in tasks)
        {
            var taskFrame = new Frame
            {
                Padding = 12,
                CornerRadius = 8,
                BackgroundColor = Color.FromArgb("#F5F5F5"),
                BorderColor = Colors.Transparent,
                HasShadow = false
            };
            
            var taskStack = new VerticalStackLayout { Spacing = 2 };
            
            taskStack.Children.Add(new Label
            {
                Text = task.Title,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#333"),
                LineBreakMode = LineBreakMode.WordWrap
            });
            
            // Show category if picking from "any"
            if (categoryLabel == "Any Category")
            {
                taskStack.Children.Add(new Label
                {
                    Text = task.Category,
                    FontSize = 12,
                    TextColor = Color.FromArgb("#7B1FA2")
                });
            }
            
            // Show notes preview if exists
            if (!string.IsNullOrWhiteSpace(task.Notes))
            {
                taskStack.Children.Add(new Label
                {
                    Text = task.Notes.Length > 60 ? task.Notes.Substring(0, 60) + "..." : task.Notes,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#999"),
                    LineBreakMode = LineBreakMode.TailTruncation
                });
            }
            
            taskFrame.Content = taskStack;
            
            var tapGesture = new TapGestureRecognizer();
            var capturedTask = task;
            tapGesture.Tapped += (s, e) =>
            {
                if (Content is Grid mainGrid)
                {
                    mainGrid.Children.Remove(overlay);
                }
                tcs.TrySetResult(capturedTask);
            };
            taskFrame.GestureRecognizers.Add(tapGesture);
            
            taskList.Children.Add(taskFrame);
        }
        
        scrollView.Content = taskList;
        cardStack.Children.Add(scrollView);
        
        // Cancel button
        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#7B1FA2"),
            FontSize = 14,
            HeightRequest = 48
        };
        cancelBtn.Clicked += (s, e) =>
        {
            if (Content is Grid mainGrid)
            {
                mainGrid.Children.Remove(overlay);
            }
            tcs.TrySetResult(null);
        };
        cardStack.Children.Add(cancelBtn);
        
        card.Content = cardStack;
        overlay.Children.Add(card);
        
        // Add overlay to page
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

    #endregion
}
