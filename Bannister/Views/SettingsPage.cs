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

    public SettingsPage(AuthService auth, DatabaseService db, BackupService backup)
    {
        _auth = auth;
        _db = db;
        _backup = backup;

        Title = "Settings";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
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
}
