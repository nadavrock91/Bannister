namespace Bannister.Views;

public class FullScreenImagePage : ContentPage
{
    public FullScreenImagePage(ImageSource source)
    {
        Title = "";
        BackgroundColor = Colors.Black;
        NavigationPage.SetHasNavigationBar(this, false);

        var image = new Image
        {
            Source = source,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await Navigation.PopAsync();
        image.GestureRecognizers.Add(tap);

        var hint = new Label
        {
            Text = "Tap anywhere to close",
            FontSize = 13,
            TextColor = Colors.White.WithAlpha(0.6f),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, 24)
        };

        var rootGrid = new Grid
        {
            BackgroundColor = Colors.Black,
            Children = { image, hint }
        };

        Content = rootGrid;
    }

    protected override bool OnBackButtonPressed()
    {
        Navigation.PopAsync();
        return true;
    }
}
