using Bannister.Services;

namespace Bannister.Views;

public class ImageGenerationHubPage : ContentPage
{
    private readonly OpenAIKeyService _keyService;
    private readonly OpenAIImageService _imageService;
    private readonly OwnerModeService _ownerMode;
    private readonly View _normalContent;

    public ImageGenerationHubPage(OpenAIKeyService keyService, OpenAIImageService imageService, OwnerModeService ownerMode)
    {
        _keyService = keyService;
        _imageService = imageService;
        _ownerMode = ownerMode;
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
            async () => await Navigation.PushAsync(new ChatGptImageGenerationPage(_keyService, _imageService, _ownerMode))));

        _normalContent = new ScrollView { Content = stack };
        Content = _normalContent;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Content = await _ownerMode.IsUnlockedAsync()
            ? _normalContent
            : CreateLockedContent();
    }

    private View CreateLockedContent()
    {
        var backButton = new Button
        {
            Text = "Back",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333333"),
            CornerRadius = 8,
            WidthRequest = 140,
            HorizontalOptions = LayoutOptions.Center
        };
        backButton.Clicked += async (_, _) => await Navigation.PopAsync();

        return new Grid
        {
            Padding = 24,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 12,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label
                        {
                            Text = "Owner Mode Locked",
                            FontSize = 24,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#222"),
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        new Label
                        {
                            Text = "Image Generation is available only when Owner Mode is unlocked.",
                            FontSize = 14,
                            TextColor = Color.FromArgb("#666"),
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        backButton
                    }
                }
            }
        };
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
