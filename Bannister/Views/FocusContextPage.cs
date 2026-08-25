using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class FocusContextPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly TaskService _tasks;
    private readonly WeeklyChallengeService _challengeService;
    private readonly DatabaseService _db;

    private VerticalStackLayout _bulletsList = null!;
    private VerticalStackLayout _archivedList = null!;
    private bool _showArchived;
    private bool _initialized;

    public FocusContextPage(AuthService auth, TaskService tasks, WeeklyChallengeService challengeService, DatabaseService db)
    {
        _auth = auth;
        _tasks = tasks;
        _challengeService = challengeService;
        _db = db;
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
        var addBtn = new Button { Text = "+ Add Point", BackgroundColor = Color.FromArgb("#7B1FA2"), TextColor = Colors.White, CornerRadius = 8, HeightRequest = 40, FontSize = 13, Padding = new Thickness(14, 0) };
        addBtn.Clicked += async (_, _) => await AddBulletAsync();
        btnRow.Children.Add(addBtn);
        var exportBtn = new Button { Text = "\U0001F4CB Export for LLM", BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White, CornerRadius = 8, HeightRequest = 40, FontSize = 13, Padding = new Thickness(14, 0) };
        exportBtn.Clicked += async (_, _) => await ExportForLlmAsync();
        btnRow.Children.Add(exportBtn);
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
        var conn = await _db.GetConnectionAsync();
        var active = await conn.Table<FocusBulletPoint>()
            .Where(b => b.Username == _auth.CurrentUsername && b.Status == "active")
            .OrderBy(b => b.SortOrder).ToListAsync();

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

        if (!isArchived)
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
        else
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
        var bullets = await conn.Table<FocusBulletPoint>().Where(b => b.Username == _auth.CurrentUsername && b.Status == "active").OrderBy(b => b.SortOrder).ToListAsync();
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
        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert("Exported", $"Copied to clipboard:\n\n{bullets.Count} focus points\n{(challenge != null ? $"Focus tasks from {challenge.FocusCategory}" : "No active challenge")}", "OK");
    }
}
