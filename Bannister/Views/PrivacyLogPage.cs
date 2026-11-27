using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class PrivacyLogPage : Bannister.Views.ModeThemedPage
{
    private readonly ActivityService _activityService;
    private readonly GameService _gameService;
    private readonly AuthService _auth;

    // Time filter: null=all, 0=today, 1=yesterday,
    // 7=last week, 30=last month
    private int? _timeFilter = null;

    private VerticalStackLayout _contentContainer = null!;
    private Button _btnAll = null!;
    private Button _btnToday = null!;
    private Button _btnYesterday = null!;
    private Button _btnWeek = null!;
    private Button _btnMonth = null!;

    public PrivacyLogPage(
        ActivityService activityService,
        GameService gameService,
        AuthService auth)
    {
        _activityService = activityService;
        _gameService = gameService;
        _auth = auth;
        Title = "Privacy Log";

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAndRenderAsync();
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
            Text = " Privacy Log",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true));
        stack.Children.Add((new Label
        {
            Text = "Recent visibility changes across activities " +
                   "and games, sorted by most recent.",
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        // Time filter row
        var filterRow = new HorizontalStackLayout
        {
            Spacing = 6,
            Margin = new Thickness(0, 4, 0, 0)
        };

        _btnAll = MakeFilterBtn("All", true, ThemeKey.NeutralHoverButton);
        _btnAll.Clicked += async (_, _) =>
        {
            _timeFilter = null;
            UpdateFilterButtons();
            await LoadAndRenderAsync();
        };
        filterRow.Children.Add(_btnAll);

        _btnToday = MakeFilterBtn("Today", false, ThemeKey.ButtonPrimaryBackground);
        _btnToday.Clicked += async (_, _) =>
        {
            _timeFilter = 0;
            UpdateFilterButtons();
            await LoadAndRenderAsync();
        };
        filterRow.Children.Add(_btnToday);

        _btnYesterday = MakeFilterBtn("Yesterday", false, ThemeKey.ButtonPrimaryBackground);
        _btnYesterday.Clicked += async (_, _) =>
        {
            _timeFilter = 1;
            UpdateFilterButtons();
            await LoadAndRenderAsync();
        };
        filterRow.Children.Add(_btnYesterday);

        _btnWeek = MakeFilterBtn("7 Days", false, ThemeKey.ButtonPrimaryBackground);
        _btnWeek.Clicked += async (_, _) =>
        {
            _timeFilter = 7;
            UpdateFilterButtons();
            await LoadAndRenderAsync();
        };
        filterRow.Children.Add(_btnWeek);

        _btnMonth = MakeFilterBtn("30 Days", false, ThemeKey.ButtonPrimaryBackground);
        _btnMonth.Clicked += async (_, _) =>
        {
            _timeFilter = 30;
            UpdateFilterButtons();
            await LoadAndRenderAsync();
        };
        filterRow.Children.Add(_btnMonth);

        stack.Children.Add(filterRow);

        _contentContainer = new VerticalStackLayout { Spacing = 8 };
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
        }).ThemeColor(VisualElement.BackgroundColorProperty, active ? activeHex : ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, active ? ThemeKey.OnColorText : ThemeKey.NeutralButtonText);

    private void UpdateFilterButtons()
    {
        StyleBtn(_btnAll, _timeFilter == null, ThemeKey.NeutralHoverButton);
        StyleBtn(_btnToday, _timeFilter == 0, ThemeKey.ButtonPrimaryBackground);
        StyleBtn(_btnYesterday, _timeFilter == 1, ThemeKey.ButtonPrimaryBackground);
        StyleBtn(_btnWeek, _timeFilter == 7, ThemeKey.ButtonPrimaryBackground);
        StyleBtn(_btnMonth, _timeFilter == 30, ThemeKey.ButtonPrimaryBackground);
    }

    private static void StyleBtn(
        Button btn, bool active, ThemeKey activeHex)
    {
        btn.ThemeColor(VisualElement.BackgroundColorProperty, active ? activeHex : ThemeKey.NeutralButtonBackground);
        btn.ThemeColor(Button.TextColorProperty, active ? ThemeKey.OnColorText : ThemeKey.NeutralButtonText);
    }

    private async Task LoadAndRenderAsync()
    {
        _contentContainer.Children.Clear();

        var now = DateTime.UtcNow;
        var cutoff = _timeFilter switch
        {
            0 => now.Date, // today
            1 => now.Date.AddDays(-1), // yesterday start
            7 => now.AddDays(-7),
            30 => now.AddDays(-30),
            _ => (DateTime?)null
        };
        var cutoffEnd = _timeFilter == 1
            ? now.Date // yesterday end = today start
            : (DateTime?)null;

        // Load activities
        var activities = await _activityService
            .GetActivitiesAsync(_auth.CurrentUsername);
        var actChanges = activities
            .Where(a => a.VisibilityChangedAt.HasValue)
            .Where(a => cutoff == null ||
                a.VisibilityChangedAt!.Value >= cutoff.Value)
            .Where(a => cutoffEnd == null ||
                a.VisibilityChangedAt!.Value < cutoffEnd.Value)
            .OrderByDescending(a => a.VisibilityChangedAt)
            .ToList();

        // Load games
        var games = await _gameService
            .GetGamesAsync(_auth.CurrentUsername);
        var gameChanges = games
            .Where(g => g.VisibilityChangedAt.HasValue)
            .Where(g => cutoff == null ||
                g.VisibilityChangedAt!.Value >= cutoff.Value)
            .Where(g => cutoffEnd == null ||
                g.VisibilityChangedAt!.Value < cutoffEnd.Value)
            .OrderByDescending(g => g.VisibilityChangedAt)
            .ToList();

        if (actChanges.Count == 0 && gameChanges.Count == 0)
        {
            _contentContainer.Children.Add((new Label
            {
                Text = "No visibility changes found " +
                       "for the selected period.",
                FontSize = 13,
                FontAttributes = FontAttributes.Italic
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
            return;
        }

        // Most recent change overall
        var mostRecent = actChanges
            .Select(a => a.VisibilityChangedAt!.Value)
            .Concat(gameChanges
                .Select(g => g.VisibilityChangedAt!.Value))
            .Max();
        int daysSince = (int)(now - mostRecent).TotalDays;

        _contentContainer.Children.Add((new Label
        {
            Text = daysSince == 0
                ? "Most recent change: today"
                : daysSince == 1
                    ? "Most recent change: yesterday"
                    : $"Most recent change: {daysSince} days ago",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText1, classifyPageText: true));

        // Games section
        if (gameChanges.Count > 0)
        {
            _contentContainer.Children.Add((new Label
            {
                Text = $" Games ({gameChanges.Count})",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 8, 0, 2)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true));

            foreach (var g in gameChanges)
                _contentContainer.Children.Add(
                    BuildLogRow(
                        g.DisplayName,
                        g.GameVisibility,
                        g.VisibilityChangedAt!.Value,
                        now));
        }

        // Activities section
        if (actChanges.Count > 0)
        {
            _contentContainer.Children.Add((new Label
            {
                Text = $"⚡ Activities ({actChanges.Count})",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 8, 0, 2)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true));

            foreach (var a in actChanges)
                _contentContainer.Children.Add(
                    BuildLogRow(
                        $"{a.Name} ({a.Game})",
                        a.ActivityVisibility,
                        a.VisibilityChangedAt!.Value,
                        now));
        }
    }

    private static View BuildLogRow(
        string name, int visibility,
        DateTime changedAt, DateTime now)
    {
        int days = (int)(now - changedAt).TotalDays;
        string when = days == 0 ? "Today"
            : days == 1 ? "Yesterday"
            : $"{days}d ago";

        string visLabel = visibility switch
        {
            1 => " Public",
            2 => " Both",
            _ => " Private"
        };
        ThemeKey visColor = visibility switch
        {
            1 => ThemeKey.InfoText,
            2 => ThemeKey.SuccessText,
            _ => ThemeKey.AccentPurpleStrongText
        };

        var row = (new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(70)),
                new ColumnDefinition(new GridLength(70))
            },
            ColumnSpacing = 8,
            Padding = new Thickness(12, 8)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var nameStack = new VerticalStackLayout { Spacing = 1 };
        nameStack.Children.Add((new Label
        {
            Text = name,
            FontSize = 13,
            LineBreakMode = LineBreakMode.TailTruncation
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true));
        nameStack.Children.Add((new Label
        {
            Text = changedAt.ToLocalTime()
                .ToString("dd MMM yyyy HH:mm"),
            FontSize = 10
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        row.Add(nameStack, 0, 0);

        row.Add((new Label
        {
            Text = visLabel,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, visColor, ThemeKey.PageText3, classifyPageText: true), 1, 0);

        row.Add((new Label
        {
            Text = when,
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true), 2, 0);

        return (new Frame
        {
            Content = row,
            Padding = 0,
            CornerRadius = 6,
            HasShadow = false,
            Margin = new Thickness(0, 1)
        }).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);
    }
}
