using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Configure Secondary Device Mode: pick master/secondary, set server URL + credentials,
/// trigger manual upload/download, and view last sync status.
/// </summary>
public class SyncSettingsPage : ContentPage
{
    private const string QueuePromptSnoozedUntilKey = "queue_prompt_snoozed_until";
    private readonly DeviceModeService _deviceMode;
    private readonly SyncService _sync;
    private readonly DatabaseService _db;
    private readonly AuthService _auth;
    private readonly OperationApplierService _applier;

    private RadioButton _rbMaster;
    private RadioButton _rbSecondary;
    private Entry _txtServerUrl;
    private Entry _txtUsername;
    private Entry _txtPassword;
    private Label _lblLastSync;
    private Label _lblStatus;
    private Button _btnRegister;
    private Button _btnSaveCreds;
    private Button _btnDownload;
    private Button _btnUpload;
    private Button _btnApplyQueuedOps;
    private HorizontalStackLayout _queueReminderRow;
    private Label _lblQueueReminder;
    private Button _btnResetQueueReminder;
    private ActivityIndicator _busyIndicator;
    private bool _loadingSettings;
    private bool _isBusy;

    public SyncSettingsPage(
        DeviceModeService deviceMode,
        SyncService sync,
        DatabaseService db,
        AuthService auth,
        OperationApplierService applier)
    {
        _deviceMode = deviceMode;
        _sync = sync;
        _db = db;
        _auth = auth;
        _applier = applier;

        Title = "Sync & Devices";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadSettingsAsync();
    }

    private void BuildUI()
    {
        var scroll = new ScrollView();
        var main = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 20
        };

        // Header
        main.Children.Add(new Label
        {
            Text = "🔄 Sync & Devices",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        main.Children.Add(new Label
        {
            Text = "Run Bannister on multiple devices with one device as master (read/write) " +
                   "and others as secondary (read-only). The database is SQLCipher-encrypted before " +
                   "leaving your device.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineHeight = 1.4
        });

        // ===== Mode section =====
        main.Children.Add(BuildModeFrame());

        // ===== Server section =====
        main.Children.Add(BuildServerFrame());

        // ===== Sync section =====
        main.Children.Add(BuildSyncFrame());

        scroll.Content = main;
        Content = scroll;
    }

    private Frame BuildModeFrame()
    {
        var frame = NewSectionFrame();
        var stack = new VerticalStackLayout { Spacing = 12 };

        stack.Children.Add(new Label
        {
            Text = "Device Mode",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        _rbMaster = new RadioButton
        {
            Content = "Master  —  this device can read AND write",
            GroupName = "DeviceMode",
            FontSize = 14
        };
        _rbMaster.CheckedChanged += OnModeChanged;
        stack.Children.Add(_rbMaster);

        _rbSecondary = new RadioButton
        {
            Content = "Secondary  —  this device is READ-ONLY",
            GroupName = "DeviceMode",
            FontSize = 14
        };
        _rbSecondary.CheckedChanged += OnModeChanged;
        stack.Children.Add(_rbSecondary);

        stack.Children.Add(new Label
        {
            Text = "Only one device should be master. Secondary devices show data but block edits.",
            FontSize = 12,
            TextColor = Color.FromArgb("#888")
        });

        frame.Content = stack;
        return frame;
    }

    private Frame BuildServerFrame()
    {
        var frame = NewSectionFrame();
        var stack = new VerticalStackLayout { Spacing = 12 };

        stack.Children.Add(new Label
        {
            Text = "Sync Server",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        stack.Children.Add(new Label
        {
            Text = "Server URL",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#555")
        });
        _txtServerUrl = new Entry
        {
            Placeholder = "https://yourdomain.com/bannister/sync.php",
            FontSize = 14,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            BackgroundColor = Colors.White
        };
        _txtServerUrl.Unfocused += (s, e) => SaveServerUrl();
        stack.Children.Add(_txtServerUrl);

        stack.Children.Add(new Label
        {
            Text = "Username",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#555")
        });
        _txtUsername = new Entry
        {
            Placeholder = "sync username",
            FontSize = 14,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            BackgroundColor = Colors.White
        };
        stack.Children.Add(_txtUsername);

        stack.Children.Add(new Label
        {
            Text = "Password (stored as a hash)",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#555")
        });
        _txtPassword = new Entry
        {
            Placeholder = "sync password",
            IsPassword = true,
            FontSize = 14,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            BackgroundColor = Colors.White
        };
        stack.Children.Add(_txtPassword);

        stack.Children.Add(new Label
        {
            Text = "First time? Tap Register. Already have an account? Tap Save Credentials.",
            FontSize = 12,
            TextColor = Color.FromArgb("#777")
        });

        var buttonRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10
        };

        _btnRegister = new Button
        {
            Text = "Register New Account",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44
        };
        _btnRegister.Clicked += OnRegisterClicked;
        buttonRow.Add(_btnRegister, 0, 0);

        _btnSaveCreds = new Button
        {
            Text = "💾 Save Credentials",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44
        };
        _btnSaveCreds.Text = "Save Credentials";
        _btnSaveCreds.Clicked += OnSaveCredentialsClicked;
        buttonRow.Add(_btnSaveCreds, 1, 0);

        stack.Children.Add(buttonRow);

        frame.Content = stack;
        return frame;
    }

    private Frame BuildSyncFrame()
    {
        var frame = NewSectionFrame();
        var stack = new VerticalStackLayout { Spacing = 12 };

        stack.Children.Add(new Label
        {
            Text = "Sync Now",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        _lblLastSync = new Label
        {
            Text = "Last sync: never",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        };
        stack.Children.Add(_lblLastSync);

        _btnUpload = new Button
        {
            Text = "⬆️ Upload to Server  (master only)",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48
        };
        _btnUpload.Clicked += OnUploadClicked;
        stack.Children.Add(_btnUpload);

        _btnDownload = new Button
        {
            Text = "⬇️ Download from Server",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48
        };
        _btnDownload.Clicked += OnDownloadClicked;
        stack.Children.Add(_btnDownload);

        _btnApplyQueuedOps = new Button
        {
            Text = "Apply Queued Operations from Secondaries",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48,
            IsVisible = false
        };
        _btnApplyQueuedOps.Clicked += OnApplyQueuedOperationsClicked;
        stack.Children.Add(_btnApplyQueuedOps);

        _lblQueueReminder = new Label
        {
            Text = "",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.FillAndExpand
        };

        _btnResetQueueReminder = new Button
        {
            Text = "Reset",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            HeightRequest = 38,
            Padding = new Thickness(14, 0)
        };
        _btnResetQueueReminder.Clicked += OnResetQueueReminderClicked;

        _queueReminderRow = new HorizontalStackLayout
        {
            Spacing = 10,
            IsVisible = false,
            Children = { _lblQueueReminder, _btnResetQueueReminder }
        };
        stack.Children.Add(_queueReminderRow);

        _busyIndicator = new ActivityIndicator
        {
            IsVisible = false,
            IsRunning = false,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 24,
            HeightRequest = 24,
            Color = Color.FromArgb("#5B63EE")
        };

        _lblStatus = new Label
        {
            Text = "",
            FontSize = 13,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalOptions = LayoutOptions.Center
        };
        stack.Children.Add(new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { _busyIndicator, _lblStatus }
        });

        frame.Content = stack;
        return frame;
    }

    private Frame NewSectionFrame() => new Frame
    {
        Padding = 20,
        CornerRadius = 12,
        BackgroundColor = Colors.White,
        HasShadow = true,
        BorderColor = Colors.Transparent
    };

    // ===== Behaviour =====

    private async Task LoadSettingsAsync()
    {
        _loadingSettings = true;

        if (_deviceMode.CurrentMode == DeviceModeService.Mode.Master)
            _rbMaster.IsChecked = true;
        else
            _rbSecondary.IsChecked = true;

        _txtServerUrl.Text = _deviceMode.ServerUrl;
        var (user, passwordHash) = await _deviceMode.GetSyncCredentialsAsync();
        _txtUsername.Text =
            !string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(passwordHash)
                ? user
                : "";
        // Don't restore the password — it's only a hash on disk. Leave the field blank.
        _txtPassword.Text = "";

        UpdateLastSyncLabel();
        UpdateButtonsForMode();
        UpdateQueueReminderSnoozeRow();

        _loadingSettings = false;
    }

    private void UpdateLastSyncLabel()
    {
        var last = _deviceMode.LastSyncUtc;
        _lblLastSync.Text = last.HasValue
            ? $"Last sync: {last.Value.ToLocalTime():MMM d, yyyy HH:mm}"
            : "Last sync: never";
    }

    private void UpdateButtonsForMode()
    {
        bool isMaster = _deviceMode.CurrentMode == DeviceModeService.Mode.Master;
        _btnUpload.IsEnabled = isMaster && !_isBusy;
        _btnUpload.Opacity = isMaster && !_isBusy ? 1.0 : 0.5;
        _btnApplyQueuedOps.IsVisible = isMaster;
        _btnApplyQueuedOps.IsEnabled = isMaster && !_isBusy;
        _btnApplyQueuedOps.Opacity = isMaster && !_isBusy ? 1.0 : 0.5;
    }

    private void UpdateQueueReminderSnoozeRow()
    {
        bool isMaster = !_db.IsReadOnly;
        if (!isMaster || !Preferences.Default.ContainsKey(QueuePromptSnoozedUntilKey))
        {
            _queueReminderRow.IsVisible = false;
            return;
        }

        var raw = Preferences.Default.Get(QueuePromptSnoozedUntilKey, "");
        if (!DateTime.TryParseExact(
                raw,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var snoozeDate) ||
            snoozeDate <= DateTime.Today)
        {
            _queueReminderRow.IsVisible = false;
            return;
        }

        _lblQueueReminder.Text = $"Queue reminder snoozed until {snoozeDate:MMM d, yyyy}.";
        _queueReminderRow.IsVisible = true;
    }

    private void SetBusy(bool isBusy, string statusText = "")
    {
        _isBusy = isBusy;

        _rbMaster.IsEnabled = !isBusy;
        _rbSecondary.IsEnabled = !isBusy;
        _txtServerUrl.IsEnabled = !isBusy;
        _txtUsername.IsEnabled = !isBusy;
        _txtPassword.IsEnabled = !isBusy;

        _btnRegister.IsEnabled = !isBusy;
        _btnSaveCreds.IsEnabled = !isBusy;
        _btnDownload.IsEnabled = !isBusy;
        _btnResetQueueReminder.IsEnabled = !isBusy;

        _busyIndicator.IsVisible = isBusy;
        _busyIndicator.IsRunning = isBusy;

        if (!string.IsNullOrWhiteSpace(statusText))
        {
            _lblStatus.Text = statusText;
            _lblStatus.TextColor = Color.FromArgb("#5B63EE");
        }

        UpdateButtonsForMode();
    }

    private async void OnModeChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (_isBusy) return;
        if (_loadingSettings || _isBusy) return;
        if (!e.Value) return; // only react to the newly-checked button

        var newMode = _rbMaster.IsChecked
            ? DeviceModeService.Mode.Master
            : DeviceModeService.Mode.Secondary;

        if (_deviceMode.CurrentMode == newMode) return;

        bool confirm = await DisplayAlert(
            "Switch Device Mode?",
            newMode == DeviceModeService.Mode.Secondary
                ? "Switch this device to READ-ONLY mode?\n\n" +
                  "All edits will be blocked until you switch back. The current database will be " +
                  "reopened in read-only mode. You will not lose any data."
                : "Switch this device to MASTER mode?\n\n" +
                  "Make sure no other device is currently the master, otherwise their changes will " +
                  "be overwritten on the next sync.",
            "Switch",
            "Cancel");

        if (!confirm)
        {
            // Revert the radio without re-firing the handler
            _loadingSettings = true;
            if (_deviceMode.CurrentMode == DeviceModeService.Mode.Master)
                _rbMaster.IsChecked = true;
            else
                _rbSecondary.IsChecked = true;
            _loadingSettings = false;
            return;
        }

        _deviceMode.SetMode(newMode);

        // Reinitialize the DB connection so it opens with the right flags.
        try
        {
            await _db.ReinitializeAsync();
            UpdateButtonsForMode();
            UpdateQueueReminderSnoozeRow();
            await DisplayAlert("Mode Switched",
                newMode == DeviceModeService.Mode.Secondary
                    ? "This device is now in READ-ONLY mode."
                    : "This device is now in MASTER mode.",
                "OK");
        }
        catch (Exception ex)
        {
            // Likely: switching to Secondary on a device that has no .db file yet.
            await DisplayAlert("Note", ex.Message + "\n\nDownload the latest database to begin.", "OK");
        }
    }

    private void SaveServerUrl()
    {
        if (_loadingSettings) return;
        _deviceMode.ServerUrl = _txtServerUrl.Text?.Trim() ?? "";
    }

    private async void OnSaveCredentialsClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtUsername.Text) ||
            string.IsNullOrWhiteSpace(_txtPassword.Text))
        {
            await DisplayAlert("Missing", "Enter both a username and password.", "OK");
            return;
        }

        SetBusy(true, "Saving credentials...");
        try
        {
            _deviceMode.ServerUrl = _txtServerUrl.Text?.Trim() ?? "";
            await _deviceMode.SetSyncCredentialsAsync(_txtUsername.Text.Trim(), _txtPassword.Text);
            _txtPassword.Text = "";

            await DisplayAlert("Saved",
                "Credentials saved. The password is hashed and stored in SecureStorage. " +
                "It is not sent in plaintext over the network when using HTTPS.",
                "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtUsername.Text) ||
            string.IsNullOrWhiteSpace(_txtPassword.Text))
        {
            await DisplayAlert("Missing", "Enter both a username and password.", "OK");
            return;
        }

        var username = _txtUsername.Text.Trim().ToLowerInvariant();
        bool confirm = await DisplayAlert(
            "Register New Account?",
            $"Register new account '{username}' on the server? You'll use these credentials to sync between devices.",
            "Register",
            "Cancel");

        if (!confirm) return;
        SetBusy(true, "Registering...");
        try
        {
            _deviceMode.ServerUrl = _txtServerUrl.Text?.Trim() ?? "";
            var result = await _sync.RegisterAsync(username, _txtPassword.Text);

            if (result.Success)
            {
                _txtPassword.Text = "";
                _lblStatus.Text = result.Message;
                _lblStatus.TextColor = Color.FromArgb("#2E7D32");
                await DisplayAlert("Registered", result.Message, "OK");
                return;
            }

            _lblStatus.Text = result.Message;
            _lblStatus.TextColor = Color.FromArgb("#C62828");
            await DisplayAlert("Registration Failed", result.Message, "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnUploadClicked(object? sender, EventArgs e)
    {
        SetBusy(true, "Uploading...");
        try
        {
        _lblStatus.Text = "Uploading…";
        _lblStatus.TextColor = Color.FromArgb("#5B63EE");
        var result = await _sync.UploadAsync();
        _lblStatus.Text = result.Message;
        _lblStatus.TextColor = result.Success ? Color.FromArgb("#2E7D32") : Color.FromArgb("#C62828");
        UpdateLastSyncLabel();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnDownloadClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Download Database?",
            "This will REPLACE the current database on this device with the latest copy from " +
            "the server. Local changes that haven't been uploaded will be lost.\n\nContinue?",
            "Download",
            "Cancel");
        if (!confirm) return;

        SetBusy(true, "Downloading...");
        try
        {
            var result = await _sync.DownloadAsync();
            _lblStatus.Text = result.Message;
            _lblStatus.TextColor = result.Success ? Color.FromArgb("#2E7D32") : Color.FromArgb("#C62828");
            UpdateLastSyncLabel();

            if (result.Success)
            {
                await DisplayAlert("Downloaded",
                    "Database installed. You may need to restart the app or navigate away and back " +
                    "for all pages to reflect the new data.",
                    "OK");
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnApplyQueuedOperationsClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new QueuedOperationsPage(_sync, _applier, _db));
    }

    private async void OnResetQueueReminderClicked(object? sender, EventArgs e)
    {
        bool reset = await DisplayAlert(
            "Reset Queue Reminder?",
            "Reset queue reminder so you'll be prompted again on next app launch?",
            "Yes",
            "No");

        if (!reset) return;

        Preferences.Default.Remove(QueuePromptSnoozedUntilKey);
        UpdateQueueReminderSnoozeRow();
    }

    private async void OnDownloadClickedLegacy(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Download Database?",
            "This will REPLACE the current database on this device with the latest copy from " +
            "the server. Local changes that haven't been uploaded will be lost.\n\nContinue?",
            "Download",
            "Cancel");
        if (!confirm) return;

        _lblStatus.Text = "Downloading…";
        _lblStatus.TextColor = Color.FromArgb("#5B63EE");
        var result = await _sync.DownloadAsync();
        _lblStatus.Text = result.Message;
        _lblStatus.TextColor = result.Success ? Color.FromArgb("#2E7D32") : Color.FromArgb("#C62828");
        UpdateLastSyncLabel();

        if (result.Success)
        {
            await DisplayAlert("Downloaded",
                "Database installed. You may need to restart the app or navigate away and back " +
                "for all pages to reflect the new data.",
                "OK");
        }
    }
}
