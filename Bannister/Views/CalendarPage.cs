using Bannister.Services;
using Bannister.Models;
using SQLite;

namespace Bannister.Views;

/// <summary>
/// Calendar page showing a card grid of days for the current month.
/// Each day card shows how many tasks are due on that day.
/// Previous/Next buttons to navigate months.
/// </summary>
public class CalendarPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly TaskService _taskService;
    private readonly IdeasService? _ideasService;
    private readonly DatabaseService _db;

    private Label _monthLabel;
    private Grid _calendarGrid;
    private int _year;
    private int _month;

    // Task counts keyed by day-of-month
    private Dictionary<int, int> _taskCounts = new();
    private Dictionary<int, int> _completedCounts = new();

    public CalendarPage(AuthService auth, TaskService taskService, IdeasService? ideasService = null, DatabaseService? db = null)
    {
        _auth = auth;
        _taskService = taskService;
        _ideasService = ideasService;
        _db = db;
        Title = "Calendar";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        var now = DateTime.Today;
        _year = now.Year;
        _month = now.Month;

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
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

        var prevBtn = new Button
        {
            Text = "← Prev", FontSize = 13, HeightRequest = 38,
            BackgroundColor = Color.FromArgb("#455A64"), TextColor = Colors.White,
            CornerRadius = 6, Padding = new Thickness(14, 0)
        };
        prevBtn.Clicked += async (s, e) => { GoToPreviousMonth(); await LoadMonthAsync(); };
        navRow.Add(prevBtn, 0, 0);

        _monthLabel = new Label
        {
            Text = "", FontSize = 22, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0"),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Center
        };
        navRow.Add(_monthLabel, 1, 0);

        var todayBtn = new Button
        {
            Text = "Today", FontSize = 12, HeightRequest = 38,
            BackgroundColor = Color.FromArgb("#E3F2FD"), TextColor = Color.FromArgb("#1565C0"),
            CornerRadius = 6, Padding = new Thickness(12, 0)
        };
        todayBtn.Clicked += async (s, e) => { var now = DateTime.Today; _year = now.Year; _month = now.Month; await LoadMonthAsync(); };
        navRow.Add(todayBtn, 2, 0);

        var nextBtn = new Button
        {
            Text = "Next →", FontSize = 13, HeightRequest = 38,
            BackgroundColor = Color.FromArgb("#455A64"), TextColor = Colors.White,
            CornerRadius = 6, Padding = new Thickness(14, 0)
        };
        nextBtn.Clicked += async (s, e) => { GoToNextMonth(); await LoadMonthAsync(); };
        navRow.Add(nextBtn, 3, 0);

        var importBtn = new Button
        {
            Text = "📥 Import", FontSize = 12, HeightRequest = 38,
            BackgroundColor = Color.FromArgb("#795548"), TextColor = Colors.White,
            CornerRadius = 6, Padding = new Thickness(12, 0)
        };
        importBtn.Clicked += OnImportClicked;
        navRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        navRow.Add(importBtn, 4, 0);

        var dedupBtn = new Button
        {
            Text = "🧹 Dedup", FontSize = 12, HeightRequest = 38,
            BackgroundColor = Color.FromArgb("#5D4037"), TextColor = Colors.White,
            CornerRadius = 6, Padding = new Thickness(12, 0)
        };
        dedupBtn.Clicked += OnDedupClicked;
        navRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        navRow.Add(dedupBtn, 5, 0);

        var moveBtn = new Button
        {
            Text = "📦 Move", FontSize = 12, HeightRequest = 38,
            BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White,
            CornerRadius = 6, Padding = new Thickness(12, 0)
        };
        moveBtn.Clicked += OnMoveTasksClicked;
        navRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        navRow.Add(moveBtn, 6, 0);

        mainStack.Children.Add(navRow);

        // Day-of-week headers
        var dowRow = new Grid { ColumnSpacing = 4 };
        for (int i = 0; i < 7; i++)
            dowRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        string[] dayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        for (int i = 0; i < 7; i++)
        {
            dowRow.Add(new Label
            {
                Text = dayNames[i], FontSize = 11, FontAttributes = FontAttributes.Bold,
                TextColor = (i >= 5) ? Color.FromArgb("#C62828") : Color.FromArgb("#666"),
                HorizontalTextAlignment = TextAlignment.Center
            }, i, 0);
        }
        mainStack.Children.Add(dowRow);

        // Calendar grid (will be rebuilt each month)
        _calendarGrid = new Grid { ColumnSpacing = 4, RowSpacing = 4 };
        for (int i = 0; i < 7; i++)
            _calendarGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        mainStack.Children.Add(_calendarGrid);

        Content = new ScrollView { Content = mainStack };
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
        }
        catch { }

        BuildCalendarGrid();
    }

    private void BuildCalendarGrid()
    {
        _calendarGrid.Children.Clear();
        _calendarGrid.RowDefinitions.Clear();

        var firstDay = new DateTime(_year, _month, 1);
        int daysInMonth = DateTime.DaysInMonth(_year, _month);

        // Monday = 0, Sunday = 6
        int startOffset = ((int)firstDay.DayOfWeek + 6) % 7;

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
            bool isToday = isCurrentMonth && day == today;
            bool isWeekend = col >= 5;
            bool isPast = isCurrentMonth && day < today;

            var card = BuildDayCard(day, activeTasks, doneTasks, isToday, isWeekend, isPast);
            _calendarGrid.Add(card, col, row);
        }
    }

    private Frame BuildDayCard(int day, int activeTasks, int doneTasks, bool isToday, bool isWeekend, bool isPast)
    {
        Color bg;
        Color border;

        if (isToday)
        {
            bg = Color.FromArgb("#E3F2FD");
            border = Color.FromArgb("#1565C0");
        }
        else if (activeTasks > 0)
        {
            bg = Color.FromArgb("#FFF8E1");
            border = Color.FromArgb("#FF8F00");
        }
        else if (doneTasks > 0)
        {
            bg = Color.FromArgb("#E8F5E9");
            border = Color.FromArgb("#4CAF50");
        }
        else
        {
            bg = isPast ? Color.FromArgb("#EEEEEE") : Colors.White;
            border = Color.FromArgb("#E0E0E0");
        }

        var frame = new Frame
        {
            BackgroundColor = bg, BorderColor = border,
            CornerRadius = 8, Padding = 8, HasShadow = false,
            MinimumHeightRequest = 70
        };

        var stack = new VerticalStackLayout { Spacing = 4 };

        // Day number
        stack.Children.Add(new Label
        {
            Text = day.ToString(),
            FontSize = isToday ? 18 : 15,
            FontAttributes = isToday ? FontAttributes.Bold : FontAttributes.None,
            TextColor = isToday ? Color.FromArgb("#1565C0") : isWeekend ? Color.FromArgb("#C62828") : Color.FromArgb("#333")
        });

        // Task counts
        if (activeTasks > 0)
        {
            stack.Children.Add(new Label
            {
                Text = $"📋 {activeTasks}",
                FontSize = 12, FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#E65100")
            });
        }

        if (doneTasks > 0)
        {
            stack.Children.Add(new Label
            {
                Text = $"✓ {doneTasks}",
                FontSize = 11,
                TextColor = Color.FromArgb("#4CAF50")
            });
        }

        if (activeTasks == 0 && doneTasks == 0)
        {
            stack.Children.Add(new Label
            {
                Text = "—", FontSize = 11,
                TextColor = Color.FromArgb("#CCC")
            });
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
        var page = new CalendarDayPage(_auth, _taskService, date, _ideasService);
        page.Disappearing += async (s, e) => await LoadMonthAsync();
        await Navigation.PushAsync(page);
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
        if (_ideasService != null)
            try { await _ideasService.CreateIdeaAsync(_auth.CurrentUsername, $"[{date:MMM d}] {title.Trim()}", "calendar_tasks"); } catch { }

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
            var progressOverlay = new Grid
            {
                BackgroundColor = Color.FromArgb("#CC000000"),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };
            var progressStack = new VerticalStackLayout
            {
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Spacing = 12
            };
            var spinner = new ActivityIndicator { IsRunning = true, Color = Colors.White, HeightRequest = 40, WidthRequest = 40 };
            var progressLabel = new Label { Text = "Importing...", FontSize = 16, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center };
            var progressDetail = new Label { Text = "0 / ?", FontSize = 13, TextColor = Color.FromArgb("#BBDEFB"), HorizontalTextAlignment = TextAlignment.Center };
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
            if (_ideasService != null && ideasToInsert.Count > 0)
            {
                progressLabel.Text = "Logging to ideas...";
                progressDetail.Text = $"0 / {ideasToInsert.Count}";
                await Task.Yield();

                // Use direct DB insert for ideas too
                var ideaConn = await _db.GetConnectionAsync();
                for (int i = 0; i < ideasToInsert.Count; i += batchSize)
                {
                    var batch = ideasToInsert.Skip(i).Take(batchSize).ToList();
                    await ideaConn.RunInTransactionAsync(db =>
                    {
                        foreach (var (text, cat) in batch)
                        {
                            var idea = new IdeaItem
                            {
                                Username = username,
                                Title = text,
                                Category = cat,
                                CreatedAt = DateTime.Now
                            };
                            db.Insert(idea);
                        }
                    });

                    int done = Math.Min(i + batchSize, ideasToInsert.Count);
                    progressDetail.Text = $"{done} / {ideasToInsert.Count} ideas";
                    await Task.Yield();
                }
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

        var overlay = new Grid { BackgroundColor = Color.FromArgb("#CC000000"), HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };
        var pStack = new VerticalStackLayout { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Spacing = 12 };
        pStack.Children.Add(new ActivityIndicator { IsRunning = true, Color = Colors.White, HeightRequest = 40, WidthRequest = 40 });
        pStack.Children.Add(new Label { Text = "Moving tasks...", FontSize = 16, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center });
        var pDetail = new Label { Text = $"0 / {tasksToMove.Count}", FontSize = 13, TextColor = Color.FromArgb("#BBDEFB"), HorizontalTextAlignment = TextAlignment.Center };
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

        var popup = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill
        };

        var card = new Frame
        {
            BackgroundColor = Colors.White, CornerRadius = 12, Padding = 20,
            WidthRequest = 480, HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center, HasShadow = true
        };

        var stack = new VerticalStackLayout { Spacing = 10 };

        stack.Children.Add(new Label { Text = "📦 Move Tasks", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0") });
        stack.Children.Add(new Label { Text = "Move all tasks from a date range into a single target date.", FontSize = 12, TextColor = Color.FromArgb("#666") });

        // Mode: both dates selected by default
        string currentMode = "both";

        // Start date
        var startLabel = new Label { Text = "From (start date):", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") };
        var startPicker = new DatePicker { Date = DateTime.Today.AddDays(-7), BackgroundColor = Color.FromArgb("#F5F5F5"), FontSize = 14 };
        var startEnabled = true;

        // End date
        var endLabel = new Label { Text = "To (end date):", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") };
        var endPicker = new DatePicker { Date = DateTime.Today, BackgroundColor = Color.FromArgb("#F5F5F5"), FontSize = 14 };
        var endEnabled = true;

        // Mode buttons
        stack.Children.Add(new Label { Text = "Range mode:", FontSize = 12, TextColor = Color.FromArgb("#666"), Margin = new Thickness(0, 4, 0, 0) });

        var modeRow = new HorizontalStackLayout { Spacing = 6 };

        var bothBtn = new Button { Text = "📅 Both Dates", FontSize = 11, HeightRequest = 32, BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(10, 0) };
        var afterBtn = new Button { Text = "→ All From Start", FontSize = 11, HeightRequest = 32, BackgroundColor = Color.FromArgb("#9E9E9E"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(10, 0) };
        var beforeBtn = new Button { Text = "← All Up To End", FontSize = 11, HeightRequest = 32, BackgroundColor = Color.FromArgb("#9E9E9E"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(10, 0) };

        void UpdateModeUI()
        {
            bothBtn.BackgroundColor = currentMode == "both" ? Color.FromArgb("#1565C0") : Color.FromArgb("#9E9E9E");
            afterBtn.BackgroundColor = currentMode == "after" ? Color.FromArgb("#E65100") : Color.FromArgb("#9E9E9E");
            beforeBtn.BackgroundColor = currentMode == "before" ? Color.FromArgb("#6A1B9A") : Color.FromArgb("#9E9E9E");

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
        stack.Children.Add(new Label { Text = "Move all to this date:", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#2E7D32"), Margin = new Thickness(0, 6, 0, 0) });
        var targetPicker = new DatePicker { Date = DateTime.Today, BackgroundColor = Color.FromArgb("#E8F5E9"), FontSize = 14 };
        stack.Children.Add(targetPicker);

        // Buttons
        var btnRow = new HorizontalStackLayout { Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };
        var moveBtn = new Button { Text = "📦 Move Tasks", FontSize = 13, HeightRequest = 40, BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(16, 0), FontAttributes = FontAttributes.Bold };
        var cancelBtn = new Button { Text = "Cancel", FontSize = 13, HeightRequest = 40, BackgroundColor = Color.FromArgb("#9E9E9E"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(16, 0) };
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

            var progressOverlay = new Grid
            {
                BackgroundColor = Color.FromArgb("#CC000000"),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };
            var progressStack = new VerticalStackLayout
            {
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Spacing = 12
            };
            progressStack.Children.Add(new ActivityIndicator { IsRunning = true, Color = Colors.White, HeightRequest = 40, WidthRequest = 40 });
            var progressLabel = new Label { Text = "Deleting duplicates...", FontSize = 16, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center };
            var progressDetail = new Label { Text = $"0 / {tasksToDelete.Count}", FontSize = 13, TextColor = Color.FromArgb("#BBDEFB"), HorizontalTextAlignment = TextAlignment.Center };
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

        var popup = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        var card = new Frame
        {
            BackgroundColor = Colors.White, CornerRadius = 12, Padding = 20,
            WidthRequest = 500, MaximumHeightRequest = 600,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center, HasShadow = true
        };

        var stack = new VerticalStackLayout { Spacing = 10 };

        stack.Children.Add(new Label
        {
            Text = $"🧹 Duplicate {current} of {total}", FontSize = 16, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#5D4037")
        });

        // Full task text
        stack.Children.Add(new Label { Text = "Task:", FontSize = 11, TextColor = Color.FromArgb("#888") });
        var taskFrame = new Frame
        {
            BackgroundColor = Color.FromArgb("#FFF8E1"), BorderColor = Color.FromArgb("#FFB74D"),
            Padding = 10, CornerRadius = 6, HasShadow = false
        };
        taskFrame.Content = new ScrollView
        {
            Content = new Label { Text = taskTitle, FontSize = 13, TextColor = Color.FromArgb("#333"), LineBreakMode = LineBreakMode.WordWrap },
            MaximumHeightRequest = 100
        };
        stack.Children.Add(taskFrame);

        // Dates
        int totalCopies = dates.Sum(d => d.Count());
        stack.Children.Add(new Label { Text = $"Found on {dates.Count} dates ({totalCopies} copies):", FontSize = 12, TextColor = Color.FromArgb("#666") });

        var dateListScroll = new ScrollView { MaximumHeightRequest = 120 };
        var dateListStack = new VerticalStackLayout { Spacing = 2 };
        foreach (var dg in dates)
            dateListStack.Children.Add(new Label { Text = $"  • {dg.Key:ddd, MMM d, yyyy} — {dg.Count()}", FontSize = 12, TextColor = Color.FromArgb("#555") });
        dateListScroll.Content = dateListStack;
        stack.Children.Add(dateListScroll);

        // Date picker
        stack.Children.Add(new Label { Text = "Keep on this date (delete all others):", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333"), Margin = new Thickness(0, 6, 0, 0) });

        var datePicker = new DatePicker { Date = DateTime.Today, BackgroundColor = Color.FromArgb("#F5F5F5"), FontSize = 14 };
        stack.Children.Add(datePicker);

        // Buttons
        var btnRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };

        var keepBtn = new Button { Text = "✅ Keep", FontSize = 12, HeightRequest = 38, BackgroundColor = Color.FromArgb("#2E7D32"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(14, 0) };
        var skipBtn = new Button { Text = "⏭️ Skip", FontSize = 12, HeightRequest = 38, BackgroundColor = Color.FromArgb("#9E9E9E"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(14, 0) };
        var cancelBtn = new Button { Text = "Cancel All", FontSize = 12, HeightRequest = 38, BackgroundColor = Color.FromArgb("#C62828"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(14, 0) };

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

        var popup = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        var card = new Frame
        {
            BackgroundColor = Colors.White, CornerRadius = 12, Padding = 20,
            WidthRequest = 420, MaximumHeightRequest = 500,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center, HasShadow = true
        };

        var mainStack = new VerticalStackLayout { Spacing = 10 };

        mainStack.Children.Add(new Label
        {
            Text = "Select Task Columns", FontSize = 18, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });
        mainStack.Children.Add(new Label
        {
            Text = "Check the columns that contain task data:", FontSize = 12, TextColor = Color.FromArgb("#666")
        });

        var checkboxes = new List<(CheckBox cb, string name)>();

        var toggleRow = new HorizontalStackLayout { Spacing = 8 };
        var selectAllBtn = new Button
        {
            Text = "Select All", FontSize = 12, HeightRequest = 30,
            BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White,
            CornerRadius = 6, Padding = new Thickness(10, 0)
        };
        selectAllBtn.Clicked += (s, e) => { foreach (var (cb, _) in checkboxes) cb.IsChecked = true; };
        toggleRow.Children.Add(selectAllBtn);

        var deselectAllBtn = new Button
        {
            Text = "Deselect All", FontSize = 12, HeightRequest = 30,
            BackgroundColor = Color.FromArgb("#9E9E9E"), TextColor = Colors.White,
            CornerRadius = 6, Padding = new Thickness(10, 0)
        };
        deselectAllBtn.Clicked += (s, e) => { foreach (var (cb, _) in checkboxes) cb.IsChecked = false; };
        toggleRow.Children.Add(deselectAllBtn);
        mainStack.Children.Add(toggleRow);

        var checkboxStack = new VerticalStackLayout { Spacing = 4 };
        foreach (var col in columns)
        {
            var row = new HorizontalStackLayout { Spacing = 8 };
            var cb = new CheckBox { IsChecked = true, Color = Color.FromArgb("#1565C0") };
            row.Children.Add(cb);
            row.Children.Add(new Label { Text = col, FontSize = 14, TextColor = Color.FromArgb("#333"), VerticalOptions = LayoutOptions.Center });
            checkboxes.Add((cb, col));
            checkboxStack.Children.Add(row);
        }

        var scrollView = new ScrollView { Content = checkboxStack, MaximumHeightRequest = 280 };
        mainStack.Children.Add(scrollView);

        var btnRow = new HorizontalStackLayout { Spacing = 12, HorizontalOptions = LayoutOptions.End, Margin = new Thickness(0, 8, 0, 0) };
        var cancelBtn = new Button { Text = "Cancel", BackgroundColor = Color.FromArgb("#9E9E9E"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(16, 8) };
        var confirmBtn = new Button { Text = "✅ Confirm", BackgroundColor = Color.FromArgb("#2E7D32"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(16, 8), FontAttributes = FontAttributes.Bold };
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
public class CalendarDayPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly TaskService _taskService;
    private readonly DateTime _date;
    private readonly IdeasService? _ideasService;

    private VerticalStackLayout _activeStack;
    private VerticalStackLayout _completedStack;
    private VerticalStackLayout _urgentStack;
    private Label _activeHeader;
    private Label _completedHeader;
    private Label _urgentHeader;

    public CalendarDayPage(AuthService auth, TaskService taskService, DateTime date, IdeasService? ideasService = null)
    {
        _auth = auth;
        _taskService = taskService;
        _date = date.Date;
        _ideasService = ideasService;
        Title = date.ToString("MMM d, yyyy");
        BackgroundColor = Color.FromArgb("#F5F5F5");
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
        var headerFrame = new Frame
        {
            BackgroundColor = Color.FromArgb("#1565C0"), BorderColor = Colors.Transparent,
            CornerRadius = 10, Padding = 16, HasShadow = true
        };
        var headerStack = new VerticalStackLayout { Spacing = 4 };
        headerStack.Children.Add(new Label
        {
            Text = _date.ToString("dddd"), FontSize = 14,
            TextColor = Color.FromArgb("#BBDEFB")
        });
        headerStack.Children.Add(new Label
        {
            Text = _date.ToString("MMMM d, yyyy"), FontSize = 22,
            FontAttributes = FontAttributes.Bold, TextColor = Colors.White
        });

        bool isToday = _date == DateTime.Today;
        bool isPast = _date < DateTime.Today;
        if (isToday)
            headerStack.Children.Add(new Label { Text = "Today", FontSize = 13, TextColor = Color.FromArgb("#64B5F6") });
        else if (isPast)
            headerStack.Children.Add(new Label { Text = "Past", FontSize = 13, TextColor = Color.FromArgb("#90A4AE") });

        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        // Add task button
        var addBtn = new Button
        {
            Text = "+ Add Task", FontSize = 14, HeightRequest = 44,
            BackgroundColor = Color.FromArgb("#4CAF50"), TextColor = Colors.White,
            CornerRadius = 8, Padding = new Thickness(20, 0),
            HorizontalOptions = LayoutOptions.Start
        };
        addBtn.Clicked += OnAddTaskClicked;
        mainStack.Children.Add(addBtn);

        // Urgent tasks (above everything)
        _urgentHeader = new Label
        {
            Text = "🔴 Urgent (0)", FontSize = 16, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#C62828"), Margin = new Thickness(0, 8, 0, 4),
            IsVisible = false
        };
        mainStack.Children.Add(_urgentHeader);

        _urgentStack = new VerticalStackLayout { Spacing = 6 };
        mainStack.Children.Add(_urgentStack);

        // Active tasks
        _activeHeader = new Label
        {
            Text = "📋 Active Tasks (0)", FontSize = 16, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#E65100"), Margin = new Thickness(0, 8, 0, 4)
        };
        mainStack.Children.Add(_activeHeader);

        _activeStack = new VerticalStackLayout { Spacing = 6 };
        mainStack.Children.Add(_activeStack);

        // Completed tasks
        _completedHeader = new Label
        {
            Text = "✓ Completed (0)", FontSize = 16, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#4CAF50"), Margin = new Thickness(0, 12, 0, 4)
        };
        mainStack.Children.Add(_completedHeader);

        _completedStack = new VerticalStackLayout { Spacing = 6 };
        mainStack.Children.Add(_completedStack);

        Content = new ScrollView { Content = mainStack };
    }

    private async Task LoadTasksAsync()
    {
        var active = await _taskService.GetActiveTasksAsync(_auth.CurrentUsername);
        var completed = await _taskService.GetCompletedTasksAsync(_auth.CurrentUsername);

        var dayActive = active.Where(t => t.DueDate.HasValue && t.DueDate.Value.Date == _date).ToList();
        var dayCompleted = completed.Where(t => t.DueDate.HasValue && t.DueDate.Value.Date == _date).ToList();

        var urgent = dayActive.Where(t => t.IsUrgent).ToList();
        var regular = dayActive.Where(t => !t.IsUrgent).ToList();

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
            _activeStack.Children.Add(new Label { Text = "No active tasks for this day.", FontSize = 13, TextColor = Color.FromArgb("#999"), Margin = new Thickness(8, 4) });
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
            _completedStack.Children.Add(new Label { Text = "No completed tasks.", FontSize = 13, TextColor = Color.FromArgb("#999"), Margin = new Thickness(8, 4) });
        }
        else
        {
            foreach (var task in dayCompleted)
                _completedStack.Children.Add(BuildTaskCard(task, true));
        }
    }

    private Frame BuildTaskCard(TaskItem task, bool isCompleted)
    {
        string priorityIcon = task.Priority switch { 3 => "🔴", 2 => "🟡", 1 => "🟢", _ => "⚪" };

        var frame = new Frame
        {
            BackgroundColor = isCompleted ? Color.FromArgb("#E8F5E9") : task.IsUrgent ? Color.FromArgb("#FFEBEE") : Colors.White,
            BorderColor = isCompleted ? Color.FromArgb("#4CAF50") : task.IsUrgent ? Color.FromArgb("#C62828") : Color.FromArgb("#E0E0E0"),
            CornerRadius = 8, Padding = 12, HasShadow = !isCompleted
        };

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
        textStack.Children.Add(new Label
        {
            Text = $"{urgentPrefix}{priorityIcon} {task.Title}",
            FontSize = 14, FontAttributes = isCompleted ? FontAttributes.None : FontAttributes.Bold,
            TextColor = isCompleted ? Color.FromArgb("#666") : task.IsUrgent ? Color.FromArgb("#C62828") : Color.FromArgb("#333"),
            TextDecorations = isCompleted ? TextDecorations.Strikethrough : TextDecorations.None,
            LineBreakMode = LineBreakMode.WordWrap
        });

        if (!string.IsNullOrEmpty(task.Category))
            textStack.Children.Add(new Label { Text = $"📁 {task.Category}", FontSize = 11, TextColor = Color.FromArgb("#888") });

        if (!string.IsNullOrEmpty(task.Notes))
            textStack.Children.Add(new Label { Text = task.Notes, FontSize = 11, TextColor = Color.FromArgb("#999"), MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation });

        grid.Add(textStack, 0, 0);

        // Action buttons
        var btnStack = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };

        if (!isCompleted)
        {
            // Urgent toggle
            var urgentBtn = new Button
            {
                Text = task.IsUrgent ? "🔴" : "⚪",
                FontSize = 14, WidthRequest = 36, HeightRequest = 32,
                BackgroundColor = task.IsUrgent ? Color.FromArgb("#FFCDD2") : Color.FromArgb("#F5F5F5"),
                TextColor = Color.FromArgb("#C62828"),
                CornerRadius = 4, Padding = 0
            };
            urgentBtn.Clicked += async (s, e) =>
            {
                task.IsUrgent = !task.IsUrgent;
                await _taskService.UpdateTaskAsync(task);
                await LoadTasksAsync();
            };
            btnStack.Children.Add(urgentBtn);

            var completeBtn = new Button
            {
                Text = "✅", FontSize = 14, WidthRequest = 36, HeightRequest = 32,
                BackgroundColor = Color.FromArgb("#E8F5E9"), TextColor = Color.FromArgb("#2E7D32"),
                CornerRadius = 4, Padding = 0
            };
            completeBtn.Clicked += async (s, e) =>
            {
                await _taskService.CompleteTaskAsync(task);
                await LoadTasksAsync();
            };
            btnStack.Children.Add(completeBtn);

            // Postpone button
            var postponeBtn = new Button
            {
                Text = "⏩", FontSize = 14, WidthRequest = 36, HeightRequest = 32,
                BackgroundColor = Color.FromArgb("#E3F2FD"), TextColor = Color.FromArgb("#1565C0"),
                CornerRadius = 4, Padding = 0
            };
            postponeBtn.Clicked += async (s, ev) => await ShowPostponeAsync(task);
            btnStack.Children.Add(postponeBtn);
        }
        else
        {
            var uncompleteBtn = new Button
            {
                Text = "↩", FontSize = 14, WidthRequest = 36, HeightRequest = 32,
                BackgroundColor = Color.FromArgb("#FFF8E1"), TextColor = Color.FromArgb("#F57F17"),
                CornerRadius = 4, Padding = 0
            };
            uncompleteBtn.Clicked += async (s, e) =>
            {
                await _taskService.UncompleteTaskAsync(task);
                await LoadTasksAsync();
            };
            btnStack.Children.Add(uncompleteBtn);
        }

        var deleteBtn = new Button
        {
            Text = "🗑", FontSize = 14, WidthRequest = 36, HeightRequest = 32,
            BackgroundColor = Color.FromArgb("#FFEBEE"), TextColor = Color.FromArgb("#C62828"),
            CornerRadius = 4, Padding = 0
        };
        deleteBtn.Clicked += async (s, e) =>
        {
            if (await DisplayAlert("Delete?", $"Delete \"{task.Title}\"?", "Delete", "Cancel"))
            {
                await _taskService.DeleteTaskAsync(task);
                await LoadTasksAsync();
            }
        };
        btnStack.Children.Add(deleteBtn);

        grid.Add(btnStack, 1, 0);
        frame.Content = grid;
        return frame;
    }

    private async Task ShowPostponeAsync(TaskItem task)
    {
        var tcs = new TaskCompletionSource<DateTime?>();

        var popup = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill
        };

        var card = new Frame
        {
            BackgroundColor = Colors.White, CornerRadius = 12, Padding = 20,
            WidthRequest = 400, HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center, HasShadow = true
        };

        var stack = new VerticalStackLayout { Spacing = 10 };

        stack.Children.Add(new Label { Text = "⏩ Postpone Task", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0") });

        // Task title
        string truncTitle = task.Title.Length > 80 ? task.Title.Substring(0, 77) + "..." : task.Title;
        stack.Children.Add(new Label { Text = truncTitle, FontSize = 13, TextColor = Color.FromArgb("#333") });

        // Current date
        string currentDateStr = task.DueDate.HasValue ? task.DueDate.Value.ToString("MMM d, yyyy") : "No date";
        stack.Children.Add(new Label { Text = $"Currently: {currentDateStr}", FontSize = 12, TextColor = Color.FromArgb("#888") });

        // Target date picker — default tomorrow
        var tomorrow = DateTime.Today.AddDays(1);
        stack.Children.Add(new Label { Text = "Postpone to:", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333"), Margin = new Thickness(0, 4, 0, 0) });

        var datePicker = new DatePicker
        {
            Date = tomorrow,
            MinimumDate = DateTime.Today.AddDays(1), // Can't postpone to past or today
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            FontSize = 14
        };
        stack.Children.Add(datePicker);

        // Preview label
        var previewLabel = new Label { Text = $"→ {tomorrow:ddd, MMM d, yyyy}", FontSize = 13, TextColor = Color.FromArgb("#1565C0"), FontAttributes = FontAttributes.Bold };
        datePicker.DateSelected += (s, e) =>
        {
            previewLabel.Text = $"→ {datePicker.Date:ddd, MMM d, yyyy}";
        };
        stack.Children.Add(previewLabel);

        // Quick buttons
        stack.Children.Add(new Label { Text = "Quick postpone:", FontSize = 11, TextColor = Color.FromArgb("#888"), Margin = new Thickness(0, 4, 0, 0) });

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
            var qBtn = new Button
            {
                Text = label, FontSize = 11, HeightRequest = 30,
                BackgroundColor = Color.FromArgb("#E3F2FD"), TextColor = Color.FromArgb("#1565C0"),
                CornerRadius = 6, Padding = new Thickness(8, 0)
            };
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

        var confirmBtn = new Button { Text = "⏩ Postpone", FontSize = 13, HeightRequest = 38, BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(16, 0), FontAttributes = FontAttributes.Bold };
        var cancelBtn = new Button { Text = "Cancel", FontSize = 13, HeightRequest = 38, BackgroundColor = Color.FromArgb("#9E9E9E"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(16, 0) };

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
        if (_ideasService != null)
            try { await _ideasService.CreateIdeaAsync(_auth.CurrentUsername, $"[{_date:MMM d}] {title.Trim()}", "calendar_tasks"); } catch { }

        await LoadTasksAsync();
    }
}
