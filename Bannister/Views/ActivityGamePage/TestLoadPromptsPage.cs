using Bannister.Services;

namespace Bannister.Views;

internal class TestLoadPromptsPage : Bannister.Views.ModeThemedPage
{
    private readonly TaskCompletionSource<string?> _completion = new();
    private bool _isClosing;

    public TestLoadPromptsPage()
    {
        Title = "Test Load Prompts";


        var stack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12
        };

        stack.Children.Add((new Label
        {
            Text = "Test Load Prompts",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true));

        foreach (string option in new[]
        {
            "Broken Streak",
            "Stale Activities",
            "Expired Activities",
            "Habit Target (Expired Mode)",
            "Auto Award",
            "Activity Suggestions",
            "Catch-Up (Missed Days)"
        })
        {
            var button = (new Button
            {
                Text = option,
                HeightRequest = 48,
                CornerRadius = 8
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoHoverButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
            button.Clicked += async (_, _) => await CloseAsync(option);
            stack.Children.Add(button);
        }

        var cancelButton = (new Button
        {
            Text = "Cancel",
            HeightRequest = 48,
            CornerRadius = 8
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator).ThemeColor(Button.TextColorProperty, ThemeKey.CardText1);
        cancelButton.Clicked += async (_, _) => await CloseAsync(null);
        stack.Children.Add(cancelButton);

        Content = new ScrollView { Content = stack };
    }

    public Task<string?> WaitForResultAsync() => _completion.Task;

    private async Task CloseAsync(string? result)
    {
        if (_isClosing) return;

        _isClosing = true;
        await Navigation.PopModalAsync();
        _completion.TrySetResult(result);
    }

    protected override void OnDisappearing()
    {
        if (!_isClosing)
            _completion.TrySetResult(null);

        base.OnDisappearing();
    }
}
