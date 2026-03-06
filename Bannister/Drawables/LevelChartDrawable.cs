using Bannister.Models;

namespace Bannister.Drawables;

/// <summary>
/// Custom drawable for rendering Level over time chart with step visualization.
/// Aggregates data by day for cleaner display.
/// </summary>
public class LevelChartDrawable : IDrawable
{
    private readonly List<ChartDataPoint> _rawData;

    public LevelChartDrawable(List<ChartDataPoint> data)
    {
        _rawData = data;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (_rawData == null || _rawData.Count == 0)
        {
            DrawNoData(canvas, dirtyRect);
            return;
        }

        // Aggregate by day - take the max level for each day
        var data = _rawData
            .GroupBy(d => d.Date.Date)
            .Select(g => new ChartDataPoint
            {
                Date = g.Key,
                Value = g.Max(x => x.Value)
            })
            .OrderBy(d => d.Date)
            .ToList();

        // Chart area
        float leftMargin = 60;
        float rightMargin = 20;
        float topMargin = 30;
        float bottomMargin = 40;
        
        float chartWidth = dirtyRect.Width - leftMargin - rightMargin;
        float chartHeight = dirtyRect.Height - topMargin - bottomMargin;

        // Background
        canvas.FillColor = Color.FromArgb("#FAFAFA");
        canvas.FillRectangle(leftMargin, topMargin, chartWidth, chartHeight);

        // Get min/max values
        int minValue = data.Min(d => d.Value);
        int maxValue = data.Max(d => d.Value);
        
        // Add some padding to min/max
        if (maxValue == minValue) 
        {
            minValue = Math.Max(0, minValue - 1);
            maxValue = maxValue + 1;
        }

        // Draw horizontal grid lines
        canvas.StrokeColor = Color.FromArgb("#E0E0E0");
        canvas.StrokeSize = 1;
        int gridLines = 5;
        for (int i = 0; i <= gridLines; i++)
        {
            float y = topMargin + (chartHeight * i / gridLines);
            canvas.DrawLine(leftMargin, y, leftMargin + chartWidth, y);
        }

        // Draw axes
        canvas.StrokeColor = Color.FromArgb("#BDBDBD");
        canvas.StrokeSize = 2;
        canvas.DrawLine(leftMargin, topMargin, leftMargin, topMargin + chartHeight);  // Y axis
        canvas.DrawLine(leftMargin, topMargin + chartHeight, leftMargin + chartWidth, topMargin + chartHeight); // X axis

        // Draw step line (for levels)
        canvas.StrokeColor = Color.FromArgb("#4CAF50");
        canvas.StrokeSize = 3;

        PathF path = new PathF();
        bool pathStarted = false;

        for (int i = 0; i < data.Count; i++)
        {
            float x = leftMargin + (chartWidth * i / Math.Max(1, data.Count - 1));
            float y = topMargin + chartHeight - (chartHeight * (data[i].Value - minValue) / (float)(maxValue - minValue));

            if (!pathStarted)
            {
                path.MoveTo(x, y);
                pathStarted = true;
            }
            else
            {
                float prevX = leftMargin + (chartWidth * (i - 1) / Math.Max(1, data.Count - 1));
                // Draw step: horizontal then vertical
                path.LineTo(x, path.LastPoint.Y); // Horizontal to new X
                path.LineTo(x, y); // Vertical to new Y
            }
        }

        canvas.DrawPath(path);

        // Draw points only if we have fewer than 60 data points
        if (data.Count <= 60)
        {
            canvas.FillColor = Color.FromArgb("#4CAF50");
            for (int i = 0; i < data.Count; i++)
            {
                float x = leftMargin + (chartWidth * i / Math.Max(1, data.Count - 1));
                float y = topMargin + chartHeight - (chartHeight * (data[i].Value - minValue) / (float)(maxValue - minValue));
                canvas.FillCircle(x, y, 4);
            }
        }

        // Draw Y-axis labels
        canvas.FontColor = Color.FromArgb("#666666");
        canvas.FontSize = 11;
        canvas.DrawString($"Level {maxValue}", 5, topMargin + 5, HorizontalAlignment.Left);
        canvas.DrawString($"Level {minValue}", 5, topMargin + chartHeight - 5, HorizontalAlignment.Left);

        // Draw X-axis labels (dates)
        if (data.Count > 0)
        {
            canvas.DrawString(data[0].Date.ToString("MMM dd"), leftMargin, topMargin + chartHeight + 20, HorizontalAlignment.Left);
            canvas.DrawString(data[data.Count - 1].Date.ToString("MMM dd"), leftMargin + chartWidth, topMargin + chartHeight + 20, HorizontalAlignment.Right);
            
            // Middle date if enough data
            if (data.Count > 2)
            {
                int midIndex = data.Count / 2;
                float midX = leftMargin + (chartWidth * midIndex / Math.Max(1, data.Count - 1));
                canvas.DrawString(data[midIndex].Date.ToString("MMM dd"), midX, topMargin + chartHeight + 20, HorizontalAlignment.Center);
            }
        }
    }

    private void DrawNoData(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FontColor = Colors.Gray;
        canvas.FontSize = 14;
        canvas.DrawString("No data available", dirtyRect.Width / 2, dirtyRect.Height / 2, HorizontalAlignment.Center);
    }
}
