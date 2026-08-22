using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class ChallengeTasksPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly TaskService _tasks;
    private readonly WeeklyChallengeService _challengeService;
    private readonly IdeasService? _ideasService;
    private readonly bool _isFocusMode;

    private VerticalStackLayout _chartContainer = null!;
    private VerticalStackLayout _commitmentsList = null!;
    private VerticalStackLayout _topCandidatesList = null!;
    private Label _progressLabel = null!;
    private Label _summaryLabel = null!;
    private Button _addCommitmentBtn = null!;
    private bool _topCandidatesExpanded;

    public ChallengeTasksPage(
        AuthService auth,
        TaskService tasks,
        WeeklyChallengeService challengeService,
        IdeasService? ideasService,
        bool isFocusMode)
    {
        _auth = auth;
        _tasks = tasks;
        _challengeService = challengeService;
        _ideasService = ideasService;
        _isFocusMode = isFocusMode;
        Title = isFocusMode ? "Focus Tasks" : "Free Tasks";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout { Padding = 20, Spacing = 12 };
        var accent = _isFocusMode ? Color.FromArgb("#7B1FA2") : Color.FromArgb("#1565C0");
        var pale = _isFocusMode ? Color.FromArgb("#F3E5F5") : Color.FromArgb("#E3F2FD");

        mainStack.Children.Add(new Label
        {
            Text = _isFocusMode ? "\U0001F3AF Focus Tasks" : "\U0001F30D Free Tasks",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = accent
        });
        mainStack.Children.Add(new Label
        {
            Text = _isFocusMode
                ? "Tasks toward your weekly focus category."
                : "Tasks from any non-focus category.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        _chartContainer = new VerticalStackLayout { Spacing = 4 };
        mainStack.Children.Add(_chartContainer);
        _summaryLabel = new Label { FontSize = 12, TextColor = Color.FromArgb("#888") };
        mainStack.Children.Add(_summaryLabel);
        _progressLabel = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = accent
        };
        mainStack.Children.Add(_progressLabel);
        _commitmentsList = new VerticalStackLayout { Spacing = 4 };
        mainStack.Children.Add(_commitmentsList);

        _addCommitmentBtn = new Button
        {
            Text = _isFocusMode ? "+ Pick Focus Task" : "+ Pick Free Task",
            BackgroundColor = pale,
            TextColor = accent,
            FontSize = 13,
            CornerRadius = 8,
            HeightRequest = 40
        };
        _addCommitmentBtn.Clicked += async (_, _) => await AddCommitmentAsync();
        mainStack.Children.Add(_addCommitmentBtn);

        _topCandidatesList = new VerticalStackLayout { Spacing = 2 };
        mainStack.Children.Add(_topCandidatesList);

        var candidateButtons = new HorizontalStackLayout { Spacing = 8 };
        var addCandidate = new Button
        {
            Text = "+ Top Candidate",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            FontSize = 11,
            CornerRadius = 4,
            HeightRequest = 28,
            Padding = new Thickness(8, 0)
        };
        addCandidate.Clicked += async (_, _) => await AddTopCandidateAsync();
        candidateButtons.Children.Add(addCandidate);

        var markExisting = new Button
        {
            Text = "\u2B50 From Existing",
            BackgroundColor = pale,
            TextColor = accent,
            FontSize = 11,
            CornerRadius = 4,
            HeightRequest = 28,
            Padding = new Thickness(8, 0)
        };
        markExisting.Clicked += async (_, _) => await MarkExistingAsTopCandidateAsync();
        candidateButtons.Children.Add(markExisting);
        mainStack.Children.Add(candidateButtons);

        if (_isFocusMode)
        {
            var consult = new Button
            {
                Text = "Consult LLM",
                BackgroundColor = Color.FromArgb("#E1BEE7"),
                TextColor = Color.FromArgb("#7B1FA2"),
                FontSize = 11,
                CornerRadius = 4,
                HeightRequest = 28
            };
            consult.Clicked += async (_, _) => await ConsultLlmAsync();
            mainStack.Children.Add(consult);
        }

        var rootGrid = new Grid();
        rootGrid.Children.Add(new ScrollView { Content = mainStack });
        Content = rootGrid;
    }

    private async Task RefreshAsync()
    {
        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (challenge == null)
        {
            _progressLabel.Text = "No active challenge. Start one from the Tasks page.";
            _summaryLabel.Text = "";
            _commitmentsList.Children.Clear();
            _topCandidatesList.Children.Clear();
            _addCommitmentBtn.IsVisible = false;
            return;
        }

        await _challengeService.ProcessWeekEndAsync(_auth.CurrentUsername);
        challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (challenge == null) return;

        var commitments = await _challengeService.GetCurrentWeekCommitmentsAsync(challenge.Id);
        var relevant = commitments.Where(c => c.IsFocusTask == _isFocusMode).ToList();
        var (focusTarget, freeTarget) = WeeklyChallengeService.CalculateTaskSplit(
            challenge.CurrentAllowance, challenge.FreeTaskRatio);
        int target = _isFocusMode ? focusTarget : freeTarget;
        int completed = relevant.Count(c => c.IsCompleted);

        _summaryLabel.Text = $"Allowance: {challenge.CurrentAllowance}/wk \u2022 Ratio: 1 free per {challenge.FreeTaskRatio} \u2022 Streak: {challenge.SuccessStreak} weeks";
        _progressLabel.Text = _isFocusMode
            ? $"Focus: {completed}/{target} ({challenge.FocusCategory})"
            : target > 0 ? $"Free: {completed}/{target}" : "Free Tasks: unlocks at allowance 3";

        var taskLookup = (await _tasks.GetActiveTasksAsync(_auth.CurrentUsername))
            .Concat(await _tasks.GetCompletedTasksAsync(_auth.CurrentUsername))
            .GroupBy(t => t.Id)
            .ToDictionary(g => g.Key, g => g.First());

        _commitmentsList.Children.Clear();
        if (relevant.Count > 0)
        {
            var headerGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(40) },
                    new ColumnDefinition { Width = new GridLength(50) },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = new GridLength(90) },
                    new ColumnDefinition { Width = new GridLength(40) }
                },
                BackgroundColor = _isFocusMode ? Color.FromArgb("#7B1FA2") : Color.FromArgb("#1565C0"),
                Padding = new Thickness(4, 6)
            };

            var headers = new[] { "", "Pri", "Title", "Category", "" };
            for (int i = 0; i < headers.Length; i++)
            {
                var label = new Label
                {
                    Text = headers[i],
                    FontSize = 10,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                    VerticalOptions = LayoutOptions.Center
                };
                Grid.SetColumn(label, i);
                headerGrid.Children.Add(label);
            }
            _commitmentsList.Children.Add(headerGrid);

            foreach (var commitment in relevant)
            {
                taskLookup.TryGetValue(commitment.TaskId, out var task);

                var rowGrid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(40) },
                        new ColumnDefinition { Width = new GridLength(50) },
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = new GridLength(90) },
                        new ColumnDefinition { Width = new GridLength(40) }
                    },
                    BackgroundColor = commitment.IsCompleted ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#FAFAFA"),
                    Padding = new Thickness(4, 4),
                    Margin = new Thickness(0, 1)
                };

                var checkbox = new CheckBox
                {
                    IsChecked = commitment.IsCompleted,
                    Color = _isFocusMode ? Color.FromArgb("#7B1FA2") : Color.FromArgb("#1565C0"),
                    Scale = 0.8
                };
                var capturedCommitment = commitment;
                var capturedTask = task;
                checkbox.CheckedChanged += async (_, e) =>
                {
                    if (e.Value && !capturedCommitment.IsCompleted)
                    {
                        if (capturedTask != null)
                            await _tasks.CompleteTaskAsync(capturedTask);
                        await _challengeService.MarkCommitmentCompletedAsync(capturedCommitment.TaskId);
                        await RefreshAsync();
                    }
                };
                Grid.SetColumn(checkbox, 0);
                rowGrid.Children.Add(checkbox);

                string dot = (task?.Priority ?? 2) switch
                {
                    1 => "\U0001F534",
                    3 => "\U0001F7E2",
                    _ => "\U0001F7E1"
                };
                var priorityLabel = new Label { Text = dot, FontSize = 12, VerticalOptions = LayoutOptions.Center };
                Grid.SetColumn(priorityLabel, 1);
                rowGrid.Children.Add(priorityLabel);

                var titleLabel = new Label
                {
                    Text = task?.Title ?? $"Task #{commitment.TaskId}",
                    FontSize = 12,
                    TextColor = commitment.IsCompleted ? Color.FromArgb("#999") : Color.FromArgb("#333"),
                    TextDecorations = commitment.IsCompleted ? TextDecorations.Strikethrough : TextDecorations.None,
                    VerticalOptions = LayoutOptions.Center,
                    LineBreakMode = LineBreakMode.WordWrap
                };
                Grid.SetColumn(titleLabel, 2);
                rowGrid.Children.Add(titleLabel);

                var categoryLabel = new Label
                {
                    Text = task?.Category ?? "",
                    FontSize = 10,
                    TextColor = Color.FromArgb("#888"),
                    VerticalOptions = LayoutOptions.Center
                };
                Grid.SetColumn(categoryLabel, 3);
                rowGrid.Children.Add(categoryLabel);

                var removeButton = new Button
                {
                    Text = "\u2716",
                    BackgroundColor = Colors.Transparent,
                    TextColor = Color.FromArgb("#C62828"),
                    WidthRequest = 30,
                    HeightRequest = 26,
                    Padding = 0,
                    FontSize = 10
                };
                var capturedForRemove = commitment;
                removeButton.Clicked += async (_, _) =>
                {
                    bool confirm = await DisplayAlert("Remove?", "Remove this commitment?", "Remove", "Cancel");
                    if (!confirm) return;
                    await _challengeService.RemoveCommitmentAsync(capturedForRemove.Id);
                    await RefreshAsync();
                };
                Grid.SetColumn(removeButton, 4);
                rowGrid.Children.Add(removeButton);

                _commitmentsList.Children.Add(rowGrid);
            }
        }

        _addCommitmentBtn.IsVisible = target > relevant.Count;
        _addCommitmentBtn.IsEnabled = _addCommitmentBtn.IsVisible;
        await RefreshTopCandidatesAsync(challenge, commitments.Select(c => c.TaskId).ToHashSet());

        if (_isFocusMode) await RefreshChartAsync();
        else _chartContainer.IsVisible = false;
    }

    private async Task RefreshTopCandidatesAsync(WeeklyChallenge challenge, HashSet<int> committedIds)
    {
        _topCandidatesList.Children.Clear();
        var candidates = (await _tasks.GetActiveTasksAsync(_auth.CurrentUsername))
            .Where(t => t.IsTopCandidate && !committedIds.Contains(t.Id) &&
                (_isFocusMode
                    ? string.Equals(t.Category, challenge.FocusCategory, StringComparison.OrdinalIgnoreCase)
                    : !string.Equals(t.Category, challenge.FocusCategory, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (candidates.Count == 0) return;

        var header = new Label
        {
            Text = _topCandidatesExpanded
                ? $"\u25BC \u2B50 Top Candidates ({candidates.Count})"
                : $"\u25B6 \u2B50 Top Candidates ({candidates.Count})",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FF9800")
        };
        var headerTap = new TapGestureRecognizer();
        headerTap.Tapped += async (_, _) =>
        {
            _topCandidatesExpanded = !_topCandidatesExpanded;
            await RefreshAsync();
        };
        header.GestureRecognizers.Add(headerTap);
        _topCandidatesList.Children.Add(header);
        if (!_topCandidatesExpanded) return;

        var table = new Grid
        {
            ColumnSpacing = 1,
            RowSpacing = 1,
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        string[] tableHeaders = { "Priority", "Title", "Category", "Actions" };
        for (int column = 0; column < tableHeaders.Length; column++)
        {
            table.Add(new Label
            {
                Text = tableHeaders[column],
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                BackgroundColor = Color.FromArgb("#FF9800"),
                Padding = new Thickness(8, 6)
            }, column, 0);
        }

        for (int rowIndex = 0; rowIndex < candidates.Count; rowIndex++)
        {
            var task = candidates[rowIndex];
            int gridRow = rowIndex + 1;
            var rowColor = rowIndex % 2 == 0 ? Color.FromArgb("#FFF8E1") : Colors.White;

            table.Add(CreateCandidateCell(PriorityDot(task.Priority), rowColor), 0, gridRow);
            table.Add(CreateCandidateCell(task.Title, rowColor), 1, gridRow);
            table.Add(CreateCandidateCell(task.Category, rowColor), 2, gridRow);

            var actionsCell = new Grid { BackgroundColor = rowColor, Padding = new Thickness(4, 0) };
            var unstar = new Button
            {
                Text = "\u2B50",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#FF9800"),
                WidthRequest = 34,
                HeightRequest = 30,
                Padding = 0
            };
            unstar.Clicked += async (_, _) =>
            {
                task.IsTopCandidate = false;
                await _tasks.UpdateTaskAsync(task);
                await RefreshAsync();
            };
            actionsCell.Children.Add(unstar);
            table.Add(actionsCell, 3, gridRow);
        }

        _topCandidatesList.Children.Add(table);
    }

    private static Label CreateCandidateCell(string text, Color backgroundColor) => new()
    {
        Text = text,
        FontSize = 11,
        TextColor = Color.FromArgb("#333"),
        BackgroundColor = backgroundColor,
        Padding = new Thickness(8, 6),
        VerticalTextAlignment = TextAlignment.Center,
        LineBreakMode = LineBreakMode.WordWrap
    };

    private async Task AddCommitmentAsync()
    {
        if (DateTime.Today.DayOfWeek == DayOfWeek.Saturday)
        {
            await DisplayAlert("Deadline Passed", "You can designate tasks again starting Sunday.", "OK");
            return;
        }
        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (challenge == null) return;
        var available = _isFocusMode
            ? await _challengeService.GetAvailableFocusTasksAsync(_auth.CurrentUsername, challenge.FocusCategory)
            : await _challengeService.GetAvailableNonFocusTasksAsync(_auth.CurrentUsername, challenge.FocusCategory);
        if (available.Count == 0)
        {
            await DisplayAlert("No Tasks", "No available tasks for this section.", "OK");
            return;
        }
        var selected = await ShowTaskPickerAsync(available, _isFocusMode ? challenge.FocusCategory : "Non-focus");
        if (selected == null) return;
        await _challengeService.AddCommitmentAsync(challenge.Id, selected.Id, _isFocusMode);
        await RefreshAsync();
    }

    private async Task AddTopCandidateAsync()
    {
        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (challenge == null) return;
        string? title = await DisplayPromptAsync("New Top Candidate", "Task title:", "Create", "Cancel", maxLength: 200);
        if (string.IsNullOrWhiteSpace(title)) return;
        string category = challenge.FocusCategory;
        if (!_isFocusMode)
        {
            var categories = (await _tasks.GetActiveTasksAsync(_auth.CurrentUsername))
                .Select(t => t.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c) && !string.Equals(c, challenge.FocusCategory, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();
            if (categories.Count == 0)
                categories.Add(string.Equals("General", challenge.FocusCategory, StringComparison.OrdinalIgnoreCase) ? "Other" : "General");
            var picked = await DisplayActionSheet("Category", "Cancel", null, categories.ToArray());
            if (string.IsNullOrEmpty(picked) || picked == "Cancel") return;
            category = picked;
        }
        var priorityChoice = await DisplayActionSheet("Priority", "Cancel", null, "\U0001F534 High", "\U0001F7E1 Medium", "\U0001F7E2 Low");
        if (string.IsNullOrEmpty(priorityChoice) || priorityChoice == "Cancel") return;
        int priority = priorityChoice.Contains("High") ? 1 : priorityChoice.Contains("Low") ? 3 : 2;
        var task = await _tasks.CreateTaskAsync(_auth.CurrentUsername, title.Trim(), category, priority);
        task.IsTopCandidate = true;
        await _tasks.UpdateTaskAsync(task);
        if (_ideasService != null)
        {
            try { await _ideasService.CreateIdeaAsync(_auth.CurrentUsername, title.Trim(), "tasks_ideas"); } catch { }
        }
        await RefreshAsync();
    }

    private async Task MarkExistingAsTopCandidateAsync()
    {
        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (challenge == null) return;
        var tasks = (await _tasks.GetActiveTasksAsync(_auth.CurrentUsername))
            .Where(t => !t.IsTopCandidate && (_isFocusMode
                ? string.Equals(t.Category, challenge.FocusCategory, StringComparison.OrdinalIgnoreCase)
                : !string.Equals(t.Category, challenge.FocusCategory, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (tasks.Count == 0)
        {
            await DisplayAlert("No Tasks", "No available tasks to mark.", "OK");
            return;
        }
        var selected = await ShowTaskPickerAsync(tasks, _isFocusMode ? challenge.FocusCategory : "Non-focus");
        if (selected == null) return;
        selected.IsTopCandidate = true;
        await _tasks.UpdateTaskAsync(selected);
        await RefreshAsync();
    }

    private async Task ConsultLlmAsync()
    {
        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        if (challenge == null) return;
        var tasks = await _challengeService.GetAvailableFocusTasksAsync(_auth.CurrentUsername, challenge.FocusCategory);
        if (tasks.Count == 0)
        {
            await DisplayAlert("No Tasks", "No focus tasks to prioritize.", "OK");
            return;
        }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Prioritize these weekly focus tasks.");
        sb.AppendLine($"FOCUS CATEGORY: {challenge.FocusCategory}");
        sb.AppendLine("ID|TITLE|PRIORITY|NOTES");
        foreach (var task in tasks)
            sb.AppendLine($"{task.Id}|{task.Title}|{task.Priority}|{task.Notes}");
        sb.AppendLine("Return ID|PRIORITY|PICK_THIS_WEEK|REASON, one task per line.");
        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert("Prompt Copied", $"Exported {tasks.Count} tasks. Paste into your LLM.", "OK");
    }

    private async Task<TaskItem?> ShowTaskPickerAsync(List<TaskItem> tasks, string categoryLabel)
    {
        var tcs = new TaskCompletionSource<TaskItem?>();
        var overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };
        var list = new VerticalStackLayout { Padding = 8, Spacing = 4 };
        var search = new Entry { Placeholder = "Search tasks...", Margin = new Thickness(12, 8, 12, 0) };
        void Rebuild(string query)
        {
            list.Children.Clear();
            var shown = tasks.Where(t => string.IsNullOrWhiteSpace(query) ||
                t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (t.Notes ?? "").Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => t.IsTopCandidate).ThenBy(t => t.Priority).ThenBy(t => t.Title).ToList();
            foreach (var task in shown) list.Children.Add(BuildPickerCard(task, overlay, tcs));
            if (shown.Count == 0) list.Children.Add(new Label { Text = "No tasks match your search.", TextColor = Color.FromArgb("#999") });
        }
        search.TextChanged += (_, e) => Rebuild(e.NewTextValue ?? "");
        var cancel = new Button { Text = "Cancel", BackgroundColor = Colors.Transparent, TextColor = Color.FromArgb("#7B1FA2") };
        cancel.Clicked += (_, _) =>
        {
            if (Content is Grid rootGrid)
                rootGrid.Children.Remove(overlay);
            tcs.TrySetResult(null);
        };
        var stack = new VerticalStackLayout
        {
            Children =
            {
                new Label { Text = $"Pick Task — {categoryLabel}", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, BackgroundColor = Color.FromArgb("#7B1FA2"), Padding = 16 },
                search,
                new ScrollView { MaximumHeightRequest = 400, Content = list },
                cancel
            }
        };
        overlay.Children.Add(new Frame
        {
            Padding = 0, CornerRadius = 12, BackgroundColor = Colors.White,
            MaximumWidthRequest = 480, MaximumHeightRequest = 600,
            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(16, 0), Content = stack
        });
        if (Content is Grid rootGrid)
            rootGrid.Children.Add(overlay);
        Rebuild("");
        return await tcs.Task;
    }

    private Frame BuildPickerCard(TaskItem task, Grid overlay, TaskCompletionSource<TaskItem?> tcs)
    {
        var frame = new Frame
        {
            Padding = 12, CornerRadius = 8, HasShadow = false,
            BackgroundColor = task.IsTopCandidate ? Color.FromArgb("#FFF3E0") : Color.FromArgb("#F5F5F5"),
            BorderColor = task.IsTopCandidate ? Color.FromArgb("#FF9800") : Colors.Transparent,
            Content = new Label
            {
                Text = $"{PriorityDot(task.Priority)} {(task.IsTopCandidate ? "\u2B50 " : "")}{task.Title}",
                FontSize = 14, TextColor = Color.FromArgb("#333"), LineBreakMode = LineBreakMode.WordWrap
            }
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (Content is Grid rootGrid)
                rootGrid.Children.Remove(overlay);
            tcs.TrySetResult(task);
        };
        frame.GestureRecognizers.Add(tap);
        return frame;
    }

    private async Task RefreshChartAsync()
    {
        _chartContainer.IsVisible = true;
        _chartContainer.Children.Clear();
        _chartContainer.Children.Add(new Label
        {
            Text = "Focus Allowance History", FontSize = 14,
            FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#7B1FA2")
        });
        var history = await _challengeService.GetChallengeHistoryAsync(_auth.CurrentUsername, 12);
        if (history.Count == 0)
        {
            _chartContainer.Children.Add(new Label { Text = "No challenge history yet.", FontSize = 12, TextColor = Color.FromArgb("#999"), FontAttributes = FontAttributes.Italic });
            return;
        }
        history.Reverse();
        int max = Math.Max(1, history.Max(c => c.CurrentAllowance));
        var grid = new Grid { ColumnSpacing = 4, RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) } };
        for (int i = 0; i < history.Count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var challenge = history[i];
            var barStack = new VerticalStackLayout { VerticalOptions = LayoutOptions.End, HorizontalOptions = LayoutOptions.Center };
            barStack.Children.Add(new Label { Text = challenge.CurrentAllowance.ToString(), FontSize = 9, HorizontalTextAlignment = TextAlignment.Center });
            barStack.Children.Add(new BoxView { HeightRequest = Math.Max(8, challenge.CurrentAllowance / (double)max * 80), WidthRequest = 20, CornerRadius = 4, Color = Color.FromArgb("#7B1FA2") });
            grid.Add(barStack, i, 0);
            grid.Add(new Label { Text = challenge.StartedAt.ToString("M/d"), FontSize = 8, TextColor = Color.FromArgb("#999"), HorizontalTextAlignment = TextAlignment.Center }, i, 1);
        }
        _chartContainer.Children.Add(new Frame { Padding = 12, CornerRadius = 10, BackgroundColor = Color.FromArgb("#F5F5F5"), BorderColor = Colors.Transparent, HasShadow = false, Content = grid });
    }

    private static string PriorityDot(int priority) => priority switch
    {
        1 => "\U0001F534",
        3 => "\U0001F7E2",
        _ => "\U0001F7E1"
    };
}
