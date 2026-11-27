using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class SortGamesPage : Bannister.Views.ModeThemedPage
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

        stack.Children.Add((new Label
        {
            Text = " Sort Games",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        stack.Children.Add((new Label
        {
            Text = "Set each game to Public, Private, or Both. " +
                   "Controls which games appear in each display mode.",
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        // Legend
        var legendRow = new HorizontalStackLayout { Spacing = 16 };
        legendRow.Children.Add(MakeLegend(" Public", ThemeKey.InfoText));
        legendRow.Children.Add(MakeLegend(" Private", ThemeKey.HighlightVioletText));
        legendRow.Children.Add(MakeLegend(" Both", ThemeKey.SuccessText));
        stack.Children.Add(legendRow);

        // Filter row
        var filterRow = new HorizontalStackLayout { Spacing = 6 };

        _filterAllBtn = MakeFilterBtn("Show All", true, ThemeKey.CardText2);
        _filterAllBtn.Clicked += (_, _) =>
        {
            _viewFilter = null;
            UpdateFilterButtons();
            RenderContent();
        };
        filterRow.Children.Add(_filterAllBtn);

        _filterPublicBtn = MakeFilterBtn(" Public", false, ThemeKey.InfoText);
        _filterPublicBtn.Clicked += (_, _) =>
        {
            _viewFilter = 1;
            UpdateFilterButtons();
            RenderContent();
        };
        filterRow.Children.Add(_filterPublicBtn);

        _filterPrivateBtn = MakeFilterBtn(" Private", false, ThemeKey.HighlightVioletText);
        _filterPrivateBtn.Clicked += (_, _) =>
        {
            _viewFilter = 0;
            UpdateFilterButtons();
            RenderContent();
        };
        filterRow.Children.Add(_filterPrivateBtn);

        _filterBothBtn = MakeFilterBtn(" Both", false, ThemeKey.SuccessText);
        _filterBothBtn.Clicked += (_, _) =>
        {
            _viewFilter = 2;
            UpdateFilterButtons();
            RenderContent();
        };
        filterRow.Children.Add(_filterBothBtn);

        stack.Children.Add(filterRow);

        var autoSortBtn = (new Button
        {
            Text = " Auto-Sort from Activities",
            CornerRadius = 8,
            FontSize = 13,
            HeightRequest = 40,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessBackground).ThemeColor(Button.TextColorProperty, ThemeKey.SuccessText);
        autoSortBtn.Clicked += async (_, _) =>
            await AutoSortFromActivitiesAsync();
        stack.Children.Add(autoSortBtn);

        _contentContainer = new VerticalStackLayout { Spacing = 2 };
        stack.Children.Add(_contentContainer);

        Content = new ScrollView { Content = stack };
    }

    private static Label MakeLegend(string text, ThemeKey hex)
        => (new Label
        {
            Text = text,
            FontSize = 11
        }).ThemeColor(Label.TextColorProperty, hex, ThemeKey.PageText3, classifyPageText: true);

    private static Button MakeFilterBtn(
        string text, bool active, ThemeKey activeHex)
        => (new Button
        {
            Text = text,
            FontSize = 11,
            HeightRequest = 30,
            CornerRadius = 6,
            Padding = new Thickness(8, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, active ? activeHex : ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, active ? ThemeKey.ButtonPrimaryText : ThemeKey.NeutralButtonText);

    private void UpdateFilterButtons()
    {
        StyleBtn(_filterAllBtn, _viewFilter == null, ThemeKey.CardText2);
        StyleBtn(_filterPublicBtn, _viewFilter == 1, ThemeKey.InfoText);
        StyleBtn(_filterPrivateBtn, _viewFilter == 0, ThemeKey.HighlightVioletText);
        StyleBtn(_filterBothBtn, _viewFilter == 2, ThemeKey.SuccessText);
    }

    private static void StyleBtn(
        Button btn, bool active, ThemeKey activeHex)
    {
        btn.ThemeColor(VisualElement.BackgroundColorProperty, active ? activeHex : ThemeKey.NeutralButtonBackground);
        btn.ThemeColor(Button.TextColorProperty, active ? ThemeKey.ButtonPrimaryText : ThemeKey.NeutralButtonText);
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
            _contentContainer.Children.Add((new Label
            {
                Text = "No games match the current filter.",
                FontSize = 13,
                FontAttributes = FontAttributes.Italic
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
            return;
        }

        // Set All row
        var setAllRow = (new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(40)),
                new ColumnDefinition(new GridLength(40)),
                new ColumnDefinition(new GridLength(40))
            },
            ColumnSpacing = 4,
            Padding = new Thickness(10, 6)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);
        setAllRow.Add((new Label
        {
            Text = "Set all to →",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true), 0, 0);

        var setAllPub = VisBtn("", false,
            ThemeKey.InfoText, ThemeKey.RowSelected, ThemeKey.InfoText);
        setAllPub.Clicked += async (_, _) =>
            await SetAllVisibilityAsync(filtered, 1);
        setAllRow.Add(setAllPub, 1, 0);

        var setAllPriv = VisBtn("", false,
            ThemeKey.HighlightVioletText, ThemeKey.HighlightViolet, ThemeKey.HighlightVioletText);
        setAllPriv.Clicked += async (_, _) =>
            await SetAllVisibilityAsync(filtered, 0);
        setAllRow.Add(setAllPriv, 2, 0);

        var setAllBoth = VisBtn("", false,
            ThemeKey.SuccessText, ThemeKey.SuccessBackground, ThemeKey.SuccessText);
        setAllBoth.Clicked += async (_, _) =>
            await SetAllVisibilityAsync(filtered, 2);
        setAllRow.Add(setAllBoth, 3, 0);

        _contentContainer.Children.Add((new Frame
        {
            Content = setAllRow,
            Padding = 0,
            CornerRadius = 6,
            HasShadow = false,
            Margin = new Thickness(0, 0, 0, 6)
        }).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface));

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
        var row = (new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(40)),
                new ColumnDefinition(new GridLength(40)),
                new ColumnDefinition(new GridLength(40))
            },
            ColumnSpacing = 4,
            Padding = new Thickness(10, 8)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var nameStack = new VerticalStackLayout { Spacing = 1 };
        nameStack.Children.Add((new Label
        {
            Text = game.DisplayName,
            FontSize = 14,
            LineBreakMode = LineBreakMode.TailTruncation
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        if (game.VisibilityChangedAt.HasValue)
            nameStack.Children.Add((new Label
            {
                Text = game.VisibilityChangedAt.Value
                    .ToLocalTime().ToString("dd MMM yyyy"),
                FontSize = 10
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        row.Add(nameStack, 0, 0);

        var pubBtn = VisBtn("",
            game.GameVisibility == 1,
            ThemeKey.InfoText, ThemeKey.RowSelected, ThemeKey.InfoText);
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
            ThemeKey.HighlightVioletText, ThemeKey.HighlightViolet, ThemeKey.HighlightVioletText);
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
            ThemeKey.SuccessText, ThemeKey.SuccessBackground, ThemeKey.SuccessText);
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

        return (new Frame
        {
            Content = row,
            Padding = 0,
            CornerRadius = 6,
            HasShadow = false,
            Margin = new Thickness(0, 1)
        }).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);
    }

    private static Button VisBtn(string text, bool active,
        ThemeKey activeBg, ThemeKey inactiveBg, ThemeKey inactiveFg)
        => (new Button
        {
            Text = text,
            FontSize = 14,
            HeightRequest = 36,
            WidthRequest = 36,
            CornerRadius = 6,
            Padding = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, active ? activeBg : inactiveBg).ThemeColor(Button.TextColorProperty, active ? ThemeKey.ButtonPrimaryText : inactiveFg);
}
