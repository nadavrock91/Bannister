using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class SortGamesPage : ContentPage
{
    private readonly GameService _gameService;
    private readonly ActivityService _activityService;
    private readonly AuthService _auth;

    private List<Game> _games = new();
    private int? _viewFilter = null;
    private bool _isSaving = false;

    private VerticalStackLayout _contentContainer = null!;
    private Button _filterAllBtn = null!;
    private Button _filterPublicBtn = null!;
    private Button _filterPrivateBtn = null!;
    private Button _filterBothBtn = null!;

    public SortGamesPage(
        GameService gameService,
        ActivityService activityService,
        AuthService auth)
    {
        _gameService = gameService;
        _activityService = activityService;
        _auth = auth;
        Title = "Sort Games";
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
        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 12
        };

        stack.Children.Add(new Label
        {
            Text = " Sort Games",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        stack.Children.Add(new Label
        {
            Text = "Set each game to Public, Private, or Both. " +
                   "Controls which games appear in each display mode.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        // Legend
        var legendRow = new HorizontalStackLayout { Spacing = 16 };
        legendRow.Children.Add(MakeLegend(" Public", "#1565C0"));
        legendRow.Children.Add(MakeLegend(" Private", "#6A0DAD"));
        legendRow.Children.Add(MakeLegend(" Both", "#2E7D32"));
        stack.Children.Add(legendRow);

        // Filter row
        var filterRow = new HorizontalStackLayout { Spacing = 6 };

        _filterAllBtn = MakeFilterBtn("Show All", true, "#555555");
        _filterAllBtn.Clicked += (_, _) =>
        {
            _viewFilter = null;
            UpdateFilterButtons();
            RenderContent();
        };
        filterRow.Children.Add(_filterAllBtn);

        _filterPublicBtn = MakeFilterBtn(" Public", false, "#1565C0");
        _filterPublicBtn.Clicked += (_, _) =>
        {
            _viewFilter = 1;
            UpdateFilterButtons();
            RenderContent();
        };
        filterRow.Children.Add(_filterPublicBtn);

        _filterPrivateBtn = MakeFilterBtn(" Private", false, "#6A0DAD");
        _filterPrivateBtn.Clicked += (_, _) =>
        {
            _viewFilter = 0;
            UpdateFilterButtons();
            RenderContent();
        };
        filterRow.Children.Add(_filterPrivateBtn);

        _filterBothBtn = MakeFilterBtn(" Both", false, "#2E7D32");
        _filterBothBtn.Clicked += (_, _) =>
        {
            _viewFilter = 2;
            UpdateFilterButtons();
            RenderContent();
        };
        filterRow.Children.Add(_filterBothBtn);

        stack.Children.Add(filterRow);

        var autoSortBtn = new Button
        {
            Text = " Auto-Sort from Activities",
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            TextColor = Color.FromArgb("#2E7D32"),
            CornerRadius = 8,
            FontSize = 13,
            HeightRequest = 40,
            FontAttributes = FontAttributes.Bold
        };
        autoSortBtn.Clicked += async (_, _) =>
            await AutoSortFromActivitiesAsync();
        stack.Children.Add(autoSortBtn);

        _contentContainer = new VerticalStackLayout { Spacing = 2 };
        stack.Children.Add(_contentContainer);

        Content = new ScrollView { Content = stack };
    }

    private static Label MakeLegend(string text, string hex)
        => new Label
        {
            Text = text,
            FontSize = 11,
            TextColor = Color.FromArgb(hex)
        };

    private static Button MakeFilterBtn(
        string text, bool active, string activeHex)
        => new Button
        {
            Text = text,
            FontSize = 11,
            HeightRequest = 30,
            CornerRadius = 6,
            Padding = new Thickness(8, 0),
            BackgroundColor = active
                ? Color.FromArgb(activeHex)
                : Color.FromArgb("#ECEFF1"),
            TextColor = active
                ? Colors.White
                : Color.FromArgb("#37474F")
        };

    private void UpdateFilterButtons()
    {
        StyleBtn(_filterAllBtn, _viewFilter == null, "#555555");
        StyleBtn(_filterPublicBtn, _viewFilter == 1, "#1565C0");
        StyleBtn(_filterPrivateBtn, _viewFilter == 0, "#6A0DAD");
        StyleBtn(_filterBothBtn, _viewFilter == 2, "#2E7D32");
    }

    private static void StyleBtn(
        Button btn, bool active, string activeHex)
    {
        btn.BackgroundColor = active
            ? Color.FromArgb(activeHex)
            : Color.FromArgb("#ECEFF1");
        btn.TextColor = active
            ? Colors.White
            : Color.FromArgb("#37474F");
    }

    private async Task LoadDataAsync()
    {
        _games = await _gameService.GetGamesAsync(
            _auth.CurrentUsername);

        // Seed timestamp for games that have never had one set
        var unseeded = _games
            .Where(g => !g.VisibilityChangedAt.HasValue)
            .ToList();
        foreach (var g in unseeded)
        {
            g.VisibilityChangedAt = DateTime.UtcNow;
            await _gameService.UpdateGameAsync(g);
        }

        RenderContent();
    }

    private async Task AutoSortFromActivitiesAsync()
    {
        if (_isSaving) return;
        bool confirm = await DisplayAlert(
            "Auto-Sort Games",
            "Sets each game's visibility based on its activities:\n\n" +
            "• Only public activities → Game = Public\n" +
            "• Only private activities → Game = Private\n" +
            "• Mix of both → Game = Both\n" +
            "• No activities → unchanged\n\nProceed?",
            "Auto-Sort", "Cancel");
        if (!confirm) return;
        _isSaving = true;
        try
        {
            int changed = 0;
            foreach (var game in _games)
            {
                var acts = await _activityService
                    .GetActivitiesAsync(
                        _auth.CurrentUsername, game.GameId);
                if (acts.Count == 0) continue;
                bool hasPublic = acts.Any(a =>
                    a.ActivityVisibility == 1 ||
                    a.ActivityVisibility == 2);
                bool hasPrivate = acts.Any(a =>
                    a.ActivityVisibility == 0 ||
                    a.ActivityVisibility == 2);
                int newVis = (hasPublic && hasPrivate) ? 2
                    : hasPublic ? 1 : 0;
                if (game.GameVisibility != newVis)
                {
                    game.GameVisibility = newVis;
                    game.VisibilityChangedAt = DateTime.UtcNow;
                    await _gameService.UpdateGameAsync(game);
                    changed++;
                }
            }
            await DisplayAlert("Done",
                $"{changed} game{(changed == 1 ? "" : "s")} updated.",
                "OK");
            RenderContent();
        }
        finally { _isSaving = false; }
    }

    private void RenderContent()
    {
        _contentContainer.Children.Clear();

        var filtered = _games
            .OrderBy(g => g.DisplayName,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (_viewFilter == 1)
            filtered = filtered.Where(g =>
                g.GameVisibility == 1 ||
                g.GameVisibility == 2).ToList();
        else if (_viewFilter == 0)
            filtered = filtered.Where(g =>
                g.GameVisibility == 0 ||
                g.GameVisibility == 2).ToList();
        else if (_viewFilter == 2)
            filtered = filtered.Where(g =>
                g.GameVisibility == 2).ToList();

        if (filtered.Count == 0)
        {
            _contentContainer.Children.Add(new Label
            {
                Text = "No games match the current filter.",
                FontSize = 13,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic
            });
            return;
        }

        // Set All row
        var setAllRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(40)),
                new ColumnDefinition(new GridLength(40)),
                new ColumnDefinition(new GridLength(40))
            },
            ColumnSpacing = 4,
            Padding = new Thickness(10, 6),
            BackgroundColor = Color.FromArgb("#ECEFF1")
        };
        setAllRow.Add(new Label
        {
            Text = "Set all to →",
            FontSize = 12,
            TextColor = Color.FromArgb("#555"),
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        var setAllPub = VisBtn("", false,
            "#1565C0", "#E3F2FD", "#1565C0");
        setAllPub.Clicked += async (_, _) =>
            await SetAllVisibilityAsync(filtered, 1);
        setAllRow.Add(setAllPub, 1, 0);

        var setAllPriv = VisBtn("", false,
            "#6A0DAD", "#F3E5F5", "#6A0DAD");
        setAllPriv.Clicked += async (_, _) =>
            await SetAllVisibilityAsync(filtered, 0);
        setAllRow.Add(setAllPriv, 2, 0);

        var setAllBoth = VisBtn("", false,
            "#2E7D32", "#E8F5E9", "#2E7D32");
        setAllBoth.Clicked += async (_, _) =>
            await SetAllVisibilityAsync(filtered, 2);
        setAllRow.Add(setAllBoth, 3, 0);

        _contentContainer.Children.Add(new Frame
        {
            Content = setAllRow,
            Padding = 0,
            CornerRadius = 6,
            HasShadow = false,
            BorderColor = Color.FromArgb("#BDBDBD"),
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            Margin = new Thickness(0, 0, 0, 6)
        });

        foreach (var game in filtered)
            _contentContainer.Children.Add(BuildGameRow(game));
    }

    private async Task SetAllVisibilityAsync(
        List<Game> games, int visibility)
    {
        if (_isSaving) return;
        _isSaving = true;
        try
        {
            foreach (var g in games)
            {
                g.GameVisibility = visibility;
                g.VisibilityChangedAt = DateTime.UtcNow;
                await _gameService.UpdateGameAsync(g);
            }
            RenderContent();
        }
        finally { _isSaving = false; }
    }

    private View BuildGameRow(Game game)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(40)),
                new ColumnDefinition(new GridLength(40)),
                new ColumnDefinition(new GridLength(40))
            },
            ColumnSpacing = 4,
            Padding = new Thickness(10, 8),
            BackgroundColor = Colors.White
        };

        var nameStack = new VerticalStackLayout { Spacing = 1 };
        nameStack.Children.Add(new Label
        {
            Text = game.DisplayName,
            FontSize = 14,
            TextColor = Color.FromArgb("#222"),
            LineBreakMode = LineBreakMode.TailTruncation
        });
        if (game.VisibilityChangedAt.HasValue)
            nameStack.Children.Add(new Label
            {
                Text = game.VisibilityChangedAt.Value
                    .ToLocalTime().ToString("dd MMM yyyy"),
                FontSize = 10,
                TextColor = Color.FromArgb("#999")
            });
        row.Add(nameStack, 0, 0);

        var pubBtn = VisBtn("",
            game.GameVisibility == 1,
            "#1565C0", "#E3F2FD", "#1565C0");
        pubBtn.Clicked += async (_, _) =>
        {
            if (_isSaving) return;
            _isSaving = true;
            try
            {
                game.GameVisibility = 1;
                game.VisibilityChangedAt = DateTime.UtcNow;
                await _gameService.UpdateGameAsync(game);
                RenderContent();
            }
            finally { _isSaving = false; }
        };
        row.Add(pubBtn, 1, 0);

        var privBtn = VisBtn("",
            game.GameVisibility == 0,
            "#6A0DAD", "#F3E5F5", "#6A0DAD");
        privBtn.Clicked += async (_, _) =>
        {
            if (_isSaving) return;
            _isSaving = true;
            try
            {
                game.GameVisibility = 0;
                game.VisibilityChangedAt = DateTime.UtcNow;
                await _gameService.UpdateGameAsync(game);
                RenderContent();
            }
            finally { _isSaving = false; }
        };
        row.Add(privBtn, 2, 0);

        var bothBtn = VisBtn("",
            game.GameVisibility == 2,
            "#2E7D32", "#E8F5E9", "#2E7D32");
        bothBtn.Clicked += async (_, _) =>
        {
            if (_isSaving) return;
            _isSaving = true;
            try
            {
                game.GameVisibility = 2;
                game.VisibilityChangedAt = DateTime.UtcNow;
                await _gameService.UpdateGameAsync(game);
                RenderContent();
            }
            finally { _isSaving = false; }
        };
        row.Add(bothBtn, 3, 0);

        return new Frame
        {
            Content = row,
            Padding = 0,
            CornerRadius = 6,
            HasShadow = false,
            BorderColor = Color.FromArgb("#E0E0E0"),
            BackgroundColor = Colors.White,
            Margin = new Thickness(0, 1)
        };
    }

    private static Button VisBtn(string text, bool active,
        string activeBg, string inactiveBg, string inactiveFg)
        => new Button
        {
            Text = text,
            FontSize = 14,
            HeightRequest = 36,
            WidthRequest = 36,
            CornerRadius = 6,
            Padding = 0,
            BackgroundColor = active
                ? Color.FromArgb(activeBg)
                : Color.FromArgb(inactiveBg),
            TextColor = active
                ? Colors.White
                : Color.FromArgb(inactiveFg)
        };
}
