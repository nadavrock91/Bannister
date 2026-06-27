using Bannister.Services;
using Bannister.Models;
using System.IO;

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
    private Switch _websiteBuilderInterruptSwitch;
    private Label _websiteBuilderInterruptStatus;
    private Switch _habitScoldingSwitch;
    private Label _habitScoldingStatus;
    private Image _defaultScoldImage;
    private Button _resetDefaultScoldImageButton;
    private VerticalStackLayout _frequencyOverridesStack;
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

        // Games first-entry interrupts section
        var gamesInterruptFrame = new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var gamesInterruptStack = new VerticalStackLayout { Spacing = 12 };

        gamesInterruptStack.Children.Add(new Label
        {
            Text = "Games First Entry Interrupts",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        var websiteBuilderInterruptRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        var websiteBuilderInterruptText = new VerticalStackLayout { Spacing = 4 };
        websiteBuilderInterruptText.Children.Add(new Label
        {
            Text = "Website Builder daily interrupt",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });
        websiteBuilderInterruptText.Children.Add(new Label
        {
            Text = "When entering Games, remind you to do your Website Builder task first.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });
        Grid.SetColumn(websiteBuilderInterruptText, 0);
        websiteBuilderInterruptRow.Children.Add(websiteBuilderInterruptText);

        _websiteBuilderInterruptSwitch = new Switch
        {
            IsToggled = true,
            VerticalOptions = LayoutOptions.Center
        };
        _websiteBuilderInterruptSwitch.Toggled += OnWebsiteBuilderInterruptToggled;
        Grid.SetColumn(_websiteBuilderInterruptSwitch, 1);
        websiteBuilderInterruptRow.Children.Add(_websiteBuilderInterruptSwitch);

        gamesInterruptStack.Children.Add(websiteBuilderInterruptRow);

        _websiteBuilderInterruptStatus = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        };
        gamesInterruptStack.Children.Add(_websiteBuilderInterruptStatus);

        gamesInterruptFrame.Content = gamesInterruptStack;
        mainStack.Children.Add(gamesInterruptFrame);

        mainStack.Children.Add(BuildHabitScoldingSection());

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

    private Frame BuildHabitScoldingSection()
    {
        var frame = new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var stack = new VerticalStackLayout { Spacing = 14 };
        stack.Children.Add(new Label
        {
            Text = "Habit Scolding",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        var toggleRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        var toggleText = new VerticalStackLayout { Spacing = 4 };
        toggleText.Children.Add(new Label
        {
            Text = "Enable scolding popups",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });
        toggleText.Children.Add(new Label
        {
            Text = "Show a popup when a habit allowance is stuck at 1 too long.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });
        toggleRow.Add(toggleText, 0, 0);

        _habitScoldingSwitch = new Switch
        {
            IsToggled = true,
            VerticalOptions = LayoutOptions.Center
        };
        _habitScoldingSwitch.Toggled += OnHabitScoldingToggled;
        toggleRow.Add(_habitScoldingSwitch, 1, 0);
        stack.Children.Add(toggleRow);

        _habitScoldingStatus = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        };
        stack.Children.Add(_habitScoldingStatus);

        var defaultRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };

        _defaultScoldImage = new Image
        {
            WidthRequest = 60,
            HeightRequest = 60,
            Aspect = Aspect.AspectFit
        };
        defaultRow.Add(_defaultScoldImage, 0, 0);

        defaultRow.Add(new Label
        {
            Text = "Default scold image",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center
        }, 1, 0);

        var changeButton = new Button
        {
            Text = "Change",
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            TextColor = Color.FromArgb("#2E7D32"),
            CornerRadius = 8,
            Padding = new Thickness(10, 6)
        };
        changeButton.Clicked += OnChangeDefaultScoldImageClicked;
        defaultRow.Add(changeButton, 2, 0);

        _resetDefaultScoldImageButton = new Button
        {
            Text = "Reset",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            Padding = new Thickness(10, 6)
        };
        _resetDefaultScoldImageButton.Clicked += OnResetDefaultScoldImageClicked;
        defaultRow.Add(_resetDefaultScoldImageButton, 3, 0);
        stack.Children.Add(defaultRow);

        stack.Children.Add(new Label
        {
            Text = "Per-frequency overrides",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            Margin = new Thickness(0, 8, 0, 0)
        });

        _frequencyOverridesStack = new VerticalStackLayout { Spacing = 8 };
        stack.Children.Add(_frequencyOverridesStack);

        frame.Content = stack;
        return frame;
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

        bool websiteBuilderInterruptEnabled = await GetWebsiteBuilderInterruptEnabledAsync();
        _websiteBuilderInterruptSwitch.IsToggled = websiteBuilderInterruptEnabled;
        UpdateWebsiteBuilderInterruptStatus(websiteBuilderInterruptEnabled);

        await LoadHabitScoldingSettingsAsync();
        _loadingSettings = false;
    }

    private async void OnCalendarBeforeGamesToggled(object? sender, ToggledEventArgs e)
    {
        if (_loadingSettings)
            return;

        await SetCalendarBeforeGamesBlockEnabledAsync(e.Value);
        UpdateCalendarBeforeGamesStatus(e.Value);
    }

    private async void OnWebsiteBuilderInterruptToggled(object? sender, ToggledEventArgs e)
    {
        if (_loadingSettings)
            return;

        await SetWebsiteBuilderInterruptEnabledAsync(e.Value);
        UpdateWebsiteBuilderInterruptStatus(e.Value);
    }

    private async void OnHabitScoldingToggled(object? sender, ToggledEventArgs e)
    {
        if (_loadingSettings)
            return;

        await NewHabitService.SetScoldingMasterEnabledAsync(_auth.CurrentUsername, e.Value);
        UpdateHabitScoldingStatus(e.Value);
    }

    private async Task LoadHabitScoldingSettingsAsync()
    {
        bool enabled = await NewHabitService.IsScoldingMasterEnabledAsync(_auth.CurrentUsername);
        _habitScoldingSwitch.IsToggled = enabled;
        UpdateHabitScoldingStatus(enabled);

        var defaultPath = await NewHabitService.GetDefaultScoldImagePathAsync(_auth.CurrentUsername);
        bool hasDefault = !string.IsNullOrWhiteSpace(defaultPath) && File.Exists(defaultPath);
        _defaultScoldImage.Source = hasDefault ? ImageSource.FromFile(defaultPath!) : "scold_default.png";
        _resetDefaultScoldImageButton.IsVisible = hasDefault;

        await LoadFrequencyOverrideRowsAsync();
    }

    private void UpdateHabitScoldingStatus(bool enabled)
    {
        _habitScoldingStatus.Text = enabled
            ? "Enabled. Stagnant habit allowances can trigger one scolding popup per day."
            : "Disabled. Stagnant habit allowances will not show scolding popups.";
    }

    private async Task LoadFrequencyOverrideRowsAsync()
    {
        _frequencyOverridesStack.Children.Clear();
        var habitService = Application.Current?.Handler?.MauiContext?.Services.GetService<NewHabitService>();
        if (habitService == null)
            return;

        foreach (var frequency in new[] { "Daily", "Weekly", "Monthly" })
        {
            try
            {
                var allowance = await habitService.GetOrCreateAllowanceAsync(_auth.CurrentUsername, frequency);
                _frequencyOverridesStack.Children.Add(CreateFrequencyOverrideRow(habitService, allowance));
            }
            catch
            {
                _frequencyOverridesStack.Children.Add(new Label
                {
                    Text = $"{frequency}: unavailable",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#666")
                });
            }
        }
    }

    private View CreateFrequencyOverrideRow(NewHabitService habitService, HabitAllowance allowance)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Padding = new Thickness(0, 8),
            ColumnSpacing = 12
        };

        row.Add(new Label
        {
            Text = allowance.Frequency,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        string status = allowance.ScoldingDisabled
            ? "Disabled"
            : !string.IsNullOrWhiteSpace(allowance.ScoldImagePath) && File.Exists(allowance.ScoldImagePath)
                ? "Custom image"
                : "(default)";

        row.Add(new Label
        {
            Text = status,
            FontSize = 12,
            TextColor = allowance.ScoldingDisabled ? Color.FromArgb("#C62828") : Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        }, 1, 0);

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) => await ShowFrequencyScoldingOptionsAsync(habitService, allowance);
        row.GestureRecognizers.Add(tap);
        return row;
    }

    private async Task ShowFrequencyScoldingOptionsAsync(NewHabitService habitService, HabitAllowance allowance)
    {
        var actions = new List<string> { "Change image" };
        bool hasCustom = !string.IsNullOrWhiteSpace(allowance.ScoldImagePath);
        if (hasCustom)
            actions.Add("Clear custom image");
        actions.Add(allowance.ScoldingDisabled ? "Enable scolding" : "Disable scolding");

        string result = await DisplayActionSheet(
            $"{allowance.Frequency} scolding",
            "Cancel",
            null,
            actions.ToArray());

        if (result == "Change image")
        {
            var path = await PickAndCopyScoldImageAsync($"scold_habit_{allowance.Id}");
            if (!string.IsNullOrWhiteSpace(path))
                await habitService.SetScoldImageAsync(allowance.Id, path);
        }
        else if (result == "Clear custom image")
        {
            await habitService.SetScoldImageAsync(allowance.Id, "");
        }
        else if (result == "Enable scolding")
        {
            await habitService.SetScoldingDisabledAsync(allowance.Id, false);
        }
        else if (result == "Disable scolding")
        {
            await habitService.SetScoldingDisabledAsync(allowance.Id, true);
        }

        await LoadHabitScoldingSettingsAsync();
    }

    private async void OnChangeDefaultScoldImageClicked(object? sender, EventArgs e)
    {
        var path = await PickAndCopyScoldImageAsync("scold_habit_default_user");
        if (!string.IsNullOrWhiteSpace(path))
        {
            await NewHabitService.SetDefaultScoldImagePathAsync(_auth.CurrentUsername, path);
            await LoadHabitScoldingSettingsAsync();
        }
    }

    private async void OnResetDefaultScoldImageClicked(object? sender, EventArgs e)
    {
        await NewHabitService.SetDefaultScoldImagePathAsync(_auth.CurrentUsername, null);
        await LoadHabitScoldingSettingsAsync();
    }

    private async Task<string?> PickAndCopyScoldImageAsync(string filePrefix)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Pick scold image",
                FileTypes = FilePickerFileType.Images
            });

            if (result == null)
                return null;

            var extension = Path.GetExtension(result.FileName);
            var fileName = $"{filePrefix}_{Guid.NewGuid().ToString().Substring(0, 8)}{extension}";
            var destination = Path.Combine(FileSystem.AppDataDirectory, fileName);

            using var source = await result.OpenReadAsync();
            using var target = File.Create(destination);
            await source.CopyToAsync(target);
            return destination;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error picking scold image: {ex.Message}");
            return null;
        }
    }

    private void UpdateCalendarBeforeGamesStatus(bool enabled)
    {
        _calendarBeforeGamesStatus.Text = enabled
            ? "Enabled. Games will require a Calendar visit first each day."
            : "Disabled. Games can be opened directly from Home.";
    }

    private void UpdateWebsiteBuilderInterruptStatus(bool enabled)
    {
        _websiteBuilderInterruptStatus.Text = enabled
            ? "Enabled. Games will remind you about the Website Builder task once per day."
            : "Disabled. Games will not show the Website Builder daily reminder.";
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

    private async Task<bool> GetWebsiteBuilderInterruptEnabledAsync()
    {
        string? value = null;
        try { value = await SecureStorage.GetAsync(GetWebsiteBuilderInterruptEnabledKey()); } catch { }
        return value != "0";
    }

    private async Task SetWebsiteBuilderInterruptEnabledAsync(bool enabled)
    {
        try { await SecureStorage.SetAsync(GetWebsiteBuilderInterruptEnabledKey(), enabled ? "1" : "0"); } catch { }
    }

    private string GetWebsiteBuilderInterruptEnabledKey() => $"website_builder_interrupt_enabled_{_auth.CurrentUsername}";
}
