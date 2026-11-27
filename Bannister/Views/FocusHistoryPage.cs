using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class FocusHistoryPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly WeeklyChallengeService _challengeService;
    private readonly TaskService _tasks;
    private VerticalStackLayout _historyContainer = null!;

    public FocusHistoryPage(
        AuthService auth,
        WeeklyChallengeService challengeService,
        TaskService tasks)
    {
        _auth = auth;
        _challengeService = challengeService;
        _tasks = tasks;
        Title = "Focus History";

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadHistoryAsync();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout { Padding = 20, Spacing = 12 };
        mainStack.Children.Add((new Label
        {
            Text = "\U0001F4C8 Focus History",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText1, classifyPageText: true));
        mainStack.Children.Add((new Label
        {
            Text = "Chronological record of focus tasks, completions, streak changes, and allowance changes.",
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        _historyContainer = new VerticalStackLayout { Spacing = 8 };
        mainStack.Children.Add(_historyContainer);
        Content = new ScrollView { Content = mainStack };
    }

    private async Task LoadHistoryAsync()
    {
        _historyContainer.Children.Clear();
        var challenges = await _challengeService.GetChallengeHistoryAsync(
            _auth.CurrentUsername,
            52);

        if (challenges.Count == 0)
        {
            _historyContainer.Children.Add((new Label
            {
                Text = "No focus challenge history yet.",
                FontSize = 13,
                FontAttributes = FontAttributes.Italic
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
            return;
        }

        var activeTasks = await _tasks.GetActiveTasksAsync(_auth.CurrentUsername);
        var completedTasks = await _tasks.GetCompletedTasksAsync(_auth.CurrentUsername);
        var taskLookup = activeTasks
            .Concat(completedTasks)
            .GroupBy(task => task.Id)
            .ToDictionary(group => group.Key, group => group.First());

        int previousAllowance = 0;
        int previousStreak = 0;

        foreach (var challenge in challenges.OrderBy(challenge => challenge.StartedAt))
        {
            var commitments = await _challengeService.GetCurrentWeekCommitmentsAsync(challenge.Id);
            var weekFrame = (new Frame
            {
                Padding = 14,
                CornerRadius = 10,
                HasShadow = false
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);
            var weekStack = new VerticalStackLayout { Spacing = 6 };
            string weekDate = challenge.StartedAt.ToLocalTime().ToString("MMM dd, yyyy");

            string allowanceChange = "";
            if (previousAllowance > 0 && challenge.CurrentAllowance != previousAllowance)
            {
                string icon = challenge.CurrentAllowance > previousAllowance ? "\u2B06\uFE0F" : "\u2B07\uFE0F";
                allowanceChange = $"{icon} Allowance {previousAllowance}\u2192{challenge.CurrentAllowance}";
            }

            string streakChange = "";
            if (previousStreak > 0 && challenge.SuccessStreak != previousStreak)
            {
                string icon = challenge.SuccessStreak > previousStreak ? "\U0001F525" : "\u274C";
                streakChange = $"{icon} Streak {previousStreak}\u2192{challenge.SuccessStreak}";
            }

            ThemeKey headerColor = challenge.IsActive ? ThemeKey.SuccessText : challenge.SuccessStreak > previousStreak ? ThemeKey.SuccessText : ThemeKey.CardText1;

            weekStack.Children.Add((new Label
            {
                Text = $"\U0001F4C5 {weekDate} \u2022 {challenge.FocusCategory} \u2022 Allowance: {challenge.CurrentAllowance} \u2022 Streak: {challenge.SuccessStreak}",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                LineBreakMode = LineBreakMode.WordWrap
            }).ThemeColor(Label.TextColorProperty, headerColor, ThemeKey.PageText1, classifyPageText: true));

            var changes = new[] { allowanceChange, streakChange }
                .Where(change => !string.IsNullOrEmpty(change));
            string changesText = string.Join("  ", changes);
            if (!string.IsNullOrEmpty(changesText))
            {
                weekStack.Children.Add((new Label
                {
                    Text = changesText,
                    FontSize = 11
                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
            }

            if (challenge.IsActive)
            {
                weekStack.Children.Add((new Label
                {
                    Text = "\U0001F7E2 Current Week",
                    FontSize = 11,
                    FontAttributes = FontAttributes.Bold
                }).ThemeColor(Label.TextColorProperty, ThemeKey.SuccessText, ThemeKey.PageText3, classifyPageText: true));
            }

            if (commitments.Count > 0)
            {
                foreach (var commitment in commitments.OrderBy(commitment => commitment.Id))
                {
                    taskLookup.TryGetValue(commitment.TaskId, out var task);
                    string taskTitle = task?.Title ?? $"Task #{commitment.TaskId}";
                    string statusIcon = commitment.IsCompleted ? "\u2705" : "\u26AA";
                    string focusLabel = commitment.IsFocusTask ? "Focus" : "Free";
                    string completedDate = commitment.IsCompleted && commitment.CompletedAt.HasValue
                        ? $" \u2022 Done {commitment.CompletedAt.Value.ToLocalTime():M/d}"
                        : "";

                    weekStack.Children.Add((new Label
                    {
                        Text = $"  {statusIcon} [{focusLabel}] {taskTitle}{completedDate}",
                        FontSize = 12,
                        LineBreakMode = LineBreakMode.WordWrap
                    }).ThemeColor(Label.TextColorProperty, commitment.IsCompleted ? ThemeKey.SuccessText : ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
                }

                int focusDone = commitments.Count(commitment => commitment.IsFocusTask && commitment.IsCompleted);
                int focusTotal = commitments.Count(commitment => commitment.IsFocusTask);
                int freeDone = commitments.Count(commitment => !commitment.IsFocusTask && commitment.IsCompleted);
                int freeTotal = commitments.Count(commitment => !commitment.IsFocusTask);
                weekStack.Children.Add((new Label
                {
                    Text = $"  Focus: {focusDone}/{focusTotal} \u2022 Free: {freeDone}/{freeTotal}",
                    FontSize = 10
                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
            }
            else
            {
                weekStack.Children.Add((new Label
                {
                    Text = "  No tasks committed this week",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Italic
                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
            }

            var manualEdits = WeeklyChallengeService.ParseManualEdits(challenge.ManualEditsJson);
            if (manualEdits.Count > 0)
            {
                weekStack.Children.Add((new Label
                {
                    Text = "\u270F Manual Edits:",
                    FontSize = 10,
                    FontAttributes = FontAttributes.Bold
                }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText3, classifyPageText: true));

                foreach (var edit in manualEdits)
                {
                    string editDate = DateTime.TryParse(edit.Date, out var parsedDate)
                        ? parsedDate.ToLocalTime().ToString("M/d HH:mm")
                        : "";
                    string reason = string.IsNullOrWhiteSpace(edit.Reason)
                        ? ""
                        : $" — {edit.Reason}";
                    weekStack.Children.Add((new Label
                    {
                        Text = $"    {editDate}: {edit.Field} {edit.OldValue}\u2192{edit.NewValue}{reason}",
                        FontSize = 10
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText3, classifyPageText: true));
                }
            }

            previousAllowance = challenge.CurrentAllowance;
            previousStreak = challenge.SuccessStreak;
            weekFrame.Content = weekStack;
            _historyContainer.Children.Add(weekFrame);
        }
    }
}
