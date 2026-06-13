using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class CustomGamePlayPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly CustomGameService _customGames;
    private readonly int _gameId;
    private readonly Label _scoreLabel = new();
    private readonly Label _statusLabel = new();
    private readonly FlexLayout _buttonsLayout = new()
    {
        Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
        Direction = Microsoft.Maui.Layouts.FlexDirection.Row,
        JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start
    };
    private IDispatcherTimer? _timer;
    private CustomGame? _game;
    private CustomGameInstance? _instance;
    private bool _endingPromptOpen;

    public CustomGamePlayPage(AuthService auth, CustomGameService customGames, int gameId)
    {
        _auth = auth;
        _customGames = customGames;
        _gameId = gameId;
        BackgroundColor = Color.FromArgb("#FAFAFA");

        var endButton = new Button
        {
            Text = "End Game Now",
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            CornerRadius = 8,
            HeightRequest = 44
        };
        endButton.Clicked += async (_, _) => await EndManuallyAsync();

        var topScores = new Button
        {
            Text = "View Top Scores",
            BackgroundColor = Color.FromArgb("#EDE7F6"),
            TextColor = Color.FromArgb("#512DA8"),
            CornerRadius = 8,
            HeightRequest = 44
        };
        topScores.Clicked += async (_, _) => await Navigation.PushAsync(new CustomGameTopScoresPage(_auth, _customGames, _gameId));

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 14,
                Children =
                {
                    new Label { Text = "Custom Game", FontSize = 14, TextColor = Color.FromArgb("#666666") },
                    _scoreLabel,
                    _statusLabel,
                    _buttonsLayout,
                    endButton,
                    topScores
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
        StartTimer();
    }

    protected override void OnDisappearing()
    {
        StopTimer();
        base.OnDisappearing();
    }

    private async Task LoadAsync()
    {
        _game = await _customGames.GetGameAsync(_gameId);
        if (_game == null)
        {
            await DisplayAlert("Not Found", "Custom game was not found.", "OK");
            await Navigation.PopAsync();
            return;
        }

        Title = _game.Name;
        _instance = await _customGames.GetActiveInstanceAsync(_game.Id, _auth.CurrentUsername)
            ?? await _customGames.StartInstanceAsync(_game.Id, _auth.CurrentUsername);

        await BuildButtonsAsync();
        UpdateDisplay();
        await CheckEndConditionsAsync();
    }

    private async Task BuildButtonsAsync()
    {
        _buttonsLayout.Children.Clear();
        var buttons = await _customGames.GetButtonsAsync(_gameId);
        foreach (var customButton in buttons)
        {
            var button = new Button
            {
                Text = customButton.Label,
                BackgroundColor = TryParseColor(customButton.Color, customButton.PointValue >= 0 ? "#2E7D32" : "#C62828"),
                TextColor = Colors.White,
                CornerRadius = 8,
                Margin = new Thickness(0, 0, 8, 8),
                HeightRequest = 48,
                MinimumWidthRequest = 96
            };
            button.Clicked += async (_, _) => await ApplyButtonAsync(customButton);
            _buttonsLayout.Children.Add(button);
        }
    }

    private async Task ApplyButtonAsync(CustomGameButton button)
    {
        if (_instance == null || !_instance.InProgress) return;

        int before = _instance.FinalScore;
        _instance = await _customGames.UpdateInstanceScoreAsync(_instance.Id, button.PointValue);
        UpdateDisplay();

        if (_game?.EndType == 2 && _instance != null)
        {
            int target = _game.EndValueAmount ?? 0;
            bool crossed = target >= 0
                ? before < target && _instance.FinalScore >= target
                : before > target && _instance.FinalScore <= target;
            if (crossed)
            {
                await PromptFinishAsync("Target reached — log score?");
            }
        }
    }

    private void StartTimer()
    {
        StopTimer();
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += async (_, _) =>
        {
            UpdateDisplay();
            await CheckEndConditionsAsync();
        };
        _timer.Start();
    }

    private void StopTimer()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer = null;
        }
    }

    private async Task CheckEndConditionsAsync()
    {
        if (_game == null || _instance == null || !_instance.InProgress || _endingPromptOpen)
        {
            return;
        }

        if (_game.EndType == 0)
        {
            int seconds = _game.EndValueSeconds ?? 0;
            if (seconds > 0 && (DateTime.UtcNow - _instance.StartedAt).TotalSeconds >= seconds)
            {
                await PromptFinishAsync("Time up — log score?");
            }
        }
        else if (_game.EndType == 1 && _game.EndValueDate.HasValue && DateTime.UtcNow >= _game.EndValueDate.Value)
        {
            await PromptFinishAsync("Game expired — log final score?");
        }
    }

    private async Task PromptFinishAsync(string message)
    {
        if (_instance == null || _endingPromptOpen) return;
        _endingPromptOpen = true;
        StopTimer();
        bool log = await DisplayAlert(_game?.Name ?? "Custom Game", message, "Yes", "No");
        if (log)
        {
            await _customGames.EndInstanceAsync(_instance.Id);
            await DisplayAlert("Score Logged", $"Final score: {_instance.FinalScore}", "OK");
            await Navigation.PopAsync();
        }
        else
        {
            await _customGames.DeleteInstanceAsync(_instance.Id);
            await Navigation.PopAsync();
        }
        _endingPromptOpen = false;
    }

    private async Task EndManuallyAsync()
    {
        if (_instance == null || _endingPromptOpen) return;

        _endingPromptOpen = true;
        StopTimer();

        string action = await DisplayActionSheet(
            "End Game",
            "Cancel",
            null,
            $"End and Log Score ({_instance.FinalScore})",
            "End Without Logging");

        if (action?.StartsWith("End and Log Score", StringComparison.Ordinal) == true)
        {
            await _customGames.EndInstanceAsync(_instance.Id);
            await Navigation.PopAsync();
        }
        else if (action == "End Without Logging")
        {
            await _customGames.DeleteInstanceAsync(_instance.Id);
            await Navigation.PopAsync();
        }
        else
        {
            _endingPromptOpen = false;
            StartTimer();
        }
    }

    private void UpdateDisplay()
    {
        if (_game == null || _instance == null) return;

        _scoreLabel.Text = _instance.FinalScore.ToString();
        _scoreLabel.FontSize = 56;
        _scoreLabel.FontAttributes = FontAttributes.Bold;
        _scoreLabel.TextColor = Color.FromArgb("#222222");
        _scoreLabel.HorizontalTextAlignment = TextAlignment.Center;

        _statusLabel.Text = _game.EndType switch
        {
            0 => $"Time remaining: {GetRemainingTime()}",
            1 => _game.EndValueDate.HasValue ? $"Expires: {_game.EndValueDate.Value.ToLocalTime():g}" : "Date end",
            2 => $"Target: {_instance.FinalScore} / {_game.EndValueAmount ?? 0}",
            _ => ""
        };
        _statusLabel.FontSize = 16;
        _statusLabel.TextColor = Color.FromArgb("#666666");
        _statusLabel.HorizontalTextAlignment = TextAlignment.Center;
    }

    private string GetRemainingTime()
    {
        if (_game == null || _instance == null) return "0:00";
        int total = _game.EndValueSeconds ?? 0;
        int elapsed = (int)Math.Max(0, (DateTime.UtcNow - _instance.StartedAt).TotalSeconds);
        int remaining = Math.Max(0, total - elapsed);
        return TimeSpan.FromSeconds(remaining).ToString(remaining >= 3600 ? "h\\:mm\\:ss" : "m\\:ss");
    }

    private static Color TryParseColor(string color, string fallback)
    {
        try
        {
            return Color.FromArgb(string.IsNullOrWhiteSpace(color) ? fallback : color);
        }
        catch
        {
            return Color.FromArgb(fallback);
        }
    }
}
