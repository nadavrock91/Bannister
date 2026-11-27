using Bannister.Services;

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
        BackgroundColor = AppColors.Get(ThemeKey.Overlay);

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
            Text = title,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        stack.Children.Add((new Label
        {
            Text = subtitle,
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        _editor = (new Editor
        {
            Placeholder = placeholder,
            Text = initialValue,
            HeightRequest = 200,
            FontSize = 14,
            AutoSize = EditorAutoSizeOption.Disabled
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
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

        var btnConfirm = (new Button
        {
            Text = confirmText,
            CornerRadius = 8,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnConfirm.Clicked += (s, e) => Close(_editor.Text);
        Grid.SetColumn(btnConfirm, 0);
        buttonGrid.Children.Add(btnConfirm);

        var btnCancel = (new Button
        {
            Text = cancelText,
            CornerRadius = 8,
            HeightRequest = 44
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator).ThemeColor(Button.TextColorProperty, ThemeKey.CardText1);
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
