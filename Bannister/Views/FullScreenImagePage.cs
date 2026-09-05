using SkiaSharp;

namespace Bannister.Views;

public class FullScreenImagePage : ContentPage
{
    // Non-interactive mode — crop preview
    public FullScreenImagePage(ImageSource source)
    {
        Title = "";
        BackgroundColor = Colors.Black;
        NavigationPage.SetHasNavigationBar(this, false);

        var image = new Image
        {
            Source = source,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            InputTransparent = true
        };

        var hint = new Label
        {
            Text = "Tap anywhere to close",
            FontSize = 13,
            TextColor = Colors.White.WithAlpha(0.6f),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, 24)
        };

        var rootGrid = new Grid
        {
            BackgroundColor = Colors.Black,
            Children = { image, hint }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await Navigation.PopAsync();
        rootGrid.GestureRecognizers.Add(tap);

        Content = rootGrid;
    }

    // Interactive grid mode — accepts raw PNG bytes for redraw
    public FullScreenImagePage(
        byte[] sourceImageBytes,
        bool[,] selected,
        int rows,
        int cols,
        int imagePixelW,
        int imagePixelH,
        Action<bool[,]>? onSelectionChanged)
    {
        Title = "";
        BackgroundColor = Colors.Black;
        NavigationPage.SetHasNavigationBar(this, false);

        // Deep-copy selection
        bool[,] localSelected = new bool[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                localSelected[r, c] = selected[r, c];

        var displayImage = new Image
        {
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            InputTransparent = true
        };

        void Redraw()
        {
            using var srcBmp = SKBitmap.Decode(sourceImageBytes);
            if (srcBmp == null) return;

            using var overlayBmp = srcBmp.Copy();
            using var canvas = new SKCanvas(overlayBmp);

            int cellW = imagePixelW / cols;
            int cellH = imagePixelH / rows;

            using var fillPaint = new SKPaint
            {
                Color = new SKColor(30, 136, 229, 110),
                Style = SKPaintStyle.Fill
            };
            using var borderPaint = new SKPaint
            {
                Color = new SKColor(30, 136, 229, 255),
                StrokeWidth = 5,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };
            using var linePaint = new SKPaint
            {
                Color = new SKColor(255, 255, 255, 140),
                StrokeWidth = 2,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };
            using var textPaint = new SKPaint
            {
                Color = SKColors.White,
                TextSize = Math.Max(20, cellW / 8f),
                IsAntialias = true,
                FakeBoldText = true
            };
            using var shadowPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 180),
                TextSize = textPaint.TextSize,
                IsAntialias = true,
                FakeBoldText = true
            };

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int x = c * cellW;
                    int y = r * cellH;
                    var cellRect = new SKRect(x, y, x + cellW, y + cellH);
                    if (localSelected[r, c])
                    {
                        canvas.DrawRect(cellRect, fillPaint);
                        canvas.DrawRect(cellRect, borderPaint);
                    }
                }
            }

            for (int c = 1; c < cols; c++)
                canvas.DrawLine(c * cellW, 0, c * cellW, imagePixelH, linePaint);
            for (int r = 1; r < rows; r++)
                canvas.DrawLine(0, r * cellH, imagePixelW, r * cellH, linePaint);

            int panel = 1;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float tx = c * cellW + cellW * 0.05f;
                    float ty = r * cellH + textPaint.TextSize + cellH * 0.04f;
                    canvas.DrawText(panel.ToString(), tx + 2, ty + 2, shadowPaint);
                    canvas.DrawText(panel.ToString(), tx, ty, textPaint);
                    panel++;
                }
            }

            using var img = SKImage.FromBitmap(overlayBmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 90);
            var bytes = data.ToArray();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                displayImage.Source = ImageSource.FromStream(
                    () => new MemoryStream(bytes));
            });
        }

        Redraw();

        var hint = new Label
        {
            Text = "Tap panels to toggle. Blue = selected.",
            FontSize = 13,
            TextColor = Colors.White.WithAlpha(0.8f),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, 70)
        };

        var closeBtn = new Button
        {
            Text = "✓ Done",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44,
            WidthRequest = 140,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, 16)
        };

        // FIX: PopAsync first, THEN invoke callback so UI updates
        // land after the page is back in view
        closeBtn.Clicked += async (_, _) =>
        {
            await Navigation.PopAsync();
            onSelectionChanged?.Invoke(localSelected);
        };

        var rootGrid = new Grid
        {
            BackgroundColor = Colors.Black,
            Children = { displayImage, hint, closeBtn }
        };

#if WINDOWS
        var pointer = new PointerGestureRecognizer();
        pointer.PointerPressed += (s, e) =>
        {
            var pos = e.GetPosition(rootGrid);
            if (pos == null) return;
            HandleGridTap(pos.Value.X, pos.Value.Y,
                rootGrid.Width, rootGrid.Height,
                imagePixelW, imagePixelH,
                rows, cols, localSelected);
            Redraw();
        };
        rootGrid.GestureRecognizers.Add(pointer);
#else
        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, e) =>
        {
            var pos = e.GetPosition(rootGrid);
            if (pos == null) return;
            HandleGridTap(pos.Value.X, pos.Value.Y,
                rootGrid.Width, rootGrid.Height,
                imagePixelW, imagePixelH,
                rows, cols, localSelected);
            Redraw();
        };
        rootGrid.GestureRecognizers.Add(tap);
#endif

        Content = rootGrid;
    }

    private static void HandleGridTap(
        double tapX, double tapY,
        double viewW, double viewH,
        int imagePixelW, int imagePixelH,
        int rows, int cols,
        bool[,] selected)
    {
        if (viewW <= 0 || viewH <= 0) return;

        double imageAspect = (double)imagePixelW / imagePixelH;
        double viewAspect = viewW / viewH;

        double renderedW, renderedH, offsetX, offsetY;
        if (imageAspect > viewAspect)
        {
            renderedW = viewW;
            renderedH = viewW / imageAspect;
            offsetX = 0;
            offsetY = (viewH - renderedH) / 2.0;
        }
        else
        {
            renderedH = viewH;
            renderedW = viewH * imageAspect;
            offsetX = (viewW - renderedW) / 2.0;
            offsetY = 0;
        }

        double relX = tapX - offsetX;
        double relY = tapY - offsetY;

        if (relX < 0 || relY < 0 || relX > renderedW || relY > renderedH)
            return;

        int col = Math.Clamp((int)(relX / renderedW * cols), 0, cols - 1);
        int row = Math.Clamp((int)(relY / renderedH * rows), 0, rows - 1);

        selected[row, col] = !selected[row, col];
    }

    protected override bool OnBackButtonPressed()
    {
        Navigation.PopAsync();
        return true;
    }
}
