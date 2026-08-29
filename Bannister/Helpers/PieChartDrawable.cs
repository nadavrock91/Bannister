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

        // Guard: if not yet laid out, skip
        if (w < 10f || h < 10f) return;

        // Fixed layout: legend left 40%, pie right 60%
        float legendW = (float)Math.Floor(w * 0.40f);
        float pieLeft = legendW + 4f;
        float pieW = w - pieLeft;

        float pieCx = pieLeft + pieW / 2f;
        float pieCy = h / 2f;
        float radius = (float)Math.Floor(Math.Min(pieW / 2f, h / 2f)) - 12f;
        if (radius < 4f) return;

        // Draw pie slices as polygon wedges (FillArc is unreliable on Win2D)
        float startAngle = -90f;
        foreach (var slice in Slices)
        {
            float sweep = (slice.Value / total) * 360f;
            float endAngle = startAngle + sweep;

            var path = new PathF();
            path.MoveTo(pieCx, pieCy);

            // walk the arc in 2-degree steps
            int steps = Math.Max(2, (int)Math.Ceiling(sweep / 2f));
            for (int i = 0; i <= steps; i++)
            {
                float a = startAngle + (sweep * i / steps);
                double rad = a * Math.PI / 180.0;
                float px = pieCx + radius * (float)Math.Cos(rad);
                float py = pieCy + radius * (float)Math.Sin(rad);
                path.LineTo(px, py);
            }
            path.Close();

            canvas.FillColor = slice.Color;
            canvas.FillPath(path);

            // thin white separator on the leading edge
            double r0 = startAngle * Math.PI / 180.0;
            canvas.StrokeColor = Colors.White;
            canvas.StrokeSize = 1.5f;
            canvas.DrawLine(pieCx, pieCy,
                pieCx + radius * (float)Math.Cos(r0),
                pieCy + radius * (float)Math.Sin(r0));

            startAngle = endAngle;
        }

        // Draw legend on left
        float boxSize = 12f;
        float lineH = 20f;
        float totalLegendH = Slices.Count * lineH;
        float lx = 6f;
        float ly = (h - totalLegendH) / 2f;
        if (ly < 6f) ly = 6f;

        float maxLabelW = legendW - lx - boxSize - 10f;
        if (maxLabelW < 20f) maxLabelW = 20f;

        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        foreach (var slice in Slices)
        {
            // Color box
            canvas.FillColor = slice.Color;
            canvas.FillRectangle(lx, ly + 2f, boxSize, boxSize);

            // Label text
            canvas.FontColor = Color.FromArgb("#212121");
            canvas.FontSize = 10.5f;
            string text = $"{slice.Label}: {CurrencySymbol}{slice.Value.ToString("N0")}";
            canvas.DrawString(text,
                lx + boxSize + 4f, ly,
                maxLabelW, lineH,
                HorizontalAlignment.Left, VerticalAlignment.Center);

            ly += lineH;
        }
    }
}
