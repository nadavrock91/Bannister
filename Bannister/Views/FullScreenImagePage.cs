using SkiaSharp;

namespace Bannister.Views;

public sealed class GridGeometry
{
    public int Cols;
    public int Rows;
    public int CellW;
    public int CellH;
    public bool SeparateBottom;
    public int BottomCols;
    public int BottomCellW;
    public int BottomCellH;
    public int[] ColEdges = Array.Empty<int>();
    public int[] RowEdges = Array.Empty<int>();
    public int BottomTop;
    public int[] BottomColEdges = Array.Empty<int>();
    public int MainRows => SeparateBottom ? Math.Max(1, Rows - 1) : Rows;

    public void EnsureEdges(int imgW, int imgH)
    {
        if (ColEdges.Length != Cols + 1)
            ColEdges = Enumerable.Range(0, Cols + 1).Select(i => i * CellW).ToArray();
        if (RowEdges.Length != MainRows + 1)
            RowEdges = Enumerable.Range(0, MainRows + 1).Select(i => i * CellH).ToArray();
        if (SeparateBottom)
        {
            if (BottomTop <= 0) BottomTop = Math.Max(0, imgH - BottomCellH);
            if (BottomColEdges.Length != BottomCols + 1)
                BottomColEdges = Enumerable.Range(0, BottomCols + 1)
                    .Select(i => i * BottomCellW).ToArray();
        }
    }

    public GridGeometry Clone()
    {
        var clone = (GridGeometry)MemberwiseClone();
        clone.ColEdges = (int[])ColEdges.Clone();
        clone.RowEdges = (int[])RowEdges.Clone();
        clone.BottomColEdges = (int[])BottomColEdges.Clone();
        return clone;
    }
}

public class FullScreenImagePage : ContentPage
{
    private enum HandleKind { MainV, MainH, BottomV, BottomH }

    private sealed class Handle
    {
        public BoxView View = null!;
        public HandleKind Kind;
        public int Index;
    }

    private SKBitmap? _geometryBitmap;
    private double _panStartImageX = -1;
    private double _panStartImageY = -1;

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

    public FullScreenImagePage(
        byte[] sourceImageBytes,
        bool[,] selected,
        int imagePixelW,
        int imagePixelH,
        GridGeometry geometry,
        Action<bool[,]>? onSelectionChanged,
        Action<GridGeometry>? onGeometryChanged)
    {
        Title = "";
        BackgroundColor = Colors.Black;
        NavigationPage.SetHasNavigationBar(this, false);

        var g = geometry.Clone();
        g.EnsureEdges(imagePixelW, imagePixelH);
        int selectionRows = selected.GetLength(0);
        int selectionCols = selected.GetLength(1);
        var localSelected = new bool[selectionRows, selectionCols];
        for (int r = 0; r < selectionRows; r++)
            for (int c = 0; c < selectionCols; c++)
                localSelected[r, c] = selected[r, c];

        _geometryBitmap = SKBitmap.Decode(sourceImageBytes);

        var displayImage = new Image
        {
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            InputTransparent = true
        };

        var overlay = new AbsoluteLayout
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            InputTransparent = true
        };

        var handles = new List<Handle>();
        var dragLabel = new Label
        {
            TextColor = Colors.White,
            BackgroundColor = Colors.Black.WithAlpha(0.7f),
            FontSize = 12,
            Padding = new Thickness(8, 4),
            IsVisible = false
        };

        void BuildHandles()
        {
            overlay.Children.Clear();
            handles.Clear();

            void AddHandle(HandleKind kind, int index, string color)
            {
                var view = new BoxView
                {
                    Color = Color.FromArgb(color),
                    CornerRadius = 2,
                    Opacity = 0.9
                };
                handles.Add(new Handle { View = view, Kind = kind, Index = index });
                overlay.Children.Add(view);
            }

            for (int k = 1; k <= g.Cols; k++)
                AddHandle(HandleKind.MainV, k, "#42A5F5");
            for (int k = 1; k <= g.MainRows; k++)
                AddHandle(HandleKind.MainH, k, "#42A5F5");
            if (g.SeparateBottom)
            {
                AddHandle(HandleKind.BottomH, 1, "#FFD54F");
                for (int k = 1; k <= g.BottomCols; k++)
                    AddHandle(HandleKind.BottomV, k, "#FFD54F");
            }

            overlay.Children.Add(dragLabel);
        }

        BuildHandles();

#if WINDOWS
        const string geometryHint = "Tap panels to toggle · drag any line to move just that line";
#else
        const string geometryHint = "Tap a grid line to select it, then use ◀▶▲▼ to nudge · tap panel to toggle";
#endif
        var hint = new Label
        {
            Text = geometryHint,
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

        var rootGrid = new Grid
        {
            BackgroundColor = Colors.Black,
            Children = { displayImage, overlay, hint, closeBtn }
        };

        (double renderedW, double renderedH, double offsetX, double offsetY) GetFit(
            double viewW, double viewH)
        {
            if (viewW <= 0 || viewH <= 0 || imagePixelW <= 0 || imagePixelH <= 0)
                return (0, 0, 0, 0);
            double imageAspect = (double)imagePixelW / imagePixelH;
            double viewAspect = viewW / viewH;
            if (imageAspect > viewAspect)
            {
                double renderedW = viewW;
                double renderedH = viewW / imageAspect;
                return (renderedW, renderedH, 0, (viewH - renderedH) / 2.0);
            }
            else
            {
                double renderedH = viewH;
                double renderedW = viewH * imageAspect;
                return (renderedW, renderedH, (viewW - renderedW) / 2.0, 0);
            }
        }

        (double x, double y) ImageToView(double x, double y)
        {
            var fit = GetFit(rootGrid.Width, rootGrid.Height);
            return (
                fit.offsetX + x / imagePixelW * fit.renderedW,
                fit.offsetY + y / imagePixelH * fit.renderedH);
        }

        (double x, double y) ViewToImage(double x, double y)
        {
            var fit = GetFit(rootGrid.Width, rootGrid.Height);
            if (fit.renderedW <= 0 || fit.renderedH <= 0) return (0, 0);
            return (
                (x - fit.offsetX) / fit.renderedW * imagePixelW,
                (y - fit.offsetY) / fit.renderedH * imagePixelH);
        }

        void PositionHandle(Handle handle)
        {
            var fit = GetFit(rootGrid.Width, rootGrid.Height);
            if (fit.renderedW <= 0 || fit.renderedH <= 0) return;

            double x1, y1, x2, y2;
            switch (handle.Kind)
            {
                case HandleKind.MainV:
                    x1 = x2 = g.ColEdges[handle.Index];
                    y1 = 0;
                    y2 = g.RowEdges[g.MainRows];
                    handle.View.IsVisible = x1 >= 0 && x1 <= imagePixelW;
                    break;
                case HandleKind.MainH:
                    x1 = 0;
                    x2 = g.ColEdges[g.Cols];
                    y1 = y2 = g.RowEdges[handle.Index];
                    handle.View.IsVisible = y1 >= 0 && y1 <= imagePixelH;
                    break;
                case HandleKind.BottomH:
                    x1 = 0;
                    x2 = imagePixelW;
                    y1 = y2 = g.BottomTop;
                    handle.View.IsVisible = y1 >= 0 && y1 <= imagePixelH;
                    break;
                default:
                    x1 = x2 = g.BottomColEdges[handle.Index];
                    y1 = g.BottomTop;
                    y2 = imagePixelH;
                    handle.View.IsVisible = x1 >= 0 && x1 <= imagePixelW;
                    break;
            }

            if (!handle.View.IsVisible) return;
            var start = ImageToView(x1, y1);
            var end = ImageToView(x2, y2);
            if (handle.Kind is HandleKind.MainV or HandleKind.BottomV)
                AbsoluteLayout.SetLayoutBounds(handle.View,
                    new Rect(start.x - 2, Math.Min(start.y, end.y), 4,
                        Math.Max(4, Math.Abs(end.y - start.y))));
            else
                AbsoluteLayout.SetLayoutBounds(handle.View,
                    new Rect(Math.Min(start.x, end.x), start.y - 2,
                        Math.Max(4, Math.Abs(end.x - start.x)), 4));
        }

        void PositionHandles()
        {
            foreach (var handle in handles)
                PositionHandle(handle);
        }

        void Redraw()
        {
            if (_geometryBitmap == null) return;
            using var overlayBmp = _geometryBitmap.Copy();
            using var canvas = new SKCanvas(overlayBmp);

            int mainRows = g.MainRows;
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
                Color = new SKColor(255, 255, 255, 200),
                StrokeWidth = 2,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };
            using var yellowPaint = new SKPaint
            {
                Color = new SKColor(255, 213, 79, 230),
                StrokeWidth = 3,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };
            using var textPaint = new SKPaint
            {
                Color = SKColors.White,
                TextSize = Math.Max(20, (g.ColEdges[1] - g.ColEdges[0]) / 8f),
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

            for (int r = 0; r < mainRows; r++)
            {
                for (int c = 0; c < g.Cols; c++)
                {
                    int left = g.ColEdges[c];
                    int top = g.RowEdges[r];
                    int right = g.ColEdges[c + 1];
                    int bottom = g.RowEdges[r + 1];
                    var rect = new SKRect(left, top, right, bottom);
                    if (r < selectionRows && c < selectionCols && localSelected[r, c])
                    {
                        canvas.DrawRect(rect, fillPaint);
                        canvas.DrawRect(rect, borderPaint);
                    }
                    int panel = r * g.Cols + c + 1;
                    float tx = left + (right - left) * 0.05f;
                    float ty = top + textPaint.TextSize + (bottom - top) * 0.04f;
                    canvas.DrawText(panel.ToString(), tx + 2, ty + 2, shadowPaint);
                    canvas.DrawText(panel.ToString(), tx, ty, textPaint);
                }
            }
            for (int c = 1; c <= g.Cols; c++)
                canvas.DrawLine(g.ColEdges[c], 0, g.ColEdges[c],
                    g.RowEdges[mainRows], linePaint);
            for (int r = 1; r <= mainRows; r++)
                canvas.DrawLine(0, g.RowEdges[r], g.ColEdges[g.Cols],
                    g.RowEdges[r], linePaint);

            if (g.SeparateBottom)
            {
                int bottomY = g.BottomTop;
                int botH = imagePixelH - bottomY;
                canvas.DrawLine(0, bottomY, imagePixelW, bottomY, yellowPaint);
                for (int c = 1; c <= g.BottomCols; c++)
                    canvas.DrawLine(g.BottomColEdges[c], bottomY,
                        g.BottomColEdges[c], imagePixelH, yellowPaint);
                for (int c = 0; c < g.BottomCols; c++)
                {
                    int left = g.BottomColEdges[c];
                    int right = g.BottomColEdges[c + 1];
                    var rect = new SKRect(left, bottomY, right, imagePixelH);
                    if (g.MainRows < selectionRows && c < selectionCols &&
                        localSelected[g.MainRows, c])
                    {
                        canvas.DrawRect(rect, fillPaint);
                        canvas.DrawRect(rect, borderPaint);
                    }
                    int panel = g.MainRows * g.Cols + c + 1;
                    float tx = left + (right - left) * 0.05f;
                    float ty = bottomY + textPaint.TextSize + botH * 0.04f;
                    canvas.DrawText(panel.ToString(), tx + 2, ty + 2, shadowPaint);
                    canvas.DrawText(panel.ToString(), tx, ty, textPaint);
                }
            }

            using var img = SKImage.FromBitmap(overlayBmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 90);
            var bytes = data.ToArray();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                displayImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
            });
        }

        closeBtn.Clicked += async (_, _) =>
        {
            await Navigation.PopAsync();
            onSelectionChanged?.Invoke(localSelected);
            onGeometryChanged?.Invoke(g);
        };

        rootGrid.SizeChanged += (_, _) => PositionHandles();

#if WINDOWS
        Handle? activeHandle = null;
        var pointer = new PointerGestureRecognizer();
        pointer.PointerPressed += (_, e) =>
        {
            var pos = e.GetPosition(rootGrid);
            if (pos == null) return;
            var fit = GetFit(rootGrid.Width, rootGrid.Height);

            double bestDistance = double.MaxValue;
            Handle? bestHandle = null;
            foreach (var handle in handles.Where(h => h.View.IsVisible))
            {
                var bounds = AbsoluteLayout.GetLayoutBounds(handle.View);
                bool vertical = handle.Kind is HandleKind.MainV or HandleKind.BottomV;
                double distance = vertical
                    ? Math.Abs(pos.Value.X - (bounds.X + bounds.Width / 2.0))
                    : Math.Abs(pos.Value.Y - (bounds.Y + bounds.Height / 2.0));
                bool withinSpan = vertical
                    ? pos.Value.Y >= bounds.Y - 14 && pos.Value.Y <= bounds.Bottom + 14
                    : pos.Value.X >= bounds.X - 14 && pos.Value.X <= bounds.Right + 14;
                if (withinSpan && distance <= 14 && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestHandle = handle;
                }
            }

            if (bestHandle != null)
            {
                activeHandle = bestHandle;
            }
            else
            {
                HandleGridTap(pos.Value.X, pos.Value.Y, fit.renderedW, fit.renderedH,
                    fit.offsetX, fit.offsetY, imagePixelW, imagePixelH, g, localSelected);
                Redraw();
            }
        };
        pointer.PointerMoved += (_, e) =>
        {
            if (activeHandle == null) return;
            var pos = e.GetPosition(rootGrid);
            if (pos == null) return;
            var imagePos = ViewToImage(pos.Value.X, pos.Value.Y);
            int x = Math.Clamp((int)Math.Round(imagePos.x), 0, imagePixelW);
            int y = Math.Clamp((int)Math.Round(imagePos.y), 0, imagePixelH);
            dragLabel.IsVisible = true;
            switch (activeHandle.Kind)
            {
                case HandleKind.MainV:
                {
                    int min = g.ColEdges[activeHandle.Index - 1] + 2;
                    int max = activeHandle.Index < g.Cols
                        ? g.ColEdges[activeHandle.Index + 1] - 2
                        : imagePixelW;
                    int value = Math.Clamp(x, min, Math.Max(min, max));
                    g.ColEdges[activeHandle.Index] = value;
                    dragLabel.Text = $"x = {value} px (col {activeHandle.Index} width {value - g.ColEdges[activeHandle.Index - 1]})";
                    break;
                }
                case HandleKind.MainH:
                {
                    int min = g.RowEdges[activeHandle.Index - 1] + 2;
                    int max = activeHandle.Index < g.MainRows
                        ? g.RowEdges[activeHandle.Index + 1] - 2
                        : imagePixelH;
                    int value = Math.Clamp(y, min, Math.Max(min, max));
                    g.RowEdges[activeHandle.Index] = value;
                    dragLabel.Text = $"y = {value} px (row {activeHandle.Index} height {value - g.RowEdges[activeHandle.Index - 1]})";
                    break;
                }
                case HandleKind.BottomH:
                {
                    int value = Math.Clamp(y, 1, Math.Max(1, imagePixelH - 2));
                    g.BottomTop = value;
                    dragLabel.Text = $"strip top = {value} px (height {imagePixelH - value})";
                    break;
                }
                case HandleKind.BottomV:
                {
                    int min = g.BottomColEdges[activeHandle.Index - 1] + 2;
                    int max = activeHandle.Index < g.BottomCols
                        ? g.BottomColEdges[activeHandle.Index + 1] - 2
                        : imagePixelW;
                    int value = Math.Clamp(x, min, Math.Max(min, max));
                    g.BottomColEdges[activeHandle.Index] = value;
                    dragLabel.Text = $"x = {value} px (col {activeHandle.Index} width {value - g.BottomColEdges[activeHandle.Index - 1]})";
                    break;
                }
            }
            PositionHandle(activeHandle);
            AbsoluteLayout.SetLayoutBounds(dragLabel,
                new Rect(Math.Clamp(pos.Value.X + 12, 0, Math.Max(0, rootGrid.Width - 160)),
                    Math.Clamp(pos.Value.Y + 12, 0, Math.Max(0, rootGrid.Height - 40)),
                    150, 34));
        };
        pointer.PointerReleased += (_, _) =>
        {
            if (activeHandle == null) return;
            activeHandle = null;
            dragLabel.IsVisible = false;
            Redraw();
            PositionHandles();
        };
        rootGrid.GestureRecognizers.Add(pointer);
#else
        // Android / other platforms: tap to toggle, pan to drag handles
        Handle? activeHandle = null;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, e) =>
        {
            // Only toggle if no drag is in progress
            if (activeHandle != null) return;
            var pos = e.GetPosition(rootGrid);
            if (pos == null) return;
            var fit = GetFit(rootGrid.Width, rootGrid.Height);
            HandleGridTap(pos.Value.X, pos.Value.Y, fit.renderedW, fit.renderedH,
                fit.offsetX, fit.offsetY, imagePixelW, imagePixelH, g, localSelected);
            Redraw();
        };
        rootGrid.GestureRecognizers.Add(tap);

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += (_, e) =>
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                {
                    _panStartImageX = -1;
                    _panStartImageY = -1;
                    activeHandle = null;
                    break;
                }
                case GestureStatus.Running:
                {
                    break;
                }
                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    activeHandle = null;
                    break;
            }
        };

        Handle? selectedHandle = null;
        Button? nudgeA = null, nudgeB = null;

        var nudgeLeft = new Button
        {
            Text = "◀", FontSize = 20, BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White, CornerRadius = 8, WidthRequest = 52, HeightRequest = 52,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(16, 0, 0, 80), IsVisible = false
        };
        var nudgeRight = new Button
        {
            Text = "▶", FontSize = 20, BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White, CornerRadius = 8, WidthRequest = 52, HeightRequest = 52,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(76, 0, 0, 80), IsVisible = false
        };
        var nudgeUp = new Button
        {
            Text = "▲", FontSize = 20, BackgroundColor = Color.FromArgb("#E65100"),
            TextColor = Colors.White, CornerRadius = 8, WidthRequest = 52, HeightRequest = 52,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(136, 0, 0, 80), IsVisible = false
        };
        var nudgeDown = new Button
        {
            Text = "▼", FontSize = 20, BackgroundColor = Color.FromArgb("#E65100"),
            TextColor = Colors.White, CornerRadius = 8, WidthRequest = 52, HeightRequest = 52,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(196, 0, 0, 80), IsVisible = false
        };
        var nudgeLabel = new Label
        {
            Text = "", FontSize = 12, TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(16, 0, 0, 140), IsVisible = false,
            BackgroundColor = Color.FromArgb("#80000000")
        };
        var cancelBtn = new Button
        {
            Text = "✕", FontSize = 16, BackgroundColor = Color.FromArgb("#555"),
            TextColor = Colors.White, CornerRadius = 8, WidthRequest = 52, HeightRequest = 52,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(256, 0, 0, 80), IsVisible = false
        };
        rootGrid.Children.Add(nudgeLeft);
        rootGrid.Children.Add(nudgeRight);
        rootGrid.Children.Add(nudgeUp);
        rootGrid.Children.Add(nudgeDown);
        rootGrid.Children.Add(nudgeLabel);
        rootGrid.Children.Add(cancelBtn);

        void ShowNudge(Handle h)
        {
            selectedHandle = h;
            bool isV = h.Kind is HandleKind.MainV or HandleKind.BottomV;
            nudgeLeft.IsVisible = isV;
            nudgeRight.IsVisible = isV;
            nudgeUp.IsVisible = !isV;
            nudgeDown.IsVisible = !isV;
            nudgeLabel.IsVisible = true;
            cancelBtn.IsVisible = true;
            nudgeLabel.Text = isV
                ? $"Col line {h.Index}: x = {(h.Kind == HandleKind.MainV ? g.ColEdges[h.Index] : g.BottomColEdges[h.Index])} px"
                : $"Row line {h.Index}: y = {(h.Kind == HandleKind.MainH ? g.RowEdges[h.Index] : g.BottomTop)} px";
        }

        void HideNudge()
        {
            selectedHandle = null;
            nudgeLeft.IsVisible = nudgeRight.IsVisible = nudgeUp.IsVisible =
                nudgeDown.IsVisible = nudgeLabel.IsVisible = cancelBtn.IsVisible = false;
        }

        void ApplyNudge(int dx, int dy)
        {
            if (selectedHandle == null) return;
            var h = selectedHandle;
            switch (h.Kind)
            {
                case HandleKind.MainV:
                {
                    int min = g.ColEdges[h.Index - 1] + 2;
                    int max = h.Index < g.Cols ? g.ColEdges[h.Index + 1] - 2 : imagePixelW;
                    g.ColEdges[h.Index] = Math.Clamp(g.ColEdges[h.Index] + dx, min, Math.Max(min, max));
                    break;
                }
                case HandleKind.MainH:
                {
                    int min = g.RowEdges[h.Index - 1] + 2;
                    int max = h.Index < g.MainRows ? g.RowEdges[h.Index + 1] - 2 : imagePixelH;
                    g.RowEdges[h.Index] = Math.Clamp(g.RowEdges[h.Index] + dy, min, Math.Max(min, max));
                    break;
                }
                case HandleKind.BottomH:
                    g.BottomTop = Math.Clamp(g.BottomTop + dy, 1, imagePixelH - 2);
                    break;
                case HandleKind.BottomV:
                {
                    int min = g.BottomColEdges[h.Index - 1] + 2;
                    int max = h.Index < g.BottomCols ? g.BottomColEdges[h.Index + 1] - 2 : imagePixelW;
                    g.BottomColEdges[h.Index] = Math.Clamp(g.BottomColEdges[h.Index] + dx, min, Math.Max(min, max));
                    break;
                }
            }
            PositionHandle(h);
            Redraw();
            ShowNudge(h);
        }

        const int Step = 10;
        nudgeLeft.Clicked += (_, _) => ApplyNudge(-Step, 0);
        nudgeRight.Clicked += (_, _) => ApplyNudge(+Step, 0);
        nudgeUp.Clicked += (_, _) => ApplyNudge(0, -Step);
        nudgeDown.Clicked += (_, _) => ApplyNudge(0, +Step);
        cancelBtn.Clicked += (_, _) => HideNudge();

        rootGrid.GestureRecognizers.Remove(tap);
        var tap2 = new TapGestureRecognizer();
        tap2.Tapped += (_, e) =>
        {
            var pos = e.GetPosition(rootGrid);
            if (pos == null) return;

            double bestDist = double.MaxValue;
            Handle? bestH = null;
            foreach (var h in handles.Where(h => h.View.IsVisible))
            {
                var bounds = AbsoluteLayout.GetLayoutBounds(h.View);
                bool isV = h.Kind is HandleKind.MainV or HandleKind.BottomV;
                double dist = isV
                    ? Math.Abs(pos.Value.X - (bounds.X + bounds.Width / 2.0))
                    : Math.Abs(pos.Value.Y - (bounds.Y + bounds.Height / 2.0));
                bool inSpan = isV
                    ? pos.Value.Y >= bounds.Y - 20 && pos.Value.Y <= bounds.Bottom + 20
                    : pos.Value.X >= bounds.X - 20 && pos.Value.X <= bounds.Right + 20;
                if (inSpan && dist <= 22 && dist < bestDist)
                {
                    bestDist = dist;
                    bestH = h;
                }
            }

            if (bestH != null)
            {
                ShowNudge(bestH);
                return;
            }

            HideNudge();
            var fit = GetFit(rootGrid.Width, rootGrid.Height);
            HandleGridTap(pos.Value.X, pos.Value.Y, fit.renderedW, fit.renderedH,
                fit.offsetX, fit.offsetY, imagePixelW, imagePixelH, g, localSelected);
            Redraw();
        };
        rootGrid.GestureRecognizers.Add(tap2);
#endif

        Content = rootGrid;
        Redraw();
    }

    private static void HandleGridTap(
        double tapX, double tapY,
        double renderedW, double renderedH,
        double offsetX, double offsetY,
        int imagePixelW, int imagePixelH,
        GridGeometry geometry,
        bool[,] selected)
    {
        if (renderedW <= 0 || renderedH <= 0) return;
        double relX = tapX - offsetX;
        double relY = tapY - offsetY;
        if (relX < 0 || relY < 0 || relX > renderedW || relY > renderedH) return;

        int ix = Math.Clamp((int)(relX / renderedW * imagePixelW), 0, imagePixelW - 1);
        int iy = Math.Clamp((int)(relY / renderedH * imagePixelH), 0, imagePixelH - 1);

        if (geometry.SeparateBottom && iy >= geometry.BottomTop)
        {
            for (int c = 0; c < geometry.BottomCols; c++)
            {
                if (geometry.BottomColEdges[c] <= ix && ix < geometry.BottomColEdges[c + 1])
                {
                    if (geometry.MainRows < selected.GetLength(0) && c < selected.GetLength(1))
                        selected[geometry.MainRows, c] = !selected[geometry.MainRows, c];
                    break;
                }
            }
            return;
        }

        int mainCol = -1;
        int mainRow = -1;
        for (int c = 0; c < geometry.Cols; c++)
            if (geometry.ColEdges[c] <= ix && ix < geometry.ColEdges[c + 1])
            {
                mainCol = c;
                break;
            }
        for (int r = 0; r < geometry.MainRows; r++)
            if (geometry.RowEdges[r] <= iy && iy < geometry.RowEdges[r + 1])
            {
                mainRow = r;
                break;
            }
        if (mainRow >= 0 && mainCol >= 0 && mainRow < selected.GetLength(0) &&
            mainCol < selected.GetLength(1))
            selected[mainRow, mainCol] = !selected[mainRow, mainCol];
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
