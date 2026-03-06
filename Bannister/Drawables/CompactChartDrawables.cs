using Bannister.Models;

namespace Bannister.Drawables;

/// <summary>
/// Compact version of LevelChartDrawable for grid cards.
/// Smaller margins, readable date labels at bottom.
/// </summary>
public class LevelChartCompactDrawable : IDrawable
{
    private readonly List<ChartDataPoint> _rawData;

    public LevelChartCompactDrawable(List<ChartDataPoint> data)
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

        // Compact chart area - smaller margins, space for date label at bottom
        float leftMargin = 30;
        float rightMargin = 8;
        float topMargin = 15;
        float bottomMargin = 25; // Space for date labels
        
        float chartWidth = dirtyRect.Width - leftMargin - rightMargin;
        float chartHeight = dirtyRect.Height - topMargin - bottomMargin;

        if (chartWidth <= 0 || chartHeight <= 0)
            return;

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

        // Draw light horizontal grid lines
        canvas.StrokeColor = Color.FromArgb("#E8E8E8");
        canvas.StrokeSize = 1;
        int gridLines = 3;
        for (int i = 0; i <= gridLines; i++)
        {
            float y = topMargin + (chartHeight * i / gridLines);
            canvas.DrawLine(leftMargin, y, leftMargin + chartWidth, y);
        }

        // Draw axes
        canvas.StrokeColor = Color.FromArgb("#CCCCCC");
        canvas.StrokeSize = 1;
        canvas.DrawLine(leftMargin, topMargin, leftMargin, topMargin + chartHeight);  // Y axis
        canvas.DrawLine(leftMargin, topMargin + chartHeight, leftMargin + chartWidth, topMargin + chartHeight); // X axis

        // Draw step line (for levels)
        canvas.StrokeColor = Color.FromArgb("#4CAF50");
        canvas.StrokeSize = 2;

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
                // Draw step: horizontal then vertical
                path.LineTo(x, path.LastPoint.Y);
                path.LineTo(x, y);
            }
        }

        canvas.DrawPath(path);

        // Draw points only if we have fewer than 30 data points (compact view)
        if (data.Count <= 30)
        {
            canvas.FillColor = Color.FromArgb("#4CAF50");
            for (int i = 0; i < data.Count; i++)
            {
                float x = leftMargin + (chartWidth * i / Math.Max(1, data.Count - 1));
                float y = topMargin + chartHeight - (chartHeight * (data[i].Value - minValue) / (float)(maxValue - minValue));
                canvas.FillCircle(x, y, 3);
            }
        }

        // Draw Y-axis labels (compact)
        canvas.FontColor = Color.FromArgb("#666666");
        canvas.FontSize = 9;
        canvas.DrawString($"{maxValue}", 2, topMargin + 4, HorizontalAlignment.Left);
        canvas.DrawString($"{minValue}", 2, topMargin + chartHeight - 2, HorizontalAlignment.Left);

        // Draw X-axis date labels - clear and readable
        if (data.Count > 0)
        {
            canvas.FontColor = Color.FromArgb("#555555");
            canvas.FontSize = 9;
            
            // Start date on left
            canvas.DrawString(
                data[0].Date.ToString("MMM dd"), 
                leftMargin, 
                topMargin + chartHeight + 12, 
                HorizontalAlignment.Left);
            
            // End date on right
            canvas.DrawString(
                data[data.Count - 1].Date.ToString("MMM dd"), 
                leftMargin + chartWidth, 
                topMargin + chartHeight + 12, 
                HorizontalAlignment.Right);
        }
    }

    private void DrawNoData(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FontColor = Colors.Gray;
        canvas.FontSize = 12;
        canvas.DrawString("No data", dirtyRect.Width / 2, dirtyRect.Height / 2, HorizontalAlignment.Center);
    }
}

/// <summary>
/// Compact version of ExpChartDrawable for grid cards.
/// Smaller margins, readable date labels at bottom.
/// </summary>
public class ExpChartCompactDrawable : IDrawable
{
    private readonly List<ChartDataPoint> _rawData;

    public ExpChartCompactDrawable(List<ChartDataPoint> data)
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

        // Aggregate by day - take the max EXP for each day (end of day value)
        var data = _rawData
            .GroupBy(d => d.Date.Date)
            .Select(g => new ChartDataPoint
            {
                Date = g.Key,
                Value = g.Max(x => x.Value)
            })
            .OrderBy(d => d.Date)
            .ToList();

        // Compact chart area - smaller margins, space for date label at bottom
        float leftMargin = 35;
        float rightMargin = 8;
        float topMargin = 15;
        float bottomMargin = 25; // Space for date labels
        
        float chartWidth = dirtyRect.Width - leftMargin - rightMargin;
        float chartHeight = dirtyRect.Height - topMargin - bottomMargin;

        if (chartWidth <= 0 || chartHeight <= 0)
            return;

        // Background
        canvas.FillColor = Color.FromArgb("#FAFAFA");
        canvas.FillRectangle(leftMargin, topMargin, chartWidth, chartHeight);

        // Get min/max values
        int minValue = 0; // EXP always starts from 0
        int maxValue = data.Max(d => d.Value);
        
        if (maxValue == minValue) maxValue = minValue + 100;

        // Draw light horizontal grid lines
        canvas.StrokeColor = Color.FromArgb("#E8E8E8");
        canvas.StrokeSize = 1;
        int gridLines = 3;
        for (int i = 0; i <= gridLines; i++)
        {
            float y = topMargin + (chartHeight * i / gridLines);
            canvas.DrawLine(leftMargin, y, leftMargin + chartWidth, y);
        }

        // Draw axes
        canvas.StrokeColor = Color.FromArgb("#CCCCCC");
        canvas.StrokeSize = 1;
        canvas.DrawLine(leftMargin, topMargin, leftMargin, topMargin + chartHeight);  // Y axis
        canvas.DrawLine(leftMargin, topMargin + chartHeight, leftMargin + chartWidth, topMargin + chartHeight); // X axis

        // Draw filled area under the line
        PathF areaPath = new PathF();
        areaPath.MoveTo(leftMargin, topMargin + chartHeight); // Start at bottom-left

        for (int i = 0; i < data.Count; i++)
        {
            float x = leftMargin + (chartWidth * i / Math.Max(1, data.Count - 1));
            float y = topMargin + chartHeight - (chartHeight * (data[i].Value - minValue) / (float)(maxValue - minValue));
            areaPath.LineTo(x, y);
        }

        // Close the area
        float lastX = leftMargin + chartWidth;
        areaPath.LineTo(lastX, topMargin + chartHeight);
        areaPath.Close();

        // Fill with semi-transparent color
        canvas.FillColor = Color.FromArgb("#335B63EE");
        canvas.FillPath(areaPath);

        // Draw the line on top
        canvas.StrokeColor = Color.FromArgb("#5B63EE");
        canvas.StrokeSize = 2;

        PathF linePath = new PathF();
        for (int i = 0; i < data.Count; i++)
        {
            float x = leftMargin + (chartWidth * i / Math.Max(1, data.Count - 1));
            float y = topMargin + chartHeight - (chartHeight * (data[i].Value - minValue) / (float)(maxValue - minValue));

            if (i == 0)
                linePath.MoveTo(x, y);
            else
                linePath.LineTo(x, y);
        }

        canvas.DrawPath(linePath);

        // Draw points only if we have fewer than 30 data points (compact view)
        if (data.Count <= 30)
        {
            canvas.FillColor = Color.FromArgb("#5B63EE");
            for (int i = 0; i < data.Count; i++)
            {
                float x = leftMargin + (chartWidth * i / Math.Max(1, data.Count - 1));
                float y = topMargin + chartHeight - (chartHeight * (data[i].Value - minValue) / (float)(maxValue - minValue));
                canvas.FillCircle(x, y, 3);
            }
        }

        // Draw Y-axis labels (compact, use K for thousands)
        canvas.FontColor = Color.FromArgb("#666666");
        canvas.FontSize = 9;
        string maxLabel = maxValue >= 1000 ? $"{maxValue / 1000.0:0.#}K" : maxValue.ToString();
        canvas.DrawString(maxLabel, 2, topMargin + 4, HorizontalAlignment.Left);
        canvas.DrawString("0", 2, topMargin + chartHeight - 2, HorizontalAlignment.Left);

        // Draw X-axis date labels - clear and readable
        if (data.Count > 0)
        {
            canvas.FontColor = Color.FromArgb("#555555");
            canvas.FontSize = 9;
            
            // Start date on left
            canvas.DrawString(
                data[0].Date.ToString("MMM dd"), 
                leftMargin, 
                topMargin + chartHeight + 12, 
                HorizontalAlignment.Left);
            
            // End date on right
            canvas.DrawString(
                data[data.Count - 1].Date.ToString("MMM dd"), 
                leftMargin + chartWidth, 
                topMargin + chartHeight + 12, 
                HorizontalAlignment.Right);
        }
    }

    private void DrawNoData(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FontColor = Colors.Gray;
        canvas.FontSize = 12;
        canvas.DrawString("No data", dirtyRect.Width / 2, dirtyRect.Height / 2, HorizontalAlignment.Center);
    }
}
