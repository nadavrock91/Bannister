namespace Bannister.Views;

/// <summary>
/// Reusable multi-line text input modal.
/// Replaces DisplayPromptAsync when multi-line paste support is needed.
/// </summary>
public class TextInputPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _tcs = new();
    private Editor _editor;

    public TextInputPage(string title, string subtitle, string placeholder = "Type or paste text...",
        string confirmText = "OK", string cancelText = "Cancel", string initialValue = "")
    {
        BackgroundColor = Color.FromArgb("#80000000");

        var card = new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 12,
            Padding = 20,
            HasShadow = true,
            WidthRequest = 500,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var stack = new VerticalStackLayout { Spacing = 12 };

        stack.Children.Add(new Label
        {
            Text = title,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        stack.Children.Add(new Label
        {
            Text = subtitle,
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        _editor = new Editor
        {
            Placeholder = placeholder,
            Text = initialValue,
            HeightRequest = 200,
            FontSize = 14,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            AutoSize = EditorAutoSizeOption.Disabled
        };
        stack.Children.Add(_editor);

        var buttonGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12
        };

        var btnConfirm = new Button
        {
            Text = confirmText,
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        };
        btnConfirm.Clicked += (s, e) => Close(_editor.Text);
        Grid.SetColumn(btnConfirm, 0);
        buttonGrid.Children.Add(btnConfirm);

        var btnCancel = new Button
        {
            Text = cancelText,
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            HeightRequest = 44
        };
        btnCancel.Clicked += (s, e) => Close(null);
        Grid.SetColumn(btnCancel, 1);
        buttonGrid.Children.Add(btnCancel);

        stack.Children.Add(buttonGrid);
        card.Content = stack;

        var mainGrid = new Grid();
        var backdrop = new BoxView { BackgroundColor = Colors.Transparent };
        var backdropTap = new TapGestureRecognizer();
        backdropTap.Tapped += (s, e) => Close(null);
        backdrop.GestureRecognizers.Add(backdropTap);
        mainGrid.Children.Add(backdrop);
        mainGrid.Children.Add(card);

        Content = mainGrid;
    }

    public Task<string?> WaitForResultAsync() => _tcs.Task;

    private async void Close(string? result)
    {
        _tcs.TrySetResult(result);
        await Navigation.PopModalAsync(animated: false);
    }

    protected override void OnDisappearing()
    {
        _tcs.TrySetResult(null);
        base.OnDisappearing();
    }
}
