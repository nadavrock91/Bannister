using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Modal page for changing the user's password.
/// All password fields are masked (IsPassword = true).
/// </summary>
public class ChangePasswordPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DatabaseService _db;
    private readonly BackupService _backup;

    private Entry _txtCurrentPassword;
    private Entry _txtNewPassword;
    private Entry _txtConfirmPassword;
    private Label _lblError;

    public ChangePasswordPage(AuthService auth, DatabaseService db, BackupService backup)
    {
        _auth = auth;
        _db = db;
        _backup = backup;

        Title = "Change Password";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    private void BuildUI()
    {
        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16,
            VerticalOptions = LayoutOptions.Center,
            MaximumWidthRequest = 400
        };

        // Header
        mainStack.Children.Add(new Label
        {
            Text = "🔑 Change Password",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            HorizontalTextAlignment = TextAlignment.Center
        });

        mainStack.Children.Add(new Label
        {
            Text = "Your database will be re-encrypted with the new password.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12)
        });

        // Current password
        var currentFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 10,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var currentStack = new VerticalStackLayout { Spacing = 8 };
        currentStack.Children.Add(new Label
        {
            Text = "Current Password",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        _txtCurrentPassword = new Entry
        {
            Placeholder = "Enter current password",
            IsPassword = true,
            FontSize = 16,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            ReturnType = ReturnType.Next
        };
        _txtCurrentPassword.Completed += (s, e) => _txtNewPassword.Focus();
        currentStack.Children.Add(_txtCurrentPassword);
        currentFrame.Content = currentStack;
        mainStack.Children.Add(currentFrame);

        // New password
        var newFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 10,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var newStack = new VerticalStackLayout { Spacing = 8 };
        newStack.Children.Add(new Label
        {
            Text = "New Password",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        _txtNewPassword = new Entry
        {
            Placeholder = "Enter new password (min 4 characters)",
            IsPassword = true,
            FontSize = 16,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            ReturnType = ReturnType.Next
        };
        _txtNewPassword.Completed += (s, e) => _txtConfirmPassword.Focus();
        newStack.Children.Add(_txtNewPassword);
        newFrame.Content = newStack;
        mainStack.Children.Add(newFrame);

        // Confirm new password
        var confirmFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 10,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var confirmStack = new VerticalStackLayout { Spacing = 8 };
        confirmStack.Children.Add(new Label
        {
            Text = "Confirm New Password",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        _txtConfirmPassword = new Entry
        {
            Placeholder = "Re-enter new password",
            IsPassword = true,
            FontSize = 16,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            ReturnType = ReturnType.Done
        };
        _txtConfirmPassword.Completed += (s, e) => OnChangePasswordClicked(s, e);
        confirmStack.Children.Add(_txtConfirmPassword);
        confirmFrame.Content = confirmStack;
        mainStack.Children.Add(confirmFrame);

        // Error label
        _lblError = new Label
        {
            Text = "",
            TextColor = Color.FromArgb("#C62828"),
            FontSize = 14,
            IsVisible = false,
            HorizontalTextAlignment = TextAlignment.Center
        };
        mainStack.Children.Add(_lblError);

        // Buttons
        var buttonGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var btnChange = new Button
        {
            Text = "Change Password",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 50,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        };
        btnChange.Clicked += OnChangePasswordClicked;
        Grid.SetColumn(btnChange, 0);
        buttonGrid.Children.Add(btnChange);

        var btnCancel = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            HeightRequest = 50,
            FontSize = 16
        };
        btnCancel.Clicked += OnCancelClicked;
        Grid.SetColumn(btnCancel, 1);
        buttonGrid.Children.Add(btnCancel);

        mainStack.Children.Add(buttonGrid);

        scrollView.Content = mainStack;
        Content = scrollView;
    }

    private void ShowError(string message)
    {
        _lblError.Text = message;
        _lblError.IsVisible = true;
    }

    private async void OnChangePasswordClicked(object? sender, EventArgs e)
    {
        _lblError.IsVisible = false;

        string currentPassword = _txtCurrentPassword.Text?.Trim() ?? "";
        string newPassword = _txtNewPassword.Text?.Trim() ?? "";
        string confirmPassword = _txtConfirmPassword.Text?.Trim() ?? "";

        // Validate
        if (string.IsNullOrEmpty(currentPassword))
        {
            ShowError("Please enter your current password.");
            _txtCurrentPassword.Focus();
            return;
        }

        if (string.IsNullOrEmpty(newPassword))
        {
            ShowError("Please enter a new password.");
            _txtNewPassword.Focus();
            return;
        }

        if (newPassword.Length < 4)
        {
            ShowError("New password must be at least 4 characters.");
            _txtNewPassword.Focus();
            return;
        }

        if (newPassword != confirmPassword)
        {
            ShowError("New passwords don't match.");
            _txtConfirmPassword.Text = "";
            _txtConfirmPassword.Focus();
            return;
        }

        if (currentPassword == newPassword)
        {
            ShowError("New password must be different from current password.");
            _txtNewPassword.Text = "";
            _txtConfirmPassword.Text = "";
            _txtNewPassword.Focus();
            return;
        }

        // Create backup before changing
        await _backup.AutoBackupAsync("pre_password_change");

        // Change password (updates hash + rekeys DB)
        bool success = await _auth.ChangePasswordAsync(currentPassword, newPassword);

        if (success)
        {
            await DisplayAlert("Success",
                "Password changed successfully.\n\n" +
                "🔒 Your database has been re-encrypted with the new password.\n\n" +
                "A backup was created before the change.",
                "OK");

            await Navigation.PopAsync();
        }
        else
        {
            ShowError("Current password is incorrect.");
            _txtCurrentPassword.Text = "";
            _txtCurrentPassword.Focus();
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
