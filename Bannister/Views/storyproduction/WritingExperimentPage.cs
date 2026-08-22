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
    private VerticalStackLayout _historySection = null!;
    private VerticalStackLayout _comparisonSection = null!;

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
        _historySection = new VerticalStackLayout { Spacing = 6 };
        _mainContent.Children.Add(_phaseLabel);
        _mainContent.Children.Add(_statusLabel);
        _mainContent.Children.Add(_todaySection);
        _mainContent.Children.Add(_comparisonSection);
        _mainContent.Children.Add(_historySection);
        Content = new ScrollView { Content = _mainContent };
    }

    private async Task RefreshAsync()
    {
        var experiment = await _experimentService.GetActiveExperimentAsync(_auth.CurrentUsername);
        _todaySection.Children.Clear();
        _historySection.Children.Clear();
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

        await ShowTodayAsync(experiment, entries);
        if (experiment.Phase == "challenger" && baselineCount > 0 && challengerCount > 0)
            await ShowComparisonAsync(experiment);

        if (experiment.Phase == "baseline" && baselineCount >= 7)
        {
            var button = new Button { Text = "⚔️ Start Challenger Phase", BackgroundColor = Color.FromArgb("#6A1B9A"), TextColor = Colors.White, CornerRadius = 8, HeightRequest = 44, FontSize = 14, FontAttributes = FontAttributes.Bold };
            button.Clicked += async (_, _) => await StartChallengerAsync(experiment);
            _todaySection.Children.Add(button);
        }

        ShowHistory(entries);
        var archiveButton = new Button { Text = "Archive Experiment", BackgroundColor = Color.FromArgb("#E0E0E0"), TextColor = Color.FromArgb("#666"), CornerRadius = 8, HeightRequest = 36, FontSize = 12 };
        archiveButton.Clicked += async (_, _) =>
        {
            if (!await DisplayAlert("Archive?", "Archive this experiment and start fresh?", "Archive", "Cancel")) return;
            experiment.Status = "archived";
            experiment.CompletedAt = DateTime.UtcNow;
            await _experimentService.UpdateExperimentAsync(experiment);
            await RefreshAsync();
        };
        _historySection.Children.Add(archiveButton);
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

    private async Task ShowTodayAsync(WritingExperiment experiment, List<WritingExperimentEntry> entries)
    {
        var entry = await _experimentService.GetTodayEntryAsync(experiment.Id);
        if (entry == null)
        {
            bool baseline = experiment.Phase == "baseline" || entries.Count % 2 == 0;
            string process = baseline ? experiment.BaselineProcessName : experiment.ChallengerProcessName;
            entry = await _experimentService.CreateTodayEntryAsync(experiment.Id, process, baseline);
        }

        var stack = new VerticalStackLayout { Spacing = 8 };
        stack.Children.Add(new Label { Text = $"Today's Assignment — {DateTime.Today:ddd MMM dd}", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") });
        stack.Children.Add(new Label { Text = $"\U0001F4DD Process: {entry.AssignedProcess}", FontSize = 14, TextColor = entry.IsBaseline ? Color.FromArgb("#3F51B5") : Color.FromArgb("#E65100"), FontAttributes = FontAttributes.Bold });
        stack.Children.Add(new Label { Text = entry.IsBaseline ? "\U0001F4CA Baseline" : "⚔️ Challenger", FontSize = 12, TextColor = Color.FromArgb("#888") });

        if (entry.IsCompleted)
        {
            stack.Children.Add(new Label { Text = $"✅ Completed — Retention: {entry.RetentionScore}/10", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#2E7D32") });
            if (!string.IsNullOrWhiteSpace(entry.ExecutionNotes)) stack.Children.Add(new Label { Text = $"Execution: {entry.ExecutionNotes}", FontSize = 11, TextColor = Color.FromArgb("#666"), FontAttributes = FontAttributes.Italic });
            if (!string.IsNullOrWhiteSpace(entry.RetentionNotes)) stack.Children.Add(new Label { Text = $"Retention: {entry.RetentionNotes}", FontSize = 11, TextColor = Color.FromArgb("#666"), FontAttributes = FontAttributes.Italic });
        }
        else
        {
            var button = new Button { Text = "\U0001F4DD Record Today's Results", BackgroundColor = Color.FromArgb("#6A1B9A"), TextColor = Colors.White, CornerRadius = 8, HeightRequest = 44, FontSize = 14, FontAttributes = FontAttributes.Bold };
            var captured = entry;
            button.Clicked += async (_, _) => await RecordResultsAsync(captured);
            stack.Children.Add(button);
        }

        _todaySection.Children.Add(new Frame { Padding = 16, CornerRadius = 12, BackgroundColor = entry.IsBaseline ? Color.FromArgb("#E8EAF6") : Color.FromArgb("#FFF3E0"), BorderColor = entry.IsBaseline ? Color.FromArgb("#5C6BC0") : Color.FromArgb("#FF9800"), HasShadow = false, Content = stack });
    }

    private async Task RecordResultsAsync(WritingExperimentEntry entry)
    {
        string? execution = await DisplayPromptAsync("Execution Notes", "How exactly did you follow the process today? What did you do step by step?", "Next", "Cancel", maxLength: 1000);
        if (execution == null) return;
        string? value = await DisplayPromptAsync("Retention Score", "Rate how well the story/ideas stuck with you (1 = nothing retained, 10 = vivid recall):", "Next", "Cancel", initialValue: "5", maxLength: 2, keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(value) || !int.TryParse(value, out int score)) return;
        string? notes = await DisplayPromptAsync("Retention Notes (optional)", "What do you remember? What stuck? What faded?", "Save", "Skip", maxLength: 1000);
        await _experimentService.CompleteEntryAsync(entry.Id, execution.Trim(), Math.Clamp(score, 1, 10), notes?.Trim() ?? "");
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
        AddComparisonCell(grid, $"{baselineAvg:F1}/10", 0, 1, 24, "#3F51B5", true);
        AddComparisonCell(grid, $"{challengerAvg:F1}/10", 1, 1, 24, "#E65100", true);
        AddComparisonCell(grid, $"{baselineCount} entries", 0, 2, 11, "#888", false);
        AddComparisonCell(grid, $"{challengerCount} entries", 1, 2, 11, "#888", false);
        _comparisonSection.Children.Add(new Frame { Padding = 14, CornerRadius = 10, BackgroundColor = Colors.White, BorderColor = Color.FromArgb("#E0E0E0"), HasShadow = false, Content = grid });

        if (baselineCount >= 7 && challengerCount >= 7)
        {
            string recommendation = baselineAvg > challengerAvg + .5 ? $"\U0001F3C6 Baseline ({experiment.BaselineProcessName}) wins by {baselineAvg - challengerAvg:F1} points. Consider keeping it as your default."
                : challengerAvg > baselineAvg + .5 ? $"\U0001F3C6 Challenger ({experiment.ChallengerProcessName}) wins by {challengerAvg - baselineAvg:F1} points. Consider switching your default."
                : "\U0001F91D Too close to call. Consider running more days or testing a different challenger.";
            _comparisonSection.Children.Add(new Label { Text = recommendation, FontSize = 13, TextColor = Color.FromArgb("#6A1B9A"), FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.WordWrap });
        }
    }

    private static void AddComparisonCell(Grid grid, string text, int column, int row, double size, string color, bool bold)
    {
        var label = new Label { Text = text, FontSize = size, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None, TextColor = Color.FromArgb(color), HorizontalTextAlignment = TextAlignment.Center };
        Grid.SetColumn(label, column); Grid.SetRow(label, row); grid.Children.Add(label);
    }

    private void ShowHistory(List<WritingExperimentEntry> entries)
    {
        if (entries.Count == 0) return;
        _historySection.Children.Add(new Label { Text = $"\U0001F4C5 History ({entries.Count} entries)", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") });
        foreach (var entry in entries.OrderByDescending(e => e.Date).Take(14))
        {
            string preview = string.IsNullOrWhiteSpace(entry.ExecutionNotes) ? "" : entry.ExecutionNotes.Length > 40 ? entry.ExecutionNotes[..40] + "..." : entry.ExecutionNotes;
            string score = entry.IsCompleted ? $"{entry.RetentionScore}/10" : "Not completed";
            var text = $"{(entry.IsBaseline ? "\U0001F4CA" : "⚔️")} {entry.Date:ddd M/d}   {entry.AssignedProcess}   {score}" + (preview.Length > 0 ? $"\n{preview}" : "");
            _historySection.Children.Add(new Frame { Padding = 10, CornerRadius = 8, BackgroundColor = !entry.IsCompleted ? Color.FromArgb("#F5F5F5") : entry.IsBaseline ? Color.FromArgb("#E8EAF6") : Color.FromArgb("#FFF3E0"), BorderColor = Colors.Transparent, HasShadow = false, Content = new Label { Text = text, FontSize = 11, TextColor = Color.FromArgb("#555"), LineBreakMode = LineBreakMode.WordWrap } });
        }
    }
}
