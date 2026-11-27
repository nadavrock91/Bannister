using Bannister.Services;
using Bannister.Models;
using SQLite;

namespace Bannister.Views;

/// <summary>
/// Calendar page showing a card grid of days for the current month.
/// Each day card shows how many tasks are due on that day.
/// Previous/Next buttons to navigate months.
/// </summary>
public class CalendarPage : Bannister.Views.ModeThemedPage
{
    private const string NotYetPlacedCategory = "not yet placed";

    private readonly AuthService _auth;
    private readonly TaskService _taskService;
    private readonly IdeaLoggerService? _ideaLogger;
    private readonly DatabaseService _db;
    private readonly LookoutService _lookoutService;
    private readonly RoutineService _routineService;
    private readonly PostponedTaskService _postponedTaskService;
    private readonly DailyReminderService _reminderService;

    private Label _monthLabel;
    private Grid _calendarGrid;
    private Grid _dowRow;
    private VerticalStackLayout _unplacedToolbarContainer;
    private VerticalStackLayout _unplacedGridContainer;
    private Button _placeSelectedTaskButton;
    private Button _weekStartButton;
    private int _year;
    private int _month;
    private bool _weekStartsSunday;
    private List<TaskItem> _unplacedTasks = new();
    private TaskItem? _selectedUnplacedTask;
    private int _unplacedGridLoadVersion = 0;

    // Task counts keyed by day-of-month
    private Dictionary<int, int> _taskCounts = new();
    private Dictionary<int, int> _completedCounts = new();
    private Dictionary<int, int> _lookoutCounts = new();
    private Dictionary<int, int> _postponedCounts = new();

    public CalendarPage(AuthService auth, TaskService taskService, IdeasService? ideasService = null, DatabaseService? db = null, LookoutService? lookoutService = null, RoutineService? routineService = null, PostponedTaskService? postponedTaskService = null, DailyReminderService? reminderService = null)
    {
        _auth = auth;
        _taskService = taskService;
        _ideaLogger = Application.Current?.Handler?.MauiContext?.Services.GetService<IdeaLoggerService>();
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _lookoutService = lookoutService ?? new LookoutService(_db);
        _routineService = routineService ?? new RoutineService(_db, _auth);
        _postponedTaskService = postponedTaskService ?? new PostponedTaskService(_db);
        _reminderService = reminderService
            ?? Application.Current?.Handler?.MauiContext?
                .Services.GetService<DailyReminderService>()
            ?? throw new InvalidOperationException(
                "DailyReminderService not registered.");
        Title = "Calendar";


        var now = DateTime.Today;
        _year = now.Year;
        _month = now.Month;

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadCalendarSettingsAsync();
        await LoadMonthAsync();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout { Padding = 16, Spacing = 12 };

        // Header with navigation
        var navRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };

        var prevBtn = (new Button
        {
            Text = "← Prev", FontSize = 13, HeightRequest = 38,
            CornerRadius = 6, Padding = new Thickness(14, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralDarkButton).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        prevBtn.Clicked += async (s, e) => { GoToPreviousMonth(); await LoadMonthAsync(); };
        navRow.Add(prevBtn, 0, 0);

        _monthLabel = (new Label
        {
            Text = "", FontSize = 22, FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText1);
        navRow.Add(_monthLabel, 1, 0);

        var todayBtn = (new Button
        {
            Text = "Today", FontSize = 12, HeightRequest = 38,
            CornerRadius = 6, Padding = new Thickness(12, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        todayBtn.Clicked += async (s, e) => { var now = DateTime.Today; _year = now.Year; _month = now.Month; await LoadMonthAsync(); };
        navRow.Add(todayBtn, 2, 0);

        var nextBtn = (new Button
        {
            Text = "Next →", FontSize = 13, HeightRequest = 38,
            CornerRadius = 6, Padding = new Thickness(14, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralDarkButton).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        nextBtn.Clicked += async (s, e) => { GoToNextMonth(); await LoadMonthAsync(); };
        navRow.Add(nextBtn, 3, 0);

        var importBtn = (new Button
        {
            Text = "📥 Import", FontSize = 12, HeightRequest = 38,
            CornerRadius = 6, Padding = new Thickness(12, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralDarkButton).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        importBtn.Clicked += OnImportClicked;
        navRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        navRow.Add(importBtn, 4, 0);

        var dedupBtn = (new Button
        {
            Text = "🧹 Dedup", FontSize = 12, HeightRequest = 38,
            CornerRadius = 6, Padding = new Thickness(12, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralDarkButton).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        dedupBtn.Clicked += OnDedupClicked;
        navRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        navRow.Add(dedupBtn, 5, 0);

        var allLookuoutsBtn = (new Button
        {
            Text = " Lookouts",
            FontSize = 12,
            HeightRequest = 38,
            CornerRadius = 6,
            Padding = new Thickness(12, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        allLookuoutsBtn.Clicked += async (_, _) =>
        {
            var page = new AllLookoutsPage(
                _auth, _lookoutService);
            await Navigation.PushAsync(page);
        };
        navRow.ColumnDefinitions.Add(
            new ColumnDefinition(GridLength.Auto));
        navRow.Add(allLookuoutsBtn, 6, 0);

        var moveBtn = (new Button
        {
            Text = "📦 Move", FontSize = 12, HeightRequest = 38,
            CornerRadius = 6, Padding = new Thickness(12, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ButtonPrimaryBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        moveBtn.Clicked += OnMoveTasksClicked;
        navRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        navRow.Add(moveBtn, 7, 0);

        var postponedBtn = (new Button
        {
            Text = "Postponed Tasks", FontSize = 12, HeightRequest = 38,
            CornerRadius = 6, Padding = new Thickness(12, 0),
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningBackground).ThemeColor(Button.TextColorProperty, ThemeKey.WarningText);
        postponedBtn.Clicked += async (s, e) => await Navigation.PushAsync(new PostponedTasksManagementPage(_auth, _postponedTaskService));
        navRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        navRow.Add(postponedBtn, 8, 0);

        var routinesBtn = (new Button
        {
            Text = "Routines", FontSize = 12, HeightRequest = 38,
            CornerRadius = 6, Padding = new Thickness(12, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        routinesBtn.Clicked += async (s, e) => await Navigation.PushAsync(new RoutinesPage(_auth, _routineService));
        navRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        navRow.Add(routinesBtn, 9, 0);

        _weekStartButton = (new Button
        {
            Text = "Week: Mon",
            FontSize = 12,
            HeightRequest = 38,
            CornerRadius = 6,
            Padding = new Thickness(12, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightIndigo).ThemeColor(Button.TextColorProperty, ThemeKey.HighlightIndigoText);
        ToolTipProperties.SetText(_weekStartButton, "Change calendar week display order");
        _weekStartButton.Clicked += async (s, e) => await ChangeWeekStartAsync();
        navRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        navRow.Add(_weekStartButton, 10, 0);

        var remindersBtn = (new Button
        {
            Text = " Reminders",
            FontSize = 12,
            HeightRequest = 38,
            CornerRadius = 6,
            Padding = new Thickness(12, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        remindersBtn.Clicked += async (_, _) =>
        {
            var page = new DailyRemindersPage(
                _auth, _reminderService, _taskService);
            await Navigation.PushAsync(page);
        };
        navRow.ColumnDefinitions.Add(
            new ColumnDefinition(GridLength.Auto));
        navRow.Add(remindersBtn, 11, 0);

        mainStack.Children.Add(navRow);

        // Day-of-week headers
        _dowRow = new Grid { ColumnSpacing = 4 };
        for (int i = 0; i < 7; i++)
            _dowRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        UpdateDayOfWeekHeaders();
        mainStack.Children.Add(_dowRow);

        // Calendar grid (will be rebuilt each month)
        _calendarGrid = new Grid { ColumnSpacing = 4, RowSpacing = 4 };
        for (int i = 0; i < 7; i++)
            _calendarGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        mainStack.Children.Add(_calendarGrid);

        var unplacedHeader = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        unplacedHeader.Add((new Label
        {
            Text = "Not yet placed calendar tasks",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText1), 0, 0);

        _placeSelectedTaskButton = (new Button
        {
            Text = "Move to date",
            FontSize = 12,
            HeightRequest = 34,
            CornerRadius = 6,
            Padding = new Thickness(12, 0),
            IsEnabled = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        ToolTipProperties.SetText(_placeSelectedTaskButton, "Move the selected not yet placed task to a calendar date");
        _placeSelectedTaskButton.Clicked += async (s, e) => await MoveSelectedUnplacedTaskToDateAsync();
        unplacedHeader.Add(_placeSelectedTaskButton, 1, 0);
        mainStack.Children.Add(unplacedHeader);

        _unplacedToolbarContainer = new VerticalStackLayout { Spacing = 2 };
        mainStack.Children.Add(_unplacedToolbarContainer);

        var unplacedScroll = new ScrollView
        {
            Orientation = ScrollOrientation.Both,
            MaximumHeightRequest = 320
        };
        _unplacedGridContainer = new VerticalStackLayout { Spacing = 4 };
        unplacedScroll.Content = _unplacedGridContainer;
        mainStack.Children.Add(unplacedScroll);

        Content = new ScrollView { Content = mainStack };
    }

    private async Task LoadCalendarSettingsAsync()
    {
        string key = GetWeekStartStorageKey();
        string? value = null;
        try { value = await SecureStorage.GetAsync(key); } catch { }

        _weekStartsSunday = string.Equals(value, "Sunday", StringComparison.OrdinalIgnoreCase);
        UpdateWeekStartButton();
        UpdateDayOfWeekHeaders();
    }

    private async Task ChangeWeekStartAsync()
    {
        string current = _weekStartsSunday ? "Sunday to Saturday" : "Monday to Sunday";
        string action = await DisplayActionSheet(
            $"Current: {current}",
            "Cancel",
            null,
            "Monday to Sunday",
            "Sunday to Saturday");

        if (action == "Sunday to Saturday")
            _weekStartsSunday = true;
        else if (action == "Monday to Sunday")
            _weekStartsSunday = false;
        else
            return;

        try { await SecureStorage.SetAsync(GetWeekStartStorageKey(), _weekStartsSunday ? "Sunday" : "Monday"); } catch { }

        UpdateWeekStartButton();
        UpdateDayOfWeekHeaders();
        BuildCalendarGrid();
    }

    private string GetWeekStartStorageKey()
    {
        return $"calendar_week_start_{_auth.CurrentUsername}";
    }

    private void UpdateWeekStartButton()
    {
        if (_weekStartButton != null)
            _weekStartButton.Text = _weekStartsSunday ? "Week: Sun" : "Week: Mon";
    }

    private void UpdateDayOfWeekHeaders()
    {
        if (_dowRow == null)
            return;

        _dowRow.Children.Clear();
        string[] dayNames = _weekStartsSunday
            ? new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" }
            : new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        for (int i = 0; i < 7; i++)
        {
            _dowRow.Add((new Label
            {
                Text = dayNames[i],
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center
            }).ThemeColor(Label.TextColorProperty, IsWeekendColumn(i) ? ThemeKey.ErrorText : ThemeKey.CardText2, ThemeKey.PageText3), i, 0);
        }
    }

    private void GoToPreviousMonth()
    {
        _month--;
        if (_month < 1) { _month = 12; _year--; }
    }

    private void GoToNextMonth()
    {
        _month++;
        if (_month > 12) { _month = 1; _year++; }
    }

    private async Task LoadMonthAsync()
    {
        _monthLabel.Text = $"{new DateTime(_year, _month, 1):MMMM yyyy}";

        // Load all tasks and count by due date day
        _taskCounts.Clear();
        _completedCounts.Clear();
        _lookoutCounts.Clear();
        _postponedCounts.Clear();

        try
        {
            var active = await _taskService.GetActiveTasksAsync(_auth.CurrentUsername);
            var completed = await _taskService.GetCompletedTasksAsync(_auth.CurrentUsername);

            foreach (var task in active)
            {
                if (task.DueDate.HasValue && task.DueDate.Value.Year == _year && task.DueDate.Value.Month == _month)
                {
                    int day = task.DueDate.Value.Day;
                    _taskCounts[day] = _taskCounts.GetValueOrDefault(day) + 1;
                }
            }

            foreach (var task in completed)
            {
                if (task.DueDate.HasValue && task.DueDate.Value.Year == _year && task.DueDate.Value.Month == _month)
                {
                    int day = task.DueDate.Value.Day;
                    _completedCounts[day] = _completedCounts.GetValueOrDefault(day) + 1;
                }
            }

            _lookoutCounts = await _lookoutService.GetCountsByDayAsync(_auth.CurrentUsername, _year, _month);

            var postponed = await _postponedTaskService.GetActiveAsync(_auth.CurrentUsername);
            _postponedCounts = postponed
                .Where(t => t.CurrentDate.Year == _year && t.CurrentDate.Month == _month)
                .GroupBy(t => t.CurrentDate.Day)
                .ToDictionary(g => g.Key, g => g.Count());
        }
        catch { }

        BuildCalendarGrid();
        await LoadUnplacedTasksGridAsync();
    }

    private void BuildCalendarGrid()
    {
        _calendarGrid.Children.Clear();
        _calendarGrid.RowDefinitions.Clear();

        var firstDay = new DateTime(_year, _month, 1);
        int daysInMonth = DateTime.DaysInMonth(_year, _month);

        int startOffset = _weekStartsSunday
            ? (int)firstDay.DayOfWeek
            : ((int)firstDay.DayOfWeek + 6) % 7;

        int totalCells = startOffset + daysInMonth;
        int rows = (int)Math.Ceiling(totalCells / 7.0);

        for (int r = 0; r < rows; r++)
            _calendarGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        int today = DateTime.Today.Day;
        bool isCurrentMonth = DateTime.Today.Year == _year && DateTime.Today.Month == _month;

        for (int day = 1; day <= daysInMonth; day++)
        {
            int cellIndex = startOffset + day - 1;
            int col = cellIndex % 7;
            int row = cellIndex / 7;

            int activeTasks = _taskCounts.GetValueOrDefault(day);
            int doneTasks = _completedCounts.GetValueOrDefault(day);
            int lookouts = _lookoutCounts.GetValueOrDefault(day);
            int postponedTasks = _postponedCounts.GetValueOrDefault(day);
            bool isToday = isCurrentMonth && day == today;
            bool isWeekend = IsWeekendColumn(col);
            bool isPast = isCurrentMonth && day < today;

            var card = BuildDayCard(day, activeTasks, doneTasks, lookouts, postponedTasks, isToday, isWeekend, isPast);
            _calendarGrid.Add(card, col, row);
        }
    }

    private bool IsWeekendColumn(int col)
    {
        return _weekStartsSunday ? col == 0 || col == 6 : col >= 5;
    }

    private Frame BuildDayCard(int day, int activeTasks, int doneTasks, int lookouts, int postponedTasks, bool isToday, bool isWeekend, bool isPast)
    {
        ThemeKey bg;
        ThemeKey border;

        if (isToday)
        {
            bg = ThemeKey.InfoBackground;
            border = ThemeKey.InfoText;
        }
        else if (activeTasks > 0)
        {
            bg = ThemeKey.HighlightYellow;
            border = ThemeKey.WarningButtonBackground;
        }
        else if (lookouts > 0)
        {
            bg = ThemeKey.HighlightViolet;
            border = ThemeKey.HighlightVioletText;
        }
        else if (postponedTasks > 0)
        {
            bg = ThemeKey.WarningBackground;
            border = ThemeKey.WarningText;
        }
        else if (doneTasks > 0)
        {
            bg = ThemeKey.SuccessBackground;
            border = ThemeKey.SuccessButtonBackground;
        }
        else
        {
            bg = isPast ? ThemeKey.SecondarySurface : ThemeKey.CardBackground;
            border = ThemeKey.Separator;
        }

        var frame = (new Frame
        {
            CornerRadius = 8, Padding = 8, HasShadow = false,
            MinimumHeightRequest = 70
        }).ThemeColor(VisualElement.BackgroundColorProperty, bg).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, border);

        var stack = new VerticalStackLayout { Spacing = 4 };

        // Day number
        stack.Children.Add((new Label
        {
            Text = day.ToString(),
            FontSize = isToday ? 18 : 15,
            FontAttributes = isToday ? FontAttributes.Bold : FontAttributes.None
        }).ThemeColor(Label.TextColorProperty, isToday ? ThemeKey.InfoText : isWeekend ? ThemeKey.ErrorText : ThemeKey.CardText1, ThemeKey.PageText1));

        // Task counts
        if (activeTasks > 0)
        {
            stack.Children.Add((new Label
            {
                Text = $"📋 {activeTasks}",
                FontSize = 12, FontAttributes = FontAttributes.Bold
            }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText3));
        }

        if (doneTasks > 0)
        {
            stack.Children.Add((new Label
            {
                Text = $"✓ {doneTasks}",
                FontSize = 11
            }).ThemeColor(Label.TextColorProperty, ThemeKey.SuccessButtonBackground, ThemeKey.PageText3));
        }

        if (lookouts > 0)
        {
            stack.Children.Add((new Label
            {
                Text = $"Lookouts {lookouts}",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold
            }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText3));
        }

        if (postponedTasks > 0)
        {
            stack.Children.Add((new Label
            {
                Text = $"Postponed {postponedTasks}",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold
            }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText3));
        }

        if (activeTasks == 0 && doneTasks == 0 && lookouts == 0 && postponedTasks == 0)
        {
            stack.Children.Add((new Label
            {
                Text = "—", FontSize = 11
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3));
        }

        frame.Content = stack;

        // Tap to navigate to day detail page
        int capturedDay = day;
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) => await NavigateToDayAsync(capturedDay);
        frame.GestureRecognizers.Add(tap);

        return frame;
    }

    private async Task NavigateToDayAsync(int day)
    {
        var date = new DateTime(_year, _month, day);
        var page = new CalendarDayPage(_auth, _taskService, date, ideaLogger: _ideaLogger, lookoutService: _lookoutService, routineService: _routineService, postponedTaskService: _postponedTaskService);
        await Navigation.PushAsync(page);
    }

    private async Task LoadUnplacedTasksGridAsync()
    {
        int loadVersion = ++_unplacedGridLoadVersion;
        _unplacedToolbarContainer.Children.Clear();
        _unplacedGridContainer.Children.Clear();
        _selectedUnplacedTask = null;
        _placeSelectedTaskButton.IsEnabled = false;

        var active = await _taskService.GetActiveTasksAsync(_auth.CurrentUsername);
        if (loadVersion != _unplacedGridLoadVersion)
            return;

        _unplacedTasks = active
            .Where(t => t.Category.Equals(NotYetPlacedCategory, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .ToList();

        _unplacedToolbarContainer.Children.Clear();
        _unplacedGridContainer.Children.Clear();

        if (_unplacedTasks.Count == 0)
        {
            _unplacedGridContainer.Children.Add((new Label
            {
                Text = "No not yet placed tasks.",
                FontSize = 13,
                Margin = new Thickness(4, 8)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2));
            return;
        }

        var headers = new List<string> { "Id", "Title", "Priority", "Notes", "CreatedAt" };
        var fullRows = _unplacedTasks.Select(BuildUnplacedTaskGridRow).ToList();
        var displayRows = fullRows
            .Select(row => row.Select(v => v.Length > 50 ? v.Substring(0, 47) + "..." : v).ToList())
            .ToList();

        var dataGrid = DataGridView.Create(headers, displayRows)
            .WithHeaderStyle(AppColors.Get(ThemeKey.InfoText), AppColors.Get(ThemeKey.ButtonPrimaryText))
            .WithAlternateRowColor(AppColors.Get(ThemeKey.HighlightViolet))
            .WithColumnWidths(60, 240)
            .WithCellPadding(6)
            .WithFontSize(12, 12)
            .WithFullRows(fullRows)
            .WithIdColumn("Id")
            .OnCellTapped((s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _unplacedTasks.Count)
                {
                    _selectedUnplacedTask = _unplacedTasks[e.RowIndex];
                    _placeSelectedTaskButton.IsEnabled = true;
                }
            })
            .WithUpdateCallback(UpdateUnplacedTaskGridCellAsync)
            .Build();

        _unplacedToolbarContainer.Children.Add(dataGrid.ToolbarView);
        _unplacedGridContainer.Children.Add(dataGrid.GridView);
    }

    private static List<string> BuildUnplacedTaskGridRow(TaskItem task)
    {
        return new List<string>
        {
            task.Id.ToString(),
            task.Title,
            task.Priority switch { 1 => "High", 3 => "Low", _ => "Medium" },
            task.Notes ?? "",
            task.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
        };
    }

    private async Task<bool> UpdateUnplacedTaskGridCellAsync(string idValue, string columnName, string newValue)
    {
        if (!int.TryParse(idValue, out int id))
            return false;

        var task = _unplacedTasks.FirstOrDefault(t => t.Id == id);
        if (task == null)
        {
            var active = await _taskService.GetActiveTasksAsync(_auth.CurrentUsername);
            task = active.FirstOrDefault(t => t.Id == id);
        }

        if (task == null)
            return false;

        switch (columnName)
        {
            case "Title":
                if (string.IsNullOrWhiteSpace(newValue)) return false;
                task.Title = newValue.Trim();
                break;
            case "Priority":
                task.Priority = ParseCalendarTaskPriority(newValue);
                break;
            case "Notes":
                task.Notes = newValue == "NULL" ? "" : newValue;
                break;
            case "Id":
            case "CreatedAt":
            default:
                return false;
        }

        await _taskService.UpdateTaskAsync(task);
        await LoadUnplacedTasksGridAsync();
        return true;
    }

    private static int ParseCalendarTaskPriority(string value)
    {
        if (int.TryParse(value, out int numeric))
            return Math.Clamp(numeric, 1, 3);

        return value.Trim().ToLowerInvariant() switch
        {
            "high" => 1,
            "low" => 3,
            _ => 2
        };
    }

    private async Task MoveSelectedUnplacedTaskToDateAsync()
    {
        if (_selectedUnplacedTask == null)
            return;

        var date = await ShowDatePickerPopupAsync(
            "Move task to date",
            _selectedUnplacedTask.Title,
            new DateTime(_year, _month, Math.Min(DateTime.Today.Day, DateTime.DaysInMonth(_year, _month))));
        if (date == null)
            return;

        _selectedUnplacedTask.DueDate = date.Value.Date;
        if (_selectedUnplacedTask.Category.Equals(NotYetPlacedCategory, StringComparison.OrdinalIgnoreCase))
            _selectedUnplacedTask.Category = "Imported";

        await _taskService.UpdateTaskAsync(_selectedUnplacedTask);
        await LoadMonthAsync();
    }

    private async Task<DateTime?> ShowDatePickerPopupAsync(string title, string taskTitle, DateTime initialDate)
    {
        var tcs = new TaskCompletionSource<DateTime?>();

        var popup = (new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);

        var card = (new Frame
        {
            CornerRadius = 12,
            Padding = 20,
            WidthRequest = 420,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var stack = new VerticalStackLayout { Spacing = 10 };
        stack.Children.Add((new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText1));
        stack.Children.Add((new Label
        {
            Text = taskTitle.Length > 120 ? taskTitle.Substring(0, 117) + "..." : taskTitle,
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1));

        var datePicker = (new DatePicker
        {
            Date = initialDate.Date,
            FontSize = 14
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground);
        stack.Children.Add(datePicker);

        var buttonRow = new HorizontalStackLayout { Spacing = 10, Margin = new Thickness(0, 8, 0, 0) };
        var moveButton = (new Button
        {
            Text = "Move",
            FontSize = 13,
            HeightRequest = 38,
            CornerRadius = 6,
            Padding = new Thickness(16, 0),
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        var cancelButton = (new Button
        {
            Text = "Cancel",
            FontSize = 13,
            HeightRequest = 38,
            CornerRadius = 6,
            Padding = new Thickness(16, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        buttonRow.Children.Add(moveButton);
        buttonRow.Children.Add(cancelButton);
        stack.Children.Add(buttonRow);

        card.Content = stack;
        popup.Children.Add(card);

        var originalContent = Content;
        var wrapper = new Grid();
        Content = wrapper;
        wrapper.Children.Add(originalContent);
        wrapper.Children.Add(popup);

        void Dismiss()
        {
            wrapper.Children.Remove(popup);
            wrapper.Children.Remove(originalContent);
            Content = originalContent;
        }

        moveButton.Clicked += (s, e) =>
        {
            Dismiss();
            tcs.TrySetResult(datePicker.Date);
        };
        cancelButton.Clicked += (s, e) =>
        {
            Dismiss();
            tcs.TrySetResult(null);
        };

        return await tcs.Task;
    }

    private async Task AddTaskForDateAsync(DateTime date)
    {
        string? title = await DisplayPromptAsync("New Task",
            $"Add task for {date:MMM d, yyyy}:",
            "Add", "Cancel",
            placeholder: "Task title...");

        if (string.IsNullOrWhiteSpace(title)) return;

        var categories = await _taskService.GetCategoriesAsync(_auth.CurrentUsername);
        var catOptions = categories.Count > 0
            ? categories.Concat(new[] { "+ New Category" }).ToArray()
            : new[] { "General", "+ New Category" };

        string? category = await DisplayActionSheet("Category", "Cancel", null, catOptions);
        if (string.IsNullOrEmpty(category) || category == "Cancel") return;

        if (category == "+ New Category")
        {
            category = await DisplayPromptAsync("Category", "Enter category name:");
            if (string.IsNullOrWhiteSpace(category)) category = "General";
        }

        string? priorityChoice = await DisplayActionSheet("Priority", "Cancel", null,
            "🔴 High", "🟡 Medium", "🟢 Low");
        int priority = priorityChoice switch
        {
            "🔴 High" => 3,
            "🟡 Medium" => 2,
            "🟢 Low" => 1,
            _ => 2
        };

        await _taskService.CreateTaskAsync(
            _auth.CurrentUsername, title.Trim(), category.Trim(), priority, date);

        // Log to ideas
        string ideaText = $"[{date:MMM d}] {title.Trim()}";
        if (_ideaLogger != null)
            await _ideaLogger.LogSilentlyAsync(new IdeaLogRequest
            {
                Username = _auth.CurrentUsername,
                Title = ideaText,
                Body = ideaText,
                Category = "calendar_tasks"
            });

        await LoadMonthAsync();
    }

    // ===================== IMPORT FROM .DB =====================

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        SQLiteAsyncConnection? importConn = null;

        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select a .db database file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".db", ".sqlite", ".sqlite3" } },
                    { DevicePlatform.Android, new[] { "application/octet-stream" } },
                    { DevicePlatform.iOS, new[] { "public.database" } }
                })
            });
            if (result == null) return;

            if (!System.IO.File.Exists(result.FullPath))
            { await DisplayAlert("Error", "File not found.", "OK"); return; }

            importConn = new SQLiteAsyncConnection(result.FullPath,
                SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex, storeDateTimeAsTicks: false);

            // Get tables
            var tables = await importConn.QueryAsync<ImportTableInfo>(
                "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name");
            if (tables.Count == 0) { await DisplayAlert("Empty", "No tables found.", "OK"); return; }

            var selectedTable = await DisplayActionSheet("Select table", "Cancel", null,
                tables.Select(t => t.Name).ToArray());
            if (string.IsNullOrEmpty(selectedTable) || selectedTable == "Cancel") return;

            // Get columns
            var columns = await importConn.QueryAsync<ImportColumnInfo>($"PRAGMA table_info([{selectedTable}])");
            var colNames = columns.Select(c => c.Name).ToArray();
            if (colNames.Length == 0) { await DisplayAlert("Error", "No columns.", "OK"); return; }

            // Pick date column
            var dateColumn = await DisplayActionSheet("Which column has the DATE?", "Cancel", null, colNames);
            if (string.IsNullOrEmpty(dateColumn) || dateColumn == "Cancel") return;

            // Pick task columns via checkbox popup
            var remainingCols = colNames.Where(c => c != dateColumn).ToList();
            if (remainingCols.Count == 0)
            { await DisplayAlert("Error", "No columns left for tasks.", "OK"); return; }

            var taskColumns = await ShowColumnPickerAsync(remainingCols);
            if (taskColumns == null || taskColumns.Count == 0)
            { await DisplayAlert("Error", "Select at least one task column.", "OK"); return; }

            // Confirm
            string summary = $"Table: {selectedTable}\nDate column: {dateColumn}\nTask columns ({taskColumns.Count}): {string.Join(", ", taskColumns)}\n\nDuplicate tasks on the same date will be skipped.";
            if (!await DisplayAlert("Confirm Import", summary, "Import", "Cancel")) return;

            // Show progress overlay
            var progressOverlay = (new Grid
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);
            var progressStack = new VerticalStackLayout
            {
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Spacing = 12
            };
            var spinner = (new ActivityIndicator { IsRunning = true, HeightRequest = 40, WidthRequest = 40 }).ThemeColor(ActivityIndicator.ColorProperty, ThemeKey.ButtonPrimaryText);
            var progressLabel = (new Label { Text = "Importing...", FontSize = 16, HorizontalTextAlignment = TextAlignment.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1);
            var progressDetail = (new Label { Text = "0 / ?", FontSize = 13, HorizontalTextAlignment = TextAlignment.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightIndigo, ThemeKey.PageText1);
            progressStack.Children.Add(spinner);
            progressStack.Children.Add(progressLabel);
            progressStack.Children.Add(progressDetail);
            progressOverlay.Children.Add(progressStack);

            var originalContent = this.Content;
            var progressWrapper = new Grid();
            this.Content = progressWrapper;
            progressWrapper.Children.Add(originalContent);
            progressWrapper.Children.Add(progressOverlay);

            // Build query
            var selectCols = new List<string> { $"[{dateColumn}]" };
            foreach (var tc in taskColumns) selectCols.Add($"[{tc}]");

            const string SEP = "║";
            var castCols = selectCols.Select(c => $"COALESCE(CAST({c} AS TEXT), '')").ToList();
            string query = $"SELECT {string.Join($" || '{SEP}' || ", castCols)} AS RowData FROM [{selectedTable}]";

            var rows = await importConn.QueryAsync<ImportRowResult>(query);

            // Load existing tasks to check duplicates
            var existingActive = await _taskService.GetActiveTasksAsync(_auth.CurrentUsername);
            var existingCompleted = await _taskService.GetCompletedTasksAsync(_auth.CurrentUsername);
            var allExisting = existingActive.Concat(existingCompleted).ToList();

            var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in allExisting)
            {
                if (t.DueDate.HasValue)
                    existingKeys.Add($"{t.DueDate.Value.Date:yyyy-MM-dd}|{t.Title.Trim()}");
            }

            int imported = 0, skipped = 0, duplicates = 0;
            int totalRows = rows.Count;
            string username = _auth.CurrentUsername;

            string[] dateFormats = new[]
            {
                "MM/dd/yyyy", "M/d/yyyy", "MM-dd-yyyy", "M-d-yyyy",
                "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy",
                "yyyy-MM-dd", "yyyy/MM/dd",
                "MM/dd/yyyy HH:mm:ss", "M/d/yyyy h:mm:ss tt",
                "dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss",
                "MMM d, yyyy", "d MMM yyyy"
            };

            // Phase 1: Parse all rows into task items (fast, no DB calls)
            progressLabel.Text = "Parsing rows...";
            progressDetail.Text = $"0 / {totalRows}";
            await Task.Yield();

            var tasksToInsert = new List<TaskItem>();
            var ideasToInsert = new List<(string text, string category)>();

            for (int rowIdx = 0; rowIdx < rows.Count; rowIdx++)
            {
                if (rowIdx % 100 == 0)
                {
                    progressDetail.Text = $"Parsing {rowIdx} / {totalRows}";
                    await Task.Yield();
                }

                var row = rows[rowIdx];
                if (row?.RowData == null) { skipped++; continue; }

                var parts = row.RowData.Split(SEP);
                if (parts.Length < 2) { skipped++; continue; }

                string dateStr = parts[0].Trim();
                DateTime taskDate = DateTime.Today;
                if (!string.IsNullOrWhiteSpace(dateStr))
                {
                    if (DateTime.TryParseExact(dateStr, dateFormats,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out DateTime parsed))
                        taskDate = parsed.Date;
                    else if (DateTime.TryParse(dateStr, out DateTime fallback))
                        taskDate = fallback.Date;
                }

                for (int i = 0; i < taskColumns.Count; i++)
                {
                    int partIdx = i + 1;
                    if (partIdx >= parts.Length) continue;

                    string taskText = parts[partIdx].Trim();
                    if (string.IsNullOrWhiteSpace(taskText)) continue;

                    string key = $"{taskDate:yyyy-MM-dd}|{taskText}";
                    if (existingKeys.Contains(key)) { duplicates++; continue; }

                    tasksToInsert.Add(new TaskItem
                    {
                        Username = username,
                        Title = taskText,
                        Category = "Imported",
                        Priority = 2,
                        DueDate = taskDate,
                        CreatedAt = DateTime.UtcNow
                    });

                    ideasToInsert.Add(($"[{taskDate:MMM d}] {taskText}", "calendar_tasks"));
                    existingKeys.Add(key);
                }
            }

            // Phase 2: Batch insert in single transaction (fast)
            progressLabel.Text = "Inserting tasks...";
            progressDetail.Text = $"0 / {tasksToInsert.Count}";
            await Task.Yield();

            var conn = await _db.GetConnectionAsync();
            int batchSize = 50;
            for (int i = 0; i < tasksToInsert.Count; i += batchSize)
            {
                var batch = tasksToInsert.Skip(i).Take(batchSize).ToList();
                await conn.RunInTransactionAsync(db =>
                {
                    foreach (var task in batch)
                        db.Insert(task);
                });
                imported += batch.Count;

                progressDetail.Text = $"{Math.Min(i + batchSize, tasksToInsert.Count)} / {tasksToInsert.Count} tasks";
                await Task.Yield();
            }

            // Phase 3: Batch insert ideas
            if (ideasToInsert.Count > 0)
            {
                progressLabel.Text = "Logging to ideas...";
                progressDetail.Text = $"0 / {ideasToInsert.Count}";
                await Task.Yield();

                DateTime createdAt = DateTime.Now;
                var requests = ideasToInsert.Select(x => new IdeaLogRequest
                {
                    Username = username,
                    Title = x.text,
                    Body = x.text,
                    Category = x.category,
                    CreatedAt = createdAt
                });
                if (_ideaLogger != null)
                {
                    var batchResult = await _ideaLogger.LogBatchSilentAsync(requests);
                    progressDetail.Text = $"{batchResult.SavedCount} / {ideasToInsert.Count} ideas";
                    if (batchResult.Failures.Count > 0)
                        System.Diagnostics.Debug.WriteLine($"Calendar import idea logging failures: {batchResult.Failures.Count}");
                }
                else
                {
                    progressDetail.Text = $"0 / {ideasToInsert.Count} ideas (logger unavailable)";
                }
                await Task.Yield();
            }

            await DisplayAlert("Import Complete",
                $"Imported: {imported}\nSkipped (empty): {skipped}\nDuplicates skipped: {duplicates}",
                "OK");

            // Remove progress overlay
            progressWrapper.Children.Remove(progressOverlay);
            progressWrapper.Children.Remove(originalContent);
            this.Content = originalContent;

            await LoadMonthAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Import Error", ex.Message, "OK");
        }
        finally
        {
            if (importConn != null)
                try { await importConn.CloseAsync(); } catch { }
        }
    }

    // ===================== MOVE TASKS =====================

    private async void OnMoveTasksClicked(object? sender, EventArgs e)
    {
        var result = await ShowMovePickerAsync();
        if (result == null) return;

        var (startDate, endDate, targetDate, mode) = result.Value;

        // Load all tasks
        var active = await _taskService.GetActiveTasksAsync(_auth.CurrentUsername);
        var completed = await _taskService.GetCompletedTasksAsync(_auth.CurrentUsername);
        var allTasks = active.Concat(completed).Where(t => t.DueDate.HasValue).ToList();

        // Filter by range
        List<TaskItem> tasksToMove;
        string rangeDesc;

        if (mode == "both")
        {
            tasksToMove = allTasks.Where(t => t.DueDate!.Value.Date >= startDate && t.DueDate!.Value.Date <= endDate).ToList();
            rangeDesc = $"{startDate:MMM d, yyyy} to {endDate:MMM d, yyyy}";
        }
        else if (mode == "after")
        {
            tasksToMove = allTasks.Where(t => t.DueDate!.Value.Date >= startDate).ToList();
            rangeDesc = $"from {startDate:MMM d, yyyy} onward";
        }
        else // "before"
        {
            tasksToMove = allTasks.Where(t => t.DueDate!.Value.Date <= endDate).ToList();
            rangeDesc = $"up to {endDate:MMM d, yyyy}";
        }

        // Exclude tasks already on target date
        tasksToMove = tasksToMove.Where(t => t.DueDate!.Value.Date != targetDate).ToList();

        if (tasksToMove.Count == 0)
        {
            await DisplayAlert("No Tasks", $"No tasks found in range {rangeDesc} to move.", "OK");
            return;
        }

        if (!await DisplayAlert("Confirm Move",
            $"Move {tasksToMove.Count} tasks {rangeDesc}\n→ to {targetDate:MMM d, yyyy}?",
            "Move", "Cancel")) return;

        // Batch update with progress
        var originalContent = this.Content;
        var progressWrapper = new Grid();
        this.Content = progressWrapper;
        progressWrapper.Children.Add(originalContent);

        var overlay = (new Grid { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);
        var pStack = new VerticalStackLayout { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Spacing = 12 };
        pStack.Children.Add((new ActivityIndicator { IsRunning = true, HeightRequest = 40, WidthRequest = 40 }).ThemeColor(ActivityIndicator.ColorProperty, ThemeKey.ButtonPrimaryText));
        pStack.Children.Add((new Label { Text = "Moving tasks...", FontSize = 16, HorizontalTextAlignment = TextAlignment.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1));
        var pDetail = (new Label { Text = $"0 / {tasksToMove.Count}", FontSize = 13, HorizontalTextAlignment = TextAlignment.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightIndigo, ThemeKey.PageText1);
        pStack.Children.Add(pDetail);
        overlay.Children.Add(pStack);
        progressWrapper.Children.Add(overlay);

        int moved = 0;
        int batchSize = 50;
        var conn = await _db.GetConnectionAsync();

        for (int i = 0; i < tasksToMove.Count; i += batchSize)
        {
            var batch = tasksToMove.Skip(i).Take(batchSize).ToList();
            await conn.RunInTransactionAsync(db =>
            {
                foreach (var task in batch)
                {
                    task.DueDate = targetDate;
                    db.Update(task);
                }
            });
            moved += batch.Count;
            pDetail.Text = $"{moved} / {tasksToMove.Count}";
            await Task.Yield();
        }

        progressWrapper.Children.Remove(overlay);
        progressWrapper.Children.Remove(originalContent);
        this.Content = originalContent;

        await DisplayAlert("Move Complete", $"Moved {moved} tasks to {targetDate:MMM d, yyyy}.", "OK");
        await LoadMonthAsync();
    }

    /// <summary>
    /// Popup to configure move: date range mode, start/end dates, target date.
    /// Returns (startDate, endDate, targetDate, mode) or null if cancelled.
    /// Mode: "both", "after" (from start onward), "before" (up to end).
    /// </summary>
    private async Task<(DateTime startDate, DateTime endDate, DateTime targetDate, string mode)?> ShowMovePickerAsync()
    {
        var tcs = new TaskCompletionSource<(DateTime, DateTime, DateTime, string)?>();

        var popup = (new Grid
        {
            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);

        var card = (new Frame
        {
CornerRadius = 12, Padding = 20,
            WidthRequest = 480, HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center, HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var stack = new VerticalStackLayout { Spacing = 10 };

        stack.Children.Add((new Label { Text = "📦 Move Tasks", FontSize = 18, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText1));
        stack.Children.Add((new Label { Text = "Move all tasks from a date range into a single target date.", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));

        // Mode: both dates selected by default
        string currentMode = "both";

        // Start date
        var startLabel = (new Label { Text = "From (start date):", FontSize = 12, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText3);
        var startPicker = (new DatePicker { Date = DateTime.Today.AddDays(-7), FontSize = 14 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        var startEnabled = true;

        // End date
        var endLabel = (new Label { Text = "To (end date):", FontSize = 12, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText3);
        var endPicker = (new DatePicker { Date = DateTime.Today, FontSize = 14 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        var endEnabled = true;

        // Mode buttons
        stack.Children.Add((new Label { Text = "Range mode:", FontSize = 12, Margin = new Thickness(0, 4, 0, 0) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));

        var modeRow = new HorizontalStackLayout { Spacing = 6 };

        var bothBtn = (new Button { Text = "📅 Both Dates", FontSize = 11, HeightRequest = 32, CornerRadius = 6, Padding = new Thickness(10, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ButtonPrimaryBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        var afterBtn = (new Button { Text = "→ All From Start", FontSize = 11, HeightRequest = 32, CornerRadius = 6, Padding = new Thickness(10, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        var beforeBtn = (new Button { Text = "← All Up To End", FontSize = 11, HeightRequest = 32, CornerRadius = 6, Padding = new Thickness(10, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

        void UpdateModeUI()
        {
            bothBtn.ThemeColor(VisualElement.BackgroundColorProperty, currentMode == "both" ? ThemeKey.ButtonPrimaryBackground : ThemeKey.DisabledButtonBackground);
            afterBtn.ThemeColor(VisualElement.BackgroundColorProperty, currentMode == "after" ? ThemeKey.WarningButtonBackground : ThemeKey.DisabledButtonBackground);
            beforeBtn.ThemeColor(VisualElement.BackgroundColorProperty, currentMode == "before" ? ThemeKey.HighlightVioletText : ThemeKey.DisabledButtonBackground);

            startPicker.IsEnabled = currentMode != "before";
            endPicker.IsEnabled = currentMode != "after";
            startPicker.Opacity = currentMode == "before" ? 0.4 : 1;
            endPicker.Opacity = currentMode == "after" ? 0.4 : 1;
        }

        bothBtn.Clicked += (s, ev) => { currentMode = "both"; UpdateModeUI(); };
        afterBtn.Clicked += (s, ev) => { currentMode = "after"; UpdateModeUI(); };
        beforeBtn.Clicked += (s, ev) => { currentMode = "before"; UpdateModeUI(); };

        modeRow.Children.Add(bothBtn);
        modeRow.Children.Add(afterBtn);
        modeRow.Children.Add(beforeBtn);
        stack.Children.Add(modeRow);

        // Date pickers
        stack.Children.Add(startLabel);
        stack.Children.Add(startPicker);
        stack.Children.Add(endLabel);
        stack.Children.Add(endPicker);

        // Target date
        stack.Children.Add((new Label { Text = "Move all to this date:", FontSize = 12, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 6, 0, 0) }).ThemeColor(Label.TextColorProperty, ThemeKey.SuccessText, ThemeKey.PageText3));
        var targetPicker = (new DatePicker { Date = DateTime.Today, FontSize = 14 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessBackground);
        stack.Children.Add(targetPicker);

        // Buttons
        var btnRow = new HorizontalStackLayout { Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };
        var moveBtn = (new Button { Text = "📦 Move Tasks", FontSize = 13, HeightRequest = 40, CornerRadius = 6, Padding = new Thickness(16, 0), FontAttributes = FontAttributes.Bold }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ButtonPrimaryBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        var cancelBtn = (new Button { Text = "Cancel", FontSize = 13, HeightRequest = 40, CornerRadius = 6, Padding = new Thickness(16, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnRow.Children.Add(moveBtn);
        btnRow.Children.Add(cancelBtn);
        stack.Children.Add(btnRow);

        card.Content = stack;
        popup.Children.Add(card);

        var originalContent = this.Content;
        var wrapper = new Grid();
        this.Content = wrapper;
        wrapper.Children.Add(originalContent);
        wrapper.Children.Add(popup);

        void Dismiss()
        {
            wrapper.Children.Remove(popup);
            wrapper.Children.Remove(originalContent);
            this.Content = originalContent;
        }

        moveBtn.Clicked += async (s, ev) =>
        {
            // Validate
            if (currentMode == "both" && startPicker.Date > endPicker.Date)
            {
                await DisplayAlert("Invalid Range", "Start date must be before end date.", "OK");
                return;
            }

            if (currentMode != "both")
            {
                string warning = currentMode == "after"
                    ? $"This will move ALL tasks from {startPicker.Date:MMM d, yyyy} onward. Are you sure?"
                    : $"This will move ALL tasks up to {endPicker.Date:MMM d, yyyy}. Are you sure?";
                if (!await DisplayAlert("Are you sure?", warning, "Yes", "No")) return;
            }

            Dismiss();
            tcs.TrySetResult((startPicker.Date, endPicker.Date, targetPicker.Date, currentMode));
        };

        cancelBtn.Clicked += (s, ev) => { Dismiss(); tcs.TrySetResult(null); };

        return await tcs.Task;
    }

    // ===================== DEDUP =====================

    private async void OnDedupClicked(object? sender, EventArgs e)
    {
        try
        {
            var active = await _taskService.GetActiveTasksAsync(_auth.CurrentUsername);
            var completed = await _taskService.GetCompletedTasksAsync(_auth.CurrentUsername);
            var allTasks = active.Concat(completed).Where(t => t.DueDate.HasValue).ToList();

            var byTitle = allTasks
                .GroupBy(t => t.Title.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Select(t => t.DueDate!.Value.Date).Distinct().Count() > 1)
                .OrderBy(g => g.Key)
                .ToList();

            if (byTitle.Count == 0)
            {
                await DisplayAlert("No Duplicates", "No tasks found on multiple dates.", "OK");
                return;
            }

            if (!await DisplayAlert("Duplicates Found",
                $"Found {byTitle.Count} tasks on multiple dates.\n\nPhase 1: Choose which date to keep for each.\nPhase 2: Batch delete all at once.",
                "Continue", "Cancel")) return;

            // ====== PHASE 1: Collect decisions (no DB calls) ======
            // Each decision: list of task IDs to delete
            var tasksToDelete = new List<TaskItem>();
            int totalSkipped = 0;

            for (int gi = 0; gi < byTitle.Count; gi++)
            {
                var group = byTitle[gi];
                string title = group.Key;
                var dates = group
                    .GroupBy(t => t.DueDate!.Value.Date)
                    .OrderBy(g => g.Key)
                    .ToList();

                var result = await ShowDedupPickerAsync(title, dates, gi + 1, byTitle.Count);

                if (result == null) break; // Cancel all

                if (result == "skip")
                {
                    totalSkipped++;
                    continue;
                }

                if (DateTime.TryParse(result, out DateTime keepDate))
                {
                    // Queue deletions for all NOT on keep date
                    foreach (var dateGroup in dates)
                    {
                        if (dateGroup.Key == keepDate.Date)
                        {
                            // Keep only first on the keep date, delete rest
                            var keepList = dateGroup.ToList();
                            for (int i = 1; i < keepList.Count; i++)
                                tasksToDelete.Add(keepList[i]);
                        }
                        else
                        {
                            foreach (var task in dateGroup)
                                tasksToDelete.Add(task);
                        }
                    }
                }
            }

            if (tasksToDelete.Count == 0)
            {
                await DisplayAlert("Nothing to Delete", $"Skipped: {totalSkipped}\nNo deletions queued.", "OK");
                return;
            }

            if (!await DisplayAlert("Confirm Deletion",
                $"Ready to delete {tasksToDelete.Count} duplicate tasks.\nSkipped: {totalSkipped}",
                "Delete Now", "Cancel")) return;

            // ====== PHASE 2: Batch delete with progress ======
            var originalContent = this.Content;
            var progressWrapper = new Grid();
            this.Content = progressWrapper;
            progressWrapper.Children.Add(originalContent);

            var progressOverlay = (new Grid
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);
            var progressStack = new VerticalStackLayout
            {
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Spacing = 12
            };
            progressStack.Children.Add((new ActivityIndicator { IsRunning = true, HeightRequest = 40, WidthRequest = 40 }).ThemeColor(ActivityIndicator.ColorProperty, ThemeKey.ButtonPrimaryText));
            var progressLabel = (new Label { Text = "Deleting duplicates...", FontSize = 16, HorizontalTextAlignment = TextAlignment.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1);
            var progressDetail = (new Label { Text = $"0 / {tasksToDelete.Count}", FontSize = 13, HorizontalTextAlignment = TextAlignment.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightIndigo, ThemeKey.PageText1);
            progressStack.Children.Add(progressLabel);
            progressStack.Children.Add(progressDetail);
            progressOverlay.Children.Add(progressStack);
            progressWrapper.Children.Add(progressOverlay);

            // Batch delete using transactions
            int deleted = 0;
            int batchSize = 50;
            var conn = await _db.GetConnectionAsync();

            for (int i = 0; i < tasksToDelete.Count; i += batchSize)
            {
                var batch = tasksToDelete.Skip(i).Take(batchSize).ToList();
                await conn.RunInTransactionAsync(db =>
                {
                    foreach (var task in batch)
                        db.Delete(task);
                });
                deleted += batch.Count;
                progressDetail.Text = $"{deleted} / {tasksToDelete.Count}";
                await Task.Yield();
            }

            // Remove overlay
            progressWrapper.Children.Remove(progressOverlay);
            progressWrapper.Children.Remove(originalContent);
            this.Content = originalContent;

            await DisplayAlert("Dedup Complete",
                $"Deleted: {deleted} duplicate tasks\nSkipped: {totalSkipped}",
                "OK");

            await LoadMonthAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async Task<string?> ShowDedupPickerAsync(string taskTitle, List<IGrouping<DateTime, TaskItem>> dates, int current, int total)
    {
        var tcs = new TaskCompletionSource<string?>();

        var popup = (new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);

        var card = (new Frame
        {
CornerRadius = 12, Padding = 20,
            WidthRequest = 500, MaximumHeightRequest = 600,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center, HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var stack = new VerticalStackLayout { Spacing = 10 };

        stack.Children.Add((new Label
        {
            Text = $"🧹 Duplicate {current} of {total}", FontSize = 16, FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1));

        // Full task text
        stack.Children.Add((new Label { Text = "Task:", FontSize = 11 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3));
        var taskFrame = (new Frame
        {
            Padding = 10, CornerRadius = 6, HasShadow = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightYellow).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.WarningButtonBackground);
        taskFrame.Content = new ScrollView
        {
            Content = (new Label { Text = taskTitle, FontSize = 13, LineBreakMode = LineBreakMode.WordWrap }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1),
            MaximumHeightRequest = 100
        };
        stack.Children.Add(taskFrame);

        // Dates
        int totalCopies = dates.Sum(d => d.Count());
        stack.Children.Add((new Label { Text = $"Found on {dates.Count} dates ({totalCopies} copies):", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));

        var dateListScroll = new ScrollView { MaximumHeightRequest = 120 };
        var dateListStack = new VerticalStackLayout { Spacing = 2 };
        foreach (var dg in dates)
            dateListStack.Children.Add((new Label { Text = $"  • {dg.Key:ddd, MMM d, yyyy} — {dg.Count()}", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));
        dateListScroll.Content = dateListStack;
        stack.Children.Add(dateListScroll);

        // Date picker
        stack.Children.Add((new Label { Text = "Keep on this date (delete all others):", FontSize = 12, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 6, 0, 0) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText3));

        var datePicker = (new DatePicker { Date = DateTime.Today, FontSize = 14 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        stack.Children.Add(datePicker);

        // Buttons
        var btnRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };

        var keepBtn = (new Button { Text = "✅ Keep", FontSize = 12, HeightRequest = 38, CornerRadius = 6, Padding = new Thickness(14, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        var skipBtn = (new Button { Text = "⏭️ Skip", FontSize = 12, HeightRequest = 38, CornerRadius = 6, Padding = new Thickness(14, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        var cancelBtn = (new Button { Text = "Cancel All", FontSize = 12, HeightRequest = 38, CornerRadius = 6, Padding = new Thickness(14, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

        btnRow.Children.Add(keepBtn);
        btnRow.Children.Add(skipBtn);
        btnRow.Children.Add(cancelBtn);
        stack.Children.Add(btnRow);

        card.Content = stack;
        popup.Children.Add(card);

        var originalContent = this.Content;
        var wrapper = new Grid();
        this.Content = wrapper;
        wrapper.Children.Add(originalContent);
        wrapper.Children.Add(popup);

        void Dismiss()
        {
            wrapper.Children.Remove(popup);
            wrapper.Children.Remove(originalContent);
            this.Content = originalContent;
        }

        keepBtn.Clicked += (s, ev) => { Dismiss(); tcs.TrySetResult(datePicker.Date.ToString("yyyy-MM-dd")); };
        skipBtn.Clicked += (s, ev) => { Dismiss(); tcs.TrySetResult("skip"); };
        cancelBtn.Clicked += (s, ev) => { Dismiss(); tcs.TrySetResult(null); };

        return await tcs.Task;
    }

    private class ImportTableInfo { public string Name { get; set; } = ""; }
    private class ImportColumnInfo { public string Name { get; set; } = ""; }
    private class ImportRowResult { public string RowData { get; set; } = ""; }

    /// <summary>
    /// Shows a popup with checkboxes for column selection, with Select All / Deselect All.
    /// Returns list of selected column names, or null if cancelled.
    /// </summary>
    private async Task<List<string>?> ShowColumnPickerAsync(List<string> columns)
    {
        var tcs = new TaskCompletionSource<List<string>?>();

        var popup = (new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);

        var card = (new Frame
        {
CornerRadius = 12, Padding = 20,
            WidthRequest = 420, MaximumHeightRequest = 500,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center, HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var mainStack = new VerticalStackLayout { Spacing = 10 };

        mainStack.Children.Add((new Label
        {
            Text = "Select Task Columns", FontSize = 18, FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1));
        mainStack.Children.Add((new Label
        {
            Text = "Check the columns that contain task data:", FontSize = 12         }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));

        var checkboxes = new List<(CheckBox cb, string name)>();

        var toggleRow = new HorizontalStackLayout { Spacing = 8 };
        var selectAllBtn = (new Button
        {
            Text = "Select All", FontSize = 12, HeightRequest = 30,
            CornerRadius = 6, Padding = new Thickness(10, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ButtonPrimaryBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        selectAllBtn.Clicked += (s, e) => { foreach (var (cb, _) in checkboxes) cb.IsChecked = true; };
        toggleRow.Children.Add(selectAllBtn);

        var deselectAllBtn = (new Button
        {
            Text = "Deselect All", FontSize = 12, HeightRequest = 30,
            CornerRadius = 6, Padding = new Thickness(10, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        deselectAllBtn.Clicked += (s, e) => { foreach (var (cb, _) in checkboxes) cb.IsChecked = false; };
        toggleRow.Children.Add(deselectAllBtn);
        mainStack.Children.Add(toggleRow);

        var checkboxStack = new VerticalStackLayout { Spacing = 4 };
        foreach (var col in columns)
        {
            var row = new HorizontalStackLayout { Spacing = 8 };
            var cb = (new CheckBox { IsChecked = true }).ThemeColor(CheckBox.ColorProperty, ThemeKey.InfoText);
            row.Children.Add(cb);
            row.Children.Add((new Label { Text = col, FontSize = 14, VerticalOptions = LayoutOptions.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1));
            checkboxes.Add((cb, col));
            checkboxStack.Children.Add(row);
        }

        var scrollView = new ScrollView { Content = checkboxStack, MaximumHeightRequest = 280 };
        mainStack.Children.Add(scrollView);

        var btnRow = new HorizontalStackLayout { Spacing = 12, HorizontalOptions = LayoutOptions.End, Margin = new Thickness(0, 8, 0, 0) };
        var cancelBtn = (new Button { Text = "Cancel", CornerRadius = 6, Padding = new Thickness(16, 8) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        var confirmBtn = (new Button { Text = "✅ Confirm", CornerRadius = 6, Padding = new Thickness(16, 8), FontAttributes = FontAttributes.Bold }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnRow.Children.Add(cancelBtn);
        btnRow.Children.Add(confirmBtn);
        mainStack.Children.Add(btnRow);

        card.Content = mainStack;
        popup.Children.Add(card);

        // Use a wrapper grid without swapping Content
        var pageContent = this.Content;
        var wrapper = new Grid();
        this.Content = wrapper;
        wrapper.Children.Add(pageContent);
        wrapper.Children.Add(popup);

        cancelBtn.Clicked += (s, e) =>
        {
            wrapper.Children.Remove(popup);
            wrapper.Children.Remove(pageContent);
            this.Content = pageContent;
            tcs.TrySetResult(null);
        };

        confirmBtn.Clicked += (s, e) =>
        {
            var selected = checkboxes.Where(x => x.cb.IsChecked).Select(x => x.name).ToList();
            wrapper.Children.Remove(popup);
            wrapper.Children.Remove(pageContent);
            this.Content = pageContent;
            tcs.TrySetResult(selected);
        };

        return await tcs.Task;
    }
}

/// <summary>
/// Sub-page showing all tasks for a specific date with add/complete/delete functionality.
/// </summary>
public class CalendarDayPage : Bannister.Views.ModeThemedPage
{
    private const string NotYetPlacedCategory = "not yet placed";

    private readonly AuthService _auth;
    private readonly TaskService _taskService;
    private readonly DateTime _date;
    private readonly IdeaLoggerService? _ideaLogger;
    private readonly LookoutService _lookoutService;
    private readonly RoutineService _routineService;
    private readonly PostponedTaskService _postponedTaskService;

    private VerticalStackLayout _activeStack;
    private VerticalStackLayout _completedStack;
    private VerticalStackLayout _urgentStack;
    private VerticalStackLayout _lookoutsStack;
    private VerticalStackLayout _postponedStack;
    private Label _activeHeader;
    private Label _completedHeader;
    private Label _urgentHeader;
    private Label _lookoutsHeader;
    private Label _postponedHeader;
    private int _loadTasksVersion;
    private bool _isAddingLookout;
    private Dictionary<int, Routine> _routineById = new();

    public CalendarDayPage(AuthService auth, TaskService taskService, DateTime date, IdeasService? ideasService = null, IdeaLoggerService? ideaLogger = null, LookoutService? lookoutService = null, RoutineService? routineService = null, PostponedTaskService? postponedTaskService = null)
    {
        _auth = auth;
        _taskService = taskService;
        _date = date.Date;
        _ideaLogger = ideaLogger
            ?? Application.Current?.Handler?.MauiContext?.Services.GetService<IdeaLoggerService>();
        _lookoutService = lookoutService ?? throw new ArgumentNullException(nameof(lookoutService));
        _routineService = routineService ?? throw new ArgumentNullException(nameof(routineService));
        _postponedTaskService = postponedTaskService ?? throw new ArgumentNullException(nameof(postponedTaskService));
        Title = date.ToString("MMM d, yyyy");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTasksAsync();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout { Padding = 16, Spacing = 12 };

        // Header
        var headerFrame = (new Frame
        {
BorderColor = Colors.Transparent,
            CornerRadius = 10, Padding = 16, HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoText);
        var headerStack = new VerticalStackLayout { Spacing = 4 };
        headerStack.Children.Add((new Label
        {
            Text = _date.ToString("dddd"), FontSize = 14
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightIndigo, ThemeKey.PageText1));
        headerStack.Children.Add((new Label
        {
            Text = _date.ToString("MMMM d, yyyy"), FontSize = 22,
            FontAttributes = FontAttributes.Bold         }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1));

        bool isToday = _date == DateTime.Today;
        bool isPast = _date < DateTime.Today;
        if (isToday)
            headerStack.Children.Add((new Label { Text = "Today", FontSize = 13 }).ThemeColor(Label.TextColorProperty, ThemeKey.MediaText, ThemeKey.PageText1));
        else if (isPast)
            headerStack.Children.Add((new Label { Text = "Past", FontSize = 13 }).ThemeColor(Label.TextColorProperty, ThemeKey.DisabledButtonBackground, ThemeKey.PageText1));

        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        // Date actions
        var actionRow = new HorizontalStackLayout { Spacing = 10 };

        var addBtn = (new Button
        {
            Text = "+ Add Task", FontSize = 14, HeightRequest = 44,
            CornerRadius = 8, Padding = new Thickness(20, 0),
            HorizontalOptions = LayoutOptions.Start
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        addBtn.Clicked += OnAddTaskClicked;
        actionRow.Children.Add(addBtn);

        var addLookoutBtn = (new Button
        {
            Text = "+ Add Lookout",
            FontSize = 13,
            HeightRequest = 44,
            CornerRadius = 8,
            Padding = new Thickness(16, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        ToolTipProperties.SetText(addLookoutBtn, "Add a scenario lookout for this date");
        addLookoutBtn.Clicked += async (s, e) => await AddLookoutAsync();
        actionRow.Children.Add(addLookoutBtn);

        var moveAllUnplacedBtn = (new Button
        {
            Text = "Move all to not yet placed",
            FontSize = 13,
            HeightRequest = 44,
            CornerRadius = 8,
            Padding = new Thickness(16, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        ToolTipProperties.SetText(moveAllUnplacedBtn, "Move every active task on this date to not yet placed");
        moveAllUnplacedBtn.Clicked += async (s, e) => await MoveAllActiveTasksToNotYetPlacedAsync();
        actionRow.Children.Add(moveAllUnplacedBtn);

        mainStack.Children.Add(actionRow);

        _lookoutsHeader = (new Label
        {
            Text = "Lookouts (0)",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 8, 0, 4)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText1);
        mainStack.Children.Add(_lookoutsHeader);

        _lookoutsStack = new VerticalStackLayout { Spacing = 6 };
        mainStack.Children.Add(_lookoutsStack);

        _postponedHeader = (new Label
        {
            Text = "Postponed Tasks (0)",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 8, 0, 4)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText1);
        mainStack.Children.Add(_postponedHeader);

        _postponedStack = new VerticalStackLayout { Spacing = 6 };
        mainStack.Children.Add(_postponedStack);

        // Urgent tasks (above everything)
        _urgentHeader = (new Label
        {
            Text = "🔴 Urgent (0)", FontSize = 16, FontAttributes = FontAttributes.Bold,
Margin = new Thickness(0, 8, 0, 4),
            IsVisible = false
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ErrorText, ThemeKey.PageText1);
        mainStack.Children.Add(_urgentHeader);

        _urgentStack = new VerticalStackLayout { Spacing = 6 };
        mainStack.Children.Add(_urgentStack);

        // Active tasks
        _activeHeader = (new Label
        {
            Text = "📋 Active Tasks (0)", FontSize = 16, FontAttributes = FontAttributes.Bold,
Margin = new Thickness(0, 8, 0, 4)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText1);
        mainStack.Children.Add(_activeHeader);

        _activeStack = new VerticalStackLayout { Spacing = 6 };
        mainStack.Children.Add(_activeStack);

        // Completed tasks
        _completedHeader = (new Label
        {
            Text = "✓ Completed (0)", FontSize = 16, FontAttributes = FontAttributes.Bold,
Margin = new Thickness(0, 12, 0, 4)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.SuccessButtonBackground, ThemeKey.PageText1);
        mainStack.Children.Add(_completedHeader);

        _completedStack = new VerticalStackLayout { Spacing = 6 };
        mainStack.Children.Add(_completedStack);

        Content = new ScrollView { Content = mainStack };
    }

    private async Task LoadTasksAsync()
    {
        var loadVersion = ++_loadTasksVersion;

        var active = await _taskService.GetActiveTasksAsync(_auth.CurrentUsername);
        var completed = await _taskService.GetCompletedTasksAsync(_auth.CurrentUsername);

        var dayActive = active.Where(t => t.DueDate.HasValue && t.DueDate.Value.Date == _date).ToList();
        var dayCompleted = completed.Where(t => t.DueDate.HasValue && t.DueDate.Value.Date == _date).ToList();
        var lookouts = await _lookoutService.GetForDateAsync(_auth.CurrentUsername, _date);
        var postponedTasks = await _postponedTaskService.GetActiveForDateAsync(_auth.CurrentUsername, _date);
        await LoadRoutineLookupAsync(dayActive.Concat(dayCompleted));

        if (loadVersion != _loadTasksVersion)
            return;

        var urgent = dayActive.Where(t => t.IsUrgent).ToList();
        var regular = dayActive.Where(t => !t.IsUrgent).ToList();

        var lookoutCards = new List<View>();
        foreach (var lookout in lookouts)
        {
            var card = await BuildLookoutCardAsync(lookout);
            if (loadVersion != _loadTasksVersion)
                return;

            lookoutCards.Add(card);
        }

        _lookoutsHeader.Text = $"Lookouts ({lookouts.Count})";
        _lookoutsStack.Children.Clear();
        if (lookouts.Count == 0)
        {
            _lookoutsStack.Children.Add((new Label { Text = "No lookouts for this day.", FontSize = 13, Margin = new Thickness(8, 4) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2));
        }
        else
        {
            foreach (var card in lookoutCards)
                _lookoutsStack.Children.Add(card);
        }

        _postponedHeader.Text = $"Postponed Tasks ({postponedTasks.Count})";
        _postponedStack.Children.Clear();
        if (postponedTasks.Count == 0)
        {
            _postponedStack.Children.Add((new Label { Text = "No postponed tasks for this day.", FontSize = 13, Margin = new Thickness(8, 4) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2));
        }
        else
        {
            foreach (var task in postponedTasks)
                _postponedStack.Children.Add(BuildPostponedTaskCard(task));
        }

        // Urgent section
        _urgentHeader.Text = $"🔴 Urgent ({urgent.Count})";
        _urgentHeader.IsVisible = urgent.Count > 0;
        _urgentStack.Children.Clear();
        foreach (var task in urgent)
            _urgentStack.Children.Add(BuildTaskCard(task, false));

        // Active section
        _activeHeader.Text = $"📋 Active Tasks ({regular.Count})";
        _activeStack.Children.Clear();
        if (regular.Count == 0 && urgent.Count == 0)
        {
            _activeStack.Children.Add((new Label { Text = "No active tasks for this day.", FontSize = 13, Margin = new Thickness(8, 4) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2));
        }
        else
        {
            foreach (var task in regular)
                _activeStack.Children.Add(BuildTaskCard(task, false));
        }

        // Completed section
        _completedHeader.Text = $"✓ Completed ({dayCompleted.Count})";
        _completedStack.Children.Clear();
        if (dayCompleted.Count == 0)
        {
            _completedStack.Children.Add((new Label { Text = "No completed tasks.", FontSize = 13, Margin = new Thickness(8, 4) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2));
        }
        else
        {
            foreach (var task in dayCompleted)
                _completedStack.Children.Add(BuildTaskCard(task, true));
        }
    }

    private async Task<Frame> BuildLookoutCardAsync(LookoutScenario lookout)
    {
        var items = await _lookoutService.GetItemsAsync(lookout.Id);

        var frame = (new Frame
        {
            CornerRadius = 8,
            Padding = 12,
            HasShadow = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightViolet).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.HighlightVioletText);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        var textStack = new VerticalStackLayout { Spacing = 3 };
        textStack.Children.Add((new Label
        {
            Text = lookout.Title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText1));
        textStack.Children.Add((new Label
        {
            Text = $"{items.Count} list item(s)",
            FontSize = 11
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText3));
        if (!string.IsNullOrWhiteSpace(lookout.Notes))
        {
            textStack.Children.Add((new Label
            {
                Text = lookout.Notes,
                FontSize = 11,
                MaxLines = 2,
                LineBreakMode = LineBreakMode.TailTruncation
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));
        }
        grid.Add(textStack, 0, 0);

        var buttonStack = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };

        var openBtn = (new Button
        {
            Text = "List",
            FontSize = 11,
            WidthRequest = 54,
            HeightRequest = 30,
            CornerRadius = 4,
            Padding = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        ToolTipProperties.SetText(openBtn, "Open lookout list");
        openBtn.Clicked += async (s, e) => await OpenLookoutAsync(lookout);
        buttonStack.Children.Add(openBtn);

        var dateBtn = (new Button
        {
            Text = "Date",
            FontSize = 11,
            WidthRequest = 54,
            HeightRequest = 30,
            CornerRadius = 4,
            Padding = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        ToolTipProperties.SetText(dateBtn, "Change lookout display date");
        dateBtn.Clicked += async (s, e) => await ChangeLookoutDateAsync(lookout);
        buttonStack.Children.Add(dateBtn);

        var removeBtn = (new Button
        {
            Text = "Remove",
            FontSize = 10,
            WidthRequest = 54,
            HeightRequest = 30,
            CornerRadius = 4,
            Padding = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
        ToolTipProperties.SetText(removeBtn, "Remove lookout from calendar only");
        removeBtn.Clicked += async (s, e) => await RemoveLookoutFromCalendarAsync(lookout);
        buttonStack.Children.Add(removeBtn);

        grid.Add(buttonStack, 1, 0);
        frame.Content = grid;

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) => await OpenLookoutAsync(lookout);
        frame.GestureRecognizers.Add(tap);

        return frame;
    }

    private async Task AddLookoutAsync()
    {
        if (_isAddingLookout)
            return;

        _isAddingLookout = true;
        try
        {
            string? title = await DisplayPromptAsync(
                "New Lookout",
                $"Scenario to watch for on {_date:MMM d, yyyy}:",
                "Add",
                "Cancel",
                placeholder: "Scenario title...");

            if (string.IsNullOrWhiteSpace(title))
                return;

            string? notes = await DisplayPromptAsync(
                "Lookout Notes",
                "Optional notes:",
                "Save",
                "Skip",
                initialValue: "");

            var lookout = await _lookoutService.CreateAsync(_auth.CurrentUsername, title.Trim(), _date, notes ?? "");
            await OpenLookoutAsync(lookout);
        }
        finally
        {
            _isAddingLookout = false;
        }
    }

    private async Task OpenLookoutAsync(LookoutScenario lookout)
    {
        var page = new LookoutScenarioPage(_auth, _lookoutService, lookout);
        page.Disappearing += async (s, e) => await LoadTasksAsync();
        await Navigation.PushAsync(page);
    }

    private async Task ChangeLookoutDateAsync(LookoutScenario lookout)
    {
        var date = await ShowLookoutDatePickerAsync(lookout);
        if (date == null)
            return;

        lookout.DisplayDate = date.Value.Date;
        await _lookoutService.UpdateAsync(lookout);
        await LoadTasksAsync();
    }

    private async Task<DateTime?> ShowLookoutDatePickerAsync(LookoutScenario lookout)
    {
        var tcs = new TaskCompletionSource<DateTime?>();
        var popup = (new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);

        var card = (new Frame
        {
            CornerRadius = 12,
            Padding = 20,
            WidthRequest = 420,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var stack = new VerticalStackLayout { Spacing = 10 };
        stack.Children.Add((new Label { Text = "Change Lookout Date", FontSize = 18, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText1));
        stack.Children.Add((new Label { Text = lookout.Title, FontSize = 13, LineBreakMode = LineBreakMode.WordWrap }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1));

        var picker = (new DatePicker
        {
            Date = lookout.DisplayDate?.Date ?? _date,
            FontSize = 14
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightViolet);
        stack.Children.Add(picker);

        var buttons = new HorizontalStackLayout { Spacing = 10, Margin = new Thickness(0, 8, 0, 0) };
        var saveBtn = (new Button { Text = "Save", FontSize = 13, HeightRequest = 38, CornerRadius = 6, Padding = new Thickness(16, 0), FontAttributes = FontAttributes.Bold }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        var cancelBtn = (new Button { Text = "Cancel", FontSize = 13, HeightRequest = 38, CornerRadius = 6, Padding = new Thickness(16, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        buttons.Children.Add(saveBtn);
        buttons.Children.Add(cancelBtn);
        stack.Children.Add(buttons);

        card.Content = stack;
        popup.Children.Add(card);

        var originalContent = Content;
        var wrapper = new Grid();
        Content = wrapper;
        wrapper.Children.Add(originalContent);
        wrapper.Children.Add(popup);

        void Dismiss()
        {
            wrapper.Children.Remove(popup);
            wrapper.Children.Remove(originalContent);
            Content = originalContent;
        }

        saveBtn.Clicked += (s, e) => { Dismiss(); tcs.TrySetResult(picker.Date); };
        cancelBtn.Clicked += (s, e) => { Dismiss(); tcs.TrySetResult(null); };

        return await tcs.Task;
    }

    private async Task RemoveLookoutFromCalendarAsync(LookoutScenario lookout)
    {
        if (!await DisplayAlert(
            "Remove from calendar?",
            $"Remove \"{lookout.Title}\" from this date?\n\nIts list will not be deleted.",
            "Remove",
            "Cancel"))
        {
            return;
        }

        await _lookoutService.RemoveFromCalendarAsync(lookout);
        await LoadTasksAsync();
    }

    private Frame BuildPostponedTaskCard(PostponedTask task)
    {
        var frame = (new Frame
        {
            CornerRadius = 8,
            Padding = 12,
            HasShadow = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.WarningText);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        var textStack = new VerticalStackLayout { Spacing = 3 };
        textStack.Children.Add((new Label
        {
            Text = task.Title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText1));

        if (task.TimesPostponed > 0)
            textStack.Children.Add((new Label { Text = $"Postponed {task.TimesPostponed}x", FontSize = 11, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText3));

        if (!string.IsNullOrWhiteSpace(task.Description))
            textStack.Children.Add((new Label { Text = task.Description, FontSize = 12, MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));

        grid.Add(textStack, 0, 0);

        var buttonStack = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };

        var postponeBtn = (new Button
        {
            Text = "Postpone",
            FontSize = 11,
            WidthRequest = 82,
            HeightRequest = 30,
            CornerRadius = 4,
            Padding = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightYellow).ThemeColor(Button.TextColorProperty, ThemeKey.WarningText);
        postponeBtn.Clicked += async (s, e) => await PostponePostponedTaskAsync(task);
        buttonStack.Children.Add(postponeBtn);

        var doneBtn = (new Button
        {
            Text = "Mark Done",
            FontSize = 11,
            WidthRequest = 82,
            HeightRequest = 30,
            CornerRadius = 4,
            Padding = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessBackground).ThemeColor(Button.TextColorProperty, ThemeKey.SuccessText);
        doneBtn.Clicked += async (s, e) => await MarkPostponedTaskDoneAsync(task);
        buttonStack.Children.Add(doneBtn);

        grid.Add(buttonStack, 1, 0);
        frame.Content = grid;
        return frame;
    }

    private async Task PostponePostponedTaskAsync(PostponedTask task)
    {
        var date = await ChoosePostponedTaskDateAsync("Postpone task", task.CurrentDate.Date);
        if (date == null) return;

        await _postponedTaskService.PostponeAsync(task.Id, date.Value);
        await LoadTasksAsync();
    }

    private async Task MarkPostponedTaskDoneAsync(PostponedTask task)
    {
        if (!await DisplayAlert("Mark done?", $"Mark \"{task.Title}\" done?", "Mark Done", "Cancel"))
            return;

        await _postponedTaskService.MarkDoneAsync(task.Id);
        await LoadTasksAsync();
    }

    private async Task<DateTime?> ChoosePostponedTaskDateAsync(string title, DateTime baseDate)
    {
        var action = await DisplayActionSheet(
            title,
            "Cancel",
            null,
            "7 days",
            "30 days",
            "90 days",
            "180 days",
            "365 days",
            "Pick a date...");
        return action switch
        {
            "7 days" => baseDate.AddDays(7),
            "30 days" => baseDate.AddDays(30),
            "90 days" => baseDate.AddDays(90),
            "180 days" => baseDate.AddDays(180),
            "365 days" => baseDate.AddDays(365),
            "Pick a date..." => await ShowPostponedTaskDatePickerAsync(title, baseDate),
            _ => null
        };
    }

    private async Task<DateTime?> ShowPostponedTaskDatePickerAsync(string title, DateTime initialDate)
    {
        var tcs = new TaskCompletionSource<DateTime?>();

        var popup = (new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);

        var picker = (new DatePicker
        {
            Date = initialDate.Date,
            MinimumDate = DateTime.Today.AddDays(1),
            FontSize = 14
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningBackground);

        var saveBtn = (new Button { Text = "Save", FontSize = 13, HeightRequest = 38, CornerRadius = 6, Padding = new Thickness(16, 0), FontAttributes = FontAttributes.Bold }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        var cancelBtn = (new Button { Text = "Cancel", FontSize = 13, HeightRequest = 38, CornerRadius = 6, Padding = new Thickness(16, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

        var card = (new Frame
        {
            CornerRadius = 12,
            Padding = 20,
            WidthRequest = 400,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HasShadow = true,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    (new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText1),
                    picker,
                    new HorizontalStackLayout { Spacing = 10, Children = { cancelBtn, saveBtn } }
                }
            }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        popup.Children.Add(card);

        var originalContent = Content;
        var wrapper = new Grid();
        Content = wrapper;
        wrapper.Children.Add(originalContent);
        wrapper.Children.Add(popup);

        void Dismiss(DateTime? result)
        {
            wrapper.Children.Remove(popup);
            wrapper.Children.Remove(originalContent);
            Content = originalContent;
            tcs.TrySetResult(result);
        }

        saveBtn.Clicked += (s, e) => Dismiss(picker.Date);
        cancelBtn.Clicked += (s, e) => Dismiss(null);

        return await tcs.Task;
    }

    private async Task LoadRoutineLookupAsync(IEnumerable<TaskItem> tasks)
    {
        _routineById.Clear();
        var routineIds = tasks
            .Where(t => t.RoutineId.HasValue)
            .Select(t => t.RoutineId!.Value)
            .Distinct()
            .ToList();

        foreach (int routineId in routineIds)
        {
            var routine = await _routineService.GetRoutineAsync(routineId);
            if (routine != null)
                _routineById[routineId] = routine;
        }
    }

    private Frame BuildTaskCard(TaskItem task, bool isCompleted)
    {
        string priorityIcon = task.Priority switch { 3 => "🔴", 2 => "🟡", 1 => "🟢", _ => "⚪" };
        bool isRoutine = task.RoutineId.HasValue;
        _routineById.TryGetValue(task.RoutineId ?? 0, out var routine);

        var frame = (new Frame
        {
            CornerRadius = 8, Padding = 12, HasShadow = !isCompleted
        }).ThemeColor(VisualElement.BackgroundColorProperty, isCompleted ? ThemeKey.SuccessBackground : isRoutine ? ThemeKey.HighlightTeal : task.IsUrgent ? ThemeKey.ErrorBackground : ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, isCompleted ? ThemeKey.SuccessButtonBackground : isRoutine ? ThemeKey.HighlightTealText : task.IsUrgent ? ThemeKey.ErrorText : ThemeKey.Separator);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        var textStack = new VerticalStackLayout { Spacing = 3 };

        string urgentPrefix = task.IsUrgent ? "🔴 " : "";
        string routinePrefix = isRoutine ? "↻ " : "";
        textStack.Children.Add((new Label
        {
            Text = $"{routinePrefix}{urgentPrefix}{priorityIcon} {task.Title}",
            FontSize = 14, FontAttributes = isCompleted ? FontAttributes.None : FontAttributes.Bold,
            TextDecorations = isCompleted ? TextDecorations.Strikethrough : TextDecorations.None,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, isCompleted ? ThemeKey.CardText2 : isRoutine ? ThemeKey.HighlightTealText : task.IsUrgent ? ThemeKey.ErrorText : ThemeKey.CardText1, ThemeKey.PageText1));

        if (isRoutine && routine != null)
            textStack.Children.Add((new Label { Text = $"Routine · {RoutineService.FormatRoutineFrequency(routine)}", FontSize = 11 }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightTealText, ThemeKey.PageText3));

        if (!string.IsNullOrEmpty(task.Category))
            textStack.Children.Add((new Label { Text = $"📁 {task.Category}", FontSize = 11 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3));

        if (!string.IsNullOrEmpty(task.Notes))
            textStack.Children.Add((new Label { Text = task.Notes, FontSize = 11, MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3));

        grid.Add(textStack, 0, 0);

        // Action buttons
        var btnStack = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };

        if (!isCompleted)
        {
            // Urgent toggle
            var urgentBtn = (new Button
            {
                Text = task.IsUrgent ? "🔴" : "⚪",
                FontSize = 14, WidthRequest = 36, HeightRequest = 32,
                CornerRadius = 4, Padding = 0
            }).ThemeColor(VisualElement.BackgroundColorProperty, task.IsUrgent ? ThemeKey.ErrorBackground : ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
            ToolTipProperties.SetText(urgentBtn, task.IsUrgent ? "Remove urgent flag" : "Mark as urgent");
            urgentBtn.Clicked += async (s, e) =>
            {
                task.IsUrgent = !task.IsUrgent;
                await _taskService.UpdateTaskAsync(task);
                await LoadTasksAsync();
            };
            btnStack.Children.Add(urgentBtn);

            var completeBtn = (new Button
            {
                Text = "✅", FontSize = 14, WidthRequest = 36, HeightRequest = 32,
                CornerRadius = 4, Padding = 0
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessBackground).ThemeColor(Button.TextColorProperty, ThemeKey.SuccessText);
            ToolTipProperties.SetText(completeBtn, "Mark task complete");
            completeBtn.Clicked += async (s, e) =>
            {
                await _taskService.CompleteTaskAsync(task);
                if (task.RoutineId.HasValue)
                    await _routineService.OnTaskCompletedAsync(task.Id);
                await LoadTasksAsync();
            };
            btnStack.Children.Add(completeBtn);

            // Postpone button
            var postponeBtn = (new Button
            {
                Text = isRoutine ? "↻" : "⏩", FontSize = 14, WidthRequest = 36, HeightRequest = 32,
                CornerRadius = 4, Padding = 0
            }).ThemeColor(VisualElement.BackgroundColorProperty, isRoutine ? ThemeKey.HighlightTeal : ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, isRoutine ? ThemeKey.HighlightTealText : ThemeKey.InfoText);
            ToolTipProperties.SetText(postponeBtn, isRoutine && routine != null ? RoutineService.FormatPostponeTooltip(routine) : "Move task to a later date");
            postponeBtn.Clicked += async (s, ev) =>
            {
                if (task.RoutineId.HasValue)
                {
                    await _routineService.PostponeRoutineInstanceAsync(task.Id);
                    await LoadTasksAsync();
                }
                else
                {
                    await ShowPostponeAsync(task);
                }
            };
            btnStack.Children.Add(postponeBtn);

            if (task.RoutineId.HasValue)
            {
                var editDateBtn = (new Button
                {
                    Text = "📅",
                    FontSize = 14,
                    WidthRequest = 38,
                    HeightRequest = 38,
                    CornerRadius = 6,
                    Padding = 0,
                    VerticalOptions = LayoutOptions.Center
                }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
                ToolTipProperties.SetText(editDateBtn, "Change routine date");
                var capturedTask = task;
                editDateBtn.Clicked += async (_, _) =>
                {
                    var newDate = await ShowRoutineDatePickerAsync(capturedTask);
                    if (newDate.HasValue)
                    {
                        await _routineService.SetOpenTaskDueDateAsync(
                            capturedTask.RoutineId!.Value,
                            newDate.Value);
                        await LoadTasksAsync();
                    }
                };
                btnStack.Children.Add(editDateBtn);
            }

            if (!isRoutine)
            {
                var unplaceBtn = (new Button
                {
                    Text = "📥",
                    FontSize = 14,
                    WidthRequest = 36,
                    HeightRequest = 32,
                    CornerRadius = 4,
                    Padding = 0
                }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightViolet).ThemeColor(Button.TextColorProperty, ThemeKey.HighlightVioletText);
                ToolTipProperties.SetText(unplaceBtn, "Move task to not yet placed and remove its date");
                unplaceBtn.Clicked += async (s, ev) => await MoveTaskToNotYetPlacedAsync(task);
                btnStack.Children.Add(unplaceBtn);
            }
        }
        else
        {
            var uncompleteBtn = (new Button
            {
                Text = "↩", FontSize = 14, WidthRequest = 36, HeightRequest = 32,
                CornerRadius = 4, Padding = 0
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightYellow).ThemeColor(Button.TextColorProperty, ThemeKey.HighlightYellowText);
            ToolTipProperties.SetText(uncompleteBtn, "Move task back to active");
            uncompleteBtn.Clicked += async (s, e) =>
            {
                await _taskService.UncompleteTaskAsync(task);
                await LoadTasksAsync();
            };
            btnStack.Children.Add(uncompleteBtn);
        }

        // Edit button
        var editBtn = (new Button
        {
            Text = "✏️", FontSize = 14, WidthRequest = 36, HeightRequest = 32,
            CornerRadius = 4, Padding = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        ToolTipProperties.SetText(editBtn, "Edit task title and notes");
        editBtn.Clicked += async (s, e) => await EditTaskAsync(task);
        btnStack.Children.Add(editBtn);

        if (!isRoutine)
        {
            var deleteBtn = (new Button
            {
                Text = "🗑", FontSize = 14, WidthRequest = 36, HeightRequest = 32,
                CornerRadius = 4, Padding = 0
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
            ToolTipProperties.SetText(deleteBtn, "Delete task");
            deleteBtn.Clicked += async (s, e) =>
            {
                if (await DisplayAlert("Delete?", $"Delete \"{task.Title}\"?", "Delete", "Cancel"))
                {
                    await _taskService.DeleteTaskAsync(task);
                    await LoadTasksAsync();
                }
            };
            btnStack.Children.Add(deleteBtn);
        }

        grid.Add(btnStack, 1, 0);
        frame.Content = grid;
        return frame;
    }

    private async Task<DateTime?> ShowRoutineDatePickerAsync(TaskItem task)
    {
        var initialDate = task.DueDate?.Date ?? DateTime.Today;

        string result = await DisplayPromptAsync(
            "Edit Routine Date",
            $"Current date: {initialDate:yyyy-MM-dd}\n\nEnter new date (YYYY-MM-DD):",
            "Save",
            "Cancel",
            initialValue: initialDate.ToString("yyyy-MM-dd"));

        if (string.IsNullOrEmpty(result)) return null;

        if (DateTime.TryParse(result, out DateTime newDate))
            return newDate;

        await DisplayAlert("Invalid Date", "Please enter a valid date in YYYY-MM-DD format.", "OK");
        return null;
    }

    private async Task MoveTaskToNotYetPlacedAsync(TaskItem task)
    {
        if (!await DisplayAlert(
            "Move to not yet placed?",
            $"Remove the date from \"{task.Title}\" and move it to not yet placed?",
            "Move",
            "Cancel"))
        {
            return;
        }

        task.DueDate = null;
        task.Category = NotYetPlacedCategory;
        await _taskService.UpdateTaskAsync(task);
        await LoadTasksAsync();
    }

    private async Task MoveAllActiveTasksToNotYetPlacedAsync()
    {
        var active = await _taskService.GetActiveTasksAsync(_auth.CurrentUsername);
        var tasksToMove = active
            .Where(t => t.DueDate.HasValue && t.DueDate.Value.Date == _date)
            .ToList();

        if (tasksToMove.Count == 0)
        {
            await DisplayAlert("No Tasks", "There are no active tasks on this date to move.", "OK");
            return;
        }

        if (!await DisplayAlert(
            "Move all to not yet placed?",
            $"Move {tasksToMove.Count} active task(s) from {_date:MMM d, yyyy} to not yet placed?",
            "Move All",
            "Cancel"))
        {
            return;
        }

        foreach (var task in tasksToMove)
        {
            task.DueDate = null;
            task.Category = NotYetPlacedCategory;
            await _taskService.UpdateTaskAsync(task);
        }

        await LoadTasksAsync();
    }

    private async Task EditTaskAsync(TaskItem task)
    {
        string originalTitle = task.Title;
        string originalNotes = task.Notes ?? "";

        // Edit title
        string? newTitle = await DisplayPromptAsync("Edit Task",
            "Update task title:", "Save", "Cancel",
            initialValue: task.Title);

        if (newTitle == null) return; // Cancelled

        // Edit notes
        string? newNotes = await DisplayPromptAsync("Edit Notes",
            "Update notes (optional):", "Save", "Skip",
            initialValue: task.Notes ?? "");

        string finalTitle = newTitle.Trim();
        string finalNotes = newNotes != null ? newNotes.Trim() : originalNotes;

        bool titleChanged = finalTitle != originalTitle;
        bool notesChanged = newNotes != null && finalNotes != originalNotes;

        if (!titleChanged && !notesChanged) return; // Nothing changed

        // Create new task with edited values (same date, priority, category, urgent status)
        var newTask = await _taskService.CreateTaskAsync(
            _auth.CurrentUsername, finalTitle,
            task.Category ?? "General", task.Priority,
            task.DueDate);
        newTask.IsUrgent = task.IsUrgent;
        newTask.Notes = string.IsNullOrWhiteSpace(finalNotes) ? null : finalNotes;
        await _taskService.UpdateTaskAsync(newTask);

        // Archive the original (move to "archived_edited" category, mark completed)
        task.Category = "archived_edited";
        task.IsCompleted = true;
        task.CompletedAt = DateTime.UtcNow;
        task.Notes = (task.Notes ?? "") + $"\n[Edited {DateTime.Now:MMM d, yyyy HH:mm} → new: \"{finalTitle}\"]";
        await _taskService.UpdateTaskAsync(task);

        // Log to ideas
        string dateStr = newTask.DueDate.HasValue ? newTask.DueDate.Value.ToString("MMM d") : "no date";
        string logText = titleChanged
            ? $"[{dateStr}] Edited: \"{originalTitle}\" → \"{finalTitle}\""
            : $"[{dateStr}] Notes updated: \"{finalTitle}\"";
        if (_ideaLogger != null)
            await _ideaLogger.LogSilentlyAsync(new IdeaLogRequest
            {
                Username = _auth.CurrentUsername,
                Title = logText,
                Body = logText,
                Category = "calendar_tasks"
            });

        await LoadTasksAsync();
    }

    private async Task ShowPostponeAsync(TaskItem task)
    {
        var tcs = new TaskCompletionSource<DateTime?>();

        var popup = (new Grid
        {
            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);

        var card = (new Frame
        {
CornerRadius = 12, Padding = 20,
            WidthRequest = 400, HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center, HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var stack = new VerticalStackLayout { Spacing = 10 };

        stack.Children.Add((new Label { Text = "⏩ Postpone Task", FontSize = 16, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText1));

        // Task title
        string truncTitle = task.Title.Length > 80 ? task.Title.Substring(0, 77) + "..." : task.Title;
        stack.Children.Add((new Label { Text = truncTitle, FontSize = 13 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1));

        // Current date
        string currentDateStr = task.DueDate.HasValue ? task.DueDate.Value.ToString("MMM d, yyyy") : "No date";
        stack.Children.Add((new Label { Text = $"Currently: {currentDateStr}", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3));

        // Target date picker — default tomorrow
        var tomorrow = DateTime.Today.AddDays(1);
        stack.Children.Add((new Label { Text = "Postpone to:", FontSize = 12, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 4, 0, 0) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText3));

        var datePicker = (new DatePicker
        {
            Date = tomorrow,
            MinimumDate = DateTime.Today.AddDays(1), // Can't postpone to past or today
            FontSize = 14
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground);
        stack.Children.Add(datePicker);

        // Preview label
        var previewLabel = (new Label { Text = $"→ {tomorrow:ddd, MMM d, yyyy}", FontSize = 13, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText1);
        datePicker.DateSelected += (s, e) =>
        {
            previewLabel.Text = $"→ {datePicker.Date:ddd, MMM d, yyyy}";
        };
        stack.Children.Add(previewLabel);

        // Quick buttons
        stack.Children.Add((new Label { Text = "Quick postpone:", FontSize = 11, Margin = new Thickness(0, 4, 0, 0) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3));

        var quickRow1 = new HorizontalStackLayout { Spacing = 6 };
        var quickRow2 = new HorizontalStackLayout { Spacing = 6 };

        var presets = new (string label, int days)[]
        {
            ("Tomorrow", 1), ("+7 days", 7), ("+30 days", 30),
            ("+90 days", 90), ("+180 days", 180), ("+365 days", 365)
        };

        for (int i = 0; i < presets.Length; i++)
        {
            var (label, days) = presets[i];
            var targetDate = DateTime.Today.AddDays(days);
            var qBtn = (new Button
            {
                Text = label, FontSize = 11, HeightRequest = 30,
                CornerRadius = 6, Padding = new Thickness(8, 0)
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
            qBtn.Clicked += (s, ev) =>
            {
                datePicker.Date = targetDate;
                previewLabel.Text = $"→ {targetDate:ddd, MMM d, yyyy}";
            };
            if (i < 3) quickRow1.Children.Add(qBtn);
            else quickRow2.Children.Add(qBtn);
        }

        stack.Children.Add(quickRow1);
        stack.Children.Add(quickRow2);

        // Action buttons
        var btnRow = new HorizontalStackLayout { Spacing = 10, Margin = new Thickness(0, 8, 0, 0) };

        var confirmBtn = (new Button { Text = "⏩ Postpone", FontSize = 13, HeightRequest = 38, CornerRadius = 6, Padding = new Thickness(16, 0), FontAttributes = FontAttributes.Bold }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ButtonPrimaryBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        var cancelBtn = (new Button { Text = "Cancel", FontSize = 13, HeightRequest = 38, CornerRadius = 6, Padding = new Thickness(16, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

        btnRow.Children.Add(confirmBtn);
        btnRow.Children.Add(cancelBtn);
        stack.Children.Add(btnRow);

        card.Content = stack;
        popup.Children.Add(card);

        var originalContent = this.Content;
        var wrapper = new Grid();
        this.Content = wrapper;
        wrapper.Children.Add(originalContent);
        wrapper.Children.Add(popup);

        void Dismiss()
        {
            wrapper.Children.Remove(popup);
            wrapper.Children.Remove(originalContent);
            this.Content = originalContent;
        }

        confirmBtn.Clicked += async (s, ev) =>
        {
            if (datePicker.Date <= DateTime.Today)
            {
                await DisplayAlert("Invalid", "Cannot postpone to today or past dates.", "OK");
                return;
            }
            Dismiss();
            task.DueDate = datePicker.Date;
            await _taskService.UpdateTaskAsync(task);
            await LoadTasksAsync();
        };

        cancelBtn.Clicked += (s, ev) => { Dismiss(); };
    }

    private async void OnAddTaskClicked(object? sender, EventArgs e)
    {
        string? title = await DisplayPromptAsync("New Task",
            $"Add task for {_date:MMM d, yyyy}:",
            "Add", "Cancel", placeholder: "Task title...");

        if (string.IsNullOrWhiteSpace(title)) return;

        var categories = await _taskService.GetCategoriesAsync(_auth.CurrentUsername);
        var catOptions = categories.Count > 0
            ? categories.Concat(new[] { "+ New Category" }).ToArray()
            : new[] { "General", "+ New Category" };

        string? category = await DisplayActionSheet("Category", "Cancel", null, catOptions);
        if (string.IsNullOrEmpty(category) || category == "Cancel") return;

        if (category == "+ New Category")
        {
            category = await DisplayPromptAsync("Category", "Enter category name:");
            if (string.IsNullOrWhiteSpace(category)) category = "General";
        }

        string? priorityChoice = await DisplayActionSheet("Priority", "Cancel", null,
            "🔴 High", "🟡 Medium", "🟢 Low");
        int priority = priorityChoice switch
        {
            "🔴 High" => 3,
            "🟡 Medium" => 2,
            "🟢 Low" => 1,
            _ => 2
        };

        await _taskService.CreateTaskAsync(
            _auth.CurrentUsername, title.Trim(), category.Trim(), priority, _date);

        // Log to ideas
        string ideaText = $"[{_date:MMM d}] {title.Trim()}";
        if (_ideaLogger != null)
            await _ideaLogger.LogSilentlyAsync(new IdeaLogRequest
            {
                Username = _auth.CurrentUsername,
                Title = ideaText,
                Body = ideaText,
                Category = "calendar_tasks"
            });

        await LoadTasksAsync();
    }
}

public class LookoutScenarioPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly LookoutService _lookoutService;
    private readonly LookoutScenario _lookout;

    private Label _dateLabel;
    private Label _notesLabel;
    private VerticalStackLayout _itemsStack;

    public LookoutScenarioPage(AuthService auth, LookoutService lookoutService, LookoutScenario lookout)
    {
        _auth = auth;
        _lookoutService = lookoutService;
        _lookout = lookout;
        Title = lookout.Title;

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadItemsAsync();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout { Padding = 16, Spacing = 12 };

        var header = (new Frame
        {
            BorderColor = Colors.Transparent,
            CornerRadius = 10,
            Padding = 16,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText);

        var headerStack = new VerticalStackLayout { Spacing = 4 };
        headerStack.Children.Add((new Label
        {
            Text = _lookout.Title,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1));

        _dateLabel = (new Label { FontSize = 13 }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightViolet, ThemeKey.PageText1);
        headerStack.Children.Add(_dateLabel);

        _notesLabel = (new Label
        {
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightViolet, ThemeKey.PageText1);
        headerStack.Children.Add(_notesLabel);

        header.Content = headerStack;
        mainStack.Children.Add(header);

        var actionRow = new HorizontalStackLayout { Spacing = 10 };
        var addBtn = (new Button
        {
            Text = "+ Add List Item",
            FontSize = 14,
            HeightRequest = 42,
            CornerRadius = 8,
            Padding = new Thickness(16, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        addBtn.Clicked += async (s, e) => await AddItemAsync();
        actionRow.Children.Add(addBtn);

        var editDateBtn = (new Button
        {
            Text = "Change Date",
            FontSize = 13,
            HeightRequest = 42,
            CornerRadius = 8,
            Padding = new Thickness(14, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        editDateBtn.Clicked += async (s, e) => await ChangeDateAsync();
        actionRow.Children.Add(editDateBtn);

        var removeDateBtn = (new Button
        {
            Text = "Remove From Calendar",
            FontSize = 13,
            HeightRequest = 42,
            CornerRadius = 8,
            Padding = new Thickness(14, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
        removeDateBtn.Clicked += async (s, e) => await RemoveFromCalendarAsync();
        actionRow.Children.Add(removeDateBtn);

        mainStack.Children.Add(actionRow);

        mainStack.Children.Add((new Label
        {
            Text = "Scenario task list",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 8, 0, 0)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1));

        _itemsStack = new VerticalStackLayout { Spacing = 6 };
        mainStack.Children.Add(_itemsStack);

        Content = new ScrollView { Content = mainStack };
        RefreshHeader();
    }

    private void RefreshHeader()
    {
        _dateLabel.Text = _lookout.DisplayDate.HasValue
            ? $"Displayed on {_lookout.DisplayDate.Value:dddd, MMM d, yyyy}"
            : "Not currently displayed on calendar";

        _notesLabel.Text = string.IsNullOrWhiteSpace(_lookout.Notes)
            ? ""
            : _lookout.Notes;
    }

    private async Task LoadItemsAsync()
    {
        RefreshHeader();
        _itemsStack.Children.Clear();

        var items = await _lookoutService.GetItemsAsync(_lookout.Id);
        if (items.Count == 0)
        {
            _itemsStack.Children.Add((new Label
            {
                Text = "No list items yet.",
                FontSize = 13,
                Margin = new Thickness(8, 4)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2));
            return;
        }

        foreach (var item in items)
            _itemsStack.Children.Add(BuildItemCard(item));
    }

    private Frame BuildItemCard(LookoutScenarioItem item)
    {
        var frame = (new Frame
        {
            CornerRadius = 8,
            Padding = 12,
            HasShadow = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        var textStack = new VerticalStackLayout { Spacing = 3 };
        textStack.Children.Add((new Label
        {
            Text = item.Title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1));

        if (!string.IsNullOrWhiteSpace(item.Notes))
        {
            textStack.Children.Add((new Label
            {
                Text = item.Notes,
                FontSize = 12,
                LineBreakMode = LineBreakMode.WordWrap
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));
        }

        grid.Add(textStack, 0, 0);

        var buttons = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        var editBtn = (new Button
        {
            Text = "Edit",
            FontSize = 11,
            WidthRequest = 54,
            HeightRequest = 30,
            CornerRadius = 4,
            Padding = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        editBtn.Clicked += async (s, e) => await EditItemAsync(item);
        buttons.Children.Add(editBtn);

        var deleteBtn = (new Button
        {
            Text = "Delete",
            FontSize = 10,
            WidthRequest = 54,
            HeightRequest = 30,
            CornerRadius = 4,
            Padding = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
        deleteBtn.Clicked += async (s, e) => await DeleteItemAsync(item);
        buttons.Children.Add(deleteBtn);

        grid.Add(buttons, 1, 0);
        frame.Content = grid;
        return frame;
    }

    private async Task AddItemAsync()
    {
        string? title = await DisplayPromptAsync("Add List Item", "Task/action relevant if this scenario happens:", "Add", "Cancel", placeholder: "List item...");
        if (string.IsNullOrWhiteSpace(title))
            return;

        string? notes = await DisplayPromptAsync("Item Notes", "Optional notes:", "Save", "Skip", initialValue: "");
        await _lookoutService.AddItemAsync(_lookout.Id, title.Trim(), notes ?? "");
        await LoadItemsAsync();
    }

    private async Task EditItemAsync(LookoutScenarioItem item)
    {
        string? title = await DisplayPromptAsync("Edit List Item", "Update title:", "Save", "Cancel", initialValue: item.Title);
        if (title == null)
            return;

        string? notes = await DisplayPromptAsync("Edit Notes", "Update notes:", "Save", "Skip", initialValue: item.Notes ?? "");
        if (string.IsNullOrWhiteSpace(title))
            return;

        item.Title = title.Trim();
        if (notes != null)
            item.Notes = notes.Trim();
        await _lookoutService.UpdateItemAsync(item);
        await LoadItemsAsync();
    }

    private async Task DeleteItemAsync(LookoutScenarioItem item)
    {
        if (!await DisplayAlert("Delete list item?", $"Delete \"{item.Title}\"?", "Delete", "Cancel"))
            return;

        await _lookoutService.DeleteItemAsync(item);
        await LoadItemsAsync();
    }

    private async Task ChangeDateAsync()
    {
        var date = await ShowDatePickerPopupAsync();
        if (date == null)
            return;

        _lookout.DisplayDate = date.Value.Date;
        await _lookoutService.UpdateAsync(_lookout);
        await LoadItemsAsync();
    }

    private async Task<DateTime?> ShowDatePickerPopupAsync()
    {
        var tcs = new TaskCompletionSource<DateTime?>();
        var popup = (new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);

        var card = (new Frame
        {
            CornerRadius = 12,
            Padding = 20,
            WidthRequest = 420,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var stack = new VerticalStackLayout { Spacing = 10 };
        stack.Children.Add((new Label { Text = "Change Display Date", FontSize = 18, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText1));
        var picker = (new DatePicker { Date = _lookout.DisplayDate?.Date ?? DateTime.Today, FontSize = 14 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightViolet);
        stack.Children.Add(picker);

        var buttons = new HorizontalStackLayout { Spacing = 10, Margin = new Thickness(0, 8, 0, 0) };
        var saveBtn = (new Button { Text = "Save", FontSize = 13, HeightRequest = 38, CornerRadius = 6, Padding = new Thickness(16, 0), FontAttributes = FontAttributes.Bold }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        var cancelBtn = (new Button { Text = "Cancel", FontSize = 13, HeightRequest = 38, CornerRadius = 6, Padding = new Thickness(16, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        buttons.Children.Add(saveBtn);
        buttons.Children.Add(cancelBtn);
        stack.Children.Add(buttons);

        card.Content = stack;
        popup.Children.Add(card);

        var originalContent = Content;
        var wrapper = new Grid();
        Content = wrapper;
        wrapper.Children.Add(originalContent);
        wrapper.Children.Add(popup);

        void Dismiss()
        {
            wrapper.Children.Remove(popup);
            wrapper.Children.Remove(originalContent);
            Content = originalContent;
        }

        saveBtn.Clicked += (s, e) => { Dismiss(); tcs.TrySetResult(picker.Date); };
        cancelBtn.Clicked += (s, e) => { Dismiss(); tcs.TrySetResult(null); };
        return await tcs.Task;
    }

    private async Task RemoveFromCalendarAsync()
    {
        if (!await DisplayAlert(
            "Remove from calendar?",
            $"Remove \"{_lookout.Title}\" from the calendar?\n\nThe list items will remain.",
            "Remove",
            "Cancel"))
        {
            return;
        }

        await _lookoutService.RemoveFromCalendarAsync(_lookout);
        _lookout.DisplayDate = null;
        await LoadItemsAsync();
    }
}
