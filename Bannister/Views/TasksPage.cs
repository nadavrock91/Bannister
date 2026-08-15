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
    private readonly IdeasService? _ideasService;
    
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
    
    // Challenge UI
    private Frame _challengeFrame;
    private Label _challengeFocusLabel;
    private Label _challengeProgressLabel;
    private Label _challengeStreakLabel;
    private Label _challengeAllowanceLabel;
    private VerticalStackLayout _allowanceChartContainer;
    private VerticalStackLayout _commitmentsList;
    private VerticalStackLayout _topCandidatesList;
    private Button _addCommitmentBtn;
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

    public TasksPage(AuthService auth, TaskService tasks, WeeklyChallengeService challengeService, IdeasService? ideasService = null)
    {
        _auth = auth;
        _tasks = tasks;
        _challengeService = challengeService;
        _ideasService = ideasService;
        
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

        // Data grid, matching DatabasesPage: toolbar fixed above scrollable grid.
        var gridArea = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };

        _gridToolbarContainer = new VerticalStackLayout
        {
            Padding = new Thickness(0, 0, 0, 4)
        };
        gridArea.Add(_gridToolbarContainer, 0, 0);

        var gridScroll = new ScrollView { Orientation = ScrollOrientation.Both };
        _gridContainer = new VerticalStackLayout { Spacing = 4 };
        gridScroll.Content = _gridContainer;
        gridArea.Add(gridScroll, 0, 1);

        Grid.SetColumn(gridArea, 0);
        contentGrid.Children.Add(gridArea);

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
        var challengeRow = new VerticalStackLayout { Spacing = 8 };

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

        _allowanceChartContainer = new VerticalStackLayout
        {
            Spacing = 4,
            Margin = new Thickness(0, 0, 0, 8),
            IsVisible = false
        };
        challengeRow.Children.Add(_allowanceChartContainer);

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

        _topCandidatesList = new VerticalStackLayout { Spacing = 2 };
        challengeStack.Children.Add(_topCandidatesList);

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

        _addTopCandidateBtn = new Button
        {
            Text = "+ Top Candidate",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            FontSize = 11,
            CornerRadius = 4,
            HeightRequest = 28,
            Padding = new Thickness(8, 0),
            IsVisible = false
        };
        _addTopCandidateBtn.Clicked += async (_, _) => await AddTopCandidateAsync();
        challengeStack.Children.Add(_addTopCandidateBtn);

        _consultLlmBtn = new Button
        {
            Text = "Consult LLM",
            BackgroundColor = Color.FromArgb("#E1BEE7"),
            TextColor = Color.FromArgb("#7B1FA2"),
            FontSize = 11,
            CornerRadius = 4,
            HeightRequest = 28,
            Padding = new Thickness(8, 0),
            IsVisible = false
        };
        _consultLlmBtn.Clicked += async (_, _) => await ConsultLlmForPrioritizationAsync();
        challengeStack.Children.Add(_consultLlmBtn);

        _challengeFrame.Content = challengeStack;
        challengeRow.Children.Add(_challengeFrame);

        var challengeScroll = new ScrollView
        {
            Content = challengeRow,
            Orientation = ScrollOrientation.Vertical,
            MaximumHeightRequest = 360
        };

        Grid.SetRow(challengeScroll, 1);
        mainGrid.Children.Add(challengeScroll);
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
        _gridToolbarContainer.Children.Clear();
        _gridContainer.Children.Clear();

        if (tasks.Count == 0)
        {
            _gridContainer.Children.Add(new Label
            {
                Text = "No tasks. Click + New to add one.",
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(20)
            });
            return;
        }

        var headers = new List<string> { "Id", "Status", "Title", "Category", "Priority", "DueDate", "Notes", "CreatedAt", "CompletedAt" };
        var displayRows = new List<List<string>>();
        var fullRows = new List<List<string>>();

        foreach (var task in tasks)
        {
            var row = BuildTaskGridRow(task);
            fullRows.Add(row);
            displayRows.Add(row.Select(v => v.Length > 50 ? v.Substring(0, 47) + "..." : v).ToList());
        }

        var dataGrid = DataGridView.Create(headers, displayRows)
            .WithHeaderStyle(Color.FromArgb("#5B63EE"), Colors.White)
            .WithAlternateRowColor(Color.FromArgb("#F8F9FF"))
            .WithColumnWidths(60, 220)
            .WithCellPadding(6)
            .WithFontSize(12, 12)
            .WithFullRows(fullRows)
            .WithIdColumn("Id")
            .OnCellTapped((s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _currentTasks.Count)
                    ShowDetail(_currentTasks[e.RowIndex]);
            })
            .WithUpdateCallback(UpdateTaskGridCellAsync)
            .Build();

        _gridToolbarContainer.Children.Add(dataGrid.ToolbarView);
        _gridContainer.Children.Add(dataGrid.GridView);
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

    private static List<string> BuildTaskGridRow(TaskItem task)
    {
        return new List<string>
        {
            task.Id.ToString(),
            task.IsCompleted ? "Done" : task.IsOverdue ? "Overdue" : task.IsDueToday ? "Today" : "Open",
            task.Title,
            task.Category,
            task.Priority switch { 0 => "Urgent", 1 => "High", 3 => "Low", _ => "Medium" },
            task.DueDate?.ToString("yyyy-MM-dd") ?? "",
            task.Notes ?? "",
            task.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            task.CompletedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? ""
        };
    }

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
        if (int.TryParse(value, out int numeric))
        {
            return Math.Clamp(numeric, 0, 3);
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "urgent" => 0,
            "high" => 1,
            "low" => 3,
            _ => 2
        };
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

        if (_ideasService != null)
            try { await _ideasService.CreateIdeaAsync(_auth.CurrentUsername, $"[New] {title.Trim()} ({category})", "tasks_ideas", fullIdea: $"[New] {title.Trim()} ({category})"); } catch { }

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
        string originalTitle = _selectedTask.Title;
        string? t = await DisplayPromptAsync("Edit", "Update task:", "Save", "Cancel", initialValue: _selectedTask.Title);
        if (!string.IsNullOrWhiteSpace(t) && t != _selectedTask.Title)
        {
            _selectedTask.Title = t.Trim();
            await _tasks.UpdateTaskAsync(_selectedTask);
            _detailTitle.Text = _selectedTask.Title;

            if (_ideasService != null)
                try { await _ideasService.CreateIdeaAsync(_auth.CurrentUsername, $"[Edited] \"{originalTitle}\" → \"{t.Trim()}\"", "tasks_ideas", fullIdea: $"[Edited] \"{originalTitle}\" → \"{t.Trim()}\""); } catch { }

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

    private async Task RefreshChallengeWidgetAsync(bool processWeekEnd = true)
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
            return;
        }

        if (processWeekEnd)
        {
            await _challengeService.ProcessWeekEndAsync(_auth.CurrentUsername);
            challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
            if (challenge == null)
            {
                _challengeFrame.IsVisible = false;
                _startChallengeBtn.IsVisible = true;
                _allowanceChartContainer.IsVisible = false;
                _topCandidatesList.Children.Clear();
                _addTopCandidateBtn.IsVisible = false;
                _consultLlmBtn.IsVisible = false;
                return;
            }
        }

        await RefreshAllowanceChartAsync();

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

        // Show top candidates
        _topCandidatesList.Children.Clear();

        var committedTaskIds = commitments.Select(c => c.TaskId).ToHashSet();
        var topCandidates = (await _tasks.GetActiveTasksAsync(_auth.CurrentUsername))
            .Where(t => t.IsTopCandidate &&
                   string.Equals(t.Category, challenge.FocusCategory, StringComparison.OrdinalIgnoreCase) &&
                   !committedTaskIds.Contains(t.Id))
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (topCandidates.Count > 0)
        {
            var headerLabel = new Label
            {
                Text = _topCandidatesExpanded
                    ? $"\u25BC \u2B50 Top Candidates ({topCandidates.Count})"
                    : $"\u25B6 \u2B50 Top Candidates ({topCandidates.Count})",
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#FF9800"),
                Margin = new Thickness(0, 4, 0, 0)
            };

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
                    var row = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        Padding = new Thickness(4, 2),
                        BackgroundColor = Color.FromArgb("#FFF3E0")
                    };

                    string priorityDot = task.Priority switch
                    {
                        1 => "\U0001F534",
                        3 => "\U0001F7E2",
                        _ => "\U0001F7E1"
                    };

                    var lbl = new Label
                    {
                        Text = $"{priorityDot} {task.Title}",
                        FontSize = 10,
                        TextColor = Color.FromArgb("#333"),
                        VerticalOptions = LayoutOptions.Center,
                        LineBreakMode = LineBreakMode.WordWrap
                    };
                    Grid.SetColumn(lbl, 0);
                    row.Children.Add(lbl);

                    var actionsStack = new HorizontalStackLayout { Spacing = 0 };

                    // Remove top candidate flag
                    var unstarBtn = new Button
                    {
                        Text = "\u2B50",
                        BackgroundColor = Colors.Transparent,
                        TextColor = Color.FromArgb("#FF9800"),
                        WidthRequest = 26,
                        HeightRequest = 26,
                        Padding = 0,
                        FontSize = 10
                    };
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
        _consultLlmBtn.IsVisible = true;
    }

    private async Task RefreshAllowanceChartAsync()
    {
        _allowanceChartContainer.Children.Clear();

        var history = await _challengeService.GetChallengeHistoryAsync(_auth.CurrentUsername, 12);

        if (history.Count == 0)
        {
            _allowanceChartContainer.IsVisible = true;
            _allowanceChartContainer.Children.Add(new Label
            {
                Text = "Focus Allowance History",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#7B1FA2")
            });
            _allowanceChartContainer.Children.Add(new Label
            {
                Text = "No challenge history yet. Complete your first week to see the chart.",
                FontSize = 12,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic
            });
            return;
        }

        _allowanceChartContainer.IsVisible = true;

        // Header
        _allowanceChartContainer.Children.Add(new Label
        {
            Text = "Focus Allowance History",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#7B1FA2")
        });

        // Chart area
        var chartFrame = new Frame
        {
            Padding = 12,
            CornerRadius = 10,
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            BorderColor = Colors.Transparent,
            HasShadow = false
        };

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

        // Reverse so oldest is on the left
        history.Reverse();

        int maxAllowance = history.Max(c => c.CurrentAllowance);
        if (maxAllowance < 1) maxAllowance = 1;
        double barMaxHeight = 80;

        for (int i = 0; i < history.Count; i++)
        {
            chartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            var challenge = history[i];
            double barHeight = Math.Max(8, (challenge.CurrentAllowance / (double)maxAllowance) * barMaxHeight);

            // Determine bar color based on success
            bool isSuccess = challenge.SuccessStreak > 0 || challenge.CompletedFocusTaskCount >= challenge.TargetTaskCount;
            Color barColor = isSuccess
                ? Color.FromArgb("#7B1FA2")
                : Color.FromArgb("#CE93D8");

            bool isCurrent = challenge.IsActive;

            var barStack = new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.End,
                HorizontalOptions = LayoutOptions.Center
            };

            // Value label above bar
            barStack.Children.Add(new Label
            {
                Text = challenge.CurrentAllowance.ToString(),
                FontSize = 9,
                TextColor = Color.FromArgb("#666"),
                HorizontalTextAlignment = TextAlignment.Center
            });

            // Bar
            var bar = new BoxView
            {
                HeightRequest = barHeight,
                WidthRequest = 20,
                CornerRadius = 4,
                Color = isCurrent ? Color.FromArgb("#4CAF50") : barColor,
                HorizontalOptions = LayoutOptions.Center
            };
            barStack.Children.Add(bar);

            Grid.SetColumn(barStack, i);
            Grid.SetRow(barStack, 0);
            chartGrid.Children.Add(barStack);

            // Challenge start label below
            var weekLabel = new Label
            {
                Text = challenge.StartedAt.ToString("M/d"),
                FontSize = 8,
                TextColor = Color.FromArgb("#999"),
                HorizontalTextAlignment = TextAlignment.Center
            };
            Grid.SetColumn(weekLabel, i);
            Grid.SetRow(weekLabel, 1);
            chartGrid.Children.Add(weekLabel);
        }

        chartFrame.Content = chartGrid;
        _allowanceChartContainer.Children.Add(chartFrame);

        // Summary line
        var current = history.LastOrDefault();
        if (current != null)
        {
            _allowanceChartContainer.Children.Add(new Label
            {
                Text = $"Current: {current.CurrentAllowance}/wk \u2022 Streak: {current.SuccessStreak} weeks",
                FontSize = 11,
                TextColor = Color.FromArgb("#888")
            });
        }
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

    private async Task AddTopCandidateAsync()
    {
        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (challenge == null) return;

        string? title = await DisplayPromptAsync(
            "New Top Candidate",
            $"Create a task that will appear at the top when picking focus tasks.\n\nIt will be added to {challenge.FocusCategory} and also as a normal task.",
            "Create",
            "Cancel",
            maxLength: 200);

        if (string.IsNullOrWhiteSpace(title)) return;

        string? priorityChoice = await DisplayActionSheet(
            "Priority",
            "Cancel",
            null,
            "\U0001F534 High", "\U0001F7E1 Medium", "\U0001F7E2 Low");
        if (priorityChoice == "Cancel" || string.IsNullOrEmpty(priorityChoice)) return;

        int priority = priorityChoice.Contains("High") ? 1
            : priorityChoice.Contains("Low") ? 3 : 2;

        var newTask = await _tasks.CreateTaskAsync(
            _auth.CurrentUsername,
            title.Trim(),
            challenge.FocusCategory,
            priority);

        // Mark as top candidate
        newTask.IsTopCandidate = true;
        await _tasks.UpdateTaskAsync(newTask);

        if (_ideasService != null)
        {
            try { await _ideasService.CreateIdeaAsync(_auth.CurrentUsername, title.Trim(), "tasks_ideas"); }
            catch { }
        }

        await DisplayAlert("Top Candidate Added",
            $"'{title.Trim()}' added to {challenge.FocusCategory} as a top candidate.\n\nIt will appear in a highlighted section when picking focus tasks.",
            "OK");

        await RefreshChallengeWidgetAsync();
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
            string priorityStr = task.Priority switch
            {
                1 => "HIGH",
                3 => "LOW",
                _ => "MEDIUM"
            };

            string committed = committedTaskIds.Contains(task.Id) ? " [COMMITTED THIS WEEK]" : "";
            string notes = string.IsNullOrWhiteSpace(task.Notes) ? "" : $" | Notes: {task.Notes.Trim()}";

            sb.AppendLine($"- ID:{task.Id} | {task.Title} | Current priority: {priorityStr}{committed}{notes}");
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
        sb.AppendLine("<task_id>|HIGH or MEDIUM or LOW|YES or NO|<brief reason>");
        sb.AppendLine();
        sb.AppendLine("Example:");
        sb.AppendLine("42|HIGH|YES|Quick win that unblocks other tasks");
        sb.AppendLine("17|MEDIUM|NO|Important but not urgent this week");
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
        var overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };

        var editor = new Editor
        {
            AutoSize = EditorAutoSizeOption.Disabled,
            HeightRequest = 300,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#888"),
            Placeholder = "Paste the LLM response here..."
        };

        var applyBtn = new Button
        {
            Text = "Apply Priorities",
            BackgroundColor = Color.FromArgb("#7B1FA2"),
            TextColor = Colors.White,
            CornerRadius = 8
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#9E9E9E"),
            TextColor = Colors.White,
            CornerRadius = 8
        };

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

        var card = new Frame
        {
            BackgroundColor = Colors.White,
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
                    new Label
                    {
                        Text = "Paste LLM Prioritization Result",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#222")
                    },
                    new Label
                    {
                        Text = "Paste the full response. Bannister will parse the PRIORITY TABLE and update task priorities.",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#666"),
                        FontAttributes = FontAttributes.Italic
                    },
                    editor,
                    btnRow
                }
            }
        };

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

            var priorityStr = parts[1].Trim().ToUpperInvariant();
            int newPriority = priorityStr switch
            {
                "HIGH" => 1,
                "LOW" => 3,
                _ => 2
            };

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
                string p = pick.Priority switch { 1 => "HIGH", 3 => "LOW", _ => "MED" };
                summaryLines.Add($"  [{p}] {pick.Title}");
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

        var overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };

        var card = new Frame
        {
            CornerRadius = 12,
            Padding = 0,
            BackgroundColor = Colors.White,
            HasShadow = true,
            WidthRequest = 480,
            MaximumHeightRequest = 600,
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

        // Search box
        var searchEntry = new Entry
        {
            Placeholder = "Search tasks...",
            FontSize = 13,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#333"),
            PlaceholderColor = Color.FromArgb("#999"),
            Margin = new Thickness(12, 8, 12, 0),
            HeightRequest = 38
        };
        cardStack.Children.Add(searchEntry);

        // Sort row
        var sortRow = new HorizontalStackLayout
        {
            Spacing = 6,
            Margin = new Thickness(12, 4, 12, 4)
        };

        var sortLabel = new Label
        {
            Text = "Sort:",
            FontSize = 11,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        };
        sortRow.Children.Add(sortLabel);

        var sortDateBtn = new Button
        {
            Text = "Newest ▼",
            FontSize = 10,
            HeightRequest = 28,
            Padding = new Thickness(8, 0),
            CornerRadius = 4,
            BackgroundColor = Color.FromArgb("#7B1FA2"),
            TextColor = Colors.White
        };

        var sortAlphaBtn = new Button
        {
            Text = "A-Z",
            FontSize = 10,
            HeightRequest = 28,
            Padding = new Thickness(8, 0),
            CornerRadius = 4,
            BackgroundColor = Color.FromArgb("#E1BEE7"),
            TextColor = Color.FromArgb("#7B1FA2")
        };

        var sortPriorityBtn = new Button
        {
            Text = "Priority",
            FontSize = 10,
            HeightRequest = 28,
            Padding = new Thickness(8, 0),
            CornerRadius = 4,
            BackgroundColor = Color.FromArgb("#E1BEE7"),
            TextColor = Color.FromArgb("#7B1FA2")
        };

        sortRow.Children.Add(sortDateBtn);
        sortRow.Children.Add(sortAlphaBtn);
        sortRow.Children.Add(sortPriorityBtn);

        var resultCountLabel = new Label
        {
            Text = $"{filteredTasks.Count} tasks",
            FontSize = 10,
            TextColor = Color.FromArgb("#999"),
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        sortRow.Children.Add(resultCountLabel);

        cardStack.Children.Add(sortRow);

        // Task list
        var taskList = new VerticalStackLayout { Padding = 8, Spacing = 4 };
        var scrollView = new ScrollView { MaximumHeightRequest = 350, Content = taskList };
        cardStack.Children.Add(scrollView);

        // Helper: update sort button styles
        void UpdateSortButtonStyles()
        {
            var active = Color.FromArgb("#7B1FA2");
            var activeText = Colors.White;
            var inactive = Color.FromArgb("#E1BEE7");
            var inactiveText = Color.FromArgb("#7B1FA2");

            sortDateBtn.BackgroundColor = currentSort.StartsWith("date") ? active : inactive;
            sortDateBtn.TextColor = currentSort.StartsWith("date") ? activeText : inactiveText;
            sortDateBtn.Text = currentSort == "date_desc" ? "Newest ▼" : "Oldest ▲";

            sortAlphaBtn.BackgroundColor = currentSort.StartsWith("alpha") ? active : inactive;
            sortAlphaBtn.TextColor = currentSort.StartsWith("alpha") ? activeText : inactiveText;
            sortAlphaBtn.Text = currentSort == "alpha_asc" ? "A-Z ▲" : currentSort == "alpha_desc" ? "Z-A ▼" : "A-Z";

            sortPriorityBtn.BackgroundColor = currentSort.StartsWith("priority") ? active : inactive;
            sortPriorityBtn.TextColor = currentSort.StartsWith("priority") ? activeText : inactiveText;
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
                "priority_asc" => filteredTasks.OrderBy(t => t.Priority).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList(),
                "priority_desc" => filteredTasks.OrderByDescending(t => t.Priority).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList(),
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
                taskList.Children.Add(new Label
                {
                    Text = string.IsNullOrEmpty(currentSearch)
                        ? "No tasks available."
                        : "No tasks match your search.",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#999"),
                    FontAttributes = FontAttributes.Italic,
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 20)
                });
                return;
            }

            // Split into top candidates and regular tasks
            var topCandidates = filteredTasks.Where(t => t.IsTopCandidate).ToList();
            var regularTasks = filteredTasks.Where(t => !t.IsTopCandidate).ToList();

            if (topCandidates.Count > 0)
            {
                taskList.Children.Add(new Label
                {
                    Text = "\u2B50 Top Candidates",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#FF9800"),
                    Margin = new Thickness(4, 4, 0, 2)
                });

                foreach (var task in topCandidates)
                {
                    taskList.Children.Add(BuildPickerTaskCard(task, categoryLabel, overlay, tcs));
                }

                if (regularTasks.Count > 0)
                {
                    taskList.Children.Add(new BoxView
                    {
                        HeightRequest = 1,
                        Color = Color.FromArgb("#E0E0E0"),
                        Margin = new Thickness(0, 8)
                    });

                    taskList.Children.Add(new Label
                    {
                        Text = "All Tasks",
                        FontSize = 13,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#666"),
                        Margin = new Thickness(4, 0, 0, 2)
                    });
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

        var createBtn = new Button
        {
            Text = "+ New Task",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            FontSize = 12,
            CornerRadius = 6,
            HeightRequest = 36,
            Padding = new Thickness(12, 0)
        };

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

            string? priorityChoice = await DisplayActionSheet(
                "Priority",
                "Cancel",
                null,
                " High", " Medium", " Low");
            if (priorityChoice == "Cancel" || string.IsNullOrEmpty(priorityChoice)) return;

            int priority = priorityChoice.Contains("High") ? 1
                : priorityChoice.Contains("Low") ? 3 : 2;

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

            if (_ideasService != null)
            {
                try { await _ideasService.CreateIdeaAsync(_auth.CurrentUsername, title.Trim(), "tasks_ideas"); }
                catch { }
            }

            allTasks.Add(newTask);
            ApplyFilterAndSort();
            RebuildTaskList();
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#7B1FA2"),
            FontSize = 14,
            HeightRequest = 36
        };
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
        var taskFrame = new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = task.IsTopCandidate
                ? Color.FromArgb("#FFF3E0")
                : Color.FromArgb("#F5F5F5"),
            BorderColor = task.IsTopCandidate
                ? Color.FromArgb("#FF9800")
                : Colors.Transparent,
            HasShadow = false
        };

        var taskStack = new HorizontalStackLayout { Spacing = 8 };

        string priorityDot = task.Priority switch
        {
            1 => "\U0001F534",
            3 => "\U0001F7E2",
            _ => "\U0001F7E1"
        };
        taskStack.Children.Add(new Label
        {
            Text = priorityDot,
            FontSize = 10,
            VerticalOptions = LayoutOptions.Center
        });

        var textStack = new VerticalStackLayout { Spacing = 2 };
        textStack.Children.Add(new Label
        {
            Text = task.IsTopCandidate ? $"\u2B50 {task.Title}" : task.Title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        if (categoryLabel == "Any Category")
        {
            textStack.Children.Add(new Label
            {
                Text = task.Category,
                FontSize = 12,
                TextColor = Color.FromArgb("#7B1FA2")
            });
        }

        if (!string.IsNullOrWhiteSpace(task.Notes))
        {
            textStack.Children.Add(new Label
            {
                Text = task.Notes.Length > 60 ? task.Notes[..60] + "..." : task.Notes,
                FontSize = 11,
                TextColor = Color.FromArgb("#999"),
                LineBreakMode = LineBreakMode.TailTruncation
            });
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
