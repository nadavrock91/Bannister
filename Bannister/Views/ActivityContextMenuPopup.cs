using CommunityToolkit.Maui.Views;

using Bannister.Services;

namespace Bannister.Views;

public class ActivityContextMenuPopup : Popup
{
    private readonly TaskCompletionSource<string?> _tcs = new();

    public ActivityContextMenuPopup(
        string title,
        IEnumerable<string> options)
    {
        CanBeDismissedByTappingOutsideOfPopup = true;

        double screenW =
            DeviceDisplay.MainDisplayInfo.Width /
            DeviceDisplay.MainDisplayInfo.Density;
        double popupW = Math.Min(screenW - 32, 520);

        var stack = (new VerticalStackLayout
        {
            Spacing = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        stack.Children.Add((new Label
        {
            Text = title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap,
            Padding = new Thickness(20, 16, 20, 10)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface));

        stack.Children.Add((new BoxView
        {
            HeightRequest = 1
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator));

        var scrollContent = new VerticalStackLayout { Spacing = 0 };
        foreach (var opt in options.ToList())
        {
            var optLabel = (new Label
            {
                Text = opt,
                FontSize = 14,
                Padding = new Thickness(20, 14),
                LineBreakMode = LineBreakMode.WordWrap
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

            var tap = new TapGestureRecognizer();
            string capturedOpt = opt;
            tap.Tapped += (_, _) =>
            {
                _tcs.TrySetResult(capturedOpt);
                Close();
            };
            optLabel.GestureRecognizers.Add(tap);
            scrollContent.Children.Add(optLabel);
            scrollContent.Children.Add((new BoxView
            {
                HeightRequest = 1
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface));
        }

        var cancelLabel = (new Label
        {
            Text = "Cancel",
            FontSize = 14,
            Padding = new Thickness(20, 14),
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);
        var cancelTap = new TapGestureRecognizer();
        cancelTap.Tapped += (_, _) =>
        {
            _tcs.TrySetResult(null);
            Close();
        };
        cancelLabel.GestureRecognizers.Add(cancelTap);

        scrollContent.Children.Add((new BoxView
        {
            HeightRequest = 1
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator));
        scrollContent.Children.Add(cancelLabel);

        double maxH =
            DeviceDisplay.MainDisplayInfo.Height /
            DeviceDisplay.MainDisplayInfo.Density * 0.7;
        stack.Children.Add(new ScrollView
        {
            Content = scrollContent,
            MaximumHeightRequest = maxH - 60
        });

        Content = (new Border
        {
            Content = stack,
            StrokeShape =
                new Microsoft.Maui.Controls.Shapes.RoundRectangle
                {
                    CornerRadius = new CornerRadius(12)
                },
            StrokeThickness = 0,
            WidthRequest = popupW,
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.2f,
                Radius = 12,
                Offset = new Point(0, 4)
            }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        Color = Colors.Transparent;
    }

    public Task<string?> GetResultAsync() => _tcs.Task;

    protected override Task OnDismissedByTappingOutsideOfPopup(
        CancellationToken cancellationToken)
    {
        _tcs.TrySetResult(null);
        return base.OnDismissedByTappingOutsideOfPopup(
            cancellationToken);
    }
}
