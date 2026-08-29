using Microsoft.Maui.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace Bannister.Helpers;

public class PieChartDrawable : IDrawable
{
    public List<(string Label, float Value, Color Color)> Slices { get; set; } = new();
    public string CurrencySymbol { get; set; } = "$";

    private static readonly Color[] Palette = new[]
    {
        Color.FromArgb("#2E7D32"), Color.FromArgb("#1565C0"), Color.FromArgb("#AD1457"),
        Color.FromArgb("#E65100"), Color.FromArgb("#6A1B9A"), Color.FromArgb("#00838F"),
        Color.FromArgb("#558B2F"), Color.FromArgb("#4527A0"), Color.FromArgb("#BF360C"),
        Color.FromArgb("#00695C"),
    };

    public void AssignColors(List<string> categories)
    {
        Slices = Slices.Select((s, i) =>
            (s.Label, s.Value, Palette[i % Palette.Length])).ToList();
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Slices == null || Slices.Count == 0) return;

        float total = Slices.Sum(s => s.Value);
        if (total <= 0) return;

        float w = dirtyRect.Width;
        float h = dirtyRect.Height;

        // Left half = legend, Right half = pie
        float legendWidth = w * 0.38f;
        float pieAreaX = legendWidth;
        float pieAreaW = w - legendWidth;
        float pieAreaH = h;

        float pieCx = pieAreaX + pieAreaW / 2f;
        float pieCy = pieAreaH / 2f;
        float radius = Math.Min(pieAreaW / 2f, pieAreaH / 2f) - 16f;

        // Draw pie slices
        float startAngle = -90f;
        foreach (var slice in Slices)
        {
            float sweep = (slice.Value / total) * 360f;
            canvas.FillColor = slice.Color;
            canvas.FillArc(pieCx - radius, pieCy - radius,
                           radius * 2, radius * 2,
                           startAngle, sweep, true);
            startAngle += sweep;
        }

        // Draw legend on left
        float boxSize = 13f;
        float lineH = 22f;
        float lx = 8f;
        float ly = (h - Slices.Count * lineH) / 2f;
        if (ly < 8f) ly = 8f;

        foreach (var slice in Slices)
        {
            canvas.FillColor = slice.Color;
            canvas.FillRectangle(lx, ly, boxSize, boxSize);

            canvas.FontColor = Color.FromArgb("#212121");
            canvas.FontSize = 11f;
            string label = $"{slice.Label}: {CurrencySymbol}{slice.Value.ToString("N0")}";
            canvas.DrawString(label,
                lx + boxSize + 5f, ly - 1f,
                legendWidth - lx - boxSize - 10f, lineH,
                HorizontalAlignment.Left, VerticalAlignment.Top);

            ly += lineH;
        }
    }
}
