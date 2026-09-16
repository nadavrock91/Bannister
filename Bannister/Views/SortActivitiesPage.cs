using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class SortActivitiesPage : ContentPage
{
    private readonly ActivityService _activityService;
    private readonly GameService _gameService;
    private readonly AuthService _auth;

    private List<Game> _games = new();
    private List<Activity> _allActivities = new();
    private int? _viewFilter = null; // null=all 1=public 0=private

    private VerticalStackLayout _contentContainer = null!;
    private Button _filterAllBtn = null!;
    private Button _filterPublicBtn = null!;
    private Button _filterPrivateBtn = null!;

    public SortActivitiesPage(
        ActivityService activityService,
        GameService gameService,
        AuthService auth)
    {
        _activityService = activityService;
        _gameService = gameService;
        _auth = auth;
        Title = "Sort Activities";
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
            Text = " Sort Activities",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        stack.Children.Add(new Label
        {
            Text = "Set each activity to Public, Private, or Both. " +
                   "Use the filter to narrow the view.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        // Legend
        var legendRow = new HorizontalStackLayout { Spacing = 16 };
        legendRow.Children.Add(MakeLegend(
            " Public", "#1565C0"));
        legendRow.Children.Add(MakeLegend(
            " Private", "#6A0DAD"));
        legendRow.Children.Add(MakeLegend(
            " Both", "#2E7D32"));
        stack.Children.Add(legendRow);

        // Filter row
        var filterRow = new HorizontalStackLayout { Spacing = 8 };

        _filterAllBtn = MakeFilterBtn("Show All", true, "#555555");
        _filterAllBtn.Clicked += (_, _) =>
        {
            _viewFilter = null;
            UpdateFilterButtons();
            RenderContent();
        };
        filterRow.Children.Add(_filterAllBtn);

        _filterPublicBtn = MakeFilterBtn(
            " Public", false, "#1565C0");
        _filterPublicBtn.Clicked += (_, _) =>
        {
            _viewFilter = 1;
            UpdateFilterButtons();
            RenderContent();
        };
        filterRow.Children.Add(_filterPublicBtn);

        _filterPrivateBtn = MakeFilterBtn(
            " Private", false, "#6A0DAD");
        _filterPrivateBtn.Clicked += (_, _) =>
        {
            _viewFilter = 0;
            UpdateFilterButtons();
            RenderContent();
        };
        filterRow.Children.Add(_filterPrivateBtn);

        stack.Children.Add(filterRow);

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
            FontSize = 12,
            HeightRequest = 32,
            CornerRadius = 6,
            Padding = new Thickness(10, 0),
            BackgroundColor = active
                ? Color.FromArgb(activeHex)
                : Color.FromArgb("#ECEFF1"),
            TextColor = active
                ? Colors.White
                : Color.FromArgb("#37474F")
        };

    private void UpdateFilterButtons()
    {
        StyleFilterBtn(_filterAllBtn,
            _viewFilter == null, "#555555");
        StyleFilterBtn(_filterPublicBtn,
            _viewFilter == 1, "#1565C0");
        StyleFilterBtn(_filterPrivateBtn,
            _viewFilter == 0, "#6A0DAD");
    }

    private static void StyleFilterBtn(
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
        _allActivities = await _activityService
            .GetActivitiesAsync(_auth.CurrentUsername);

        // Run migration for any unmigrated activities
        var toMigrate = _allActivities
            .Where(a => !a.VisibilityMigrated)
            .ToList();
        foreach (var act in toMigrate)
        {
            act.ActivityVisibility = act.IsPublic ? 1 : 0;
            act.VisibilityMigrated = true;
            await _activityService.UpdateActivityAsync(act);
        }
        if (toMigrate.Count > 0)
            _allActivities = await _activityService
                .GetActivitiesAsync(_auth.CurrentUsername);

        RenderContent();
    }

    private void RenderContent()
    {
        _contentContainer.Children.Clear();

        var sortedGames = _games
            .OrderBy(g => g.DisplayName,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        bool anyShown = false;
        foreach (var game in sortedGames)
        {
            var acts = _allActivities
                .Where(a => string.Equals(
                    a.Game, game.GameId,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (_viewFilter == 1)
                acts = acts.Where(a =>
                    a.ActivityVisibility == 1 ||
                    a.ActivityVisibility == 2).ToList();
            else if (_viewFilter == 0)
                acts = acts.Where(a =>
                    a.ActivityVisibility == 0 ||
                    a.ActivityVisibility == 2).ToList();

            if (acts.Count == 0) continue;
            anyShown = true;

            _contentContainer.Children.Add(new Label
            {
                Text = game.DisplayName,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1565C0"),
                Margin = new Thickness(0, 10, 0, 2)
            });

            foreach (var act in acts)
                _contentContainer.Children.Add(
                    BuildActivityRow(act));
        }

        if (!anyShown)
            _contentContainer.Children.Add(new Label
            {
                Text = "No activities match the current filter.",
                FontSize = 13,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic
            });
    }

    private View BuildActivityRow(Activity activity)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(38)),
                new ColumnDefinition(new GridLength(38)),
                new ColumnDefinition(new GridLength(38))
            },
            ColumnSpacing = 4,
            Padding = new Thickness(10, 6),
            BackgroundColor = Colors.White
        };

        row.Add(new Label
        {
            Text = activity.Name,
            FontSize = 13,
            TextColor = Color.FromArgb("#222"),
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        }, 0, 0);

        var pubBtn = VisBtn("",
            activity.ActivityVisibility == 1,
            "#1565C0", "#E3F2FD", "#1565C0");
        pubBtn.Clicked += async (_, _) =>
        {
            activity.ActivityVisibility = 1;
            activity.VisibilityMigrated = true;
            await _activityService.UpdateActivityAsync(activity);
            RenderContent();
        };
        row.Add(pubBtn, 1, 0);

        var privBtn = VisBtn("",
            activity.ActivityVisibility == 0,
            "#6A0DAD", "#F3E5F5", "#6A0DAD");
        privBtn.Clicked += async (_, _) =>
        {
            activity.ActivityVisibility = 0;
            activity.VisibilityMigrated = true;
            await _activityService.UpdateActivityAsync(activity);
            RenderContent();
        };
        row.Add(privBtn, 2, 0);

        var bothBtn = VisBtn("",
            activity.ActivityVisibility == 2,
            "#2E7D32", "#E8F5E9", "#2E7D32");
        bothBtn.Clicked += async (_, _) =>
        {
            activity.ActivityVisibility = 2;
            activity.VisibilityMigrated = true;
            await _activityService.UpdateActivityAsync(activity);
            RenderContent();
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
            FontSize = 13,
            HeightRequest = 34,
            WidthRequest = 34,
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
