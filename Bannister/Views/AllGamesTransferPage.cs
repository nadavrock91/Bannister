using System.Text.Json;
using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class AllGamesTransferPage : ContentPage
{
    private readonly GameService _games;
    private readonly ActivityService _activities;
    private readonly AuthService _auth;
    private List<GameExportGroup> _exportGroups = new();
    private VerticalStackLayout _exportContainer = null!;
    private Label _exportStatusLabel = null!;

    private class ActivityExportDto
    {
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public int ExpGain { get; set; }
        public string ImagePath { get; set; } = "";
        public bool IsAutoAward { get; set; }
        public int MeaningfulUntilLevel { get; set; } = 100;
    }

    private class GameExportDto
    {
        public string GameId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public List<ActivityExportDto> Activities { get; set; } = new();
    }

    private class GameExportGroup
    {
        public GameExportDto Game { get; set; } = new();
        public List<Activity> AllActivities { get; set; } = new();
        public HashSet<int> SelectedIndices { get; set; } = new();
    }

    public AllGamesTransferPage(GameService games, ActivityService activities, AuthService auth)
    {
        _games = games;
        _activities = activities;
        _auth = auth;
        Title = "Transfer All Games";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadExportGroupsAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout { Padding = 20, Spacing = 16 };
        stack.Children.Add(new Label { Text = " Transfer All Games", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") });
        stack.Children.Add(new Label { Text = "Select activities to export per game, then copy to clipboard. On the other device paste the JSON to import.", FontSize = 13, TextColor = Color.FromArgb("#666"), LineBreakMode = LineBreakMode.WordWrap });

        var exportHeader = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 8
        };
        exportHeader.Add(new Label { Text = "Export", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0"), VerticalOptions = LayoutOptions.Center }, 0, 0);
        var globalSelectAll = SmallButton("All", "#E3F2FD", "#1565C0", 28);
        globalSelectAll.Clicked += (_, _) =>
        {
            foreach (var g in _exportGroups)
                for (int i = 0; i < g.AllActivities.Count; i++) g.SelectedIndices.Add(i);
            RenderExportGroups();
        };
        exportHeader.Add(globalSelectAll, 1, 0);
        var globalSelectNone = SmallButton("None", "#ECEFF1", "#37474F", 28);
        globalSelectNone.Clicked += (_, _) =>
        {
            foreach (var g in _exportGroups) g.SelectedIndices.Clear();
            RenderExportGroups();
        };
        exportHeader.Add(globalSelectNone, 2, 0);
        stack.Children.Add(exportHeader);

        _exportStatusLabel = new Label { Text = "Loading...", FontSize = 12, TextColor = Color.FromArgb("#666"), IsVisible = true };
        stack.Children.Add(_exportStatusLabel);
        _exportContainer = new VerticalStackLayout { Spacing = 12 };
        stack.Children.Add(_exportContainer);
        var exportBtn = new Button { Text = " Copy Selected to Clipboard", BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White, CornerRadius = 8, FontSize = 14, HeightRequest = 44, FontAttributes = FontAttributes.Bold };
        exportBtn.Clicked += async (_, _) => await ExportSelectedAsync();
        stack.Children.Add(exportBtn);

        stack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#E0E0E0"), Margin = new Thickness(0, 8, 0, 0) });
        stack.Children.Add(new Label { Text = "Import", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#2E7D32") });
        stack.Children.Add(new Label { Text = "Paste exported JSON. All activities are imported. Duplicates (same name in same game) will ask what to do.", FontSize = 12, TextColor = Color.FromArgb("#666"), LineBreakMode = LineBreakMode.WordWrap });
        var pasteBtn = new Button { Text = " Paste & Import JSON", BackgroundColor = Color.FromArgb("#2E7D32"), TextColor = Colors.White, CornerRadius = 8, FontSize = 14, HeightRequest = 44, FontAttributes = FontAttributes.Bold };
        pasteBtn.Clicked += async (_, _) => await PasteAndImportAsync();
        stack.Children.Add(pasteBtn);
        Content = new ScrollView { Content = stack };
    }

    private static Button SmallButton(string text, string background, string foreground, double height) => new()
    {
        Text = text, BackgroundColor = Color.FromArgb(background), TextColor = Color.FromArgb(foreground),
        CornerRadius = 4, FontSize = 10, HeightRequest = height, Padding = new Thickness(6, 0)
    };

    private async Task LoadExportGroupsAsync()
    {
        var games = await _games.GetGamesAsync(_auth.CurrentUsername);
        _exportGroups.Clear();
        foreach (var game in games)
        {
            var acts = await _activities.GetActivitiesAsync(_auth.CurrentUsername, game.GameId);
            var group = new GameExportGroup
            {
                Game = new GameExportDto { GameId = game.GameId, DisplayName = game.DisplayName },
                AllActivities = acts.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList()
            };
            _exportGroups.Add(group);
        }
        int total = _exportGroups.Sum(g => g.AllActivities.Count);
        _exportStatusLabel.Text =
            $"{_exportGroups.Count} games, {total} activities. " +
            "Select what to export.";
        RenderExportGroups();
    }

    private void RenderExportGroups()
    {
        _exportContainer.Children.Clear();
        foreach (var group in _exportGroups.OrderBy(g => g.Game.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var gameSection = new VerticalStackLayout { Spacing = 4 };
            var headerRow = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 6
            };
            headerRow.Add(new Label { Text = group.Game.DisplayName, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0"), VerticalOptions = LayoutOptions.Center }, 0, 0);
            var capturedGroup = group;
            var allBtn = SmallButton("All", "#E3F2FD", "#1565C0", 24);
            allBtn.Clicked += (_, _) =>
            {
                for (int i = 0; i < capturedGroup.AllActivities.Count; i++) capturedGroup.SelectedIndices.Add(i);
                RenderExportGroups();
            };
            headerRow.Add(allBtn, 1, 0);
            var noneBtn = SmallButton("None", "#ECEFF1", "#37474F", 24);
            noneBtn.Clicked += (_, _) => { capturedGroup.SelectedIndices.Clear(); RenderExportGroups(); };
            headerRow.Add(noneBtn, 2, 0);
            gameSection.Children.Add(headerRow);

            var actStack = new VerticalStackLayout { Spacing = 2, Margin = new Thickness(12, 0, 0, 0) };
            for (int i = 0; i < group.AllActivities.Count; i++)
            {
                int capturedI = i;
                var act = group.AllActivities[i];
                bool selected = group.SelectedIndices.Contains(i);
                var row = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(new GridLength(32)), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    ColumnSpacing = 6
                };
                var cb = new CheckBox { IsChecked = selected };
                cb.CheckedChanged += (_, e) =>
                {
                    if (e.Value)
                        capturedGroup.SelectedIndices.Add(capturedI);
                    else
                        capturedGroup.SelectedIndices.Remove(capturedI);
                };
                row.Add(cb, 0, 0);
                row.Add(new Label { Text = act.Name, FontSize = 13, TextColor = Color.FromArgb("#222"), VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation }, 1, 0);
                row.Add(new Label { Text = $"+{act.ExpGain}", FontSize = 11, TextColor = Color.FromArgb("#2E7D32"), VerticalOptions = LayoutOptions.Center }, 2, 0);
                actStack.Children.Add(row);
            }
            gameSection.Children.Add(actStack);
            _exportContainer.Children.Add(gameSection);
        }
    }

    private async Task ExportSelectedAsync()
    {
        var export = new List<GameExportDto>();
        int totalSelected = 0;
        foreach (var group in _exportGroups)
        {
            if (group.SelectedIndices.Count == 0) continue;
            var selectedActs = group.SelectedIndices.OrderBy(i => i)
                .Select(i => group.AllActivities[i])
                .Select(a => new ActivityExportDto
                {
                    Name = a.Name, Category = a.Category ?? "", ExpGain = a.ExpGain,
                    ImagePath = a.ImagePath ?? "", IsAutoAward = a.IsAutoAward,
                    MeaningfulUntilLevel = a.MeaningfulUntilLevel
                }).ToList();
            export.Add(new GameExportDto { GameId = group.Game.GameId, DisplayName = group.Game.DisplayName, Activities = selectedActs });
            totalSelected += selectedActs.Count;
        }
        if (totalSelected == 0)
        {
            await DisplayAlert("Nothing selected", "Select at least one activity to export.", "OK");
            return;
        }
        var json = JsonSerializer.Serialize(new { ExportedAt = DateTime.UtcNow, Games = export }, new JsonSerializerOptions { WriteIndented = true });
        await Clipboard.SetTextAsync(json);
        await DisplayAlert("Exported", $"{totalSelected} activit{(totalSelected == 1 ? "y" : "ies")} across {export.Count} games copied to clipboard.", "OK");
    }

    private async Task PasteAndImportAsync()
    {
        var json = await Clipboard.GetTextAsync();
        if (string.IsNullOrWhiteSpace(json))
        {
            await DisplayAlert("Empty clipboard",
                "Copy exported JSON to clipboard first.", "OK");
            return;
        }

        List<GameExportDto> gameDtos;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var gamesEl = doc.RootElement.GetProperty("Games");
            gameDtos = JsonSerializer.Deserialize<List<GameExportDto>>(
                gamesEl.GetRawText(),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Parse error",
                $"Could not read JSON: {ex.Message}", "OK");
            return;
        }

        if (gameDtos.Count == 0)
        {
            await DisplayAlert("Nothing found",
                "No games found in the JSON.", "OK");
            return;
        }

        // Get existing games to detect which need creating
        var existingGames = await _games.GetGamesAsync(
            _auth.CurrentUsername);
        var existingGameIds = existingGames
            .Select(g => g.GameId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Detect all duplicates across all games upfront
        var allDuplicates = new List<string>();
        foreach (var gameDto in gameDtos)
        {
            List<Activity> existing = new();
            try
            {
                existing = await _activities.GetActivitiesAsync(
                    _auth.CurrentUsername, gameDto.GameId);
            }
            catch { }

            var existingNames = existing
                .Select(a => a.Name.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var act in gameDto.Activities)
            {
                if (existingNames.Contains(act.Name.Trim()))
                    allDuplicates.Add(
                        $"{gameDto.DisplayName} → {act.Name}");
            }
        }

        // Single prompt for all duplicates
        bool skipDupes = false;
        if (allDuplicates.Count > 0)
        {
            string dupeList = string.Join("\n",
                allDuplicates.Select(d => $"• {d}"));
            string choice = await DisplayActionSheet(
                $"{allDuplicates.Count} duplicate " +
                $"{(allDuplicates.Count == 1 ? "activity" : "activities")} " +
                $"found:\n{dupeList}",
                null, null,
                "Skip all duplicates",
                "Import all anyway (keep both)");
            skipDupes = choice == "Skip all duplicates";
        }

        int gamesCreated = 0;
        int imported = 0;
        int skipped = 0;

        foreach (var gameDto in gameDtos
            .OrderBy(g => g.DisplayName,
                StringComparer.OrdinalIgnoreCase))
        {
            // Create game if it doesn't exist
            if (!existingGameIds.Contains(gameDto.GameId))
            {
                try
                {
                    await _games.CreateGameAsync(
                        _auth.CurrentUsername,
                        gameDto.DisplayName);
                    existingGameIds.Add(gameDto.GameId);
                    gamesCreated++;
                }
                catch { }
            }

            // Get existing activity names for dupe check
            List<Activity> existingActs = new();
            try
            {
                existingActs = await _activities.GetActivitiesAsync(
                    _auth.CurrentUsername, gameDto.GameId);
            }
            catch { }

            var existingNames = existingActs
                .Select(a => a.Name.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var act in gameDto.Activities)
            {
                bool isDupe = existingNames.Contains(act.Name.Trim());
                if (isDupe && skipDupes)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    var newActivity = new Activity
                    {
                        Username = _auth.CurrentUsername,
                        Game = gameDto.GameId,
                        Name = act.Name,
                        Category = act.Category,
                        ExpGain = act.ExpGain,
                        ImagePath = act.ImagePath,
                        IsAutoAward = act.IsAutoAward,
                        MeaningfulUntilLevel = act.MeaningfulUntilLevel,
                        IsActive = true
                    };
                    await _activities.CreateActivityAsync(newActivity);
                    imported++;
                }
                catch
                {
                    skipped++;
                }
            }
        }

        var summary = new List<string>();
        if (gamesCreated > 0)
            summary.Add($"{gamesCreated} game" +
                $"{(gamesCreated == 1 ? "" : "s")} created");
        summary.Add($"{imported} activit" +
            $"{(imported == 1 ? "y" : "ies")} imported");
        if (skipped > 0)
            summary.Add($"{skipped} skipped");

        await DisplayAlert("Import Complete",
            string.Join(", ", summary) + ".", "OK");
    }
}
