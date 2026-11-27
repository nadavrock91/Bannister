using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class SortActivitiesPage : Bannister.Views.ModeThemedPage
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
            Text = " Sort Activities",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        stack.Children.Add((new Label
        {
            Text = "Tap  Public,  Private, or  Both per activity. " +
                   "Highlighted button = current setting.",
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        // Filter row
        var filterRow = new HorizontalStackLayout { Spacing = 6 };

        _filterAllBtn = MakeFilterBtn("Show All", true, ThemeKey.CardText2);
        _filterAllBtn.Clicked += (_, _) =>
        {
            _viewFilter = null;
            _currentPage = 0;
            UpdateFilterButtons();
            BuildPagedGroups();
            RenderCurrentPage();
        };
        filterRow.Children.Add(_filterAllBtn);

        _filterPublicBtn = MakeFilterBtn(" Public", false, ThemeKey.InfoText);
        _filterPublicBtn.Clicked += (_, _) =>
        {
            _viewFilter = 1;
            _currentPage = 0;
            UpdateFilterButtons();
            BuildPagedGroups();
            RenderCurrentPage();
        };
        filterRow.Children.Add(_filterPublicBtn);

        _filterPrivateBtn = MakeFilterBtn(" Private", false, ThemeKey.HighlightVioletText);
        _filterPrivateBtn.Clicked += (_, _) =>
        {
            _viewFilter = 0;
            _currentPage = 0;
            UpdateFilterButtons();
            BuildPagedGroups();
            RenderCurrentPage();
        };
        filterRow.Children.Add(_filterPrivateBtn);

        _filterBothBtn = MakeFilterBtn(" Both", false, ThemeKey.SuccessText);
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

        _prevBtn = (new Button
        {
            Text = "◀",
            FontSize = 14,
            HeightRequest = 36,
            CornerRadius = 6,
            Padding = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        _prevBtn.Clicked += (_, _) =>
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                RenderCurrentPage();
            }
        };
        navRow.Add(_prevBtn, 0, 0);

        _pageLabel = (new Label
        {
            Text = "",
            FontSize = 13,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true);
        navRow.Add(_pageLabel, 1, 0);

        _nextBtn = (new Button
        {
            Text = "▶",
            FontSize = 14,
            HeightRequest = 36,
            CornerRadius = 6,
            Padding = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
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
        StyleBtn(_filterAllBtn,
            _viewFilter == null, ThemeKey.CardText2);
        StyleBtn(_filterPublicBtn,
            _viewFilter == 1, ThemeKey.InfoText);
        StyleBtn(_filterPrivateBtn,
            _viewFilter == 0, ThemeKey.HighlightVioletText);
        StyleBtn(_filterBothBtn,
            _viewFilter == 2, ThemeKey.SuccessText);
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
            _contentContainer.Children.Add((new Label
            {
                Text = "No activities match the current filter.",
                FontSize = 13,
                FontAttributes = FontAttributes.Italic
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
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

        var setAllPubBtn = VisBtn("", false,
            ThemeKey.InfoText, ThemeKey.RowSelected, ThemeKey.InfoText);
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
            ThemeKey.HighlightVioletText, ThemeKey.HighlightViolet, ThemeKey.HighlightVioletText);
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
            ThemeKey.SuccessText, ThemeKey.SuccessBackground, ThemeKey.SuccessText);
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

        _contentContainer.Children.Add((new Frame
        {
            Content = setAllRow,
            Padding = 0,
            CornerRadius = 6,
            HasShadow = false,
            Margin = new Thickness(0, 0, 0, 6)
        }).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface));

        foreach (var act in acts)
            _contentContainer.Children.Add(
                BuildActivityRow(act));
    }

    private View BuildActivityRow(Activity activity)
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
            Text = activity.Name,
            FontSize = 13,
            LineBreakMode = LineBreakMode.TailTruncation
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        if (activity.VisibilityChangedAt.HasValue)
            nameStack.Children.Add((new Label
            {
                Text = activity.VisibilityChangedAt.Value
                    .ToLocalTime().ToString("dd MMM yyyy"),
                FontSize = 10
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        row.Add(nameStack, 0, 0);

        //  Public
        var pubBtn = VisBtn("",
            activity.ActivityVisibility == 1,
            ThemeKey.InfoText, ThemeKey.RowSelected, ThemeKey.InfoText);
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
            ThemeKey.HighlightVioletText, ThemeKey.HighlightViolet, ThemeKey.HighlightVioletText);
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
            ThemeKey.SuccessText, ThemeKey.SuccessBackground, ThemeKey.SuccessText);
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
