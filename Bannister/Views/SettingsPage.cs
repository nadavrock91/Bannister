using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Settings page accessible from HomePage.
/// Provides account management options like changing password.
/// </summary>
public class SettingsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DatabaseService _db;
    private readonly BackupService _backup;
    private Switch _calendarBeforeGamesSwitch;
    private Label _calendarBeforeGamesStatus;
    private bool _loadingSettings;

    public SettingsPage(AuthService auth, DatabaseService db, BackupService backup)
    {
        _auth = auth;
        _db = db;
        _backup = backup;

        Title = "Settings";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadHomeSettingsAsync();
    }

    private void BuildUI()
    {
        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 20
        };

        // Header
        mainStack.Children.Add(new Label
        {
            Text = "⚙️ Settings",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        // Account section
        var accountFrame = new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var accountStack = new VerticalStackLayout { Spacing = 16 };

        accountStack.Children.Add(new Label
        {
            Text = "Account",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        // Username display
        var userRow = new HorizontalStackLayout { Spacing = 8 };
        userRow.Children.Add(new Label
        {
            Text = "👤 Username:",
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        });
        userRow.Children.Add(new Label
        {
            Text = _auth.CurrentUsername,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center
        });
        accountStack.Children.Add(userRow);

        // Change password button
        var btnChangePassword = new Button
        {
            Text = "🔑 Change Password",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48,
            FontSize = 16
        };
        btnChangePassword.Clicked += OnChangePasswordClicked;
        accountStack.Children.Add(btnChangePassword);

        accountFrame.Content = accountStack;
        mainStack.Children.Add(accountFrame);

        // Home section
        var homeFrame = new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var homeStack = new VerticalStackLayout { Spacing = 12 };

        homeStack.Children.Add(new Label
        {
            Text = "Home",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        var calendarGateRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        var calendarGateText = new VerticalStackLayout { Spacing = 4 };
        calendarGateText.Children.Add(new Label
        {
            Text = "Require Calendar Before Games",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });
        calendarGateText.Children.Add(new Label
        {
            Text = "Blocks Games from Home once per day until Calendar is opened.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });
        Grid.SetColumn(calendarGateText, 0);
        calendarGateRow.Children.Add(calendarGateText);

        _calendarBeforeGamesSwitch = new Switch
        {
            IsToggled = true,
            VerticalOptions = LayoutOptions.Center
        };
        _calendarBeforeGamesSwitch.Toggled += OnCalendarBeforeGamesToggled;
        Grid.SetColumn(_calendarBeforeGamesSwitch, 1);
        calendarGateRow.Children.Add(_calendarBeforeGamesSwitch);

        homeStack.Children.Add(calendarGateRow);

        _calendarBeforeGamesStatus = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        };
        homeStack.Children.Add(_calendarBeforeGamesStatus);

        homeFrame.Content = homeStack;
        mainStack.Children.Add(homeFrame);

        // Sync & Devices section
        var syncFrame = new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var syncStack = new VerticalStackLayout { Spacing = 12 };

        syncStack.Children.Add(new Label
        {
            Text = "Sync & Devices",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        syncStack.Children.Add(new Label
        {
            Text = "Run Bannister on multiple devices with one as master (read/write) and others " +
                   "as secondary (read-only). The database is uploaded as an encrypted file.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineHeight = 1.4
        });

        var btnSyncSettings = new Button
        {
            Text = "🔄 Sync & Devices",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48,
            FontSize = 16
        };
        btnSyncSettings.Clicked += OnSyncSettingsClicked;
        syncStack.Children.Add(btnSyncSettings);

        syncFrame.Content = syncStack;
        mainStack.Children.Add(syncFrame);

        // Security section
        var securityFrame = new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var securityStack = new VerticalStackLayout { Spacing = 12 };

        securityStack.Children.Add(new Label
        {
            Text = "Security",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        securityStack.Children.Add(new Label
        {
            Text = "🔒 Your database is encrypted with your login password.\n\n" +
                   "If you change your password, the database will be re-encrypted with the new password.\n\n" +
                   "Backups are also encrypted. To restore a backup on another device, " +
                   "log in with the same password that was active when the backup was created.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineHeight = 1.4
        });

        securityFrame.Content = securityStack;
        mainStack.Children.Add(securityFrame);

        scrollView.Content = mainStack;
        Content = scrollView;
    }

    private async void OnChangePasswordClicked(object? sender, EventArgs e)
    {
        var page = new ChangePasswordPage(_auth, _db, _backup);
        await Navigation.PushAsync(page);
    }

    private async void OnSyncSettingsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("syncsettings");
    }

    private async Task LoadHomeSettingsAsync()
    {
        _loadingSettings = true;
        bool enabled = await GetCalendarBeforeGamesBlockEnabledAsync();
        _calendarBeforeGamesSwitch.IsToggled = enabled;
        UpdateCalendarBeforeGamesStatus(enabled);
        _loadingSettings = false;
    }

    private async void OnCalendarBeforeGamesToggled(object? sender, ToggledEventArgs e)
    {
        if (_loadingSettings)
            return;

        await SetCalendarBeforeGamesBlockEnabledAsync(e.Value);
        UpdateCalendarBeforeGamesStatus(e.Value);
    }

    private void UpdateCalendarBeforeGamesStatus(bool enabled)
    {
        _calendarBeforeGamesStatus.Text = enabled
            ? "Enabled. Games will require a Calendar visit first each day."
            : "Disabled. Games can be opened directly from Home.";
    }

    private async Task<bool> GetCalendarBeforeGamesBlockEnabledAsync()
    {
        string? value = null;
        try { value = await SecureStorage.GetAsync(GetCalendarBeforeGamesBlockStorageKey()); } catch { }
        return value != "false";
    }

    private async Task SetCalendarBeforeGamesBlockEnabledAsync(bool enabled)
    {
        try { await SecureStorage.SetAsync(GetCalendarBeforeGamesBlockStorageKey(), enabled ? "true" : "false"); } catch { }
    }

    private string GetCalendarBeforeGamesBlockStorageKey() => $"home_block_games_until_calendar_{_auth.CurrentUsername}";
}
