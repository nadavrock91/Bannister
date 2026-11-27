using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class ZeroCountsPage : Bannister.Views.ModeThemedPage
{
    private enum ViewMode
    {
        ActiveByGame,
        ActiveAll,
        CompletedChronological,
        CompletedByGame
    }

    private readonly AuthService _auth;
    private readonly ActivityService _activities;
    private readonly GameService _games;

    private readonly Dictionary<ViewMode, Frame> _modeFrames = new();
    private readonly Dictionary<ViewMode, Label> _modeLabels = new();
    private readonly VerticalStackLayout _contentStack;
    private ViewMode _currentMode = ViewMode.ActiveByGame;

    public ZeroCountsPage(AuthService auth, ActivityService activities, GameService games)
    {
        _auth = auth;
        _activities = activities;
        _games = games;

        Title = "Zero Counts";


        var root = new VerticalStackLayout
        {
            Padding = new Thickness(18),
            Spacing = 16
        };

        root.Children.Add((new Label
        {
            Text = "Zero Counts",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1, classifyPageText: true));

        root.Children.Add((new Label
        {
            Text = "Things you've never done - yet.",
            FontSize = 14,
            FontAttributes = FontAttributes.Italic
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));

        var modeGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnSpacing = 8,
            RowSpacing = 8
        };

        AddModeButton(modeGrid, ViewMode.ActiveByGame, "Active by Game", 0, 0);
        AddModeButton(modeGrid, ViewMode.ActiveAll, "Active All", 1, 0);
        AddModeButton(modeGrid, ViewMode.CompletedChronological, "Completed Chronological", 0, 1);
        AddModeButton(modeGrid, ViewMode.CompletedByGame, "Completed by Game", 1, 1);
        root.Children.Add(modeGrid);

        _contentStack = new VerticalStackLayout { Spacing = 12 };
        root.Children.Add(new ScrollView { Content = _contentStack });

        Content = root;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private void AddModeButton(Grid grid, ViewMode mode, string text, int column, int row)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        var frame = new Frame
        {
            Padding = new Thickness(10, 8),
            CornerRadius = 8,
            HasShadow = false,
            Content = label
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            _currentMode = mode;
            await RefreshAsync();
        };
        frame.GestureRecognizers.Add(tap);

        _modeFrames[mode] = frame;
        _modeLabels[mode] = label;
        Grid.SetColumn(frame, column);
        Grid.SetRow(frame, row);
        grid.Children.Add(frame);
    }

    private async Task RefreshAsync()
    {
        UpdateModeButtons();
        _contentStack.Children.Clear();

        var activities = await _activities.GetActivitiesAsync(_auth.CurrentUsername);
        var gameNames = await GetGameNamesAsync();

        switch (_currentMode)
        {
            case ViewMode.ActiveByGame:
                RenderGrouped(
                    activities.Where(a => a.IsZeroCount),
                    gameNames,
                    completed: false,
                    emptyMessage: "No zero counts yet");
                break;
            case ViewMode.ActiveAll:
                RenderFlat(
                    activities.Where(a => a.IsZeroCount)
                        .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase),
                    gameNames,
                    showGame: true,
                    completed: false,
                    emptyMessage: "No zero counts yet");
                break;
            case ViewMode.CompletedChronological:
                RenderFlat(
                    activities.Where(a => a.ZeroCountCompletedAt != null)
                        .OrderByDescending(a => a.ZeroCountCompletedAt),
                    gameNames,
                    showGame: true,
                    completed: true,
                    emptyMessage: "No completed zero counts yet");
                break;
            case ViewMode.CompletedByGame:
                RenderGrouped(
                    activities.Where(a => a.ZeroCountCompletedAt != null),
                    gameNames,
                    completed: true,
                    emptyMessage: "No completed zero counts yet");
                break;
        }
    }

    private async Task<Dictionary<string, string>> GetGameNamesAsync()
    {
        var games = await _games.GetGamesAsync(_auth.CurrentUsername);
        return games
            .GroupBy(g => g.GameId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().DisplayName, StringComparer.OrdinalIgnoreCase);
    }

    private void RenderGrouped(IEnumerable<Activity> activities, Dictionary<string, string> gameNames, bool completed, string emptyMessage)
    {
        var groups = activities
            .GroupBy(a => a.Game, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => GetGameName(gameNames, g.Key), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (groups.Count == 0)
        {
            RenderEmpty(emptyMessage);
            return;
        }

        foreach (var group in groups)
        {
            _contentStack.Children.Add((new Label
            {
                Text = GetGameName(gameNames, group.Key),
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 12, 0, 0)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1, classifyPageText: true));

            _contentStack.Children.Add((new BoxView
            {
                HeightRequest = 1,
                Margin = new Thickness(0, -4, 0, 4)
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralDarkButton));

            var sorted = completed
                ? group.OrderByDescending(a => a.ZeroCountCompletedAt).ToList()
                : group.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var activity in sorted)
                _contentStack.Children.Add(CreateActivityRow(activity, null, completed));
        }
    }

    private void RenderFlat(IEnumerable<Activity> activities, Dictionary<string, string> gameNames, bool showGame, bool completed, string emptyMessage)
    {
        var list = activities.ToList();
        if (list.Count == 0)
        {
            RenderEmpty(emptyMessage);
            return;
        }

        foreach (var activity in list)
            _contentStack.Children.Add(CreateActivityRow(activity, showGame ? GetGameName(gameNames, activity.Game) : null, completed));
    }

    private View CreateActivityRow(Activity activity, string? gameName, bool completed)
    {
        var stack = new VerticalStackLayout { Spacing = 4 };
        stack.Children.Add((new Label
        {
            Text = activity.Name,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.SecondarySurface, ThemeKey.PageText1, classifyPageText: true));

        if (!string.IsNullOrWhiteSpace(gameName))
        {
            stack.Children.Add((new Label
            {
                Text = gameName,
                FontSize = 12
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        }

        if (completed && activity.ZeroCountCompletedAt.HasValue)
        {
            stack.Children.Add((new Label
            {
                Text = $"Completed: {activity.ZeroCountCompletedAt.Value:yyyy-MM-dd}",
                FontSize = 12
            }).ThemeColor(Label.TextColorProperty, ThemeKey.TimelineSecondary, ThemeKey.PageText3, classifyPageText: true));
        }

        return (new Frame
        {
            CornerRadius = 8,
            Padding = 12,
            HasShadow = false,
            Content = stack
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.MediaBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.NeutralDarkButton);
    }

    private void RenderEmpty(string message)
    {
        _contentStack.Children.Add((new Label
        {
            Text = $"{message}\nMark an activity as Zero Count when creating it to add it here.",
            FontSize = 14,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 40, 0, 0)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
    }

    private void UpdateModeButtons()
    {
        foreach (var mode in _modeFrames.Keys)
        {
            bool selected = mode == _currentMode;
            _modeFrames[mode].ThemeColor(VisualElement.BackgroundColorProperty, selected ? ThemeKey.HighlightTealText : (ThemeKey?)null);
            _modeFrames[mode].ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, selected ? ThemeKey.HighlightTealText : ThemeKey.NeutralDarkButton);
            _modeLabels[mode].ThemeColor(Label.TextColorProperty, selected ? ThemeKey.ButtonPrimaryText : ThemeKey.MediaText, ThemeKey.PageText1, classifyPageText: true);
        }
    }

    private static string GetGameName(Dictionary<string, string> gameNames, string gameId)
    {
        return gameNames.TryGetValue(gameId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : gameId;
    }
}
