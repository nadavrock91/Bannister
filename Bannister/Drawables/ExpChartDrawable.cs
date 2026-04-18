using Bannister.Models;

namespace Bannister.Drawables;

/// <summary>
/// Custom drawable for rendering EXP over time chart.
/// Aggregates data by day for cleaner display.
/// </summary>
public class ExpChartDrawable : IDrawable
{
    private readonly List<ChartDataPoint> _rawData;

    public ExpChartDrawable(List<ChartDataPoint> data)
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
        int minValue = 0; // EXP always starts from 0
        int maxValue = data.Max(d => d.Value);
        
        if (maxValue == minValue) maxValue = minValue + 100;

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
        canvas.StrokeSize = 3;

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

        // Draw points only if we have fewer than 60 data points
        if (data.Count <= 60)
        {
            canvas.FillColor = Color.FromArgb("#5B63EE");
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
        canvas.DrawString($"{maxValue:N0}", 5, topMargin + 5, HorizontalAlignment.Left);
        canvas.DrawString("0", 5, topMargin + chartHeight - 5, HorizontalAlignment.Left);

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
