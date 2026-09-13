using System.Text.Json;
using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class AllGamesTransferPage : ContentPage
{
    private readonly GameService _games;
    private readonly ActivityService _activities;
    private readonly AuthService _auth;

    private List<GameImportGroup> _importGroups = new();
    private VerticalStackLayout _importContainer = null!;
    private Label _importStatusLabel = null!;
    private Button _importConfirmBtn = null!;

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

    private class GameImportGroup
    {
        public GameExportDto Game { get; set; } = new();
        public List<(ActivityExportDto Activity, bool Selected)>
            Activities { get; set; } = new();
    }

    public AllGamesTransferPage(
        GameService games,
        ActivityService activities,
        AuthService auth)
    {
        _games = games;
        _activities = activities;
        _auth = auth;
        Title = "Transfer All Games";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16
        };

        stack.Children.Add(new Label
        {
            Text = " Transfer All Games",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        stack.Children.Add(new Label
        {
            Text = "Export all games and activities to JSON, or import " +
                   "from a previously exported JSON.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        stack.Children.Add(new Label
        {
            Text = "Export",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0"),
            Margin = new Thickness(0, 8, 0, 0)
        });
        stack.Children.Add(new Label
        {
            Text = "Exports all active games and their active activities " +
                   "as JSON to clipboard.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        var exportBtn = new Button
        {
            Text = " Export All to Clipboard",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        };
        exportBtn.Clicked += async (_, _) => await ExportAllAsync();
        stack.Children.Add(exportBtn);

        stack.Children.Add(new BoxView
        {
            HeightRequest = 1,
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            Margin = new Thickness(0, 8, 0, 0)
        });
        stack.Children.Add(new Label
        {
            Text = "Import",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2E7D32")
        });
        stack.Children.Add(new Label
        {
            Text = "Paste exported JSON. Select which activities to " +
                   "import per game. Duplicates will be flagged.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        var pasteBtn = new Button
        {
            Text = " Paste Import JSON",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44
        };
        pasteBtn.Clicked += async (_, _) => await PasteImportAsync();
        stack.Children.Add(pasteBtn);

        _importStatusLabel = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            IsVisible = false,
            LineBreakMode = LineBreakMode.WordWrap
        };
        stack.Children.Add(_importStatusLabel);

        _importContainer = new VerticalStackLayout { Spacing = 16 };
        stack.Children.Add(_importContainer);

        _importConfirmBtn = new Button
        {
            Text = "✅ Import Selected",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold,
            IsVisible = false
        };
        _importConfirmBtn.Clicked += async (_, _) =>
            await ImportSelectedAsync();
        stack.Children.Add(_importConfirmBtn);

        Content = new ScrollView { Content = stack };
    }

    private async Task ExportAllAsync()
    {
        var games = await _games.GetGamesAsync(_auth.CurrentUsername);
        if (games.Count == 0)
        {
            await DisplayAlert("No games",
                "No active games found to export.", "OK");
            return;
        }

        var export = new List<GameExportDto>();
        foreach (var game in games)
        {
            var acts = await _activities.GetActivitiesAsync(
                _auth.CurrentUsername, game.GameId);
            export.Add(new GameExportDto
            {
                GameId = game.GameId,
                DisplayName = game.DisplayName,
                Activities = acts.Select(a => new ActivityExportDto
                {
                    Name = a.Name,
                    Category = a.Category ?? "",
                    ExpGain = a.ExpGain,
                    ImagePath = a.ImagePath ?? "",
                    IsAutoAward = a.IsAutoAward,
                    MeaningfulUntilLevel = a.MeaningfulUntilLevel
                }).ToList()
            });
        }

        var json = JsonSerializer.Serialize(
            new { ExportedAt = DateTime.UtcNow, Games = export },
            new JsonSerializerOptions { WriteIndented = true });

        await Clipboard.SetTextAsync(json);

        int totalActs = export.Sum(g => g.Activities.Count);
        await DisplayAlert("Exported",
            $"{games.Count} games and {totalActs} activities " +
            "copied to clipboard.\n\nPaste on another device in " +
            "the Import section of this page.",
            "OK");
    }

    private async Task PasteImportAsync()
    {
        var json = await Clipboard.GetTextAsync();
        if (string.IsNullOrWhiteSpace(json))
        {
            await DisplayAlert("Empty clipboard",
                "Copy exported JSON to clipboard first.", "OK");
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var gamesEl = doc.RootElement.GetProperty("Games");
            var gameDtos = JsonSerializer
                .Deserialize<List<GameExportDto>>(
                    gamesEl.GetRawText(),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (gameDtos == null || gameDtos.Count == 0)
            {
                await DisplayAlert("Nothing found",
                    "No games found in the JSON.", "OK");
                return;
            }

            _importGroups.Clear();
            int totalActivities = 0;

            foreach (var gameDto in gameDtos
                .OrderBy(g => g.DisplayName,
                    StringComparer.OrdinalIgnoreCase))
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

                var group = new GameImportGroup { Game = gameDto };
                foreach (var act in gameDto.Activities
                    .OrderBy(a => a.Name,
                        StringComparer.OrdinalIgnoreCase))
                {
                    bool isDupe = existingNames.Contains(act.Name.Trim());
                    group.Activities.Add((act, !isDupe));
                    totalActivities++;
                }
                _importGroups.Add(group);
            }

            RenderImportGroups();

            _importStatusLabel.Text =
                $"{gameDtos.Count} games, {totalActivities} activities. " +
                "Duplicates are unchecked. Review and tap Import Selected.";
            _importStatusLabel.IsVisible = true;
            _importConfirmBtn.IsVisible = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Parse error",
                $"Could not read JSON: {ex.Message}", "OK");
        }
    }

    private void RenderImportGroups()
    {
        _importContainer.Children.Clear();

        for (int gi = 0; gi < _importGroups.Count; gi++)
        {
            int capturedGi = gi;
            var group = _importGroups[gi];

            var gameHeader = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 8,
                Margin = new Thickness(0, 4, 0, 2)
            };

            gameHeader.Add(new Label
            {
                Text = group.Game.DisplayName,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1565C0"),
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);

            var selAllBtn = new Button
            {
                Text = "All",
                BackgroundColor = Color.FromArgb("#E3F2FD"),
                TextColor = Color.FromArgb("#1565C0"),
                CornerRadius = 4,
                FontSize = 11,
                HeightRequest = 26,
                Padding = new Thickness(8, 0)
            };
            selAllBtn.Clicked += (_, _) =>
            {
                var g = _importGroups[capturedGi];
                _importGroups[capturedGi] = new GameImportGroup
                {
                    Game = g.Game,
                    Activities = g.Activities
                        .Select(x => (x.Activity, true)).ToList()
                };
                RenderImportGroups();
            };
            gameHeader.Add(selAllBtn, 1, 0);

            var selNoneBtn = new Button
            {
                Text = "None",
                BackgroundColor = Color.FromArgb("#ECEFF1"),
                TextColor = Color.FromArgb("#37474F"),
                CornerRadius = 4,
                FontSize = 11,
                HeightRequest = 26,
                Padding = new Thickness(8, 0)
            };
            selNoneBtn.Clicked += (_, _) =>
            {
                var g = _importGroups[capturedGi];
                _importGroups[capturedGi] = new GameImportGroup
                {
                    Game = g.Game,
                    Activities = g.Activities
                        .Select(x => (x.Activity, false)).ToList()
                };
                RenderImportGroups();
            };
            gameHeader.Add(selNoneBtn, 2, 0);

            _importContainer.Children.Add(gameHeader);

            var actStack = new VerticalStackLayout
            {
                Spacing = 4,
                Margin = new Thickness(12, 0, 0, 8)
            };

            for (int ai = 0; ai < group.Activities.Count; ai++)
            {
                int capturedAi = ai;
                var (act, selected) = group.Activities[ai];

                var row = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(new GridLength(32)),
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    ColumnSpacing = 6
                };

                var cb = new CheckBox { IsChecked = selected };
                cb.CheckedChanged += (_, e) =>
                {
                    var g = _importGroups[capturedGi];
                    var acts = g.Activities.ToList();
                    acts[capturedAi] = (acts[capturedAi].Activity, e.Value);
                    g.Activities = acts;
                };
                row.Add(cb, 0, 0);

                row.Add(new Label
                {
                    Text = act.Name,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#222"),
                    VerticalOptions = LayoutOptions.Center,
                    LineBreakMode = LineBreakMode.TailTruncation
                }, 1, 0);

                row.Add(new Label
                {
                    Text = $"+{act.ExpGain}",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#2E7D32"),
                    VerticalOptions = LayoutOptions.Center
                }, 2, 0);

                row.Add(new Label
                {
                    Text = !selected ? "⚠ dupe" : "",
                    FontSize = 10,
                    TextColor = Color.FromArgb("#E65100"),
                    VerticalOptions = LayoutOptions.Center
                }, 3, 0);

                actStack.Children.Add(row);
            }

            _importContainer.Children.Add(actStack);
        }
    }

    private async Task ImportSelectedAsync()
    {
        int imported = 0;
        int skipped = 0;

        foreach (var group in _importGroups)
        {
            var selectedActs = group.Activities
                .Where(x => x.Selected)
                .Select(x => x.Activity)
                .ToList();

            foreach (var act in selectedActs)
            {
                try
                {
                    var newActivity = new Activity
                    {
                        Username = _auth.CurrentUsername,
                        Game = group.Game.GameId,
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

        await DisplayAlert("Import Complete",
            $"{imported} activit{(imported == 1 ? "y" : "ies")} imported" +
            (skipped > 0 ? $", {skipped} failed." : "."),
            "OK");

        _importGroups.Clear();
        _importContainer.Children.Clear();
        _importStatusLabel.IsVisible = false;
        _importConfirmBtn.IsVisible = false;
    }
}
