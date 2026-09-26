using Bannister.Services;

namespace BannisterTools;

public class MainPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly SyncService _sync;
    private readonly Label _statusLabel;
    private readonly Label _timerLabel;
    private readonly Label _timerStatusLabel;
    private readonly Entry _minutesEntry;
    private int _configuredSeconds = 25 * 60;
    private int _remainingSeconds = 25 * 60;
    private bool _timerRunning;
    private bool _countingUp;

    public MainPage(DatabaseService db, SyncService sync)
    {
        _db = db;
        _sync = sync;
        Title = "BannisterTools";
        BackgroundColor = Color.FromArgb("#CCF5F5F5");

        _timerLabel = new Label
        {
            Text = "25:00", FontSize = 48,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222222"),
            HorizontalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Fill,
            Margin = new Thickness(0, 10, 0, 4)
        };
        _timerStatusLabel = new Label
        {
            Text = "Ready", FontSize = 12,
            TextColor = Color.FromArgb("#666666"),
            HorizontalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Fill
        };
        _minutesEntry = new Entry
        {
            Text = "25", Placeholder = "Minutes",
            Keyboard = Keyboard.Numeric, FontSize = 14,
            HorizontalOptions = LayoutOptions.Fill
        };

        var closeButton = new Button
        {
            Text = "X", FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            WidthRequest = 38, HeightRequest = 36, Padding = 0,
            CornerRadius = 6,
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#333333"),
            HorizontalOptions = LayoutOptions.End
        };
        closeButton.Clicked += (_, _) => Application.Current?.Quit();

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        header.Add(new Label
        {
            Text = "BannisterTools", FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222222"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        header.Add(closeButton, 1, 0);

        var timerButtons = new HorizontalStackLayout
        {
            Spacing = 6, HorizontalOptions = LayoutOptions.Center
        };
        var startButton = MakeButton("Start", "#2E7D32");
        startButton.Clicked += (_, _) => StartTimer();
        var pauseButton = MakeButton("Pause", "#EF6C00");
        pauseButton.Clicked += (_, _) => PauseTimer();
        var resetButton = MakeButton("Reset", "#546E7A");
        resetButton.Clicked += (_, _) => ResetTimer();
        timerButtons.Children.Add(startButton);
        timerButtons.Children.Add(pauseButton);
        timerButtons.Children.Add(resetButton);

        var setRow = new HorizontalStackLayout
        {
            Spacing = 6, HorizontalOptions = LayoutOptions.Fill
        };
        var setButton = MakeButton("Set", "#3949AB");
        setButton.Clicked += (_, _) => SetTimer();
        setRow.Children.Add(_minutesEntry);
        setRow.Children.Add(setButton);

        _statusLabel = new Label
        {
            Text = "", FontSize = 11,
            TextColor = Color.FromArgb("#666666"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var layout = new VerticalStackLayout
        {
            Padding = 10, Spacing = 8, MaximumWidthRequest = 340,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                header, _timerLabel, _timerStatusLabel,
                timerButtons, setRow, _statusLabel
            }
        };

#if ANDROID
        var syncButton = new Button
        {
            Text = "Sync", BackgroundColor = Color.FromArgb("#3949AB"),
            TextColor = Colors.White, CornerRadius = 8,
            HeightRequest = 40
        };
        syncButton.Clicked += OnSyncClicked;
        layout.Children.Add(syncButton);
#endif

        Content = new ScrollView { Content = layout };
    }

    private static Button MakeButton(string text, string color) => new()
    {
        Text = text, BackgroundColor = Color.FromArgb(color),
        TextColor = Colors.White, CornerRadius = 6,
        FontSize = 12, HeightRequest = 36,
        Padding = new Thickness(10, 0)
    };

    private void StartTimer()
    {
        if (_timerRunning) return;
        _timerRunning = true;
        _countingUp = _remainingSeconds == 0;
        _timerStatusLabel.Text = _countingUp ? "Counting up" : "Running";
        Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
        {
            if (!_timerRunning) return false;
            if (_countingUp)
                _remainingSeconds++;
            else
            {
                _remainingSeconds--;
                if (_remainingSeconds <= 0)
                {
                    _remainingSeconds = 0;
                    _timerRunning = false;
                    _timerStatusLabel.Text = "Done!";
                    UpdateTimerLabel();
                    TryBeep();
                    return false;
                }
            }
            UpdateTimerLabel();
            return _timerRunning;
        });
    }

    private void PauseTimer()
    {
        _timerRunning = false;
        _timerStatusLabel.Text = "Paused";
    }

    private void ResetTimer()
    {
        _timerRunning = false;
        _countingUp = false;
        _remainingSeconds = _configuredSeconds;
        _timerStatusLabel.Text = "Ready";
        UpdateTimerLabel();
    }

    private void SetTimer()
    {
        if (!int.TryParse(_minutesEntry.Text, out var minutes) || minutes < 0)
        {
            _timerStatusLabel.Text = "Enter a valid number of minutes";
            return;
        }
        _configuredSeconds = minutes * 60;
        _remainingSeconds = _configuredSeconds;
        _timerRunning = false;
        _countingUp = false;
        _timerStatusLabel.Text = "Ready";
        UpdateTimerLabel();
    }

    private void UpdateTimerLabel()
    {
        var minutes = _remainingSeconds / 60;
        var seconds = _remainingSeconds % 60;
        _timerLabel.Text = $"{minutes:00}:{seconds:00}";
    }

    private static void TryBeep()
    {
#if WINDOWS
        try { Console.Beep(800, 200); } catch { }
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        string databasePath = DatabaseService.DatabasePath;
        System.Diagnostics.Debug.WriteLine(
            $"BannisterTools database: {databasePath}");
#if ANDROID
        await ReadLocalDatabaseAsync();
#endif
    }

#if ANDROID
    private async void OnSyncClicked(object? sender, EventArgs e)
    {
        try
        {
            _statusLabel.Text = "Syncing...";
            var result = await _sync.DownloadAsync();
            if (!result.Success)
            {
                _statusLabel.Text = result.Message;
                await DisplayAlert("Sync", result.Message, "OK");
                return;
            }
            await ReadLocalDatabaseAsync();
            await DisplayAlert("Sync", result.Message, "OK");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = ex.Message;
            await DisplayAlert("Sync Error", ex.Message, "OK");
        }
    }

    private async Task ReadLocalDatabaseAsync()
    {
        try
        {
            var connection = await _db.GetConnectionAsync();
            await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master");
            _statusLabel.Text = "Database ready";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Database unavailable: {ex.Message}";
        }
    }
#endif
}
