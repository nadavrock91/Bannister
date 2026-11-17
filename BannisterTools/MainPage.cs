using Bannister.Models;
using Bannister.Services;

namespace BannisterTools;

public class MainPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly SyncService _sync;
    private readonly DailyDeadlineService _deadlineService;
    private readonly ResetTimeService _resetTime;
    private readonly LoginMaster _loginMaster;
    private readonly Label _timerLabel;
    private readonly Label _timerStatusLabel;
    private readonly Entry _minutesEntry;
    private readonly VerticalStackLayout _deadlineList;
    private readonly Label _greetingLabel;
    private readonly Grid _mainContent;
    private readonly VerticalStackLayout _homePanel;
    private readonly VerticalStackLayout _timerPanel;
    private readonly VerticalStackLayout _deadlinePanel;
    private Entry _usernameEntry = null!;
    private Entry _loginPasswordEntry = null!;
    private Label _loginErrorLabel = null!;
    private VerticalStackLayout _loginSection = null!;
    private CheckBox _rememberMeCheck = null!;
    private string _username = "";
    private int _configuredSeconds = 25 * 60;
    private int _remainingSeconds = 25 * 60;
    private bool _timerRunning;
    private bool _countingUp;

    public MainPage(
        DatabaseService db,
        SyncService sync,
        DailyDeadlineService deadlineService,
        ResetTimeService resetTime,
        LoginMaster loginMaster)
    {
#if WINDOWS
        DatabaseService.SetDatabasePathOverride(ResolveBannisterDbPath());
#endif
        _db = db;
        _sync = sync;
        _deadlineService = deadlineService;
        _resetTime = resetTime;
        _loginMaster = loginMaster;
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

        _greetingLabel = new Label
        {
            FontSize = 16,
            TextColor = Color.FromArgb("#666666"),
            HorizontalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Fill
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

        _deadlineList = new VerticalStackLayout { Spacing = 6 };
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
        _deadlinePanel = new VerticalStackLayout
        {
            Spacing = 8,
            IsVisible = false,
            Children =
            {
                deadlineHomeButton,
                new Label
                {
                    Text = "Daily Deadlines",
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#222222")
                },
                _deadlineList
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
            await LoadDeadlinesAsync();
        };
        timerHomeButton.Clicked += (_, _) =>
        {
            _timerPanel.IsVisible = false;
            _homePanel.IsVisible = true;
        };
        deadlineHomeButton.Clicked += (_, _) =>
        {
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

        var layout = new VerticalStackLayout
        {
            Padding = 10, Spacing = 8, MaximumWidthRequest = 340,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                header, _loginSection, _mainContent
            }
        };

        Content = new ScrollView { Content = layout };
    }

    private static string ResolveBannisterDbPath()
    {
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
                    return candidate;
            }
        }

        return DatabaseService.DatabasePath;
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
        var savedUser = await SecureStorage.GetAsync("tools_username");
        var savedPass = await SecureStorage.GetAsync("tools_password");
        if (!string.IsNullOrEmpty(savedUser) && !string.IsNullOrEmpty(savedPass))
        {
            var user = await _loginMaster.LoginAsync(savedUser, savedPass);
            if (user != null)
            {
                _username = user.Username;
                _loginSection.IsVisible = false;
                _greetingLabel.Text = $"Welcome, {user.Username}";
                _homePanel.IsVisible = true;
                _timerPanel.IsVisible = false;
                _deadlinePanel.IsVisible = false;
                _mainContent.IsVisible = true;
                _rememberMeCheck.IsChecked = true;
                return;
            }
        }
    }

    private async Task LoginMasterAsync()
    {
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
    }

    private async Task LoadDeadlinesAsync()
    {
        _deadlineList.Children.Clear();
        if (string.IsNullOrWhiteSpace(_username))
        {
            _deadlineList.Children.Add(new Label
            {
                Text = "No active deadlines",
                TextColor = Color.FromArgb("#666666")
            });
            return;
        }

        var active = await _deadlineService.GetActiveItemsAsync(_username);
        var log = await _deadlineService.GetCurrentLogAsync(_username);
        var completedIds = ParseIds(log?.CompletedItemIds);
        if (active.Count == 0)
        {
            _deadlineList.Children.Add(new Label
            {
                Text = "No active deadlines",
                TextColor = Color.FromArgb("#666666")
            });
            return;
        }

        var conn = await _db.GetConnectionAsync();
        foreach (var item in active)
        {
            var activity = await conn.Table<Activity>()
                .Where(a => a.Id == item.ActivityId)
                .FirstOrDefaultAsync();
            var row = await DailyDeadlineViewHelper.BuildRowAsync(
                item,
                activity,
                completedIds,
                _deadlineService,
                _resetTime,
                _username,
                LoadDeadlinesAsync,
                new DailyDeadlineRowOptions
                {
                    ShowMoveButton = true,
                    ShowDeleteButton = true,
                    StackButtons = true,
                    OnMove = async i =>
                    {
                        i.IsActive = false;
                        await _deadlineService.SaveItemAsync(i);
                        await LoadDeadlinesAsync();
                    },
                    OnDelete = async i =>
                    {
                        await _deadlineService.DeleteItemAsync(i.Id);
                        await LoadDeadlinesAsync();
                    }
                });
            _deadlineList.Children.Add(row);
        }
    }

    private static HashSet<int> ParseIds(string? value) =>
        (value ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToHashSet();

}
