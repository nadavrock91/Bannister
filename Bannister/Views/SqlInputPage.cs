using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Modal page with a multi-line text editor for pasting SQL statements.
/// Replaces DisplayPromptAsync which only supports single-line input.
/// </summary>
public class SqlInputPage : Bannister.Views.ModeThemedPage
{
    private readonly TaskCompletionSource<string?> _tcs = new();
    private Editor _editor;

    public SqlInputPage()
    {

        BuildUI();
    }

    public Task<string?> WaitForResultAsync() => _tcs.Task;

    private void BuildUI()
    {
        var card = (new Frame
        {
            CornerRadius = 12,
            Padding = 20,
            HasShadow = true,
            WidthRequest = 500,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var stack = new VerticalStackLayout { Spacing = 12 };

        stack.Children.Add((new Label
        {
            Text = "🔧 Run SQL",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        stack.Children.Add((new Label
        {
            Text = "Paste an SQL statement:",
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        _editor = (new Editor
        {
            Placeholder = "SQL statement...",
            HeightRequest = 200,
            FontSize = 14,
            FontFamily = "Consolas",
            AutoSize = EditorAutoSizeOption.Disabled
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
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

        var btnRun = (new Button
        {
            Text = "Run",
            CornerRadius = 8,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnRun.Clicked += (s, e) => Close(_editor.Text);
        Grid.SetColumn(btnRun, 0);
        buttonGrid.Children.Add(btnRun);

        var btnCancel = (new Button
        {
            Text = "Cancel",
            CornerRadius = 8,
            HeightRequest = 44
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator).ThemeColor(Button.TextColorProperty, ThemeKey.CardText1);
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
