using Bannister.Services;

namespace Bannister.Views;

public class MusicProductionHubPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly MusicProductionService _musicService;
    private readonly DatabaseService _db;

    public MusicProductionHubPage(AuthService auth, MusicProductionService musicService, DatabaseService db)
    {
        _auth = auth;
        _musicService = musicService;
        _db = db;

        Title = "Music Production";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    private void BuildUI()
    {
        var scroll = new ScrollView();
        var stack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16
        };

        stack.Children.Add(new Label
        {
            Text = "Music Production",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#3949AB")
        });

        stack.Children.Add(new Label
        {
            Text = "Music planning tools",
            FontSize = 14,
            TextColor = Color.FromArgb("#666")
        });

        stack.Children.Add(CreateHubCard(
            "Music for Stories",
            "Plan music cues alongside script and visuals.",
            Color.FromArgb("#E8EAF6"),
            Color.FromArgb("#3949AB"),
            OnMusicForStoriesClicked));

        scroll.Content = stack;
        Content = scroll;
    }

    private View CreateHubCard(string title, string subtitle, Color background, Color accent, EventHandler clicked)
    {
        var frame = new Frame
        {
            Padding = 18,
            CornerRadius = 12,
            BackgroundColor = background,
            BorderColor = Colors.Transparent,
            HasShadow = true
        };

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
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = accent
        });
        textStack.Children.Add(new Label
        {
            Text = subtitle,
            FontSize = 13,
            TextColor = Color.FromArgb("#555")
        });
        Grid.SetColumn(textStack, 0);
        grid.Children.Add(textStack);

        var button = new Button
        {
            Text = "Open",
            BackgroundColor = accent,
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(18, 10),
            VerticalOptions = LayoutOptions.Center
        };
        button.Clicked += clicked;
        Grid.SetColumn(button, 1);
        grid.Children.Add(button);

        frame.Content = grid;
        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, e) => clicked(s, EventArgs.Empty);
        frame.GestureRecognizers.Add(tap);
        return frame;
    }

    private async void OnMusicForStoriesClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new MusicForStoriesPage(_auth, _musicService, _db));
    }
}
