using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class WritingExperimentPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly WritingExperimentService _experimentService;
    private readonly StoryProductionService _storyService;
    private VerticalStackLayout _mainContent = null!;
    private Label _phaseLabel = null!;
    private Label _statusLabel = null!;
    private VerticalStackLayout _todaySection = null!;
    private VerticalStackLayout _comparisonSection = null!;
    private DatePicker _weekStartPicker = null!;
    private DatePicker _weekEndPicker = null!;

    public WritingExperimentPage(AuthService auth, WritingExperimentService experimentService, StoryProductionService storyService)
    {
        _auth = auth;
        _experimentService = experimentService;
        _storyService = storyService;
        Title = "Writing Experiment";
        BackgroundColor = Color.FromArgb("#FFF8E1");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private void BuildUI()
    {
        _mainContent = new VerticalStackLayout { Padding = 20, Spacing = 14 };
        _mainContent.Children.Add(new Label { Text = "\U0001F9EA Writing Experiment", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#6A1B9A") });
        _mainContent.Children.Add(new Label
        {
            Text = "Test which writing process produces the best retention. Week 1 establishes a baseline with one process daily. Then challenge it.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });
        _phaseLabel = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") };
        _statusLabel = new Label { FontSize = 12, TextColor = Color.FromArgb("#888") };
        _todaySection = new VerticalStackLayout { Spacing = 10 };
        _comparisonSection = new VerticalStackLayout { Spacing = 8 };
        _mainContent.Children.Add(_phaseLabel);
        _mainContent.Children.Add(_statusLabel);
        _mainContent.Children.Add(_todaySection);
        _mainContent.Children.Add(_comparisonSection);

        var rootGrid = new Grid();
        rootGrid.Children.Add(new ScrollView { Content = _mainContent });
        Content = rootGrid;
    }

    private async Task RefreshAsync()
    {
        var experiment = await _experimentService.GetActiveExperimentAsync(_auth.CurrentUsername);
        _todaySection.Children.Clear();
        _comparisonSection.Children.Clear();

        if (experiment == null)
        {
            _phaseLabel.Text = "No active experiment";
            _statusLabel.Text = "";
            await ShowStartExperimentAsync();
            return;
        }

        var entries = await _experimentService.GetEntriesAsync(experiment.Id);
        int baselineCount = entries.Count(e => e.IsBaseline && e.IsCompleted);
        int challengerCount = entries.Count(e => !e.IsBaseline && e.IsCompleted);
        if (experiment.Phase == "baseline")
        {
            _phaseLabel.Text = $"\U0001F4CA Baseline Phase — Week {experiment.CurrentWeek}";
            _statusLabel.Text = $"Process: {experiment.BaselineProcessName} • {baselineCount}/7 days completed";
        }
        else
        {
            _phaseLabel.Text = $"⚔️ Challenger Phase — Week {experiment.CurrentWeek}";
            _statusLabel.Text = $"Baseline: {experiment.BaselineProcessName} vs Challenger: {experiment.ChallengerProcessName}\nBaseline: {baselineCount} entries • Challenger: {challengerCount} entries";
        }

        await ShowWeekViewAsync(experiment, entries);
        if (experiment.Phase == "challenger" && baselineCount > 0 && challengerCount > 0)
            await ShowComparisonAsync(experiment);

        if (experiment.Phase == "baseline" && baselineCount >= 7)
        {
            var button = new Button { Text = "⚔️ Start Challenger Phase", BackgroundColor = Color.FromArgb("#6A1B9A"), TextColor = Colors.White, CornerRadius = 8, HeightRequest = 44, FontSize = 14, FontAttributes = FontAttributes.Bold };
            button.Clicked += async (_, _) => await StartChallengerAsync(experiment);
            _todaySection.Children.Add(button);
        }

        var archiveButton = new Button { Text = "Archive Experiment", BackgroundColor = Color.FromArgb("#E0E0E0"), TextColor = Color.FromArgb("#666"), CornerRadius = 8, HeightRequest = 36, FontSize = 12 };
        archiveButton.Clicked += async (_, _) =>
        {
            if (!await DisplayAlert("Archive?", "Archive this experiment and start fresh?", "Archive", "Cancel")) return;
            experiment.Status = "archived";
            experiment.CompletedAt = DateTime.UtcNow;
            await _experimentService.UpdateExperimentAsync(experiment);
            await RefreshAsync();
        };
        _todaySection.Children.Add(archiveButton);
    }

    private async Task ShowStartExperimentAsync()
    {
        var processes = await _storyService.GetWritingProcessesAsync(_auth.CurrentUsername);
        if (processes.Count == 0)
        {
            _todaySection.Children.Add(new Label { Text = "No writing processes defined. Create one in Writing Processes first.", FontSize = 13, TextColor = Color.FromArgb("#C62828"), FontAttributes = FontAttributes.Italic });
            return;
        }
        _todaySection.Children.Add(new Label { Text = "Start a new experiment by picking your baseline process.\nThis process will be assigned every day for Week 1 to establish retention data.", FontSize = 13, TextColor = Color.FromArgb("#555") });
        foreach (var process in processes)
        {
            var button = new Button { Text = process.Name, BackgroundColor = Color.FromArgb("#F3E5F5"), TextColor = Color.FromArgb("#6A1B9A"), CornerRadius = 8, HeightRequest = 40, FontSize = 13 };
            var captured = process;
            button.Clicked += async (_, _) => { await _experimentService.StartExperimentAsync(_auth.CurrentUsername, captured.Name); await RefreshAsync(); };
            _todaySection.Children.Add(button);
        }
    }

    private async Task ShowWeekViewAsync(WritingExperiment experiment, List<WritingExperimentEntry> entries)
    {
        _todaySection.Children.Clear();
        var today = DateTime.UtcNow.Date;
        var windowStart = _weekStartPicker?.Date ?? today.AddDays(-6);
        var windowEnd = _weekEndPicker?.Date ?? today;
        if (windowStart < experiment.StartedAt.Date) windowStart = experiment.StartedAt.Date;
        if (windowStart > today) windowStart = today;
        if (windowEnd > today) windowEnd = today;
        if (windowEnd < windowStart) windowEnd = windowStart;

        var changeProcessButton = new Button
        {
            Text = "\U0001F504 Change Week Process",
            BackgroundColor = Color.FromArgb("#F3E5F5"),
            TextColor = Color.FromArgb("#6A1B9A"),
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12,
            Padding = new Thickness(12, 0)
        };
        changeProcessButton.Clicked += async (_, _) => await ChangeWeekProcessAsync(experiment);

        var queueButton = new Button
        {
            Text = "\U0001F4C5 Queue Future Weeks",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"),
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12,
            Padding = new Thickness(12, 0)
        };
        queueButton.Clicked += async (_, _) => await ManageProcessQueueAsync(experiment);

        _todaySection.Children.Add(new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { changeProcessButton, queueButton }
        });

        var processQueue = ParseProcessQueue(experiment.ProcessQueueJson);
        if (processQueue.Count > 0)
        {
            _todaySection.Children.Add(new Label
            {
                Text = $"\U0001F4C5 Upcoming: {string.Join(" \u2192 ", processQueue.OrderBy(item => item.WeekNumber).Select(item => $"W{item.WeekNumber}: {item.ProcessName}"))}",
                FontSize = 11,
                TextColor = Color.FromArgb("#1565C0"),
                LineBreakMode = LineBreakMode.WordWrap
            });
        }

        var dateRow = new HorizontalStackLayout { Spacing = 8 };
        dateRow.Children.Add(new Label
        {
            Text = "From:",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        });

        _weekStartPicker = new DatePicker
        {
            Date = windowStart,
            MaximumDate = today,
            MinimumDate = experiment.StartedAt.Date,
            FontSize = 12
        };
        _weekStartPicker.DateSelected += async (_, _) => await RefreshAsync();
        dateRow.Children.Add(_weekStartPicker);

        dateRow.Children.Add(new Label
        {
            Text = "To:",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        });

        _weekEndPicker = new DatePicker
        {
            Date = windowEnd,
            MaximumDate = today,
            MinimumDate = experiment.StartedAt.Date,
            FontSize = 12
        };
        _weekEndPicker.DateSelected += async (_, _) => await RefreshAsync();
        dateRow.Children.Add(_weekEndPicker);

        _todaySection.Children.Add(dateRow);

        _todaySection.Children.Add(new Label { Text = "Mark Days Completed", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") });

        for (var date = windowStart; date <= windowEnd; date = date.AddDays(1))
        {
            var existingEntry = entries.FirstOrDefault(e => e.Date.Date == date);
            bool isBaseline;
            string assignedProcess;
            if (experiment.Phase == "baseline")
            {
                isBaseline = true;
                assignedProcess = experiment.BaselineProcessName;
            }
            else
            {
                int dayIndex = entries.Count(e => e.Date.Date < date);
                isBaseline = dayIndex % 2 == 0;
                assignedProcess = isBaseline ? experiment.BaselineProcessName : experiment.ChallengerProcessName;
            }

            if (existingEntry != null)
            {
                assignedProcess = existingEntry.AssignedProcess;
                isBaseline = existingEntry.IsBaseline;
            }

            var dayGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(70) },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 8
            };
            var dateLabel = new Label { Text = date.ToString("ddd M/d"), FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333"), VerticalOptions = LayoutOptions.Center };
            Grid.SetColumn(dateLabel, 0);
            dayGrid.Children.Add(dateLabel);
            var processStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            processStack.Children.Add(new Label { Text = assignedProcess, FontSize = 11, TextColor = Color.FromArgb("#666"), LineBreakMode = LineBreakMode.WordWrap });
            if (!string.IsNullOrWhiteSpace(existingEntry?.StoryTitle))
            {
                processStack.Children.Add(new Label
                {
                    Text = $"\U0001F4D6 {existingEntry.StoryTitle}",
                    FontSize = 10,
                    TextColor = Color.FromArgb("#6A1B9A"),
                    LineBreakMode = LineBreakMode.TailTruncation
                });
            }
            Grid.SetColumn(processStack, 1);
            dayGrid.Children.Add(processStack);

            if (existingEntry?.IsCompleted == true)
            {
                var summary = new Label { Text = BuildRetentionSummary(existingEntry), FontSize = 9, TextColor = Color.FromArgb("#2E7D32"), VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.WordWrap };
                var editButton = new Button
                {
                    Text = "\u270F",
                    BackgroundColor = Colors.Transparent,
                    TextColor = Color.FromArgb("#6A1B9A"),
                    WidthRequest = 28,
                    HeightRequest = 28,
                    Padding = 0,
                    FontSize = 10
                };
                var capturedEditEntry = existingEntry;
                editButton.Clicked += async (_, _) =>
                {
                    var newTitle = await DisplayPromptAsync(
                        "Story Title",
                        "What story did you write this day?",
                        "Save",
                        "Cancel",
                        initialValue: capturedEditEntry.StoryTitle ?? "",
                        maxLength: 200);
                    if (newTitle == null) return;
                    capturedEditEntry.StoryTitle = newTitle.Trim();
                    await _experimentService.CompleteEntryAsync(capturedEditEntry);
                    await RefreshAsync();
                };

                var completedActions = new HorizontalStackLayout
                {
                    Spacing = 4,
                    VerticalOptions = LayoutOptions.Center,
                    Children = { summary, editButton }
                };
                Grid.SetColumn(completedActions, 2);
                dayGrid.Children.Add(completedActions);
            }
            else
            {
                var recordButton = new Button { Text = "\U0001F4DD Record", BackgroundColor = Color.FromArgb("#6A1B9A"), TextColor = Colors.White, CornerRadius = 4, HeightRequest = 28, FontSize = 11, Padding = new Thickness(8, 0) };
                var capturedDate = date;
                var capturedProcess = assignedProcess;
                var capturedIsBaseline = isBaseline;
                var capturedEntry = existingEntry;
                recordButton.Clicked += async (_, _) =>
                {
                    var entry = capturedEntry ?? await _experimentService.CreateEntryAsync(experiment.Id, capturedProcess, capturedIsBaseline, capturedDate);
                    await RecordResultsAsync(entry);
                };
                Grid.SetColumn(recordButton, 2);
                dayGrid.Children.Add(recordButton);
            }

            var dayFrame = new Frame
            {
                Padding = 10,
                CornerRadius = 8,
                BackgroundColor = existingEntry?.IsCompleted == true ? Color.FromArgb("#E8F5E9") : date == today ? (isBaseline ? Color.FromArgb("#E8EAF6") : Color.FromArgb("#FFF3E0")) : Color.FromArgb("#FAFAFA"),
                BorderColor = date == today ? Color.FromArgb("#6A1B9A") : Colors.Transparent,
                HasShadow = false,
                Content = dayGrid
            };
            _todaySection.Children.Add(dayFrame);
        }
    }

    private async Task RecordResultsAsync(WritingExperimentEntry entry)
    {
        string? executionNotes = await DisplayPromptAsync("Execution Notes", "How did you follow the process? What did you do?", "Next", "Cancel", maxLength: 1000);
        if (executionNotes == null) return;
        entry.ExecutionNotes = executionNotes.Trim();

        string? storyTitle = await DisplayPromptAsync(
            "Story Title",
            "What story did you write today? (optional)",
            "Next",
            "Skip",
            initialValue: entry.StoryTitle ?? "",
            maxLength: 200);
        if (storyTitle != null)
            entry.StoryTitle = storyTitle.Trim();

        var tcs = new TaskCompletionSource<bool>();
        var overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };
        var inputFields = new Dictionary<string, Entry>();
        var intervals = new[] { (Key: "10s", Label: "10 seconds"), (Key: "30s", Label: "30 seconds"), (Key: "1m", Label: "1 minute"), (Key: "2m", Label: "2 minutes"), (Key: "3m", Label: "3 minutes") };
        var formStack = new VerticalStackLayout { Spacing = 12 };
        formStack.Children.Add(new Label { Text = "Retention Test", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#6A1B9A") });
        formStack.Children.Add(new Label { Text = "What percentage of the story do you retain at each interval?", FontSize = 12, TextColor = Color.FromArgb("#666") });

        foreach (var interval in intervals)
        {
            var input = new Entry
            {
                Placeholder = "0-100",
                Keyboard = Keyboard.Numeric,
                FontSize = 13,
                TextColor = Color.FromArgb("#333"),
                BackgroundColor = Color.FromArgb("#FAFAFA"),
                HeightRequest = 36,
                WidthRequest = 80,
                HorizontalTextAlignment = TextAlignment.Center
            };

            var percentLabel = new Label
            {
                Text = "%",
                FontSize = 13,
                TextColor = Color.FromArgb("#666"),
                VerticalOptions = LayoutOptions.Center
            };

            var inputRow = new HorizontalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label
                    {
                        Text = interval.Label + ":",
                        FontSize = 13,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#333"),
                        VerticalOptions = LayoutOptions.Center,
                        WidthRequest = 90
                    },
                    input,
                    percentLabel
                }
            };

            formStack.Children.Add(inputRow);
            inputFields[interval.Key] = input;
        }

        void CloseOverlay(bool saved)
        {
            if (Content is Grid rootGrid) rootGrid.Children.Remove(overlay);
            tcs.TrySetResult(saved);
        }

        var saveButton = new Button { Text = "Save Retention", BackgroundColor = Color.FromArgb("#6A1B9A"), TextColor = Colors.White, CornerRadius = 8, HeightRequest = 44 };
        saveButton.Clicked += (_, _) =>
        {
            foreach (var (key, input) in inputFields)
            {
                int value = -1;
                if (int.TryParse(input.Text, out int parsed))
                    value = Math.Clamp(parsed, 0, 100);

                switch (key)
                {
                    case "10s": entry.Retention10s = value; break;
                    case "30s": entry.Retention30s = value; break;
                    case "1m": entry.Retention1m = value; break;
                    case "2m": entry.Retention2m = value; break;
                    case "3m": entry.Retention3m = value; break;
                }
            }
            entry.IsCompleted = true;
            entry.CompletedAt = DateTime.UtcNow;
            CloseOverlay(true);
        };
        var skipButton = new Button { Text = "Skip (mark done without retention)", BackgroundColor = Color.FromArgb("#E0E0E0"), TextColor = Color.FromArgb("#666"), CornerRadius = 8, HeightRequest = 36, FontSize = 12 };
        skipButton.Clicked += (_, _) =>
        {
            entry.IsCompleted = true;
            entry.CompletedAt = DateTime.UtcNow;
            CloseOverlay(true);
        };
        var cancelButton = new Button { Text = "Cancel", BackgroundColor = Colors.Transparent, TextColor = Color.FromArgb("#999"), HeightRequest = 36, FontSize = 12 };
        cancelButton.Clicked += (_, _) => CloseOverlay(false);
        formStack.Children.Add(saveButton);
        formStack.Children.Add(skipButton);
        formStack.Children.Add(cancelButton);

        var card = new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 12,
            Padding = 20,
            MaximumWidthRequest = 500,
            MaximumHeightRequest = 700,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(16, 20),
            Content = new ScrollView { Content = formStack }
        };
        overlay.Children.Add(card);
        if (Content is not Grid mainGrid) return;
        mainGrid.Children.Add(overlay);

        if (!await tcs.Task) return;
        await _experimentService.CompleteEntryAsync(entry);
        await RefreshAsync();
    }

    private async Task ChangeWeekProcessAsync(WritingExperiment experiment)
    {
        var processes = await _storyService.GetWritingProcessesAsync(_auth.CurrentUsername);
        if (processes.Count == 0)
        {
            await DisplayAlert("No Processes", "Create writing processes first.", "OK");
            return;
        }

        var selected = await DisplayActionSheet(
            "Change process for all days in current date range",
            "Cancel",
            null,
            processes.Select(process => process.Name).ToArray());
        if (string.IsNullOrEmpty(selected) || selected == "Cancel") return;

        var fromDate = _weekStartPicker?.Date ?? DateTime.Today.AddDays(-6);
        var toDate = _weekEndPicker?.Date ?? DateTime.Today;
        bool isBaseline = selected == experiment.BaselineProcessName;

        await _experimentService.ChangeProcessForRangeAsync(
            experiment.Id,
            fromDate,
            toDate,
            selected,
            isBaseline);

        await DisplayAlert(
            "Updated",
            $"Changed process to '{selected}' for {fromDate:M/d} - {toDate:M/d}.",
            "OK");
        await RefreshAsync();
    }

    private async Task ManageProcessQueueAsync(WritingExperiment experiment)
    {
        var processes = await _storyService.GetWritingProcessesAsync(_auth.CurrentUsername);
        if (processes.Count == 0)
        {
            await DisplayAlert("No Processes", "Create writing processes first.", "OK");
            return;
        }

        var queue = ParseProcessQueue(experiment.ProcessQueueJson);
        bool done = false;
        while (!done)
        {
            var displayOptions = queue
                .OrderBy(item => item.WeekNumber)
                .Select(item => $"Week {item.WeekNumber}: {item.ProcessName}")
                .ToList();
            displayOptions.Add("+ Add Week");
            if (queue.Count > 0) displayOptions.Add("Clear All");
            displayOptions.Add("Done");

            var choice = await DisplayActionSheet(
                $"Process Queue ({queue.Count} weeks planned)",
                "Cancel",
                null,
                displayOptions.ToArray());

            if (string.IsNullOrEmpty(choice) || choice == "Cancel" || choice == "Done")
            {
                done = true;
            }
            else if (choice == "+ Add Week")
            {
                int nextWeek = queue.Count > 0
                    ? queue.Max(item => item.WeekNumber) + 1
                    : experiment.CurrentWeek + 1;
                var picked = await DisplayActionSheet(
                    $"Process for Week {nextWeek}",
                    "Cancel",
                    null,
                    processes.Select(process => process.Name).ToArray());
                if (!string.IsNullOrEmpty(picked) && picked != "Cancel")
                    queue.Add(new ProcessQueueItem { WeekNumber = nextWeek, ProcessName = picked });
            }
            else if (choice == "Clear All")
            {
                queue.Clear();
            }
            else
            {
                var queuedEntry = queue.FirstOrDefault(item =>
                    choice == $"Week {item.WeekNumber}: {item.ProcessName}");
                if (queuedEntry == null) continue;

                var action = await DisplayActionSheet(
                    $"Week {queuedEntry.WeekNumber}: {queuedEntry.ProcessName}",
                    "Cancel",
                    null,
                    "Change Process",
                    "Remove");
                if (action == "Change Process")
                {
                    var picked = await DisplayActionSheet(
                        "New process",
                        "Cancel",
                        null,
                        processes.Select(process => process.Name).ToArray());
                    if (!string.IsNullOrEmpty(picked) && picked != "Cancel")
                        queuedEntry.ProcessName = picked;
                }
                else if (action == "Remove")
                {
                    queue.Remove(queuedEntry);
                }
            }
        }

        experiment.ProcessQueueJson = System.Text.Json.JsonSerializer.Serialize(queue);
        await _experimentService.UpdateExperimentAsync(experiment);
        await RefreshAsync();
    }

    private async Task StartChallengerAsync(WritingExperiment experiment)
    {
        var challengers = (await _storyService.GetWritingProcessesAsync(_auth.CurrentUsername)).Where(p => p.Name != experiment.BaselineProcessName).ToList();
        if (challengers.Count == 0) { await DisplayAlert("No Challengers", "Create another writing process to challenge against.", "OK"); return; }
        string? selected = await DisplayActionSheet("Pick Challenger Process", "Cancel", null, challengers.Select(p => p.Name).ToArray());
        if (string.IsNullOrEmpty(selected) || selected == "Cancel") return;
        await _experimentService.SetChallengerAsync(experiment.Id, selected);
        await RefreshAsync();
    }

    private async Task ShowComparisonAsync(WritingExperiment experiment)
    {
        var (baselineAvg, challengerAvg, baselineCount, challengerCount) = await _experimentService.GetComparisonAsync(experiment.Id);
        _comparisonSection.Children.Add(new Label { Text = "\U0001F4CA Comparison", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") });
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) }, RowSpacing = 6, ColumnSpacing = 12 };
        AddComparisonCell(grid, $"\U0001F4CA {experiment.BaselineProcessName}", 0, 0, 13, "#3F51B5", true);
        AddComparisonCell(grid, $"⚔️ {experiment.ChallengerProcessName}", 1, 0, 13, "#E65100", true);
        AddComparisonCell(grid, $"{baselineAvg:F1}%", 0, 1, 24, "#3F51B5", true);
        AddComparisonCell(grid, $"{challengerAvg:F1}%", 1, 1, 24, "#E65100", true);
        AddComparisonCell(grid, $"{baselineCount} entries", 0, 2, 11, "#888", false);
        AddComparisonCell(grid, $"{challengerCount} entries", 1, 2, 11, "#888", false);
        _comparisonSection.Children.Add(new Frame { Padding = 14, CornerRadius = 10, BackgroundColor = Colors.White, BorderColor = Color.FromArgb("#E0E0E0"), HasShadow = false, Content = grid });

        if (baselineCount >= 7 && challengerCount >= 7)
        {
            string recommendation = baselineAvg > challengerAvg + .5 ? $"\U0001F3C6 Baseline ({experiment.BaselineProcessName}) wins by {baselineAvg - challengerAvg:F1} percentage points. Consider keeping it as your default."
                : challengerAvg > baselineAvg + .5 ? $"\U0001F3C6 Challenger ({experiment.ChallengerProcessName}) wins by {challengerAvg - baselineAvg:F1} percentage points. Consider switching your default."
                : "\U0001F91D Too close to call. Consider running more days or testing a different challenger.";
            _comparisonSection.Children.Add(new Label { Text = recommendation, FontSize = 13, TextColor = Color.FromArgb("#6A1B9A"), FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.WordWrap });
        }
    }

    private static void AddComparisonCell(Grid grid, string text, int column, int row, double size, string color, bool bold)
    {
        var label = new Label { Text = text, FontSize = size, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None, TextColor = Color.FromArgb(color), HorizontalTextAlignment = TextAlignment.Center };
        Grid.SetColumn(label, column);
        Grid.SetRow(label, row);
        grid.Children.Add(label);
    }

    private static string BuildRetentionSummary(WritingExperimentEntry entry)
    {
        var values = new List<string>();
        if (entry.Retention10s >= 0) values.Add($"10s:{entry.Retention10s}%");
        if (entry.Retention30s >= 0) values.Add($"30s:{entry.Retention30s}%");
        if (entry.Retention1m >= 0) values.Add($"1m:{entry.Retention1m}%");
        if (entry.Retention2m >= 0) values.Add($"2m:{entry.Retention2m}%");
        if (entry.Retention3m >= 0) values.Add($"3m:{entry.Retention3m}%");
        return values.Count > 0 ? string.Join(" ", values) : "✅";
    }

    private class ProcessQueueItem
    {
        public ProcessQueueItem() { }

        public int WeekNumber { get; set; }
        public string ProcessName { get; set; } = "";
    }

    private static List<ProcessQueueItem> ParseProcessQueue(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<ProcessQueueItem>();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<ProcessQueueItem>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

}
