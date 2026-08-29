using Microsoft.Maui.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace Bannister.Helpers;

public class PieChartDrawable : IDrawable
{
    public List<(string Label, float Value, Color Color)> Slices { get; set; } = new();

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

        float cx = dirtyRect.Width / 2f;
        float cy = dirtyRect.Height / 2f;
        float radius = Math.Min(cx, cy) - 30f;
        float legendX = 10f;
        float legendY = 10f;

        float startAngle = -90f;
        foreach (var slice in Slices)
        {
            float sweep = (slice.Value / total) * 360f;
            canvas.FillColor = slice.Color;
            canvas.FillArc(cx - radius, cy - radius,
                           radius * 2, radius * 2,
                           startAngle, sweep, true);
            startAngle += sweep;
        }

        // Legend
        float boxSize = 12f;
        float lineH = 18f;
        int i = 0;
        foreach (var slice in Slices)
        {
            float ly = legendY + i * lineH;
            canvas.FillColor = slice.Color;
            canvas.FillRectangle(legendX, ly, boxSize, boxSize);
            canvas.FontColor = Color.FromArgb("#212121");
            canvas.FontSize = 11f;
            string pct = (slice.Value / total * 100f).ToString("0.#") + "%";
            canvas.DrawString($"{slice.Label} {pct}",
                legendX + boxSize + 4f, ly,
                200f, lineH, HorizontalAlignment.Left, VerticalAlignment.Top);
            i++;
        }
    }
}
