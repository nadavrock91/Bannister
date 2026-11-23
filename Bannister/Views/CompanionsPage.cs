using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class CompanionsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly CompanionService _service;
    private readonly GameService _games;
    private readonly VerticalStackLayout _content = new() { Padding = 16, Spacing = 10 };
    private Companion? _selected;
    private CompanionInterview? _selectedInterview;
    private bool _showArchived;

    public CompanionsPage(AuthService auth, CompanionService service, GameService games)
    {
        _auth = auth; _service = service; _games = games;
        Title = "Companions";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        Content = new ScrollView { Content = _content };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _service.EnsureMetaAsync(_auth.CurrentUsername);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _content.Children.Clear();
        if (_selected == null) await BuildListAsync(); else await BuildDetailAsync();
    }

    private async Task BuildListAsync()
    {
        _content.Children.Add(Heading("Companions", 24));
        _content.Children.Add(new Label { Text = "LLM companions that learn durable context about you through interviews.", FontSize = 13, TextColor = Color.FromArgb("#666") });
        var add = Button("+ Add Companion", "#5B63EE", Colors.White);
        add.Clicked += async (_, _) =>
        {
            var games = await _games.GetGamesAsync(_auth.CurrentUsername);
            var item = await CompanionEditPage.ShowAsync(Navigation, null, games, _auth.CurrentUsername);
            if (item == null) return;
            item.SortOrder = (await _service.GetCompanionsAsync(_auth.CurrentUsername)).Count;
            await _service.SaveCompanionAsync(item); await RefreshAsync();
        };
        _content.Children.Add(add);
        var companions = await _service.GetCompanionsAsync(_auth.CurrentUsername);
        var gamesById = (await _games.GetGamesAsync(_auth.CurrentUsername)).ToDictionary(x => x.GameId, StringComparer.OrdinalIgnoreCase);
        foreach (var companion in companions)
        {
            var points = await _service.GetPointsAsync(_auth.CurrentUsername, companion.Id);
            string scope = ScopeLabel(companion, gamesById);
            var stack = new VerticalStackLayout { Spacing = 4 };
            stack.Children.Add(Heading(companion.Nickname, 17));
            stack.Children.Add(new Label { Text = $"{scope} · {points.Count} active data point{(points.Count == 1 ? "" : "s")}", FontSize = 12, TextColor = Color.FromArgb("#666") });
            var actions = new HorizontalStackLayout { Spacing = 6 };
            var open = Button("Open", "#E3F2FD", Color.FromArgb("#1565C0"));
            open.Clicked += async (_, _) => { _selected = companion; _selectedInterview = null; await RefreshAsync(); };
            var edit = Button("Edit", "#FFF3E0", Color.FromArgb("#E65100"));
            edit.Clicked += async (_, _) =>
            {
                var edited = await CompanionEditPage.ShowAsync(Navigation, companion, await _games.GetGamesAsync(_auth.CurrentUsername), _auth.CurrentUsername);
                if (edited == null) return; await _service.SaveCompanionAsync(edited); await RefreshAsync();
            };
            actions.Children.Add(open); actions.Children.Add(edit);
            if (companion.Scope != "meta")
            {
                var delete = Button("Delete", "#FFEBEE", Color.FromArgb("#C62828"));
                delete.Clicked += async (_, _) =>
                {
                    if (!await DisplayAlert("Delete Companion", $"Delete {companion.Nickname} and all of its data points and interviews?", "Delete", "Cancel")) return;
                    await _service.DeleteCompanionAsync(companion); await RefreshAsync();
                };
                actions.Children.Add(delete);
            }
            stack.Children.Add(actions); _content.Children.Add(Card(stack));
        }
    }

    private async Task BuildDetailAsync()
    {
        var companion = _selected!;
        var back = Button("← All Companions", "#ECEFF1", Color.FromArgb("#37474F"));
        back.Clicked += async (_, _) => { _selected = null; _selectedInterview = null; await RefreshAsync(); };
        _content.Children.Add(back);
        _content.Children.Add(Heading(companion.Nickname, 24));
        var gamesById = (await _games.GetGamesAsync(_auth.CurrentUsername)).ToDictionary(x => x.GameId, StringComparer.OrdinalIgnoreCase);
        string scopeLabel = ScopeLabel(companion, gamesById);
        _content.Children.Add(new Label { Text = scopeLabel, FontSize = 13, TextColor = Color.FromArgb("#666") });

        var interviewActions = new HorizontalStackLayout { Spacing = 8 };
        var start = Button("Start Interview", "#5B63EE", Colors.White);
        start.Clicked += async (_, _) => await StartInterviewAsync(companion, gamesById);
        interviewActions.Children.Add(start);
        if (_selectedInterview != null)
        {
            var resume = Button(InterviewStepText(_selectedInterview), "#E8EAF6", Color.FromArgb("#3949AB"));
            resume.Clicked += async (_, _) => await ContinueInterviewAsync(companion, _selectedInterview, scopeLabel);
            interviewActions.Children.Add(resume);
            if (!string.IsNullOrWhiteSpace(_selectedInterview.Transcript))
            {
                var replace = Button("Paste Conversation Again", "#FFF3E0", Color.FromArgb("#E65100"));
                replace.Clicked += async (_, _) => await SaveConversationAsync(companion, _selectedInterview, scopeLabel);
                interviewActions.Children.Add(replace);
            }
        }
        _content.Children.Add(interviewActions);
        if (_selectedInterview != null)
            _content.Children.Add(new Label { Text = $"Current interview: {InterviewStepText(_selectedInterview)}", FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#5B63EE") });

        _content.Children.Add(Heading(_showArchived ? "Data Points (including archived)" : "Active Data Points", 18));
        var pointActions = new HorizontalStackLayout { Spacing = 8 };
        var addPoint = Button("+ Add Data Point", "#2E7D32", Colors.White);
        addPoint.Clicked += async (_, _) => await AddPointAsync(companion, scopeLabel);
        var archived = Button(_showArchived ? "Hide Archived" : "Show Archived", "#ECEFF1", Color.FromArgb("#555"));
        archived.Clicked += async (_, _) => { _showArchived = !_showArchived; await RefreshAsync(); };
        pointActions.Children.Add(addPoint); pointActions.Children.Add(archived); _content.Children.Add(pointActions);
        var points = await _service.GetPointsAsync(_auth.CurrentUsername, companion.Id, _showArchived);
        if (points.Count == 0) _content.Children.Add(new Label { Text = "No data points yet.", TextColor = Color.FromArgb("#999") });
        foreach (var point in points) _content.Children.Add(BuildPointCard(point));

        _content.Children.Add(Heading("Interview History", 18));
        var interviews = await _service.GetInterviewsAsync(_auth.CurrentUsername, companion.Id);
        if (interviews.Count == 0) _content.Children.Add(new Label { Text = "No interviews yet.", TextColor = Color.FromArgb("#999") });
        foreach (var interview in interviews)
        {
            var row = new VerticalStackLayout { Spacing = 4 };
            row.Children.Add(new Label { Text = $"{interview.StartedAt.ToLocalTime():dd MMM yyyy HH:mm} · {interview.QuestionCount} questions", FontAttributes = FontAttributes.Bold });
            row.Children.Add(new Label { Text = $"Added {interview.PointsAdded} · Reprioritized {interview.PointsReprioritized} · Archived {interview.PointsArchived}", FontSize = 12, TextColor = Color.FromArgb("#666") });
            row.Children.Add(new Label { Text = InterviewStepText(interview), FontSize = 12, TextColor = Color.FromArgb("#5B63EE") });
            var resume = Button("Open / Resume", "#E8EAF6", Color.FromArgb("#3949AB"));
            resume.HorizontalOptions = LayoutOptions.Start;
            resume.Clicked += async (_, _) => { _selectedInterview = interview; await RefreshAsync(); };
            var delete = Button("Delete", "#FFEBEE", Color.FromArgb("#C62828"));
            delete.HorizontalOptions = LayoutOptions.Start;
            delete.Clicked += async (_, _) =>
            {
                string? choice = await DisplayActionSheet(
                    "Delete Interview",
                    "Cancel",
                    null,
                    "Delete interview only",
                    "Delete interview and its data points");
                if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel") return;

                bool deletePoints = choice == "Delete interview and its data points";
                await _service.DeleteInterviewAsync(interview.Id, deletePoints);
                if (_selectedInterview?.Id == interview.Id) _selectedInterview = null;
                await RefreshAsync();
            };
            row.Children.Add(new HorizontalStackLayout { Spacing = 6, Children = { resume, delete } });
            _content.Children.Add(Card(row));
        }
    }

    private View BuildPointCard(CompanionDataPoint point)
    {
        var stack = new VerticalStackLayout { Spacing = 5 };
        stack.Children.Add(new Label { Text = point.Text, FontSize = 14, TextColor = point.Status == "archived" ? Color.FromArgb("#888") : Color.FromArgb("#222") });
        stack.Children.Add(new Label { Text = $"Priority {point.Priority} · {point.Source} · {point.Status} · {point.UpdatedAt.ToLocalTime():dd MMM yyyy}", FontSize = 11, TextColor = Color.FromArgb("#777") });
        var actions = new HorizontalStackLayout { Spacing = 5 };
        var edit = Button("Edit", "#FFF3E0", Color.FromArgb("#E65100"));
        edit.Clicked += async (_, _) => { var text = await DisplayPromptAsync("Edit Data Point", "Text", "Save", "Cancel", initialValue: point.Text); if (string.IsNullOrWhiteSpace(text)) return; point.Text = text.Trim(); await _service.SavePointAsync(point); await RefreshAsync(); };
        var priority = Button("Priority", "#E3F2FD", Color.FromArgb("#1565C0"));
        priority.Clicked += async (_, _) => { var value = await DisplayPromptAsync("Priority", "1-5 (5 is most important)", "Save", "Cancel", keyboard: Keyboard.Numeric, initialValue: point.Priority.ToString()); if (!int.TryParse(value, out int p) || p is < 1 or > 5) return; point.Priority = p; await _service.SavePointAsync(point); await RefreshAsync(); };
        var archive = Button(point.Status == "active" ? "Archive" : "Restore", "#ECEFF1", Color.FromArgb("#555"));
        archive.Clicked += async (_, _) => { point.Status = point.Status == "active" ? "archived" : "active"; await _service.SavePointAsync(point); await RefreshAsync(); };
        var delete = Button("Delete", "#FFEBEE", Color.FromArgb("#C62828"));
        delete.Clicked += async (_, _) => { if (!await DisplayAlert("Delete Data Point", "Delete this data point?", "Delete", "Cancel")) return; await _service.DeletePointAsync(point); await RefreshAsync(); };
        actions.Children.Add(edit); actions.Children.Add(priority); actions.Children.Add(archive); actions.Children.Add(delete); stack.Children.Add(actions);
        return Card(stack);
    }

    private async Task AddPointAsync(Companion companion, string scopeLabel)
    {
        var text = await CompanionTextInputPage.ShowAsync(Navigation, "Add Data Point", "", "Write a durable fact or observation...");
        if (string.IsNullOrWhiteSpace(text)) return;
        var priorityText = await DisplayPromptAsync("Priority", "1-5 (5 is most important)", "Save", "Cancel", keyboard: Keyboard.Numeric, initialValue: "3");
        if (!int.TryParse(priorityText, out int priority) || priority is < 1 or > 5) return;
        await _service.SaveManualPointAsync(_auth.CurrentUsername, companion, scopeLabel, text, priority);
        await RefreshAsync();
    }

    private async Task StartInterviewAsync(Companion companion, Dictionary<string, Game> gamesById)
    {
        var value = await DisplayPromptAsync("Start Interview", "Number of questions", "Copy Prompt", "Cancel", keyboard: Keyboard.Numeric, initialValue: companion.DefaultQuestionCount.ToString());
        if (value == null) return;
        if (!int.TryParse(value, out int questionCount) || questionCount is < 1 or > 50)
        {
            await DisplayAlert("Invalid Number of Questions", "Enter a whole number from 1 to 50.", "OK");
            return;
        }
        var interview = new CompanionInterview { Username = _auth.CurrentUsername, CompanionId = companion.Id, QuestionCount = questionCount, StartedAt = DateTime.UtcNow };
        await _service.SaveInterviewAsync(interview);
        string prompt = await _service.BuildInterviewPromptAsync(_auth.CurrentUsername, companion, questionCount, ScopeLabel(companion, gamesById));
        await Clipboard.SetTextAsync(prompt);
        _selectedInterview = interview;
        await DisplayAlert("Interview Prompt Copied", "Paste it into your LLM and conduct the interview. Return here to paste the conversation.", "OK");
        await RefreshAsync();
    }

    private async Task ContinueInterviewAsync(Companion companion, CompanionInterview interview, string scopeLabel)
    {
        if (string.IsNullOrWhiteSpace(interview.Transcript))
        {
            await SaveConversationAsync(companion, interview, scopeLabel);
            return;
        }
        try
        {
            var response = await CompanionTextInputPage.ShowAsync(Navigation, "Paste Extraction Result", "", "Paste the structured extraction result...");
            if (string.IsNullOrWhiteSpace(response)) return;
            var extraction = CompanionService.ParseExtraction(response);
            int recognizedCount = extraction.Adds.Count + extraction.Priorities.Count + extraction.Archives.Count + extraction.Updates.Count;
            if (recognizedCount == 0)
            {
                string details = string.Join("\n", extraction.SkippedLines.Concat(extraction.Warnings));
                string message = "Nothing recognized in the pasted result";
                if (!string.IsNullOrWhiteSpace(details)) message += $"\n\n{details}";
                await DisplayAlert("Nothing Recognized", message, "OK");
                return;
            }

            var applied = await CompanionExtractionReviewPage.ShowAsync(Navigation, extraction, async (adds, priorities, archives, updates) =>
            {
                var result = await _service.ApplyExtractionAsync(_auth.CurrentUsername, companion, interview, scopeLabel, adds, priorities, archives, updates);
                await TrySaveInterviewIdeaAsync(companion, interview, scopeLabel);
                return result;
            });
            if (applied == null) return;

            string loggingNote = applied.IdeaLoggingFailures > 0
                ? $" ({applied.IdeaLoggingFailures} not logged to Ideas)"
                : "";
            await DisplayAlert(
                "Extraction Applied",
                $"Added {applied.Added}{loggingNote}, reprioritized {applied.Reprioritized}, archived {applied.Archived}, updated {applied.Updated}",
                "OK");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[COMPANIONS] Extraction import failed: {ex}");
            await DisplayAlert("Extraction Import Failed", ex.Message, "OK");
        }
    }

    private async Task SaveConversationAsync(Companion companion, CompanionInterview interview, string scopeLabel)
    {
        var transcript = await CompanionTextInputPage.ShowAsync(Navigation, "Paste Conversation", interview.Transcript, "Paste the complete interview transcript...");
        if (string.IsNullOrWhiteSpace(transcript)) return;
        interview.Transcript = transcript.Trim();
        await _service.SaveInterviewAsync(interview);
        await TrySaveInterviewIdeaAsync(companion, interview, scopeLabel);
        var points = await _service.GetPointsAsync(_auth.CurrentUsername, companion.Id);
        await Clipboard.SetTextAsync(CompanionService.BuildExtractionPrompt(companion, interview, points));
        await DisplayAlert("Extraction Prompt Copied", "Run the copied prompt in your LLM, then return and paste the extraction result.", "OK");
        await RefreshAsync();
    }

    private async Task TrySaveInterviewIdeaAsync(Companion companion, CompanionInterview interview, string scopeLabel)
    {
        try
        {
            await _service.SaveInterviewIdeaAsync(_auth.CurrentUsername, companion, interview, scopeLabel);
        }
        catch
        {
            await DisplayAlert("Idea Logging Warning", "The interview was saved, but it could not be logged on the Ideas page.", "OK");
        }
    }

    private static string InterviewStepText(CompanionInterview interview) =>
        string.IsNullOrWhiteSpace(interview.Transcript) ? "Step 2 of 3: Paste conversation" :
        !string.IsNullOrWhiteSpace(interview.Notes) ? "Extraction applied (can review another result)" : "Step 3 of 3: Paste extraction result";

    private static string ScopeLabel(Companion item, IReadOnlyDictionary<string, Game> games) => item.Scope switch
    {
        "meta" => "Meta · all of Bannister",
        "game" => item.GameId != null && games.TryGetValue(item.GameId, out var game) ? $"Game · {game.DisplayName}" : $"Game · {item.GameId ?? "not selected"}",
        _ => $"Custom · {(string.IsNullOrWhiteSpace(item.Topic) ? "No topic" : item.Topic)}"
    };

    private static Label Heading(string text, double size) => new() { Text = text, FontSize = size, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") };
    private static Button Button(string text, string background, Color foreground) => new() { Text = text, BackgroundColor = Color.FromArgb(background), TextColor = foreground, CornerRadius = 7, FontSize = 12, HeightRequest = 38, Padding = new Thickness(10, 0) };
    private static Frame Card(View content) => new() { Content = content, Padding = 12, CornerRadius = 9, HasShadow = false, BorderColor = Color.FromArgb("#DDDDDD"), BackgroundColor = Colors.White };
}

internal sealed class CompanionEditPage : ContentPage
{
    private readonly TaskCompletionSource<Companion?> _completion = new();
    private CompanionEditPage(Companion? source, List<Game> games, string username)
    {
        Title = source == null ? "Add Companion" : "Edit Companion";
        var item = source == null ? new Companion { Username = username } : source;
        var scope = new Picker { Title = "Scope", ItemsSource = new[] { "game", "custom" }, SelectedItem = item.Scope == "game" ? "game" : "custom", IsEnabled = item.Scope != "meta" };
        var game = new Picker { Title = "Game", ItemsSource = games, ItemDisplayBinding = new Binding(nameof(Game.DisplayName)), SelectedItem = games.FirstOrDefault(x => x.GameId == item.GameId) };
        var topic = new Entry { Placeholder = "Custom topic", Text = item.Topic };
        var nickname = new Entry { Placeholder = "Nickname", Text = item.Nickname };
        var persona = new Editor { Placeholder = "Persona description", Text = item.PersonaDescription, HeightRequest = 90 };
        var stance = new Editor { Placeholder = "Interview stance", Text = item.InterviewStance, HeightRequest = 90 };
        var questionCount = new Entry { Placeholder = "Number of questions", Text = item.DefaultQuestionCount.ToString(), Keyboard = Keyboard.Numeric };
        void UpdateScope() { bool meta = item.Scope == "meta"; string selected = meta ? "meta" : scope.SelectedItem?.ToString() ?? "custom"; game.IsVisible = selected == "game"; topic.IsVisible = selected == "custom"; }
        scope.SelectedIndexChanged += (_, _) => UpdateScope(); UpdateScope();
        var save = new Button { Text = "Save", BackgroundColor = Color.FromArgb("#2E7D32"), TextColor = Colors.White };
        save.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(nickname.Text)) { await DisplayAlert("Nickname Required", "Enter a nickname.", "OK"); return; }
            if (!int.TryParse(questionCount.Text, out int questions) || questions is < 1 or > 50) { await DisplayAlert("Invalid Number of Questions", "Enter a whole number from 1 to 50.", "OK"); return; }
            string selectedScope = item.Scope == "meta" ? "meta" : scope.SelectedItem?.ToString() ?? "custom";
            if (selectedScope == "game" && game.SelectedItem is not Game) { await DisplayAlert("Game Required", "Choose a game.", "OK"); return; }
            item.Nickname = nickname.Text.Trim(); item.Scope = selectedScope; item.GameId = selectedScope == "game" ? ((Game)game.SelectedItem).GameId : null; item.Topic = selectedScope == "custom" ? topic.Text?.Trim() ?? "" : "";
            item.PersonaDescription = persona.Text?.Trim() ?? ""; item.InterviewStance = string.IsNullOrWhiteSpace(stance.Text) ? DefaultStance(item, games) : stance.Text.Trim(); item.DefaultQuestionCount = questions;
            await CloseAsync(item);
        };
        var cancel = new Button { Text = "Cancel" }; cancel.Clicked += async (_, _) => await CloseAsync(null);
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 18, Spacing = 9, Children = { new Label { Text = Title, FontSize = 22, FontAttributes = FontAttributes.Bold }, scope, game, topic, nickname, persona, stance, questionCount, new HorizontalStackLayout { Spacing = 8, Children = { save, cancel } } } } };
    }
    private static string DefaultStance(Companion item, List<Game> games) => item.Scope == "game" ? $"get to know the user in the context of {games.FirstOrDefault(x => x.GameId == item.GameId)?.DisplayName ?? "this game"}" : item.Scope == "custom" ? $"get to know the user in the context of {item.Topic}" : "get to know the user";
    private async Task CloseAsync(Companion? result) { await Navigation.PopModalAsync(); _completion.TrySetResult(result); }
    public static async Task<Companion?> ShowAsync(INavigation navigation, Companion? item, List<Game> games, string username) { var page = new CompanionEditPage(item, games, username); await navigation.PushModalAsync(page); return await page._completion.Task; }
}

internal sealed class CompanionTextInputPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _completion = new();
    private CompanionTextInputPage(string title, string initial, string placeholder)
    {
        Title = title;
        var editor = new Editor { Text = initial, Placeholder = placeholder, HeightRequest = 320, AutoSize = EditorAutoSizeOption.Disabled, BackgroundColor = Color.FromArgb("#FAFAFA") };
        var paste = new Button { Text = "Paste", BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White };
        paste.Clicked += async (_, _) => { try { if (!Clipboard.HasText) return; var text = await Clipboard.GetTextAsync(); if (string.IsNullOrEmpty(text)) return; string current = editor.Text ?? ""; int start = Math.Clamp(editor.CursorPosition, 0, current.Length); int length = Math.Clamp(editor.SelectionLength, 0, current.Length - start); editor.Text = current.Remove(start, length).Insert(start, text); editor.CursorPosition = start + text.Length; editor.SelectionLength = 0; } finally { editor.Focus(); } };
        var ok = new Button { Text = "OK", BackgroundColor = Color.FromArgb("#5B63EE"), TextColor = Colors.White }; ok.Clicked += async (_, _) => await CloseAsync(editor.Text);
        var cancel = new Button { Text = "Cancel" }; cancel.Clicked += async (_, _) => await CloseAsync(null);
        Content = new Grid { Padding = 16, RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) }, Children = { editor } };
        var buttons = new FlexLayout { Direction = Microsoft.Maui.Layouts.FlexDirection.Row, Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start, Children = { paste, ok, cancel } }; Grid.SetRow(buttons, 1); ((Grid)Content).Children.Add(buttons);
    }
    private async Task CloseAsync(string? value) { await Navigation.PopModalAsync(); _completion.TrySetResult(value); }
    public static async Task<string?> ShowAsync(INavigation navigation, string title, string initial, string placeholder) { var page = new CompanionTextInputPage(title, initial, placeholder); await navigation.PushModalAsync(page); return await page._completion.Task; }
}

internal sealed class CompanionExtractionReviewPage : ContentPage
{
    private readonly TaskCompletionSource<CompanionApplyResult?> _completion = new();
    private CompanionExtractionReviewPage(CompanionExtraction result, Func<List<CompanionAddProposal>, List<CompanionPriorityProposal>, List<CompanionArchiveProposal>, List<CompanionUpdateProposal>, Task<CompanionApplyResult>> apply)
    {
        Title = "Review Extraction";
        var stack = new VerticalStackLayout { Padding = 16, Spacing = 8 };
        stack.Children.Add(new Label { Text = "Review Proposed Changes", FontSize = 22, FontAttributes = FontAttributes.Bold });
        var adds = AddRows(stack, "Add points", result.Adds, x => $"P{x.Priority}: {x.Text}");
        var priorities = AddRows(stack, "Priority changes", result.Priorities, x => $"Point {x.PointId} → priority {x.Priority}");
        var archives = AddRows(stack, "Archives", result.Archives, x => $"Point {x.PointId}: {x.Reason}");
        var updates = AddRows(stack, "Text updates", result.Updates, x => $"Point {x.PointId}: {x.Text}");
        if (result.Warnings.Count > 0) { stack.Children.Add(new Label { Text = $"Warnings ({result.Warnings.Count})", FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#E65100") }); foreach (var warning in result.Warnings) stack.Children.Add(new Label { Text = warning, FontSize = 11, TextColor = Color.FromArgb("#777") }); }
        if (result.SkippedLines.Count > 0) { stack.Children.Add(new Label { Text = $"Skipped invalid lines ({result.SkippedLines.Count})", FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#E65100") }); foreach (var line in result.SkippedLines) stack.Children.Add(new Label { Text = line, FontSize = 11, TextColor = Color.FromArgb("#777") }); }
        var applyButton = new Button { Text = "Apply Selected", BackgroundColor = Color.FromArgb("#2E7D32"), TextColor = Colors.White };
        applyButton.Clicked += async (_, _) =>
        {
            try
            {
                applyButton.IsEnabled = false;
                var applied = await apply(Selected(result.Adds, adds), Selected(result.Priorities, priorities), Selected(result.Archives, archives), Selected(result.Updates, updates));
                await CloseAsync(applied);
            }
            catch (Exception ex)
            {
                await CloseWithErrorAsync(ex);
            }
        };
        var cancel = new Button { Text = "Cancel" }; cancel.Clicked += async (_, _) => await CloseAsync(null);
        stack.Children.Add(new HorizontalStackLayout { Spacing = 8, Children = { applyButton, cancel } }); Content = new ScrollView { Content = stack };
    }
    private static List<CheckBox> AddRows<T>(VerticalStackLayout stack, string title, List<T> values, Func<T, string> text) { var checks = new List<CheckBox>(); if (values.Count == 0) return checks; stack.Children.Add(new Label { Text = title, FontSize = 17, FontAttributes = FontAttributes.Bold }); foreach (var value in values) { var check = new CheckBox { IsChecked = true }; checks.Add(check); stack.Children.Add(new HorizontalStackLayout { Spacing = 6, Children = { check, new Label { Text = text(value), VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.WordWrap } } }); } return checks; }
    private static List<T> Selected<T>(List<T> values, List<CheckBox> checks) => values.Where((_, i) => checks[i].IsChecked).ToList();
    private async Task CloseAsync(CompanionApplyResult? result) { await Navigation.PopModalAsync(); _completion.TrySetResult(result); }
    private async Task CloseWithErrorAsync(Exception exception) { await Navigation.PopModalAsync(); _completion.TrySetException(exception); }
    public static async Task<CompanionApplyResult?> ShowAsync(INavigation navigation, CompanionExtraction result, Func<List<CompanionAddProposal>, List<CompanionPriorityProposal>, List<CompanionArchiveProposal>, List<CompanionUpdateProposal>, Task<CompanionApplyResult>> apply) { var page = new CompanionExtractionReviewPage(result, apply); await navigation.PushModalAsync(page); return await page._completion.Task; }
}
