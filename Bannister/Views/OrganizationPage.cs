using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class OrganizationPage : Bannister.Views.ModeThemedPage
{
    private readonly DatabaseService _db;
    private readonly AuthService _auth;

    public OrganizationPage(DatabaseService db, AuthService auth)
    {
        _db = db;
        _auth = auth;
        Title = "Organization";

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

        stack.Children.Add((new Label
        {
            Text = "Organization",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        // Backups sub-page button
        var backupsBtn = (new Button
        {
            Text = "️ Backup Locations",
            CornerRadius = 10,
            HeightRequest = 52,
            FontSize = 15,
            HorizontalOptions = LayoutOptions.Fill
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        backupsBtn.Clicked += async (s, e) =>
            await Navigation.PushAsync(new BackupLocationsPage(_db, _auth));
        stack.Children.Add(backupsBtn);

        scroll.Content = stack;
        Content = scroll;
    }
}
