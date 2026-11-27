using Bannister.Services;

namespace Bannister.Views;

public class ImageGenerationHubPage : Bannister.Views.ModeThemedPage
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


        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16
        };

        stack.Children.Add((new Label
        {
            Text = "Image Generation",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true));

        stack.Children.Add((new Label
        {
            Text = "Tools for generating images from prompts.",
            FontSize = 15,
            Margin = new Thickness(0, -8, 0, 8)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

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
        var backButton = (new Button
        {
            Text = "Back",
            CornerRadius = 8,
            WidthRequest = 140,
            HorizontalOptions = LayoutOptions.Center
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.CardText1);
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
                        (new Label
                        {
                            Text = "Owner Mode Locked",
                            FontSize = 24,
                            FontAttributes = FontAttributes.Bold,
                            HorizontalTextAlignment = TextAlignment.Center
                        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true),
                        (new Label
                        {
                            Text = "Image Generation is available only when Owner Mode is unlocked.",
                            FontSize = 14,
                            HorizontalTextAlignment = TextAlignment.Center
                        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true),
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

        textStack.Children.Add((new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true));

        textStack.Children.Add((new Label
        {
            Text = description,
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        var frame = (new Frame
        {
            Padding = 16,
            CornerRadius = 8,
            HasShadow = true,
            Content = textStack
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => tapped();
        frame.GestureRecognizers.Add(tap);

        return frame;
    }
}
