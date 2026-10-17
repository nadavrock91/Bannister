namespace Bannister.Views;

/// <summary>
/// Modal page with a multi-line text editor for pasting SQL statements.
/// Replaces DisplayPromptAsync which only supports single-line input.
/// </summary>
public class SqlInputPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _tcs = new();
    private Editor _editor;

    public SqlInputPage()
    {
        BackgroundColor = Color.FromArgb("#80000000");
        BuildUI();
    }

    public Task<string?> WaitForResultAsync() => _tcs.Task;

    private void BuildUI()
    {
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
            Text = "🔧 Run SQL",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        stack.Children.Add(new Label
        {
            Text = "Paste an SQL statement:",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        _editor = new Editor
        {
            Placeholder = "SQL statement...",
            HeightRequest = 200,
            FontSize = 14,
            FontFamily = "Consolas",
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            AutoSize = EditorAutoSizeOption.Disabled
        };
        stack.Children.Add(_editor);

        // Buttons
        var buttonGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12
        };

        var btnRun = new Button
        {
            Text = "Run",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        };
        btnRun.Clicked += (s, e) => Close(_editor.Text);
        Grid.SetColumn(btnRun, 0);
        buttonGrid.Children.Add(btnRun);

        var btnCancel = new Button
        {
            Text = "Cancel",
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

        // Backdrop tap to cancel
        var mainGrid = new Grid();
        var backdrop = new BoxView { BackgroundColor = Colors.Transparent };
        var backdropTap = new TapGestureRecognizer();
        backdropTap.Tapped += (s, e) => Close(null);
        backdrop.GestureRecognizers.Add(backdropTap);
        mainGrid.Children.Add(backdrop);
        mainGrid.Children.Add(card);

        Content = mainGrid;
    }

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
