using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class FocusContextPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly TaskService _tasks;
    private readonly WeeklyChallengeService _challengeService;
    private readonly DatabaseService _db;
    private readonly FocusContextService _service;

    private VerticalStackLayout _bulletsList = null!;
    private VerticalStackLayout _archivedList = null!;
    private List<FocusBulletPoint> _points = new();
    private bool _showArchived;
    private bool _initialized;

    public FocusContextPage(AuthService auth, TaskService tasks, WeeklyChallengeService challengeService, DatabaseService db)
    {
        _auth = auth;
        _tasks = tasks;
        _challengeService = challengeService;
        _db = db;
        _service = new FocusContextService(db);
        Title = "Focus Context";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await EnsureInitializedAsync();
        await LoadBulletsAsync();
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        if (_db.IsReadOnly)
        {
            _initialized = true;
            return;
        }
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<FocusBulletPoint>();
        _initialized = true;
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout { Padding = 20, Spacing = 12 };
        mainStack.Children.Add(new Label { Text = "\U0001F3AF Focus Context", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#7B1FA2") });
        mainStack.Children.Add(new Label { Text = "Define what you are focusing on as ordered bullet points. Export with your focus tasks for LLM context.", FontSize = 13, TextColor = Color.FromArgb("#666") });

        var btnRow = new HorizontalStackLayout { Spacing = 8 };
        var addBtn = new Button { Text = "+ Add Point", BackgroundColor = Color.FromArgb("#7B1FA2"), TextColor = Colors.White, CornerRadius = 8, HeightRequest = 40, FontSize = 13, Padding = new Thickness(14, 0), IsEnabled = !_db.IsReadOnly };
        addBtn.Clicked += async (_, _) => await AddBulletAsync();
        btnRow.Children.Add(addBtn);
        var exportBtn = new Button { Text = "\U0001F4CB Export for LLM", BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White, CornerRadius = 8, HeightRequest = 40, FontSize = 13, Padding = new Thickness(14, 0) };
        exportBtn.Clicked += async (_, _) => await ExportForLlmAsync();
        btnRow.Children.Add(exportBtn);
        var updateBtn = new Button
        {
            Text = " Update from Conversation",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 13,
            HeightRequest = 40,
            Padding = new Thickness(12, 0)
        };
        updateBtn.Clicked += async (_, _) => await UpdateFromConversationAsync();
        btnRow.Children.Add(updateBtn);
        mainStack.Children.Add(btnRow);

        _bulletsList = new VerticalStackLayout { Spacing = 6 };
        mainStack.Children.Add(_bulletsList);

        var archiveToggle = new Button { Text = "Show Archived", BackgroundColor = Color.FromArgb("#E0E0E0"), TextColor = Color.FromArgb("#666"), CornerRadius = 8, HeightRequest = 36, FontSize = 12 };
        archiveToggle.Clicked += async (_, _) =>
        {
            _showArchived = !_showArchived;
            archiveToggle.Text = _showArchived ? "Hide Archived" : "Show Archived";
            await LoadBulletsAsync();
        };
        mainStack.Children.Add(archiveToggle);
        _archivedList = new VerticalStackLayout { Spacing = 6, IsVisible = false };
        mainStack.Children.Add(_archivedList);
        Content = new ScrollView { Content = mainStack };
    }

    private async Task LoadBulletsAsync()
    {
        try
        {
            var conn = await _db.GetConnectionAsync();
            var active = await conn.Table<FocusBulletPoint>()
                .Where(b => b.Username == _auth.CurrentUsername && b.Status == "active")
                .OrderBy(b => b.SortOrder).ToListAsync();
            _points = active;

            _bulletsList.Children.Clear();
            if (active.Count == 0)
                _bulletsList.Children.Add(new Label { Text = "No focus points yet. Add what you are currently focusing on.", FontSize = 13, TextColor = Color.FromArgb("#999"), FontAttributes = FontAttributes.Italic });
            for (int i = 0; i < active.Count; i++)
                _bulletsList.Children.Add(BuildBulletCard(active[i], i, active.Count, false));

            _archivedList.Children.Clear();
            _archivedList.IsVisible = _showArchived;
            if (!_showArchived) return;

            var archived = await conn.Table<FocusBulletPoint>()
                .Where(b => b.Username == _auth.CurrentUsername && b.Status == "archived")
                .OrderByDescending(b => b.ArchivedAt).ToListAsync();
            if (archived.Count > 0)
            {
                _archivedList.Children.Add(new Label { Text = $"Archived ({archived.Count})", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#999") });
                foreach (var bullet in archived)
                    _archivedList.Children.Add(BuildBulletCard(bullet, -1, -1, true));
            }
        }
        catch (SQLite.SQLiteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            _bulletsList.Children.Clear();
            _bulletsList.Children.Add(new Label
            {
                Text = "Focus Context has not been synced to this device yet.",
                FontSize = 13,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic
            });
            _archivedList.Children.Clear();
            _archivedList.IsVisible = false;
        }
    }

    private Frame BuildBulletCard(FocusBulletPoint bullet, int index, int total, bool isArchived)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(30) },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        if (!isArchived)
        {
            var orderLabel = new Label { Text = $"{index + 1}.", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#7B1FA2"), VerticalOptions = LayoutOptions.Center };
            Grid.SetColumn(orderLabel, 0);
            row.Children.Add(orderLabel);
        }

        var textLabel = new Label { Text = bullet.Text, FontSize = 13, TextColor = isArchived ? Color.FromArgb("#999") : Color.FromArgb("#333"), VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.WordWrap };
        Grid.SetColumn(textLabel, 1);
        row.Children.Add(textLabel);
        var actions = new HorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };

        if (!_db.IsReadOnly && !isArchived)
        {
            if (index > 0)
            {
                var up = ActionButton("\u25B2", "#7B1FA2");
                up.Clicked += async (_, _) => await MoveBulletAsync(bullet, -1);
                actions.Children.Add(up);
            }
            if (index < total - 1)
            {
                var down = ActionButton("\u25BC", "#7B1FA2");
                down.Clicked += async (_, _) => await MoveBulletAsync(bullet, 1);
                actions.Children.Add(down);
            }

            var edit = ActionButton("\u270F\uFE0F", "#1565C0");
            edit.Clicked += async (_, _) =>
            {
                var text = await DisplayPromptAsync("Edit", "Update this point:", "Save", "Cancel", initialValue: bullet.Text, maxLength: 500);
                if (string.IsNullOrWhiteSpace(text)) return;
                bullet.Text = text.Trim();
                var conn = await _db.GetConnectionAsync();
                await conn.UpdateAsync(bullet);
                await LoadBulletsAsync();
            };
            actions.Children.Add(edit);

            var archive = ActionButton("\U0001F4E6", "#888");
            archive.Clicked += async (_, _) =>
            {
                bullet.Status = "archived";
                bullet.ArchivedAt = DateTime.UtcNow;
                var conn = await _db.GetConnectionAsync();
                await conn.UpdateAsync(bullet);
                await LoadBulletsAsync();
            };
            actions.Children.Add(archive);
        }
        else if (!_db.IsReadOnly)
        {
            var restore = ActionButton("\u267B\uFE0F", "#2E7D32");
            restore.Clicked += async (_, _) =>
            {
                bullet.Status = "active";
                bullet.ArchivedAt = null;
                var conn = await _db.GetConnectionAsync();
                var last = await conn.Table<FocusBulletPoint>().Where(b => b.Username == _auth.CurrentUsername && b.Status == "active").OrderByDescending(b => b.SortOrder).FirstOrDefaultAsync();
                bullet.SortOrder = (last?.SortOrder ?? 0) + 1;
                await conn.UpdateAsync(bullet);
                await LoadBulletsAsync();
            };
            actions.Children.Add(restore);

            var delete = ActionButton("\U0001F5D1", "#C62828");
            delete.Clicked += async (_, _) =>
            {
                if (!await DisplayAlert("Delete?", "Permanently delete this point?", "Delete", "Cancel")) return;
                var conn = await _db.GetConnectionAsync();
                await conn.DeleteAsync(bullet);
                await LoadBulletsAsync();
            };
            actions.Children.Add(delete);
        }

        Grid.SetColumn(actions, 2);
        row.Children.Add(actions);
        return new Frame { Padding = 12, CornerRadius = 8, BackgroundColor = isArchived ? Color.FromArgb("#F5F5F5") : Colors.White, BorderColor = Color.FromArgb("#E0E0E0"), HasShadow = false, Content = row };
    }

    private static Button ActionButton(string text, string color) => new()
    {
        Text = text,
        BackgroundColor = Colors.Transparent,
        TextColor = Color.FromArgb(color),
        WidthRequest = 28,
        HeightRequest = 28,
        Padding = 0,
        FontSize = 10
    };

    private async Task AddBulletAsync()
    {
        var text = await DisplayPromptAsync("New Focus Point", "What are you focusing on?", "Add", "Cancel", maxLength: 500);
        if (string.IsNullOrWhiteSpace(text)) return;
        var conn = await _db.GetConnectionAsync();
        var last = await conn.Table<FocusBulletPoint>().Where(b => b.Username == _auth.CurrentUsername && b.Status == "active").OrderByDescending(b => b.SortOrder).FirstOrDefaultAsync();
        await conn.InsertAsync(new FocusBulletPoint { Username = _auth.CurrentUsername, Text = text.Trim(), SortOrder = (last?.SortOrder ?? 0) + 1 });
        await LoadBulletsAsync();
    }

    private async Task MoveBulletAsync(FocusBulletPoint bullet, int direction)
    {
        var conn = await _db.GetConnectionAsync();
        var active = await conn.Table<FocusBulletPoint>().Where(b => b.Username == _auth.CurrentUsername && b.Status == "active").OrderBy(b => b.SortOrder).ToListAsync();
        int index = active.FindIndex(b => b.Id == bullet.Id);
        int newIndex = index + direction;
        if (index < 0 || newIndex < 0 || newIndex >= active.Count) return;
        (active[index].SortOrder, active[newIndex].SortOrder) = (active[newIndex].SortOrder, active[index].SortOrder);
        await conn.UpdateAsync(active[index]);
        await conn.UpdateAsync(active[newIndex]);
        await LoadBulletsAsync();
    }

    private async Task ExportForLlmAsync()
    {
        var conn = await _db.GetConnectionAsync();
        List<FocusBulletPoint> bullets;
        try
        {
            bullets = await conn.Table<FocusBulletPoint>().Where(b => b.Username == _auth.CurrentUsername && b.Status == "active").OrderBy(b => b.SortOrder).ToListAsync();
        }
        catch (SQLite.SQLiteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            bullets = new List<FocusBulletPoint>();
        }
        var challenge = await _challengeService.GetActiveChallengeAsync(_auth.CurrentUsername);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("CURRENT FOCUS CONTEXT:");
        sb.AppendLine("These are my current priorities and focus areas, in order:");
        sb.AppendLine();
        if (bullets.Count > 0)
            for (int i = 0; i < bullets.Count; i++) sb.AppendLine($"{i + 1}. {bullets[i].Text}");
        else sb.AppendLine("(no focus points defined)");
        sb.AppendLine();

        if (challenge != null)
        {
            sb.AppendLine($"WEEKLY FOCUS CATEGORY: {challenge.FocusCategory}");
            sb.AppendLine($"ALLOWANCE: {challenge.CurrentAllowance}/wk");
            sb.AppendLine($"STREAK: {challenge.SuccessStreak} weeks");
            sb.AppendLine();
            var commitments = await _challengeService.GetCurrentWeekCommitmentsAsync(challenge.Id);
            var allTasks = await _tasks.GetActiveTasksAsync(_auth.CurrentUsername);
            var focusTasks = allTasks.Where(t => string.Equals(t.Category, challenge.FocusCategory, StringComparison.OrdinalIgnoreCase)).OrderBy(t => t.Priority).ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList();
            var committedIds = commitments.Select(c => c.TaskId).ToHashSet();
            sb.AppendLine($"FOCUS TASKS ({focusTasks.Count} in {challenge.FocusCategory}):");
            sb.AppendLine();
            foreach (var task in focusTasks)
            {
                string p = task.Priority switch { 1 => "HIGH", 3 => "LOW", _ => "MED" };
                string committed = committedIds.Contains(task.Id) ? " [COMMITTED THIS WEEK]" : "";
                string top = task.IsTopCandidate ? " \u2B50" : "";
                string notes = string.IsNullOrWhiteSpace(task.Notes) ? "" : $" | Notes: {task.Notes.Trim()}";
                sb.AppendLine($"- [{p}]{top}{committed} {task.Title}{notes}");
            }

            var freeTasks = allTasks.Where(t => !string.Equals(t.Category, challenge.FocusCategory, StringComparison.OrdinalIgnoreCase) && t.IsTopCandidate).OrderBy(t => t.Priority).ToList();
            if (freeTasks.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"FREE TASK TOP CANDIDATES ({freeTasks.Count}):");
                foreach (var task in freeTasks)
                {
                    string p = task.Priority switch { 1 => "HIGH", 3 => "LOW", _ => "MED" };
                    sb.AppendLine($"- [{p}] {task.Title} ({task.Category})");
                }
            }
        }
        else sb.AppendLine("No active weekly challenge.");

        sb.AppendLine();
        sb.AppendLine("Based on my focus context and available tasks, help me decide what to prioritize this week.");

        bool attachHistory = await DisplayAlert(
            "Attach History?",
            "Include focus task history in the export?",
            "Yes",
            "No");
        if (attachHistory)
        {
            string? rangeText = await DisplayPromptAsync(
                "History Range",
                "How many days back?",
                "OK",
                "Cancel",
                initialValue: "30",
                maxLength: 4,
                keyboard: Keyboard.Numeric);

            int daysBack = 30;
            if (!string.IsNullOrWhiteSpace(rangeText) &&
                int.TryParse(rangeText, out int parsedDays) &&
                parsedDays > 0)
            {
                daysBack = parsedDays;
            }

            var cutoff = DateTime.UtcNow.AddDays(-daysBack);
            var challenges = await _challengeService.GetChallengeHistoryAsync(
                _auth.CurrentUsername,
                52);
            var recentChallenges = challenges
                .Where(item => item.StartedAt >= cutoff)
                .OrderBy(item => item.StartedAt)
                .ToList();

            if (recentChallenges.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"FOCUS HISTORY (last {daysBack} days, {recentChallenges.Count} weeks):");
                sb.AppendLine();

                var historyTasks = (await _tasks.GetActiveTasksAsync(_auth.CurrentUsername))
                    .Concat(await _tasks.GetCompletedTasksAsync(_auth.CurrentUsername))
                    .GroupBy(item => item.Id)
                    .ToDictionary(group => group.Key, group => group.First());

                foreach (var historyChallenge in recentChallenges)
                {
                    string date = historyChallenge.StartedAt.ToLocalTime().ToString("MMM dd");
                    sb.AppendLine($"Week of {date}: {historyChallenge.FocusCategory} | Allowance: {historyChallenge.CurrentAllowance} | Streak: {historyChallenge.SuccessStreak}");

                    var historyCommitments = await _challengeService.GetCurrentWeekCommitmentsAsync(historyChallenge.Id);
                    foreach (var commitment in historyCommitments)
                    {
                        historyTasks.TryGetValue(commitment.TaskId, out var historyTask);
                        string title = historyTask?.Title ?? $"Task #{commitment.TaskId}";
                        string status = commitment.IsCompleted ? "DONE" : "OPEN";
                        string type = commitment.IsFocusTask ? "Focus" : "Free";
                        sb.AppendLine($"  [{status}] [{type}] {title}");
                    }

                    foreach (var edit in WeeklyChallengeService.ParseManualEdits(historyChallenge.ManualEditsJson))
                    {
                        string reason = string.IsNullOrWhiteSpace(edit.Reason) ? "" : $" {edit.Reason}";
                        sb.AppendLine($"  [MANUAL] {edit.Field}: {edit.OldValue}->{edit.NewValue}{reason}");
                    }

                    sb.AppendLine();
                }
            }
        }

        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert("Exported", $"Copied to clipboard:\n\n{bullets.Count} focus points\n{(challenge != null ? $"Focus tasks from {challenge.FocusCategory}" : "No active challenge")}", "OK");
    }

    private async Task UpdateFromConversationAsync()
    {
        // Step 1: paste conversation
        var conversation = await ShowMultilineEditorAsync(
            "Paste Conversation",
            "Paste your LLM conversation transcript here:");
        if (string.IsNullOrWhiteSpace(conversation)) return;

        // Step 2: build and copy the update prompt
        var prompt = BuildUpdatePrompt(conversation);
        await Clipboard.SetTextAsync(prompt);

        await DisplayAlert("Prompt Copied",
            "The update analysis prompt has been copied to clipboard.\n\n" +
            "Paste it into your LLM, then come back and tap " +
            "'Paste LLM Update Response'.",
            "OK");

        // Step 3: paste LLM response
        var response = await ShowMultilineEditorAsync(
            "Paste LLM Update Response",
            "Paste the LLM's structured update response here:");
        if (string.IsNullOrWhiteSpace(response)) return;

        // Step 4: parse response
        var parsed = ParseUpdateResponse(response.Trim());
        if (parsed.AddPoints.Count == 0 &&
            parsed.MovePoints.Count == 0 &&
            parsed.ArchivePoints.Count == 0)
        {
            await DisplayAlert("Parse Failed",
                "Could not find any focusUpdate entries in the response. " +
                "Make sure the LLM returned the exact format requested.",
                "OK");
            return;
        }

        // Step 5: show review UI
        await ShowUpdateReviewAsync(parsed);
    }

    private string BuildUpdatePrompt(string conversation)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("You are analyzing a conversation to suggest updates " +
            "to a prioritized focus context list. The list represents the " +
            "user's current strategic context, priorities, and constraints " +
            "in order of importance.");
        sb.AppendLine();
        sb.AppendLine("CURRENT FOCUS CONTEXT POINTS (in priority order):");
        foreach (var p in _points)
            sb.AppendLine($"[ID:{p.Id}] Position {p.SortOrder}: {p.Text}");
        sb.AppendLine();
        sb.AppendLine("CONVERSATION TO ANALYZE:");
        sb.AppendLine(conversation);
        sb.AppendLine();
        sb.AppendLine("Based on the conversation, suggest:");
        sb.AppendLine("1. New points to add (with suggested insertion position)");
        sb.AppendLine("2. Existing points to move to a different position " +
            "(reprioritize)");
        sb.AppendLine("3. Existing points to archive (no longer relevant)");
        sb.AppendLine();
        sb.AppendLine("Return ONLY C#-parseable output in exactly this format. " +
            "Number each suggestion starting from 1. " +
            "Omit any section that has no suggestions:");
        sb.AppendLine();
        sb.AppendLine("// New points to add:");
        sb.AppendLine("focusUpdate.AddPoint[1].Position = <position_number>;");
        sb.AppendLine("focusUpdate.AddPoint[1].Text = \"<point_text>\";");
        sb.AppendLine();
        sb.AppendLine("// Points to move (reprioritize):");
        sb.AppendLine("focusUpdate.MovePoint[1].Id = <existing_point_id>;");
        sb.AppendLine("focusUpdate.MovePoint[1].NewPosition = <new_position>;");
        sb.AppendLine("focusUpdate.MovePoint[1].Reason = \"<brief reason>\";");
        sb.AppendLine();
        sb.AppendLine("// Points to archive:");
        sb.AppendLine("focusUpdate.ArchivePoint[1].Id = <existing_point_id>;");
        sb.AppendLine("focusUpdate.ArchivePoint[1].Reason = \"<brief reason>\";");
        return sb.ToString();
    }

    private record AddPointSuggestion(int Position, string Text);
    private record MovePointSuggestion(int Id, int NewPosition, string Reason);
    private record ArchivePointSuggestion(int Id, string Reason);
    private record UpdateSuggestions(
        List<AddPointSuggestion> AddPoints,
        List<MovePointSuggestion> MovePoints,
        List<ArchivePointSuggestion> ArchivePoints);

    private static UpdateSuggestions ParseUpdateResponse(string response)
    {
        var addPoints = new List<AddPointSuggestion>();
        var movePoints = new List<MovePointSuggestion>();
        var archivePoints = new List<ArchivePointSuggestion>();

        // Parse AddPoints
        int idx = 1;
        while (true)
        {
            var posKey = $"focusUpdate.AddPoint[{idx}].Position";
            var textKey = $"focusUpdate.AddPoint[{idx}].Text";
            var posLine = FindValue(response, posKey);
            var textLine = FindQuotedValue(response, textKey);
            if (posLine == null && textLine == null) break;
            if (int.TryParse(posLine?.Trim(), out int pos) &&
                textLine != null)
                addPoints.Add(new AddPointSuggestion(pos, textLine));
            idx++;
        }

        // Parse MovePoints
        idx = 1;
        while (true)
        {
            var idKey = $"focusUpdate.MovePoint[{idx}].Id";
            var posKey = $"focusUpdate.MovePoint[{idx}].NewPosition";
            var reasonKey = $"focusUpdate.MovePoint[{idx}].Reason";
            var idVal = FindValue(response, idKey);
            var posVal = FindValue(response, posKey);
            var reasonVal = FindQuotedValue(response, reasonKey);
            if (idVal == null && posVal == null) break;
            if (int.TryParse(idVal?.Trim(), out int id) &&
                int.TryParse(posVal?.Trim(), out int newPos))
                movePoints.Add(new MovePointSuggestion(
                    id, newPos, reasonVal ?? ""));
            idx++;
        }

        // Parse ArchivePoints
        idx = 1;
        while (true)
        {
            var idKey = $"focusUpdate.ArchivePoint[{idx}].Id";
            var reasonKey = $"focusUpdate.ArchivePoint[{idx}].Reason";
            var idVal = FindValue(response, idKey);
            var reasonVal = FindQuotedValue(response, reasonKey);
            if (idVal == null) break;
            if (int.TryParse(idVal?.Trim(), out int id))
                archivePoints.Add(new ArchivePointSuggestion(
                    id, reasonVal ?? ""));
            idx++;
        }

        return new UpdateSuggestions(addPoints, movePoints, archivePoints);
    }

    private static string? FindValue(string response, string key)
    {
        var idx = response.IndexOf(key,
            StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var eqIdx = response.IndexOf('=', idx);
        if (eqIdx < 0) return null;
        var semi = response.IndexOf(';', eqIdx);
        return semi >= 0
            ? response[(eqIdx + 1)..semi].Trim()
            : response[(eqIdx + 1)..].Trim();
    }

    private static string? FindQuotedValue(string response, string key)
    {
        var idx = response.IndexOf(key,
            StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var eqIdx = response.IndexOf('=', idx);
        if (eqIdx < 0) return null;
        var rest = response[(eqIdx + 1)..].TrimStart();
        if (!rest.StartsWith('"')) return null;
        int end = 1;
        while (end < rest.Length)
        {
            if (rest[end] == '"' && rest[end - 1] != '\\') break;
            end++;
        }
        return end < rest.Length
            ? rest[1..end].Replace("\\\"", "\"").Trim()
            : null;
    }

    private async Task ShowUpdateReviewAsync(UpdateSuggestions suggestions)
    {
        int accepted = 0;
        int skipped = 0;

        // Review Add suggestions
        foreach (var add in suggestions.AddPoints)
        {
            bool accept = await DisplayAlert(
                "Add Point?",
                $"Insert at position {add.Position}:\n\n\"{add.Text}\"",
                "Accept", "Skip");
            if (accept)
            {
                // Insert at suggested position by adjusting SortOrder
                var maxSort = _points.Count > 0
                    ? _points.Max(p => p.SortOrder)
                    : 0;
                var newPoint = await _service.AddPointAsync(
                    _auth.CurrentUsername, add.Text, maxSort + 1);
                // Move to correct position
                await _service.MoveToPositionAsync(
                    _auth.CurrentUsername, newPoint.Id, add.Position);
                accepted++;
            }
            else skipped++;
        }

        // Review Move suggestions
        foreach (var move in suggestions.MovePoints)
        {
            var point = _points.FirstOrDefault(p => p.Id == move.Id);
            if (point == null) continue;
            bool accept = await DisplayAlert(
                "Move Point?",
                $"Move from position {point.SortOrder} → {move.NewPosition}:\n\n" +
                $"\"{point.Text}\"\n\nReason: {move.Reason}",
                "Accept", "Skip");
            if (accept)
            {
                await _service.MoveToPositionAsync(
                    _auth.CurrentUsername, move.Id, move.NewPosition);
                accepted++;
            }
            else skipped++;
        }

        // Review Archive suggestions
        foreach (var archive in suggestions.ArchivePoints)
        {
            var point = _points.FirstOrDefault(p => p.Id == archive.Id);
            if (point == null) continue;
            bool accept = await DisplayAlert(
                "Archive Point?",
                $"Archive:\n\n\"{point.Text}\"\n\nReason: {archive.Reason}",
                "Accept", "Skip");
            if (accept)
            {
                await _service.ArchivePointAsync(archive.Id);
                accepted++;
            }
            else skipped++;
        }

        // Refresh the list
        await LoadBulletsAsync();

        await DisplayAlert("Update Complete",
            $"{accepted} change{(accepted == 1 ? "" : "s")} accepted, " +
            $"{skipped} skipped.",
            "OK");
    }

    private async Task<string> ShowMultilineEditorAsync(
        string title, string message)
    {
        var tcs = new TaskCompletionSource<string>();
        var editorPage = new ContentPage
        {
            Title = title,
            BackgroundColor = Color.FromArgb("#F5F5F5")
        };
        var editor = new Editor
        {
            Placeholder = "Paste here...",
            HeightRequest = 300,
            AutoSize = EditorAutoSizeOption.TextChanges,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            FontSize = 13,
            Margin = new Thickness(16)
        };
        var confirmBtn = new Button
        {
            Text = "Done",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Margin = new Thickness(16, 0)
        };
        confirmBtn.Clicked += async (_, _) =>
        {
            tcs.TrySetResult(editor.Text ?? "");
            await Navigation.PopAsync();
        };
        editorPage.Content = new VerticalStackLayout
        {
            Spacing = 12,
            Padding = 8,
            Children =
            {
                new Label
                {
                    Text = message,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#444"),
                    Margin = new Thickness(16, 16, 16, 0),
                    LineBreakMode = LineBreakMode.WordWrap
                },
                editor,
                confirmBtn
            }
        };
        await Navigation.PushAsync(editorPage);
        return await tcs.Task;
    }
}
