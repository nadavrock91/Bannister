using Bannister.Models;

namespace Bannister.Views;

public class DailyLoginPromptDisplayPage : ContentPage
{
    private readonly TaskCompletionSource _closed = new();
    private bool _isClosing;

    private DailyLoginPromptDisplayPage(DailyLoginPrompt prompt, int position, int total)
    {
        Title = "Daily Prompt";
        BackgroundColor = Color.FromArgb("#80000000");

        var container = new Grid
        {
            Padding = 24,
            BackgroundColor = Color.FromArgb("#80000000")
        };

        var card = new Frame
        {
            Padding = 24,
            CornerRadius = 12,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            BackgroundColor = SafeColor(prompt.BackgroundColor, "#5B63EE"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 520,
            MaximumWidthRequest = 720
        };

        var stack = new VerticalStackLayout { Spacing = 18 };
        stack.Children.Add(new Label
        {
            Text = $"Prompt {position} of {total}",
            FontSize = 13,
            TextColor = SafeColor(prompt.FontColor, "#FFFFFF"),
            Opacity = 0.8,
            HorizontalTextAlignment = TextAlignment.Center
        });

        stack.Children.Add(new Label
        {
            Text = prompt.Text,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = SafeColor(prompt.FontColor, "#FFFFFF"),
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap
        });

        var btnNext = new Button
        {
            Text = position == total ? "Done" : "Next",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#333333"),
            CornerRadius = 8,
            HeightRequest = 46,
            FontAttributes = FontAttributes.Bold
        };
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

    private static Color SafeColor(string value, string fallback)
    {
        try { return Color.FromArgb(value); }
        catch { return Color.FromArgb(fallback); }
    }
}
