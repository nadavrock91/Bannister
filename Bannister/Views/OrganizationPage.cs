using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class OrganizationPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly AuthService _auth;

    public OrganizationPage(DatabaseService db, AuthService auth)
    {
        _db = db;
        _auth = auth;
        Title = "Organization";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    private void BuildUI()
    {
        var scroll = new ScrollView();
        var stack = new VerticalStackLayout
        {
            Padding = new Thickness(16),
            Spacing = 16
        };

        stack.Children.Add(new Label
        {
            Text = "Organization",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238")
        });

        // Backups sub-page button
        var backupsBtn = new Button
        {
            Text = "️ Backup Locations",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"),
            CornerRadius = 10,
            HeightRequest = 52,
            FontSize = 15,
            HorizontalOptions = LayoutOptions.Fill
        };
        backupsBtn.Clicked += async (s, e) =>
            await Navigation.PushAsync(new BackupLocationsPage(_db, _auth));
        stack.Children.Add(backupsBtn);

        scroll.Content = stack;
        Content = scroll;
    }
}
