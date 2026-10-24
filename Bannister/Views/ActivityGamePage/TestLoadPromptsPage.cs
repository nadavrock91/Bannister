namespace Bannister.Views;

internal class TestLoadPromptsPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _completion = new();
    private bool _isClosing;

    public TestLoadPromptsPage()
    {
        Title = "Test Load Prompts";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        var stack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12
        };

        stack.Children.Add(new Label
        {
            Text = "Test Load Prompts",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222222")
        });

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
            var button = new Button
            {
                Text = option,
                HeightRequest = 48,
                BackgroundColor = Color.FromArgb("#1976D2"),
                TextColor = Colors.White,
                CornerRadius = 8
            };
            button.Clicked += async (_, _) => await CloseAsync(option);
            stack.Children.Add(button);
        }

        var cancelButton = new Button
        {
            Text = "Cancel",
            HeightRequest = 48,
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#333333"),
            CornerRadius = 8
        };
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
