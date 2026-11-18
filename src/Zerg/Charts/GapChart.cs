using System.Windows;
using System.Windows.Media;
using Zerg.Core.Charts;

namespace Zerg.Charts;

/// <summary>
/// Compare's Gap view: B − A on the one clock both runs share. The zero line
/// runs across the middle, the scale is even about it (so a run ahead and a
/// run behind read the same size), and the area between the line and zero is
/// the better colour where B is ahead and the worse where it is behind. At the
/// line's end, which run is ahead, by its name. Where everything goes is
/// <see cref="GapLayout"/>'s; this only paints it.
/// </summary>
public sealed class GapChart : Chart
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(GapModel),
        typeof(GapChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AheadInkProperty = RegisterInk(nameof(AheadInk), typeof(GapChart));
    public static readonly DependencyProperty BehindInkProperty = RegisterInk(nameof(BehindInk), typeof(GapChart));
    public static readonly DependencyProperty ZeroInkProperty = RegisterInk(nameof(ZeroInk), typeof(GapChart));

    readonly DrawingVisual plot = new();

    public GapChart()
    {
        Layers(plot);
        SetResourceReference(AheadInkProperty, "LiveBrush");
        SetResourceReference(BehindInkProperty, "CritBrush");
        SetResourceReference(ZeroInkProperty, "Line3Brush");
    }

    public GapModel? Model { get => (GapModel?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    public Brush AheadInk { get => Get(AheadInkProperty); set => SetValue(AheadInkProperty, value); }
    public Brush BehindInk { get => Get(BehindInkProperty); set => SetValue(BehindInkProperty, value); }
    public Brush ZeroInk { get => Get(ZeroInkProperty); set => SetValue(ZeroInkProperty, value); }

    protected override void Paint(double w, double h)
    {
        using var dc = plot.RenderOpen();
        if (w < 1 || h < 1) return;
        if (Model is not { } m || !m.Gap.Any(double.IsFinite))
        {
            DrawEmpty(dc, w, h, "Nothing to compare");
            return;
        }
        var l = GapLayout.Compute(m.Times, m.Gap, w, h);

        var grid = Hairline(GridInk);
        foreach (var v in l.Values)
        {
            dc.DrawLine(grid, new Point(l.Left, SnapLine(v.At)), new Point(l.Right, SnapLine(v.At)));
            var t = Text(v.Label, AxisSize, false, Muted);
            Draw(dc, t, l.Left - 6 - t.Width, v.At - t.Height / 2);
        }
        foreach (var x in l.Times)
        {
            var t = Text(x.Label, AxisSize, false, Muted);
            Draw(dc, t, Math.Clamp(x.At - t.Width / 2, l.Left - t.Width / 2, w - t.Width), l.Bottom + 4);
        }

        Brush Tint(Brush ink)
        {
            var c = (ink as SolidColorBrush)?.Color ?? Colors.Gray;
            var b = new SolidColorBrush(c) { Opacity = 0.30 };
            b.Freeze();
            return b;
        }
        var ahead = Tint(AheadInk);
        var behind = Tint(BehindInk);
        foreach (var stretch in l.Stretches)
        {
            if (stretch.Count < 2) continue;
            dc.DrawGeometry(ahead, null, Area(stretch, l.ZeroY, up: true));
            dc.DrawGeometry(behind, null, Area(stretch, l.ZeroY, up: false));
            var line = new StreamGeometry();
            using (var g = line.Open())
            {
                g.BeginFigure(new Point(stretch[0].X, stretch[0].Y), false, false);
                for (int i = 1; i < stretch.Count; i++) g.LineTo(new Point(stretch[i].X, stretch[i].Y), true, false);
            }
            line.Freeze();
            dc.DrawGeometry(null, LinePen(Ink2, false), line);
        }
        dc.DrawLine(Hairline(ZeroInk), new Point(l.Left, SnapLine(l.ZeroY)), new Point(l.Right, SnapLine(l.ZeroY)));

        // Who is ahead at the end, by name.
        if (l.Stretches.LastOrDefault() is { Count: > 0 } last && Last(m.Gap) is double end && end != 0)
        {
            var p = last[^1];
            var t = Text(end > 0 ? m.AheadB : m.AheadA, LabelSize, false, Ink2);
            double x = Math.Min(p.X + 6, w - t.Width - 2);
            Label(dc, t, x, Math.Clamp(p.Y - t.Height - 2, 0, h - t.Height));
        }
    }

    static double? Last(double[] gap)
    {
        for (int i = gap.Length - 1; i >= 0; i--)
            if (double.IsFinite(gap[i])) return gap[i];
        return null;
    }

    /// <summary>The area between a stretch of line and zero on one side of it.</summary>
    static StreamGeometry Area(IReadOnlyList<GapPoint> s, double zero, bool up)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(s[0].X, zero), true, true);
            foreach (var p in s) c.LineTo(new Point(p.X, up ? Math.Min(p.Y, zero) : Math.Max(p.Y, zero)), false, false);
            c.LineTo(new Point(s[^1].X, zero), false, false);
        }
        g.Freeze();
        return g;
    }

    protected override Readout? ReadoutAt(Point p) => null;

    protected override string Summary() =>
        Model is { } m && Last(m.Gap) is double end
            ? (end > 0 ? m.AheadB : end < 0 ? m.AheadA : "Level") + " at the end, by " + Zerg.Core.Format.Int(Math.Abs(end))
            : "Nothing to compare";
}
