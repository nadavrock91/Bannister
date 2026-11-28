using Bannister.Models;
using Bannister.Services;
using SkiaSharp;

namespace Bannister.Views;

internal static class CompanionAvatarPresentation
{
    public static View Create(
        Companion companion,
        double size,
        byte[]? levelImage = null,
        int? levelImageFit = null)
    {
        View content;
        byte[]? imageBytes = levelImage is { Length: > 0 } ? levelImage : companion.AvatarImage;
        if (imageBytes is { Length: > 0 })
        {
            content = new Image
            {
                Source = ImageSource.FromStream(() => new MemoryStream(imageBytes, writable: false)),
                Aspect = ImageFitCycle.ToAspect(
                    levelImage is { Length: > 0 }
                        ? levelImageFit ?? ImageFitCycle.DefaultMode
                        : companion.AvatarImageFit),
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

internal static class CompanionAvatarImageProcessor
{
    public static bool IsSupportedFileName(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp";

    public static byte[] LoadAndResizePng(Stream stream)
    {
        using var source = SKBitmap.Decode(stream)
            ?? throw new InvalidOperationException("The selected file is not a readable image.");
        return ResizeAndEncodePng(source);
    }

    public static IReadOnlyList<byte[]> SplitConceptSheet(Stream stream)
    {
        using var source = SKBitmap.Decode(stream)
            ?? throw new InvalidOperationException("The selected file is not a readable image.");
        int cellWidth = source.Width / 4;
        int cellHeight = source.Height / 5;
        if (cellWidth < 1 || cellHeight < 1)
            throw new InvalidOperationException("The concept sheet is too small to split into a 4 by 5 grid.");

        var cells = new List<byte[]>(20);
        for (int row = 0; row < 5; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                using var cell = new SKBitmap(cellWidth, cellHeight);
                var bounds = new SKRectI(
                    column * cellWidth,
                    row * cellHeight,
                    (column + 1) * cellWidth,
                    (row + 1) * cellHeight);
                if (!source.ExtractSubset(cell, bounds))
                    throw new InvalidOperationException("A concept-sheet panel could not be extracted.");
                cells.Add(ResizeAndEncodePng(cell));
            }
        }
        return cells;
    }

    private static byte[] ResizeAndEncodePng(SKBitmap source)
    {
        const int maxSide = 512;
        SKBitmap? resized = null;
        SKBitmap output = source;
        int longestSide = Math.Max(source.Width, source.Height);
        if (longestSide > maxSide)
        {
            double scale = maxSide / (double)longestSide;
            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale));
            resized = source.Resize(new SKImageInfo(width, height), SKFilterQuality.High)
                ?? throw new InvalidOperationException("The selected image could not be resized.");
            output = resized;
        }
        try
        {
            using var image = SKImage.FromBitmap(output);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        finally
        {
            resized?.Dispose();
        }
    }
}
