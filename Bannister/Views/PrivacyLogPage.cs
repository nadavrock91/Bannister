using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class PrivacyLogPage : ContentPage
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
        BackgroundColor = Color.FromArgb("#F5F5F5");
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

        stack.Children.Add(new Label
        {
            Text = " Privacy Log",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        stack.Children.Add(new Label
        {
            Text = "Recent visibility changes across activities " +
                   "and games, sorted by most recent.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        // Time filter row
        var filterRow = new HorizontalStackLayout
        {
            Spacing = 6,
            Margin = new Thickness(0, 4, 0, 0)
        };

        _btnAll = MakeFilterBtn("All", true, "#555555");
        _btnAll.Clicked += async (_, _) =>
        {
            _timeFilter = null;
            UpdateFilterButtons();
            await LoadAndRenderAsync();
        };
        filterRow.Children.Add(_btnAll);

        _btnToday = MakeFilterBtn("Today", false, "#1565C0");
        _btnToday.Clicked += async (_, _) =>
        {
            _timeFilter = 0;
            UpdateFilterButtons();
            await LoadAndRenderAsync();
        };
        filterRow.Children.Add(_btnToday);

        _btnYesterday = MakeFilterBtn("Yesterday", false, "#1565C0");
        _btnYesterday.Clicked += async (_, _) =>
        {
            _timeFilter = 1;
            UpdateFilterButtons();
            await LoadAndRenderAsync();
        };
        filterRow.Children.Add(_btnYesterday);

        _btnWeek = MakeFilterBtn("7 Days", false, "#1565C0");
        _btnWeek.Clicked += async (_, _) =>
        {
            _timeFilter = 7;
            UpdateFilterButtons();
            await LoadAndRenderAsync();
        };
        filterRow.Children.Add(_btnWeek);

        _btnMonth = MakeFilterBtn("30 Days", false, "#1565C0");
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
        StyleBtn(_btnAll, _timeFilter == null, "#555555");
        StyleBtn(_btnToday, _timeFilter == 0, "#1565C0");
        StyleBtn(_btnYesterday, _timeFilter == 1, "#1565C0");
        StyleBtn(_btnWeek, _timeFilter == 7, "#1565C0");
        StyleBtn(_btnMonth, _timeFilter == 30, "#1565C0");
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
            _contentContainer.Children.Add(new Label
            {
                Text = "No visibility changes found " +
                       "for the selected period.",
                FontSize = 13,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic
            });
            return;
        }

        // Most recent change overall
        var mostRecent = actChanges
            .Select(a => a.VisibilityChangedAt!.Value)
            .Concat(gameChanges
                .Select(g => g.VisibilityChangedAt!.Value))
            .Max();
        int daysSince = (int)(now - mostRecent).TotalDays;

        _contentContainer.Children.Add(new Label
        {
            Text = daysSince == 0
                ? "Most recent change: today"
                : daysSince == 1
                    ? "Most recent change: yesterday"
                    : $"Most recent change: {daysSince} days ago",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0")
        });

        // Games section
        if (gameChanges.Count > 0)
        {
            _contentContainer.Children.Add(new Label
            {
                Text = $" Games ({gameChanges.Count})",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#222"),
                Margin = new Thickness(0, 8, 0, 2)
            });

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
            _contentContainer.Children.Add(new Label
            {
                Text = $"⚡ Activities ({actChanges.Count})",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#222"),
                Margin = new Thickness(0, 8, 0, 2)
            });

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
        string visColor = visibility switch
        {
            1 => "#1565C0",
            2 => "#2E7D32",
            _ => "#6A0DAD"
        };

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(70)),
                new ColumnDefinition(new GridLength(70))
            },
            ColumnSpacing = 8,
            Padding = new Thickness(12, 8),
            BackgroundColor = Colors.White
        };

        var nameStack = new VerticalStackLayout { Spacing = 1 };
        nameStack.Children.Add(new Label
        {
            Text = name,
            FontSize = 13,
            TextColor = Color.FromArgb("#222"),
            LineBreakMode = LineBreakMode.TailTruncation
        });
        nameStack.Children.Add(new Label
        {
            Text = changedAt.ToLocalTime()
                .ToString("dd MMM yyyy HH:mm"),
            FontSize = 10,
            TextColor = Color.FromArgb("#999")
        });
        row.Add(nameStack, 0, 0);

        row.Add(new Label
        {
            Text = visLabel,
            FontSize = 12,
            TextColor = Color.FromArgb(visColor),
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        }, 1, 0);

        row.Add(new Label
        {
            Text = when,
            FontSize = 12,
            TextColor = Color.FromArgb("#888"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        }, 2, 0);

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
}
