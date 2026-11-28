using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

internal static class CompanionAvatarPresentation
{
    public static View Create(Companion companion, double size)
    {
        View content;
        if (companion.AvatarImage is { Length: > 0 } imageBytes)
        {
            content = new Image
            {
                Source = ImageSource.FromStream(() => new MemoryStream(imageBytes, writable: false)),
                Aspect = Aspect.AspectFill,
                HeightRequest = size,
                WidthRequest = size
            };
        }
        else
        {
            string initial = string.IsNullOrWhiteSpace(companion.Nickname)
                ? "?"
                : companion.Nickname.Trim()[0].ToString().ToUpperInvariant();
            content = (new Label
            {
                Text = initial,
                FontSize = size * 0.42,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                HeightRequest = size,
                WidthRequest = size
            }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightIndigoText);
        }

        return (new Border
        {
            Content = content,
            HeightRequest = size,
            WidthRequest = size,
            Padding = 0,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(size / 2)
            }
        }).ThemeColor(Border.StrokeProperty, ThemeKey.Separator)
          .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightIndigo);
    }
}
