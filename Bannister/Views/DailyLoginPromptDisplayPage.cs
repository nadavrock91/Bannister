using Bannister.Models;

using Bannister.Services;

namespace Bannister.Views;

public class DailyLoginPromptDisplayPage : ContentPage
{
    private readonly TaskCompletionSource _closed = new();
    private bool _isClosing;

    private DailyLoginPromptDisplayPage(DailyLoginPrompt prompt, int position, int total)
    {
        Title = "Daily Prompt";
        BackgroundColor = AppColors.Get(ThemeKey.Overlay);

        var container = (new Grid
        {
            Padding = 24
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);

        var card = (new Frame
        {
            Padding = 24,
            CornerRadius = 12,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 520,
            MaximumWidthRequest = 720
        }).ThemeColor(VisualElement.BackgroundColorProperty, SafeColor(prompt.BackgroundColor, ThemeKey.SecondaryButtonBackground));

        var stack = new VerticalStackLayout { Spacing = 18 };
        stack.Children.Add((new Label
        {
            Text = $"Prompt {position} of {total}",
            FontSize = 13,
            Opacity = 0.8,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, SafeColor(prompt.FontColor, ThemeKey.ButtonPrimaryText), ThemeKey.PageText1, classifyPageText: true));

        stack.Children.Add((new Label
        {
            Text = prompt.Text,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, SafeColor(prompt.FontColor, ThemeKey.ButtonPrimaryText), ThemeKey.PageText1, classifyPageText: true));

        var btnNext = (new Button
        {
            Text = position == total ? "Done" : "Next",
            CornerRadius = 8,
            HeightRequest = 46,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Button.TextColorProperty, ThemeKey.CardText1);
        btnNext.Clicked += async (s, e) => await CloseAsync();
        stack.Children.Add(btnNext);

        card.Content = stack;
        container.Children.Add(card);
        Content = container;
    }

    public static async Task ShowAsync(INavigation navigation, DailyLoginPrompt prompt, int position, int total)
    {
        var page = new DailyLoginPromptDisplayPage(prompt, position, total);
        await navigation.PushModalAsync(page, false);
        await page._closed.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync();
        return true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_isClosing) return;
        _closed.TrySetResult();
    }

    private async Task CloseAsync()
    {
        if (_isClosing) return;
        _isClosing = true;
        await Navigation.PopModalAsync(false);
        _closed.TrySetResult();
    }

    private static Color SafeColor(string value, ThemeKey fallback)
    {
        try { return ModeTheme.ParseStoredHex(value); }
        catch { return AppColors.Get(fallback); }
    }
}
