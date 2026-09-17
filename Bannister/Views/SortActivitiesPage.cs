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

    // null=show all, 1=public, 0=private, 2=both
    private int? _viewFilter = null;

    // Pagination
    private int _currentPage = 0;
    private const int PageSize = 1; // one game per page
    private List<(Game Game, List<Activity> Activities)>
        _pagedGroups = new();

    private VerticalStackLayout _contentContainer = null!;
    private Button _filterAllBtn = null!;
    private Button _filterPublicBtn = null!;
    private Button _filterPrivateBtn = null!;
    private Button _filterBothBtn = null!;
    private Label _pageLabel = null!;
    private Button _prevBtn = null!;
    private Button _nextBtn = null!;
    private bool _isSaving = false;

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
            Text = "Tap  Public,  Private, or  Both per activity. " +
                   "Highlighted button = current setting.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        // Filter row
        var filterRow = new HorizontalStackLayout { Spacing = 6 };

        _filterAllBtn = MakeFilterBtn("Show All", true, "#555555");
        _filterAllBtn.Clicked += (_, _) =>
        {
            _viewFilter = null;
            _currentPage = 0;
            UpdateFilterButtons();
            BuildPagedGroups();
            RenderCurrentPage();
        };
        filterRow.Children.Add(_filterAllBtn);

        _filterPublicBtn = MakeFilterBtn(" Public", false, "#1565C0");
        _filterPublicBtn.Clicked += (_, _) =>
        {
            _viewFilter = 1;
            _currentPage = 0;
            UpdateFilterButtons();
            BuildPagedGroups();
            RenderCurrentPage();
        };
        filterRow.Children.Add(_filterPublicBtn);

        _filterPrivateBtn = MakeFilterBtn(" Private", false, "#6A0DAD");
        _filterPrivateBtn.Clicked += (_, _) =>
        {
            _viewFilter = 0;
            _currentPage = 0;
            UpdateFilterButtons();
            BuildPagedGroups();
            RenderCurrentPage();
        };
        filterRow.Children.Add(_filterPrivateBtn);

        _filterBothBtn = MakeFilterBtn(" Both", false, "#2E7D32");
        _filterBothBtn.Clicked += (_, _) =>
        {
            _viewFilter = 2;
            _currentPage = 0;
            UpdateFilterButtons();
            BuildPagedGroups();
            RenderCurrentPage();
        };
        filterRow.Children.Add(_filterBothBtn);

        stack.Children.Add(filterRow);

        // Pagination controls
        var navRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(44)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(44))
            },
            Margin = new Thickness(0, 4, 0, 0)
        };

        _prevBtn = new Button
        {
            Text = "◀",
            FontSize = 14,
            HeightRequest = 36,
            CornerRadius = 6,
            Padding = 0,
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0")
        };
        _prevBtn.Clicked += (_, _) =>
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                RenderCurrentPage();
            }
        };
        navRow.Add(_prevBtn, 0, 0);

        _pageLabel = new Label
        {
            Text = "",
            FontSize = 13,
            TextColor = Color.FromArgb("#444"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        navRow.Add(_pageLabel, 1, 0);

        _nextBtn = new Button
        {
            Text = "▶",
            FontSize = 14,
            HeightRequest = 36,
            CornerRadius = 6,
            Padding = 0,
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0")
        };
        _nextBtn.Clicked += (_, _) =>
        {
            if (_currentPage < _pagedGroups.Count - 1)
            {
                _currentPage++;
                RenderCurrentPage();
            }
        };
        navRow.Add(_nextBtn, 2, 0);

        stack.Children.Add(navRow);

        _contentContainer = new VerticalStackLayout { Spacing = 2 };
        stack.Children.Add(_contentContainer);

        Content = new ScrollView { Content = stack };
    }

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
        StyleBtn(_filterAllBtn,
            _viewFilter == null, "#555555");
        StyleBtn(_filterPublicBtn,
            _viewFilter == 1, "#1565C0");
        StyleBtn(_filterPrivateBtn,
            _viewFilter == 0, "#6A0DAD");
        StyleBtn(_filterBothBtn,
            _viewFilter == 2, "#2E7D32");
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
        _allActivities = await _activityService
            .GetActivitiesAsync(_auth.CurrentUsername);

        // One-time migration
        var toMigrate = _allActivities
            .Where(a => !a.VisibilityMigrated)
            .ToList();
        foreach (var act in toMigrate)
        {
            act.ActivityVisibility = act.IsPublic ? 1 : 0;
            act.VisibilityMigrated = true;
            act.VisibilityChangedAt = act.VisibilityChangedAt
                ?? DateTime.UtcNow;
            await _activityService.UpdateActivityAsync(act);
        }

        BuildPagedGroups();
        RenderCurrentPage();
    }

    private void BuildPagedGroups()
    {
        _pagedGroups = _games
            .OrderBy(g => g.DisplayName,
                StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var acts = _allActivities
                    .Where(a => string.Equals(
                        a.Game, g.GameId,
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
                else if (_viewFilter == 2)
                    acts = acts.Where(a =>
                        a.ActivityVisibility == 2).ToList();

                return (Game: g, Activities: acts);
            })
            .Where(x => x.Activities.Count > 0)
            .ToList();

        _currentPage = Math.Min(
            _currentPage, Math.Max(0, _pagedGroups.Count - 1));
    }

    private void RenderCurrentPage()
    {
        _contentContainer.Children.Clear();

        if (_pagedGroups.Count == 0)
        {
            _pageLabel.Text = "No activities";
            _prevBtn.IsEnabled = false;
            _nextBtn.IsEnabled = false;
            _contentContainer.Children.Add(new Label
            {
                Text = "No activities match the current filter.",
                FontSize = 13,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic
            });
            return;
        }

        var (game, acts) = _pagedGroups[_currentPage];

        _pageLabel.Text =
            $"{game.DisplayName} ({_currentPage + 1}" +
            $"/{_pagedGroups.Count})";
        _prevBtn.IsEnabled = _currentPage > 0;
        _nextBtn.IsEnabled =
            _currentPage < _pagedGroups.Count - 1;

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

        var setAllPubBtn = VisBtn("", false,
            "#1565C0", "#E3F2FD", "#1565C0");
        setAllPubBtn.Clicked += async (_, _) =>
        {
            if (_isSaving) return;
            _isSaving = true;
            try
            {
                foreach (var act in acts)
                {
                    act.ActivityVisibility = 1;
                    act.VisibilityMigrated = true;
                    act.VisibilityChangedAt = DateTime.UtcNow;
                    await _activityService.UpdateActivityAsync(act);
                }
                RenderCurrentPage();
            }
            finally { _isSaving = false; }
        };
        setAllRow.Add(setAllPubBtn, 1, 0);

        var setAllPrivBtn = VisBtn("", false,
            "#6A0DAD", "#F3E5F5", "#6A0DAD");
        setAllPrivBtn.Clicked += async (_, _) =>
        {
            if (_isSaving) return;
            _isSaving = true;
            try
            {
                foreach (var act in acts)
                {
                    act.ActivityVisibility = 0;
                    act.VisibilityMigrated = true;
                    act.VisibilityChangedAt = DateTime.UtcNow;
                    await _activityService.UpdateActivityAsync(act);
                }
                RenderCurrentPage();
            }
            finally { _isSaving = false; }
        };
        setAllRow.Add(setAllPrivBtn, 2, 0);

        var setAllBothBtn = VisBtn("", false,
            "#2E7D32", "#E8F5E9", "#2E7D32");
        setAllBothBtn.Clicked += async (_, _) =>
        {
            if (_isSaving) return;
            _isSaving = true;
            try
            {
                foreach (var act in acts)
                {
                    act.ActivityVisibility = 2;
                    act.VisibilityMigrated = true;
                    act.VisibilityChangedAt = DateTime.UtcNow;
                    await _activityService.UpdateActivityAsync(act);
                }
                RenderCurrentPage();
            }
            finally { _isSaving = false; }
        };
        setAllRow.Add(setAllBothBtn, 3, 0);

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

        foreach (var act in acts)
            _contentContainer.Children.Add(
                BuildActivityRow(act));
    }

    private View BuildActivityRow(Activity activity)
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
            Text = activity.Name,
            FontSize = 13,
            TextColor = Color.FromArgb("#222"),
            LineBreakMode = LineBreakMode.TailTruncation
        });
        if (activity.VisibilityChangedAt.HasValue)
            nameStack.Children.Add(new Label
            {
                Text = activity.VisibilityChangedAt.Value
                    .ToLocalTime().ToString("dd MMM yyyy"),
                FontSize = 10,
                TextColor = Color.FromArgb("#999")
            });
        row.Add(nameStack, 0, 0);

        //  Public
        var pubBtn = VisBtn("",
            activity.ActivityVisibility == 1,
            "#1565C0", "#E3F2FD", "#1565C0");
        pubBtn.Clicked += async (_, _) =>
        {
            if (_isSaving) return;
            _isSaving = true;
            try
            {
                activity.ActivityVisibility = 1;
                activity.VisibilityMigrated = true;
                activity.VisibilityChangedAt = DateTime.UtcNow;
                await _activityService
                    .UpdateActivityAsync(activity);
                RenderCurrentPage();
            }
            finally { _isSaving = false; }
        };
        row.Add(pubBtn, 1, 0);

        //  Private
        var privBtn = VisBtn("",
            activity.ActivityVisibility == 0,
            "#6A0DAD", "#F3E5F5", "#6A0DAD");
        privBtn.Clicked += async (_, _) =>
        {
            if (_isSaving) return;
            _isSaving = true;
            try
            {
                activity.ActivityVisibility = 0;
                activity.VisibilityMigrated = true;
                activity.VisibilityChangedAt = DateTime.UtcNow;
                await _activityService
                    .UpdateActivityAsync(activity);
                RenderCurrentPage();
            }
            finally { _isSaving = false; }
        };
        row.Add(privBtn, 2, 0);

        //  Both
        var bothBtn = VisBtn("",
            activity.ActivityVisibility == 2,
            "#2E7D32", "#E8F5E9", "#2E7D32");
        bothBtn.Clicked += async (_, _) =>
        {
            if (_isSaving) return;
            _isSaving = true;
            try
            {
                activity.ActivityVisibility = 2;
                activity.VisibilityMigrated = true;
                activity.VisibilityChangedAt = DateTime.UtcNow;
                await _activityService
                    .UpdateActivityAsync(activity);
                RenderCurrentPage();
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
