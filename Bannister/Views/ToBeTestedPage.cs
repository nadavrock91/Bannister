using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class ToBeTestedPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ActivityService _activities;
    private readonly GameService _games;
    private readonly ExpService _exp;
    private readonly VerticalStackLayout _listStack;
    private readonly Label _emptyLabel;
    private bool _isNavigating;

    public ToBeTestedPage(AuthService auth, ActivityService activities, GameService games, ExpService exp)
    {
        _auth = auth;
        _activities = activities;
        _games = games;
        _exp = exp;

        Title = "To Be Tested";
        BackgroundColor = Color.FromArgb("#FAFAFA");

        _listStack = new VerticalStackLayout
        {
            Spacing = 10
        };

        _emptyLabel = new Label
        {
            Text = "No activities to test right now. Mark activities as 'To Be Tested' from their edit page to add them here.",
            FontSize = 14,
            TextColor = Color.FromArgb("#666666"),
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 32, 0, 0),
            IsVisible = false
        };

        var contentStack = new VerticalStackLayout
        {
            Spacing = 12,
            Padding = new Thickness(16),
            Children =
            {
                new Label
                {
                    Text = "To Be Tested",
                    FontSize = 28,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#222222")
                },
                new Label
                {
                    Text = "Activities you haven't tried in real life yet.",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#666666"),
                    Margin = new Thickness(0, -6, 0, 8)
                },
                _emptyLabel,
                _listStack
            }
        };

        Content = new ScrollView
        {
            Content = contentStack
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _listStack.Children.Clear();

        var activities = await _activities.GetToBeTestedActivitiesAsync(_auth.CurrentUsername);
        var games = await _games.GetGamesAsync(_auth.CurrentUsername);
        var gameNames = games
            .GroupBy(g => g.GameId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().DisplayName, StringComparer.OrdinalIgnoreCase);

        _emptyLabel.IsVisible = activities.Count == 0;

        foreach (var activity in activities)
        {
            _listStack.Children.Add(BuildActivityCard(activity, gameNames));
        }
    }

    private View BuildActivityCard(Activity activity, Dictionary<string, string> gameNames)
    {
        string gameName = gameNames.TryGetValue(activity.Game, out string? displayName)
            ? displayName
            : activity.Game;
        string category = string.IsNullOrWhiteSpace(activity.Category) ? "Misc" : activity.Category;

        var title = new Label
        {
            Text = activity.Name,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222222")
        };

        var subtitle = new Label
        {
            Text = $"{gameName} - {category}",
            FontSize = 13,
            TextColor = Color.FromArgb("#666666")
        };

        var markTriedButton = new Button
        {
            Text = "Mark as Tried",
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            TextColor = Color.FromArgb("#2E7D32"),
            CornerRadius = 8,
            HeightRequest = 40,
            Padding = new Thickness(12, 0)
        };
        markTriedButton.Clicked += async (_, _) =>
        {
            await _activities.SetIsToBeTestedAsync(activity.Id, false);
            await LoadAsync();
        };

        var openButton = new Button
        {
            Text = "Open in Game",
            BackgroundColor = Color.FromArgb("#FFF9C4"),
            TextColor = Color.FromArgb("#F57C00"),
            CornerRadius = 8,
            HeightRequest = 40,
            Padding = new Thickness(12, 0)
        };
        openButton.Clicked += async (_, _) => await OpenActivityGameAsync(activity);

        var buttonRow = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.End,
            Children =
            {
                markTriedButton,
                openButton
            }
        };

        var content = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        var textStack = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                title,
                subtitle
            }
        };

        content.Add(textStack, 0, 0);
        content.Add(buttonRow, 1, 0);

        var frame = new Frame
        {
            Content = content,
            Padding = 14,
            CornerRadius = 8,
            HasShadow = false,
            BorderColor = Color.FromArgb("#E0E0E0"),
            BackgroundColor = Colors.White
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await OpenActivityGameAsync(activity);
        frame.GestureRecognizers.Add(tap);

        return frame;
    }

    private async Task OpenActivityGameAsync(Activity activity)
    {
        if (_isNavigating)
        {
            return;
        }

        try
        {
            _isNavigating = true;
            var game = await _games.GetGameAsync(_auth.CurrentUsername, activity.Game);
            if (game == null)
            {
                await DisplayAlert("Game Not Found", "This activity's game could not be found.", "OK");
                return;
            }

            string encodedGameId = Uri.EscapeDataString(game.GameId);

            if (GameService.ShouldShowCatchUp(game, DateTime.Today, out _))
            {
                bool hasEligibleRows = await GameCatchUpPage.HasEligibleCatchUpActivitiesAsync(
                    _auth.CurrentUsername,
                    game,
                    _activities,
                    _exp);

                if (hasEligibleRows)
                {
                    await Shell.Current.GoToAsync($"gamecatchup?gameId={encodedGameId}");
                    return;
                }
            }

            await _games.UpdateLastVisitedAtAsync(_auth.CurrentUsername, game.GameId, DateTime.Now);
            await Shell.Current.GoToAsync($"activitygrid?gameId={encodedGameId}");
        }
        finally
        {
            _isNavigating = false;
        }
    }
}
