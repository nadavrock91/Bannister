using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class CustomGameTopScoresPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly CustomGameService _customGames;
    private readonly int _gameId;
    private readonly VerticalStackLayout _scoresStack = new() { Spacing = 8 };
    private readonly Label _emptyLabel;

    public CustomGameTopScoresPage(AuthService auth, CustomGameService customGames, int gameId)
    {
        _auth = auth;
        _customGames = customGames;
        _gameId = gameId;
        BackgroundColor = Color.FromArgb("#FAFAFA");

        _emptyLabel = new Label
        {
            Text = "No finished scores yet.",
            FontSize = 14,
            TextColor = Color.FromArgb("#666666"),
            HorizontalTextAlignment = TextAlignment.Center,
            IsVisible = false,
            Margin = new Thickness(0, 24, 0, 0)
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    _emptyLabel,
                    _scoresStack
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _scoresStack.Children.Clear();
        var game = await _customGames.GetGameAsync(_gameId);
        if (game == null)
        {
            await DisplayAlert("Not Found", "Custom game was not found.", "OK");
            await Navigation.PopAsync();
            return;
        }

        Title = $"{game.Name} - Top Scores";
        var scores = await _customGames.GetTopScoresAsync(game.Id, _auth.CurrentUsername, game.HigherIsBetter);
        _emptyLabel.IsVisible = scores.Count == 0;

        _scoresStack.Children.Add(new Label
        {
            Text = Title,
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222222")
        });

        for (int i = 0; i < scores.Count; i++)
        {
            _scoresStack.Children.Add(BuildScoreRow(i + 1, scores[i]));
        }
    }

    private View BuildScoreRow(int rank, CustomGameInstance score)
    {
        var duration = score.EndedAt.HasValue
            ? score.EndedAt.Value - score.StartedAt
            : TimeSpan.Zero;
        var details = new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label { Text = score.FinalScore.ToString(), FontAttributes = FontAttributes.Bold, FontSize = 20, TextColor = Color.FromArgb("#222222") },
                new Label { Text = $"{(score.EndedAt ?? score.StartedAt).ToLocalTime():g}   Duration: {FormatDuration(duration)}", FontSize = 12, TextColor = Color.FromArgb("#666666") }
            }
        };
        details.SetValue(Grid.ColumnProperty, 1);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12,
            Children =
            {
                new Label
                {
                    Text = $"#{rank}",
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 18,
                    TextColor = Color.FromArgb("#512DA8"),
                    VerticalOptions = LayoutOptions.Center
                },
                details
            }
        };

        return new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            HasShadow = false,
            BorderColor = Color.FromArgb("#E0E0E0"),
            BackgroundColor = Colors.White,
            Content = grid
        };
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return "-";
        return duration.TotalHours >= 1
            ? duration.ToString("h\\:mm\\:ss")
            : duration.ToString("m\\:ss");
    }
}
