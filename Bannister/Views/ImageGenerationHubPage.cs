namespace Bannister.Views;

public class ImageGenerationHubPage : ContentPage
{
    public ImageGenerationHubPage()
    {
        Title = "Image Generation";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16
        };

        stack.Children.Add(new Label
        {
            Text = "Image Generation",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });

        stack.Children.Add(new Label
        {
            Text = "Tools for generating images from prompts.",
            FontSize = 15,
            TextColor = Color.FromArgb("#666"),
            Margin = new Thickness(0, -8, 0, 8)
        });

        stack.Children.Add(CreateProviderCard(
            "ChatGPT API",
            "Generate images using OpenAI's image API.",
            async () => await Navigation.PushAsync(new ChatGptImageGenerationPage())));

        Content = new ScrollView { Content = stack };
    }

    private static Frame CreateProviderCard(string title, string description, Action tapped)
    {
        var textStack = new VerticalStackLayout
        {
            Spacing = 4
        };

        textStack.Children.Add(new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });

        textStack.Children.Add(new Label
        {
            Text = description,
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        var frame = new Frame
        {
            Padding = 16,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            HasShadow = true,
            Content = textStack
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => tapped();
        frame.GestureRecognizers.Add(tap);

        return frame;
    }
}
