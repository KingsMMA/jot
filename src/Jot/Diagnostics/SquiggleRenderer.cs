using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Jot.Diagnostics;

/// <summary>Draws a red wavy underline beneath each diagnostic range in the editor.</summary>
public sealed class SquiggleRenderer : IBackgroundRenderer
{
    private static readonly IPen Pen = new Pen(Brushes.Tomato, 1);

    public IReadOnlyList<Diagnostic> Diagnostics { get; set; } = [];

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Diagnostics.Count == 0 || !textView.VisualLinesValid) return;

        foreach (var diagnostic in Diagnostics)
        {
            var segment = new TextSegment { StartOffset = diagnostic.Offset, Length = Math.Max(1, diagnostic.Length) };
            try
            {
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                    DrawWavyLine(drawingContext, rect.Left, rect.Right, rect.Bottom);
            }
            catch
            {
                // The document may have changed under us; skip this frame's squiggle.
            }
        }
    }

    private static void DrawWavyLine(DrawingContext context, double left, double right, double bottom)
    {
        const double step = 4;
        const double amplitude = 2;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(left, bottom), false);
            var up = true;
            for (var x = left; x <= right; x += step)
            {
                ctx.LineTo(new Point(x, up ? bottom - amplitude : bottom));
                up = !up;
            }
        }

        context.DrawGeometry(null, Pen, geometry);
    }
}
