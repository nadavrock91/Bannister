namespace Bannister.Views;

public class WebsiteMultilineEditorPage : ContentPage
{
    private readonly Editor _editor;
    private readonly TaskCompletionSource<string?> _resultTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _closingFromButton;
    private bool _isClosing;

    public WebsiteMultilineEditorPage(
        string title,
        string subtitle,
        string initialText,
        string placeholder = "")
    {
        Title = title;
        BackgroundColor = Color.FromArgb("#F5F5F5");

        _editor = new Editor
        {
            AutoSize = EditorAutoSizeOption.Disabled,
            Text = initialText,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            FontSize = 13,
            HeightRequest = 400,
            MinimumHeightRequest = 300,
            Placeholder = placeholder
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            HeightRequest = 44,
            FontSize = 14
        };
        cancelButton.Clicked += async (_, _) => await CloseAsync(null);

        var saveButton = new Button
        {
            Text = "Save",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 44,
            FontSize = 14
        };
        saveButton.Clicked += async (_, _) => await CloseAsync(_editor.Text ?? string.Empty);

        var buttonGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10
        };
        buttonGrid.Add(cancelButton, 0, 0);
        buttonGrid.Add(saveButton, 1, 0);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        Text = title,
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#222")
                    },
                    new Label
                    {
                        Text = subtitle,
                        FontSize = 14,
                        TextColor = Color.FromArgb("#555")
                    },
                    _editor,
                    buttonGrid
                }
            }
        };
    }

    public Task<string?> ShowAsync()
    {
        return _resultTcs.Task;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (!_closingFromButton)
            _resultTcs.TrySetResult(null);
    }

    private async Task CloseAsync(string? result)
    {
        if (_isClosing)
            return;

        _isClosing = true;
        _closingFromButton = true;
        await Navigation.PopModalAsync();
        _resultTcs.TrySetResult(result);
    }
}
