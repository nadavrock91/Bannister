using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Scrollable popup page that replaces DisplayActionSheet for the Options menu.
/// Supports unlimited items without cropping.
/// </summary>
public class OptionsPopupPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _tcs = new();

    public OptionsPopupPage()
    {
        BackgroundColor = AppColors.Get(ThemeKey.Overlay); // Semi-transparent backdrop
        BuildUI();
    }

    public Task<string?> WaitForResultAsync() => _tcs.Task;

    private void BuildUI()
    {
        // Tap backdrop to cancel
        var backdropTap = new TapGestureRecognizer();
        backdropTap.Tapped += (s, e) => Close(null);

        var backdrop = new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        backdrop.GestureRecognizers.Add(backdropTap);

        // Card in the center
        var card = (new Frame
        {
            CornerRadius = 16,
            Padding = 0,
            HasShadow = true,
            WidthRequest = 340,
            MaximumHeightRequest = 620,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var cardStack = new VerticalStackLayout { Spacing = 0 };

        // Header
        var header = (new Label
        {
            Text = "⚙️ Options",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(20, 16, 20, 12)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true);
        cardStack.Children.Add(header);

        // Separator
        cardStack.Children.Add((new BoxView
        {
            HeightRequest = 1
        }).ThemeColor(BoxView.ColorProperty, ThemeKey.Separator));

        // Scrollable options list
        var scrollView = new ScrollView();
        var optionsStack = new VerticalStackLayout { Spacing = 0 };

        // Define all options
        var options = new[]
        {
            ("🔧", "Run SQL (Dev)", AppColors.Get(ThemeKey.NeutralMutedButton)),
            ("📊", "View Activity Log", AppColors.Get(ThemeKey.InfoLinkText)),
            ("🔄", "Refresh", AppColors.Get(ThemeKey.SuccessLightText)),
            ("✏️", "Manual EXP Adjustment", AppColors.Get(ThemeKey.WarningAccentText)),
            ("📁", "Change Category (All on Page)", AppColors.Get(ThemeKey.WarningAccentText)),
            ("💡", "Set All as Possible (All on Page)", AppColors.Get(ThemeKey.AccentPurpleButtonBackground)),
            ("📤", "Export Data", AppColors.Get(ThemeKey.InfoLinkText)),
            ("🔄", "Reset Load Prompts", AppColors.Get(ThemeKey.ErrorButtonBackground)),
            ("⚡", "Test Load Prompts", AppColors.Get(ThemeKey.WarningAccentText)),
        };

        foreach (var (icon, label, color) in options)
        {
            optionsStack.Children.Add(BuildOptionRow(icon, label, color));
        }

        scrollView.Content = optionsStack;
        cardStack.Children.Add(scrollView);

        // Separator
        cardStack.Children.Add((new BoxView
        {
            HeightRequest = 1
        }).ThemeColor(BoxView.ColorProperty, ThemeKey.Separator));

        // Cancel button
        var cancelBtn = (new Button
        {
            Text = "Cancel",
            FontSize = 16,
            HeightRequest = 50,
            CornerRadius = 0,
            BorderWidth = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Button.TextColorProperty, ThemeKey.CardText3);
        cancelBtn.Clicked += (s, e) => Close(null);
        cardStack.Children.Add(cancelBtn);

        card.Content = cardStack;

        // Layer: backdrop + card
        var mainGrid = new Grid();
        mainGrid.Children.Add(backdrop);
        mainGrid.Children.Add(card);

        Content = mainGrid;
    }

    private View BuildOptionRow(string icon, string label, Color accentColor)
    {
        var grid = (new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 48 },
                new ColumnDefinition { Width = GridLength.Star }
            },
            Padding = new Thickness(16, 14)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        // Hover/press effect via pointer gestures
        var pointerEnter = new PointerGestureRecognizer();
        pointerEnter.PointerEntered += (s, e) => grid.ThemeColor(BackgroundColorProperty, ThemeKey.SecondarySurface);
        var pointerExit = new PointerGestureRecognizer();
        pointerExit.PointerExited += (s, e) => grid.ThemeColor(BackgroundColorProperty, ThemeKey.CardBackground);
        grid.GestureRecognizers.Add(pointerEnter);
        grid.GestureRecognizers.Add(pointerExit);

        // Icon
        var iconLabel = new Label
        {
            Text = icon,
            FontSize = 20,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(iconLabel, 0);
        grid.Children.Add(iconLabel);

        // Label
        var textLabel = (new Label
        {
            Text = label,
            FontSize = 15,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true);
        Grid.SetColumn(textLabel, 1);
        grid.Children.Add(textLabel);

        // Tap to select
        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, e) => Close(label);
        grid.GestureRecognizers.Add(tap);

        // Bottom border
        var container = new VerticalStackLayout { Spacing = 0 };
        container.Children.Add(grid);
        container.Children.Add((new BoxView
        {
            HeightRequest = 0.5
        }).ThemeColor(BoxView.ColorProperty, ThemeKey.SecondarySurface));

        return container;
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

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}
