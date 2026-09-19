using System.Text;
using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class BatchImageAssignPage : ContentPage
{
    private readonly ActivityService _activityService;
    private readonly GameService _gameService;
    private readonly AuthService _auth;
    private List<Game> _games = new();
    private List<Activity> _allActivities = new();
    private int? _viewFilter;
    private int _currentPage;
    private List<(Game Game, List<Activity> Activities)> _pagedGroups = new();
    private bool _isSaving;
    private VerticalStackLayout _content = null!;
    private Label _pageLabel = null!;
    private Button _prev = null!, _next = null!;
    private Button _all = null!, _public = null!, _private = null!, _both = null!;

    public BatchImageAssignPage(ActivityService activityService, GameService gameService, AuthService auth)
    {
        _activityService = activityService;
        _gameService = gameService;
        _auth = auth;
        Title = "Batch Image Assign";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout { Padding = 20, Spacing = 12 };
        stack.Children.Add(new Label { Text = " Batch Image Assign", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") });
        stack.Children.Add(new Label { Text = "Export a prompt for the LLM to suggest image ideas per activity. Paste the response back to import.", FontSize = 13, TextColor = Color.FromArgb("#666"), LineBreakMode = LineBreakMode.WordWrap });

        var filters = new HorizontalStackLayout { Spacing = 6 };
        _all = Filter("Show All", true, "#555"); _public = Filter(" Public", false, "#1565C0"); _private = Filter(" Private", false, "#6A0DAD"); _both = Filter(" Both", false, "#2E7D32");
        _all.Clicked += (_, _) => SetFilter(null); _public.Clicked += (_, _) => SetFilter(1); _private.Clicked += (_, _) => SetFilter(0); _both.Clicked += (_, _) => SetFilter(2);
        filters.Children.Add(_all); filters.Children.Add(_public); filters.Children.Add(_private); filters.Children.Add(_both); stack.Children.Add(filters);

        var nav = new Grid { ColumnDefinitions = { new ColumnDefinition(new GridLength(44)), new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(44)) } };
        _prev = new Button { Text = "◀", HeightRequest = 36, Padding = 0, BackgroundColor = Color.FromArgb("#E3F2FD"), TextColor = Color.FromArgb("#1565C0") };
        _next = new Button { Text = "▶", HeightRequest = 36, Padding = 0, BackgroundColor = Color.FromArgb("#E3F2FD"), TextColor = Color.FromArgb("#1565C0") };
        _pageLabel = new Label { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, TextColor = Color.FromArgb("#444") };
        _prev.Clicked += (_, _) => { if (_currentPage > 0) { _currentPage--; RenderCurrentPage(); } };
        _next.Clicked += (_, _) => { if (_currentPage < _pagedGroups.Count - 1) { _currentPage++; RenderCurrentPage(); } };
        nav.Add(_prev, 0, 0); nav.Add(_pageLabel, 1, 0); nav.Add(_next, 2, 0); stack.Children.Add(nav);
        _content = new VerticalStackLayout { Spacing = 2 }; stack.Children.Add(_content);
        Content = new ScrollView { Content = stack };
    }

    private Button Filter(string text, bool active, string color) => new() { Text = text, FontSize = 11, HeightRequest = 30, CornerRadius = 6, Padding = new Thickness(8, 0), BackgroundColor = active ? Color.FromArgb(color) : Color.FromArgb("#ECEFF1"), TextColor = active ? Colors.White : Color.FromArgb("#37474F") };
    private void SetFilter(int? filter) { _viewFilter = filter; _currentPage = 0; Style(_all, filter == null, "#555"); Style(_public, filter == 1, "#1565C0"); Style(_private, filter == 0, "#6A0DAD"); Style(_both, filter == 2, "#2E7D32"); BuildPagedGroups(); RenderCurrentPage(); }
    private static void Style(Button b, bool active, string hex) { b.BackgroundColor = active ? Color.FromArgb(hex) : Color.FromArgb("#ECEFF1"); b.TextColor = active ? Colors.White : Color.FromArgb("#37474F"); }

    private async Task LoadDataAsync()
    {
        _games = await _gameService.GetGamesAsync(_auth.CurrentUsername);
        _allActivities = await _activityService.GetActivitiesAsync(_auth.CurrentUsername);
        BuildPagedGroups(); RenderCurrentPage();
    }

    private void BuildPagedGroups()
    {
        _pagedGroups = _games.OrderBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase).Select(g =>
        {
            var acts = _allActivities.Where(a => string.Equals(a.Game, g.GameId, StringComparison.OrdinalIgnoreCase)).OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
            if (_viewFilter == 1) acts = acts.Where(a => a.ActivityVisibility == 1 || a.ActivityVisibility == 2).ToList();
            else if (_viewFilter == 0) acts = acts.Where(a => a.ActivityVisibility == 0 || a.ActivityVisibility == 2).ToList();
            else if (_viewFilter == 2) acts = acts.Where(a => a.ActivityVisibility == 2).ToList();
            return (Game: g, Activities: acts);
        }).Where(x => x.Activities.Count > 0).ToList();
        _currentPage = Math.Min(_currentPage, Math.Max(0, _pagedGroups.Count - 1));
    }

    private void RenderCurrentPage()
    {
        _content.Children.Clear();
        if (_pagedGroups.Count == 0) { _pageLabel.Text = "No activities"; _prev.IsEnabled = _next.IsEnabled = false; _content.Children.Add(new Label { Text = "No activities match the filter.", TextColor = Color.FromArgb("#999") }); return; }
        var (game, acts) = _pagedGroups[_currentPage];
        _pageLabel.Text = $"{game.DisplayName} ({_currentPage + 1}/{_pagedGroups.Count})"; _prev.IsEnabled = _currentPage > 0; _next.IsEnabled = _currentPage < _pagedGroups.Count - 1;
        var actions = new HorizontalStackLayout { Spacing = 8 };
        var export = new Button { Text = " Export Prompt", BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White, HeightRequest = 40, Padding = new Thickness(14, 0) }; export.Clicked += async (_, _) => await ExportChunkedAsync(game, acts);
        var import = new Button { Text = " Import Response", BackgroundColor = Color.FromArgb("#2E7D32"), TextColor = Colors.White, HeightRequest = 40, Padding = new Thickness(14, 0) }; import.Clicked += async (_, _) => await ImportResponseAsync(acts);
        actions.Children.Add(export); actions.Children.Add(import);
        var clearBtn = new Button { Text = " Clear Ideas", BackgroundColor = Color.FromArgb("#FFEBEE"), TextColor = Color.FromArgb("#C62828"), CornerRadius = 8, FontSize = 13, HeightRequest = 40, Padding = new Thickness(12, 0) };
        clearBtn.Clicked += async (_, _) =>
        {
            bool confirm = await DisplayAlert("Clear Image Ideas", $"Clear all image ideas for \"{game.DisplayName}\" ({acts.Count} activities)?", "Clear", "Cancel");
            if (!confirm || _isSaving) return;
            _isSaving = true;
            try
            {
                foreach (var act in acts)
                {
                    act.ImageIdea = "";
                    await _activityService.UpdateActivityAsync(act);
                }
                await LoadDataAsync();
            }
            finally { _isSaving = false; }
        };
        actions.Children.Add(clearBtn); _content.Children.Add(actions);
        foreach (var act in acts) _content.Children.Add(BuildActivityRow(act));
    }

    private View BuildActivityRow(Activity act)
    {
        bool hasIdea = !string.IsNullOrWhiteSpace(act.ImageIdea);
        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(24)) }, Padding = new Thickness(10, 8), ColumnSpacing = 8, BackgroundColor = Colors.White };
        var labels = new VerticalStackLayout { Spacing = 2 };
        labels.Children.Add(new Label { Text = act.Name, FontSize = 13, TextColor = Color.FromArgb("#222"), LineBreakMode = LineBreakMode.WordWrap });
        labels.Children.Add(new Label { Text = hasIdea ? $" {act.ImageIdea}" : "No image idea yet", FontSize = 11, TextColor = hasIdea ? Color.FromArgb("#1565C0") : Color.FromArgb("#999"), FontAttributes = hasIdea ? FontAttributes.None : FontAttributes.Italic, LineBreakMode = LineBreakMode.WordWrap });
        row.Add(labels, 0, 0); row.Add(new BoxView { Color = hasIdea ? Color.FromArgb("#2E7D32") : Color.FromArgb("#E0E0E0"), WidthRequest = 10, HeightRequest = 10, CornerRadius = 5, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }, 1, 0);
        return new Frame { Content = row, Padding = 0, CornerRadius = 6, HasShadow = false, BorderColor = Color.FromArgb("#E0E0E0"), BackgroundColor = Colors.White, Margin = new Thickness(0, 1) };
    }

    private async Task ExportPromptAsync(Game game, List<Activity> acts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are helping assign visual image ideas to activities in a personal productivity app called Bannister.");
        sb.AppendLine(); sb.AppendLine($"The game (category) is: \"{game.DisplayName}\""); sb.AppendLine();
        sb.AppendLine("For each activity:");
        sb.AppendLine("1. If the activity name is unclear or ambiguous, ask the user one short clarifying question before suggesting an image.");
        sb.AppendLine("2. Suggest a concrete visual image that could represent it — something suitable as a stock photo or illustration.");
        sb.AppendLine("3. Return suggestions in EXACTLY this C# format, numbered from 1:"); sb.AppendLine();
        sb.AppendLine("activityImage[1].Name = \"<exact activity name>\";"); sb.AppendLine("activityImage[1].ImageIdea = \"<concrete image description>\";");
        sb.AppendLine(); sb.AppendLine("Do not use markdown or add text after the last entry. Ask clarifying questions first if needed, then output the C# format block."); sb.AppendLine(); sb.AppendLine("ACTIVITIES:");
        for (int i = 0; i < acts.Count; i++) sb.AppendLine($"[{i + 1}] {acts[i].Name}" + (string.IsNullOrWhiteSpace(acts[i].ImageIdea) ? "" : $" (current idea: {acts[i].ImageIdea})"));
        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert("Prompt Copied", $"Prompt for {acts.Count} activities copied to clipboard.", "OK");
    }

    private async Task ExportChunkedAsync(Game game, List<Activity> acts)
    {
        if (acts.Count == 0) return;
        int chunkSize = acts.Count;
        if (acts.Count > 20)
        {
            string? chunkStr = await DisplayPromptAsync("Chunk Size", $"This page has {acts.Count} activities. How many should be included per LLM export? (Recommended: 10–20)", "OK", "Cancel", initialValue: "15", keyboard: Keyboard.Numeric);
            if (string.IsNullOrWhiteSpace(chunkStr)) return;
            if (!int.TryParse(chunkStr, out chunkSize) || chunkSize < 1) chunkSize = 15;
        }

        var chunks = new List<List<Activity>>();
        for (int i = 0; i < acts.Count; i += chunkSize)
            chunks.Add(acts.Skip(i).Take(chunkSize).ToList());

        for (int c = 0; c < chunks.Count; c++)
        {
            var chunk = chunks[c];
            if (chunks.Count > 1)
            {
                bool proceed = await DisplayAlert($"Chunk {c + 1} of {chunks.Count}", $"Exporting activities {c * chunkSize + 1}–{c * chunkSize + chunk.Count} of {acts.Count}.\n\nExport this chunk?", "Export", "Stop Here");
                if (!proceed) return;
            }

            await ExportPromptAsync(game, chunk);
            bool doImport = await DisplayAlert("Import Response", chunks.Count > 1 ? $"Paste the LLM response for chunk {c + 1} of {chunks.Count}." : "Paste the LLM response to import image ideas.", "Import Now", chunks.Count > 1 && c < chunks.Count - 1 ? "Skip This Chunk" : "Cancel");
            if (doImport) await ImportResponseAsync(chunk);

            if (c < chunks.Count - 1)
            {
                bool next = await DisplayAlert("Continue?", $"Chunk {c + 1} done. {chunks.Count - c - 1} chunk(s) remaining.", "Next Chunk", "Stop Here");
                if (!next) return;
            }
        }

        if (chunks.Count > 1)
            await DisplayAlert("Complete", $"All {chunks.Count} chunks processed.", "OK");
    }

    private async Task ImportResponseAsync(List<Activity> acts)
    {
        var tcs = new TaskCompletionSource<string?>();
        var editorFull = new Editor
        {
            Placeholder = "Paste LLM response here...",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            FontSize = 12,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            AutoSize = EditorAutoSizeOption.Disabled
        };

        var confirmBtnFull = new Button
        {
            Text = "Import",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var pasteGrid = new Grid
        {
            Padding = new Thickness(16),
            RowSpacing = 10,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };

        pasteGrid.Add(new Label
        {
            Text = "Paste the C# format block " +
                   "from the LLM response " +
                   "(multi-line is supported):",
            FontSize = 13,
            TextColor = Color.FromArgb("#555"),
            LineBreakMode = LineBreakMode.WordWrap
        }, 0, 0);
        pasteGrid.Add(editorFull, 0, 1);
        pasteGrid.Add(confirmBtnFull, 0, 2);

        var pastePage = new ContentPage
        {
            Title = "Paste LLM Response",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            Content = pasteGrid
        };

        confirmBtnFull.Clicked += async (_, _) =>
        {
            tcs.TrySetResult(editorFull.Text ?? "");
            await Navigation.PopAsync();
        };
        pastePage.Disappearing += (_, _) =>
            tcs.TrySetResult(null);

        await Navigation.PushAsync(pastePage);
        string? response = await tcs.Task;
        if (string.IsNullOrWhiteSpace(response) || _isSaving) return;
        var parsed = ParseImageIdeas(response);
        if (parsed.Count == 0) { await DisplayAlert("Parse Failed", "Could not find activityImage entries.", "OK"); return; }
        _isSaving = true;
        try
        {
            int applied = 0;
            foreach (var (name, idea) in parsed)
            {
                var match = acts.FirstOrDefault(a => string.Equals(a.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match == null) continue;
                match.ImageIdea = idea; await _activityService.UpdateActivityAsync(match); applied++;
            }
            await DisplayAlert("Import Complete", $"{applied} image idea(s) saved.", "OK"); await LoadDataAsync();
        }
        finally { _isSaving = false; }
    }

    private static List<(string Name, string Idea)> ParseImageIdeas(string response)
    {
        var results = new List<(string, string)>();

        for (int idx = 1; idx <= 200; idx++)
        {
            string nameKey = $"activityImage[{idx}].Name";
            string ideaKey = $"activityImage[{idx}].ImageIdea";
            int namePos = response.IndexOf(nameKey, StringComparison.OrdinalIgnoreCase);
            if (namePos < 0) break;
            int ideaPos = response.IndexOf(ideaKey, namePos, StringComparison.OrdinalIgnoreCase);
            if (ideaPos < 0) break;
            string? name = ExtractQuoted(response, namePos + nameKey.Length);
            string? idea = ExtractQuoted(response, ideaPos + ideaKey.Length);
            if (name != null && idea != null) results.Add((name, idea));
        }

        return results;
    }

    private static string? ExtractQuoted(string text, int searchFrom)
    {
        int eq = text.IndexOf('=', searchFrom);
        if (eq < 0) return null;
        int maxLook = Math.Min(eq + 500, text.Length);
        var rest = text[(eq + 1)..maxLook].TrimStart();
        if (rest.StartsWith('"'))
        {
            int end = 1;
            while (end < rest.Length)
            {
                char c = rest[end];
                char prev = rest[end - 1];
                if (c == '"' && prev != '\\') break;
                end++;
            }
            if (end < rest.Length)
                return rest[1..end].Replace("\\\"", "\"").Trim();
            int nl = rest.IndexOfAny(new[] { '\n', '\r' });
            return nl > 1 ? rest[1..nl].Trim() : null;
        }
        int lineEnd = rest.IndexOfAny(new[] { '\n', '\r', ';' });
        return lineEnd > 0 ? rest[..lineEnd].Trim() : rest.Trim();
    }
}
