using Bannister.Models;
using Bannister.Services;
using Bannister.Views;

namespace BannisterTools;

public class MainPage : ContentPage
{
    private const string DatabasePathPreferenceKey = "tools_db_path";
    private readonly DatabaseService _db;
    private readonly SyncService _sync;
    private readonly DailyDeadlineService _deadlineService;
    private readonly ResetTimeService _resetTime;
    private readonly AppSettingsService _appSettings;
    private readonly TimedPopupService _timedPopups;
    private readonly PrivacyModeService _privacyModeService;
    private readonly GameService _gameService;
    private readonly LoginMaster _loginMaster;
    private readonly Label _timerLabel;
    private readonly Label _timerStatusLabel;
    private readonly Entry _minutesEntry;
    private readonly Label _greetingLabel;
    private readonly Button _displayModeButton;
    private readonly Grid _mainContent;
    private readonly VerticalStackLayout _homePanel;
    private readonly VerticalStackLayout _timerPanel;
    private readonly VerticalStackLayout _deadlinePanel;
    private readonly DailyDeadlinesView _deadlineView;
    private Entry _usernameEntry = null!;
    private Entry _loginPasswordEntry = null!;
    private Label _loginErrorLabel = null!;
    private VerticalStackLayout _loginSection = null!;
    private VerticalStackLayout _databaseSelectionSection = null!;
    private CheckBox _rememberMeCheck = null!;
    private string _username = "";
    private int _configuredSeconds = 25 * 60;
    private int _remainingSeconds = 25 * 60;
    private bool _timerRunning;
    private bool _countingUp;
    private CancellationTokenSource? _popupSchedulerCts;
    private readonly HashSet<int> _firedPopupIds = new();
    private string _popupResetKey = "";

    public MainPage(
        DatabaseService db,
        SyncService sync,
        DailyDeadlineService deadlineService,
        ResetTimeService resetTime,
        AppSettingsService appSettings,
        TimedPopupService timedPopups,
        PrivacyModeService privacyModeService,
        GameService gameService,
        LoginMaster loginMaster)
    {
        _db = db;
        _sync = sync;
        _deadlineService = deadlineService;
        _resetTime = resetTime;
        _appSettings = appSettings;
        _timedPopups = timedPopups;
        _privacyModeService = privacyModeService;
        _gameService = gameService;
        _loginMaster = loginMaster;
        Title = "BannisterTools";
        BackgroundColor = Color.FromArgb("#2D1B6B");

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

        var menuButton = new Button
        {
            Text = "⋮",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Color.FromArgb("#FBC02D"),
            TextColor = Colors.White,
            CornerRadius = 22,
            WidthRequest = 44,
            HeightRequest = 44,
            Padding = new Thickness(0),
            VerticalOptions = LayoutOptions.Center
        };
        menuButton.Clicked += async (_, _) =>
        {
            string action = await DisplayActionSheet(
                "Menu",
                "Cancel",
                null,
                "Manage Timed Popups",
                "Log Out",
                "Change Database Location");

            if (action == "Change Database Location")
            {
                bool confirmed = await DisplayAlert(
                    "Change Database Location",
                    "Log out and choose a different Bannister database?",
                    "Continue",
                    "Cancel");
                if (!confirmed)
                    return;

                await LogOutAsync();
                Preferences.Default.Remove(DatabasePathPreferenceKey);
                DatabaseService.ClearDatabasePathOverride();
                ShowDatabaseSelection();
                return;
            }

            if (action == "Log Out")
            {
                if (string.IsNullOrWhiteSpace(_username))
                    return;

                bool confirmed = await DisplayAlert(
                    "Log Out",
                    "Log out of BannisterTools?",
                    "Log Out",
                    "Cancel");
                if (confirmed)
                    await LogOutAsync();
                return;
            }

            if (action != "Manage Timed Popups")
                return;

            if (string.IsNullOrWhiteSpace(_username))
            {
                await DisplayAlert(
                    "Login required",
                    "Log in before managing timed popups.",
                    "OK");
                return;
            }

            await Navigation.PushModalAsync(
                new TimedPopupsPage(_timedPopups, _appSettings, _username));
        };

        var header = new Grid
        {
            BackgroundColor = Color.FromArgb("#2D1B6B"),
            Padding = new Thickness(10, 8),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 4
        };
        header.Add(new Label
        {
            Text = "BannisterTools", FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        header.Add(menuButton, 1, 0);

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

        _usernameEntry = new Entry
        {
            Placeholder = "Username",
            HorizontalOptions = LayoutOptions.Fill
        };
        _loginPasswordEntry = new Entry
        {
            Placeholder = "Password",
            IsPassword = true,
            HorizontalOptions = LayoutOptions.Fill
        };
        _rememberMeCheck = new CheckBox
        {
            Color = Color.FromArgb("#6200EE")
        };
        var rememberRow = new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                _rememberMeCheck,
                new Label
                {
                    Text = "Remember me",
                    VerticalOptions = LayoutOptions.Center
                }
            }
        };
        var loginButton = new Button
        {
            Text = "Login",
            BackgroundColor = Color.FromArgb("#6200EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48
        };
        loginButton.Clicked += async (_, _) => await LoginMasterAsync();
        _loginErrorLabel = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Colors.Red
        };
        _loginSection = new VerticalStackLayout
        {
            Spacing = 6,
            IsVisible = false,
            Children =
            {
                new Label
                {
                    Text = " Bannister Login",
                    FontSize = 20,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalTextAlignment = TextAlignment.Center,
                    HorizontalOptions = LayoutOptions.Fill
                },
                _usernameEntry,
                _loginPasswordEntry,
                rememberRow,
                loginButton,
                _loginErrorLabel
            }
        };

        var selectDatabaseButton = new Button
        {
            Text = "Select bannister.db",
            BackgroundColor = Color.FromArgb("#3949AB"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48
        };
        selectDatabaseButton.Clicked += async (_, _) =>
            await SelectDatabaseAsync();
        _databaseSelectionSection = new VerticalStackLayout
        {
            Spacing = 12,
            IsVisible = false,
            Children =
            {
                new Label
                {
                    Text = "Bannister database not found.",
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                selectDatabaseButton
            }
        };

        _greetingLabel = new Label
        {
            FontSize = 16,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Fill
        };
        _displayModeButton = new Button
        {
            Text = "⚡ All",
            FontSize = 12,
            BackgroundColor = Colors.Transparent,
            BorderColor = Colors.White,
            BorderWidth = 1,
            CornerRadius = 8,
            HeightRequest = 44,
            WidthRequest = 90,
            Padding = new Thickness(6, 0),
            HorizontalOptions = LayoutOptions.Center,
            TextColor = Colors.White
        };
        _displayModeButton.Clicked += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_username))
                return;

            var mode = _privacyModeService.CycleMode(_username);
            UpdateDisplayModeVisuals(mode);
        };
        var timerNavButton = new Button
        {
            Text = "⏱ Timer",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            HeightRequest = 56,
            CornerRadius = 10
        };
        var deadlinesNavButton = new Button
        {
            Text = "✅ Daily Deadlines",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            HeightRequest = 56,
            CornerRadius = 10
        };
        _homePanel = new VerticalStackLayout
        {
            Spacing = 12,
            IsVisible = true,
            Children =
            {
                _greetingLabel,
                _displayModeButton,
                timerNavButton,
                deadlinesNavButton
            }
        };
        var timerHomeButton = new Button
        {
            Text = "← Home",
            FontSize = 12,
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 6,
            HeightRequest = 32,
            Padding = new Thickness(10, 0),
            HorizontalOptions = LayoutOptions.Start
        };
        _timerPanel = new VerticalStackLayout
        {
            Spacing = 8,
            IsVisible = false,
            Children =
            {
                timerHomeButton,
                _timerLabel,
                _timerStatusLabel,
                timerButtons,
                setRow
            }
        };

        var deadlineHomeButton = new Button
        {
            Text = "← Home",
            FontSize = 12,
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 6,
            HeightRequest = 32,
            Padding = new Thickness(10, 0),
            HorizontalOptions = LayoutOptions.Start
        };
        _deadlineView = new DailyDeadlinesView(
            getUsername: () => _username,
            service: _deadlineService,
            resetTime: _resetTime,
            db: _db,
            privacyMode: _privacyModeService,
            games: _gameService,
            compact: true,
            showAlert: (title, msg) => DisplayAlert(title, msg, "OK"));
        _deadlinePanel = new VerticalStackLayout
        {
            Spacing = 8,
            IsVisible = false,
            Children =
            {
                deadlineHomeButton,
                _deadlineView
            }
        };

        timerNavButton.Clicked += (_, _) =>
        {
            _homePanel.IsVisible = false;
            _timerPanel.IsVisible = true;
        };
        deadlinesNavButton.Clicked += async (_, _) =>
        {
            _homePanel.IsVisible = false;
            _deadlinePanel.IsVisible = true;
            await _deadlineView.ActivateAsync(false);
        };
        timerHomeButton.Clicked += (_, _) =>
        {
            _timerPanel.IsVisible = false;
            _homePanel.IsVisible = true;
        };
        deadlineHomeButton.Clicked += (_, _) =>
        {
            _deadlineView.Deactivate();
            _deadlinePanel.IsVisible = false;
            _homePanel.IsVisible = true;
        };

        _mainContent = new Grid
        {
            IsVisible = false,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star)
            }
        };
        _mainContent.Add(_homePanel, 0, 0);
        _mainContent.Add(_timerPanel, 0, 0);
        _mainContent.Add(_deadlinePanel, 0, 0);

        var scrollContent = new VerticalStackLayout
        {
            Padding = 10, Spacing = 8, MaximumWidthRequest = 340,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                _databaseSelectionSection, _loginSection, _mainContent
            }
        };

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };
        root.Add(header, 0, 0);
        root.Add(new ScrollView { Content = scrollContent }, 0, 1);
        Content = root;
    }

    private static string? ResolveBannisterDbPath()
    {
        string savedPath = Preferences.Default.Get(
            DatabasePathPreferenceKey, "");
        if (!string.IsNullOrWhiteSpace(savedPath))
        {
            if (File.Exists(savedPath))
                return savedPath;

            Preferences.Default.Remove(DatabasePathPreferenceKey);
        }

        // On Windows, Bannister (packaged/MSIX) stores its DB under:
        // %LOCALAPPDATA%\Packages\com.bannister.app_<hash>\LocalState\bannister.db
        // BannisterTools is unpackaged, so FileSystem.AppDataDirectory points elsewhere.
        // We find the correct path by searching the Packages folder.
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        string packagesDir = Path.Combine(localAppData, "Packages");
        if (Directory.Exists(packagesDir))
        {
            foreach (var dir in Directory.GetDirectories(packagesDir, "com.bannister.app_*"))
            {
                var candidate = Path.Combine(dir, "LocalState", "bannister.db");
                if (File.Exists(candidate))
                {
                    Preferences.Default.Set(DatabasePathPreferenceKey, candidate);
                    return candidate;
                }
            }
        }

        return null;
    }

    private static void ApplyDatabasePath(string databasePath)
    {
        DatabaseService.SetDatabasePathOverride(databasePath);
        DailyDeadlineViewHelper.SetImageBaseDirOverride(
            Path.GetDirectoryName(databasePath) ?? FileSystem.AppDataDirectory);
    }

    private void ShowDatabaseSelection()
    {
        _databaseSelectionSection.IsVisible = true;
        _loginSection.IsVisible = false;
        _mainContent.IsVisible = false;
    }

    private async Task SelectDatabaseAsync()
    {
        var result = await FilePicker.PickAsync(new PickOptions
        {
            PickerTitle = "Select bannister.db",
            FileTypes = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".db" } },
                    { DevicePlatform.Android, new[] { "application/octet-stream", "application/x-sqlite3" } },
                    { DevicePlatform.iOS, new[] { "public.database" } }
                })
        });
        if (result == null || string.IsNullOrWhiteSpace(result.FullPath) ||
            !File.Exists(result.FullPath))
            return;

        string selectedPath = Path.GetFullPath(result.FullPath);
        Preferences.Default.Set(DatabasePathPreferenceKey, selectedPath);
        ApplyDatabasePath(selectedPath);
        _databaseSelectionSection.IsVisible = false;
        _loginSection.IsVisible = true;
    }

    private static Button MakeButton(string text, string color) => new()
    {
        Text = text, BackgroundColor = Color.FromArgb(color),
        TextColor = Colors.White, CornerRadius = 6,
        FontSize = 12, HeightRequest = 36,
        Padding = new Thickness(10, 0)
    };

    private void UpdateDisplayModeVisuals(ActivityDisplayMode mode)
    {
        switch (mode)
        {
            case ActivityDisplayMode.PublicOnly:
                BackgroundColor = Color.FromArgb("#1A1A2E");
                _displayModeButton.Text = " Public";
                _displayModeButton.TextColor = Color.FromArgb("#00E5FF");
                _displayModeButton.BorderColor = Color.FromArgb("#00E5FF");
                break;

            case ActivityDisplayMode.PrivateOnly:
                BackgroundColor = Color.FromArgb("#2D0A4E");
                _displayModeButton.Text = " Private";
                _displayModeButton.TextColor = Color.FromArgb("#FFD700");
                _displayModeButton.BorderColor = Color.FromArgb("#FFD700");
                break;

            default:
                BackgroundColor = Color.FromArgb("#6B73FF");
                _displayModeButton.Text = "⚡ All";
                _displayModeButton.TextColor = Colors.White;
                _displayModeButton.BorderColor = Colors.White;
                break;
        }
    }

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
        string? resolvedPath = ResolveBannisterDbPath();
        if (resolvedPath == null)
        {
            DatabaseService.ClearDatabasePathOverride();
            ShowDatabaseSelection();
            return;
        }

        ApplyDatabasePath(resolvedPath);
        string databasePath = DatabaseService.DatabasePath;
        System.Diagnostics.Debug.WriteLine(
            $"BannisterTools database: {databasePath}");
        _databaseSelectionSection.IsVisible = false;
        if (!string.IsNullOrWhiteSpace(_username))
            return;

        _loginSection.IsVisible = true;
        var savedUser = await SecureStorage.GetAsync("tools_username");
        var savedPass = await SecureStorage.GetAsync("tools_password");
        if (!string.IsNullOrEmpty(savedUser) && !string.IsNullOrEmpty(savedPass))
        {
            var user = await _loginMaster.LoginAsync(savedUser, savedPass);
            if (user != null)
            {
                _username = user.Username;
                UpdateDisplayModeVisuals(
                    _privacyModeService.GetDisplayMode(_username));
                _loginSection.IsVisible = false;
                _greetingLabel.Text = $"Welcome, {user.Username}";
                _homePanel.IsVisible = true;
                _timerPanel.IsVisible = false;
                _deadlinePanel.IsVisible = false;
                _mainContent.IsVisible = true;
                _rememberMeCheck.IsChecked = true;
                StartTimedPopupScheduler();
                return;
            }
        }
    }

    private async Task LoginMasterAsync()
    {
        if (!File.Exists(DatabaseService.DatabasePath))
        {
            Preferences.Default.Remove(DatabasePathPreferenceKey);
            DatabaseService.ClearDatabasePathOverride();
            ShowDatabaseSelection();
            return;
        }

        string username = _usernameEntry.Text?.Trim() ?? "";
        string password = _loginPasswordEntry.Text ?? "";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _loginErrorLabel.Text = "Please enter username and password.";
            return;
        }

        var user = await _loginMaster.LoginAsync(username, password);
        if (user == null)
        {
            _loginErrorLabel.Text = "Invalid username or password.";
            return;
        }

        _loginErrorLabel.Text = "";
        _username = user.Username;
        UpdateDisplayModeVisuals(
            _privacyModeService.GetDisplayMode(_username));
        if (_rememberMeCheck.IsChecked)
        {
            await SecureStorage.SetAsync("tools_username", _username);
            await SecureStorage.SetAsync("tools_password", password);
        }
        else
        {
            SecureStorage.Remove("tools_username");
            SecureStorage.Remove("tools_password");
        }
        _loginSection.IsVisible = false;
        _greetingLabel.Text = $"Welcome, {user.Username}";
        _homePanel.IsVisible = true;
        _timerPanel.IsVisible = false;
        _deadlinePanel.IsVisible = false;
        _mainContent.IsVisible = true;
        StartTimedPopupScheduler();
    }

    private async Task LogOutAsync()
    {
        _popupSchedulerCts?.Cancel();
        _popupSchedulerCts?.Dispose();
        _popupSchedulerCts = null;
        _firedPopupIds.Clear();
        _popupResetKey = "";

        _deadlineView.Deactivate();
        _timerRunning = false;

        SecureStorage.Remove("tools_username");
        SecureStorage.Remove("tools_password");

        _username = "";
        _usernameEntry.Text = "";
        _loginPasswordEntry.Text = "";
        _rememberMeCheck.IsChecked = false;
        _loginErrorLabel.Text = "";
        _greetingLabel.Text = "";

        _homePanel.IsVisible = true;
        _timerPanel.IsVisible = false;
        _deadlinePanel.IsVisible = false;
        _mainContent.IsVisible = false;
        _loginSection.IsVisible = true;

        await _db.CloseConnectionAndClearPasswordAsync();
    }

    private void StartTimedPopupScheduler()
    {
        _popupSchedulerCts?.Cancel();
        _popupSchedulerCts?.Dispose();
        _popupSchedulerCts = new CancellationTokenSource();
        var token = _popupSchedulerCts.Token;
        _firedPopupIds.Clear();
        _popupResetKey = "";

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    string resetKey = await _resetTime.GetTodayResetKey(_username);
                    if (!string.Equals(_popupResetKey, resetKey, StringComparison.Ordinal))
                    {
                        _popupResetKey = resetKey;
                        _firedPopupIds.Clear();
                    }

                    string? sleepValue = await _appSettings.GetAsync(_username, "sleep_time");
                    TimeSpan? sleepTime = TimeSpan.TryParseExact(
                        sleepValue, @"hh\:mm", null, out var parsedSleepTime)
                        ? parsedSleepTime
                        : null;
                    var now = DateTime.Now;
                    var popups = await _timedPopups.GetPopupsAsync(_username);

                    foreach (var popup in popups)
                    {
                        if (_firedPopupIds.Contains(popup.Id))
                            continue;

                        DateTime? fireTime = GetPopupFireTime(popup, sleepTime, now.Date);
                        if (!fireTime.HasValue || now < fireTime.Value ||
                            now > fireTime.Value.AddSeconds(30))
                            continue;

                        _firedPopupIds.Add(popup.Id);
                        await MainThread.InvokeOnMainThreadAsync(() =>
                            DisplayAlert("Timed Popup", popup.Label, "OK"));
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Timed popup scheduler error: {ex.Message}");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }, token);
    }

    private static DateTime? GetPopupFireTime(
        TimedPopup popup,
        TimeSpan? sleepTime,
        DateTime date)
    {
        if (string.Equals(popup.TimeMode, "relative", StringComparison.OrdinalIgnoreCase))
            return sleepTime.HasValue
                ? date.Add(sleepTime.Value).AddMinutes(popup.OffsetMinutes)
                : null;

        return TimeSpan.TryParseExact(
            popup.AbsoluteTime, @"hh\:mm", null, out var absoluteTime)
            ? date.Add(absoluteTime)
            : null;
    }

}
