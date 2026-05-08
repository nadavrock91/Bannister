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
        BackgroundColor = Color.FromArgb("#80000000"); // Semi-transparent backdrop
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
        var card = new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 16,
            Padding = 0,
            HasShadow = true,
            WidthRequest = 340,
            MaximumHeightRequest = 500,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var cardStack = new VerticalStackLayout { Spacing = 0 };

        // Header
        var header = new Label
        {
            Text = "⚙️ Options",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            Padding = new Thickness(20, 16, 20, 12)
        };
        cardStack.Children.Add(header);

        // Separator
        cardStack.Children.Add(new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#E0E0E0")
        });

        // Scrollable options list
        var scrollView = new ScrollView();
        var optionsStack = new VerticalStackLayout { Spacing = 0 };

        // Define all options
        var options = new[]
        {
            ("🔧", "Run SQL (Dev)", Color.FromArgb("#607D8B")),
            ("📊", "View Activity Log", Color.FromArgb("#2196F3")),
            ("🔄", "Refresh", Color.FromArgb("#4CAF50")),
            ("✏️", "Manual EXP Adjustment", Color.FromArgb("#FF9800")),
            ("📁", "Change Category (All on Page)", Color.FromArgb("#FF9800")),
            ("💡", "Set All as Possible (All on Page)", Color.FromArgb("#9C27B0")),
            ("📤", "Export Data", Color.FromArgb("#2196F3")),
        };

        foreach (var (icon, label, color) in options)
        {
            optionsStack.Children.Add(BuildOptionRow(icon, label, color));
        }

        scrollView.Content = optionsStack;
        cardStack.Children.Add(scrollView);

        // Separator
        cardStack.Children.Add(new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#E0E0E0")
        });

        // Cancel button
        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#999"),
            FontSize = 16,
            HeightRequest = 50,
            CornerRadius = 0,
            BorderWidth = 0
        };
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
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 48 },
                new ColumnDefinition { Width = GridLength.Star }
            },
            Padding = new Thickness(16, 14),
            BackgroundColor = Colors.White
        };

        // Hover/press effect via pointer gestures
        var pointerEnter = new PointerGestureRecognizer();
        pointerEnter.PointerEntered += (s, e) => grid.BackgroundColor = Color.FromArgb("#F5F5F5");
        var pointerExit = new PointerGestureRecognizer();
        pointerExit.PointerExited += (s, e) => grid.BackgroundColor = Colors.White;
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
        var textLabel = new Label
        {
            Text = label,
            FontSize = 15,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(textLabel, 1);
        grid.Children.Add(textLabel);

        // Tap to select
        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, e) => Close(label);
        grid.GestureRecognizers.Add(tap);

        // Bottom border
        var container = new VerticalStackLayout { Spacing = 0 };
        container.Children.Add(grid);
        container.Children.Add(new BoxView
        {
            HeightRequest = 0.5,
            Color = Color.FromArgb("#F0F0F0")
        });

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
