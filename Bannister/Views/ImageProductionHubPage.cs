using Bannister.Services;

namespace Bannister.Views;

public class ImageProductionHubPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DatabaseService _db;

    public ImageProductionHubPage(AuthService auth, DatabaseService db)
    {
        _auth = auth;
        _db = db;

        Title = "Image Production";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16,
            Children =
            {
                new Label
                {
                    Text = "🎨 Image Production",
                    FontSize = 26,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#C62828"),
                    HorizontalOptions = LayoutOptions.Center
                },
                CreateHubCard(
                    "Project Images",
                    "Project-based image prompt workflows (Hook First Frame, Clip Start Frame)",
                    OnProjectImagesTapped),
                CreateHubCard(
                    "Hooks Creation",
                    "Standalone scroll-stopping hook image prompt generator",
                    OnHooksCreationTapped)
            }
        };

        Content = new ScrollView { Content = stack };
    }

    private Frame CreateHubCard(string title, string subtitle, EventHandler<TappedEventArgs> tapped)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        var textStack = new VerticalStackLayout { Spacing = 4 };
        textStack.Children.Add(new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        textStack.Children.Add(new Label
        {
            Text = subtitle,
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        grid.Add(textStack, 0, 0);
        grid.Add(new Label
        {
            Text = "→",
            FontSize = 24,
            TextColor = Color.FromArgb("#C62828"),
            VerticalOptions = LayoutOptions.Center
        }, 1, 0);

        var frame = new Frame
        {
            BackgroundColor = Colors.White,
            Padding = 20,
            CornerRadius = 12,
            HasShadow = true,
            Content = grid
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += tapped;
        frame.GestureRecognizers.Add(tap);
        return frame;
    }

    private async void OnProjectImagesTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new ImageProductionPage(_auth, new ImageProductionService(_db)));
    }

    private async void OnHooksCreationTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new HooksCreationPage());
    }
}
