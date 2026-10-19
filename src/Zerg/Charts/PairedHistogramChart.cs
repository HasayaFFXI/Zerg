using System.Windows;
using System.Windows.Media;
using Zerg.Core;
using Zerg.Core.Charts;

namespace Zerg.Charts;

/// <summary>
/// How one action's hits, or one heal's casts, are spread in two runs (or
/// two actions of one session): in every bin a column for A beside one for
/// B, each side in its own colour, with each side's mean marked by a rule.
/// </summary>
public sealed class PairedHistogramChart : Chart
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model),
        typeof(PairedHistogramModel), typeof(PairedHistogramChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillAProperty = DependencyProperty.Register(nameof(FillA), typeof(Brush),
        typeof(PairedHistogramChart), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillBProperty = DependencyProperty.Register(nameof(FillB), typeof(Brush),
        typeof(PairedHistogramChart), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    readonly DrawingVisual columns = new();
    PairedHistogramLayout? layout;

    // The sides' colours are the user's of it: Compare gives the runs'
    // (the theme's, as on everything else of a run), the live drill-down
    // the characters'.
    public PairedHistogramChart() => Layers(columns);

    public PairedHistogramModel? Model
    {
        get => (PairedHistogramModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <summary>Run A's columns.</summary>
    public Brush FillA { get => (Brush)GetValue(FillAProperty); set => SetValue(FillAProperty, value); }
    /// <summary>Run B's columns.</summary>
    public Brush FillB { get => (Brush)GetValue(FillBProperty); set => SetValue(FillBProperty, value); }

    Brush FillOf(int run) => run == 0 ? FillA : FillB;

    protected override void Paint(double w, double h)
    {
        layout = null;
        using var dc = columns.RenderOpen();
        if (w < 1 || h < 1) return;
        var m = Model;
        var l = layout = PairedHistogramLayout.Compute(w, h, m?.Bins, m?.AvgA, m?.AvgB, m?.CountA ?? 0, m?.CountB ?? 0,
                                                       s => Text(s, AxisSize, true, Ink2).Width, m?.Unit ?? "hit");
        if (l.Empty)
        {
            DrawEmpty(dc, w, h, "No " + (m?.Unit ?? "hit") + "s in either " + (m?.Side ?? "run"));
            return;
        }
        var plot = l.Plot;

        PaintValueAxis(dc, plot, l.YTicks);

        var foot = SnapEdge(plot.Bottom);
        foreach (var c in l.Columns) PaintColumn(dc, FillOf(c.Run), c.X, c.Y, c.W, foot, c.Radius);

        PaintBinAxis(dc, plot, l.BaselineY, l.XLabels);

        // The rules are in the text's ink, not the runs': a rule in a run's
        // colour is lost against that run's own columns. The label says whose
        // it is, and the swatch ahead of it repeats that.
        foreach (var mean in l.Means)
        {
            var rule = mean.Rule;
            var x = SnapLine(rule.X);
            dc.DrawLine(Hairline(Ink2), new Point(x, rule.Top), new Point(x, rule.Bottom));
            var text = Text(rule.Label, AxisSize, true, Ink2);
            double left = rule.Align == LabelAlign.Right
                ? rule.LabelX - text.Width - PairedHistogramLayout.SwatchRoom
                : rule.LabelX;
            double top = rule.Top - text.Height + 2;
            DrawSwatch(dc, FillOf(mean.Run), left, top + text.Height / 2);
            Draw(dc, text, left + PairedHistogramLayout.SwatchRoom, top);
        }
    }

    protected override string Summary()
    {
        string unit = Model?.Unit ?? "hit";
        if (Model is not { Bins.Count: > 0 } m || m.CountA + m.CountB == 0) return Nothing("No " + unit + "s in either " + (Model?.Side ?? "run"));
        string Run(string run, int count, double? avg) =>
            $"{run}: {Format.Int(count)} {unit}{(count == 1 ? "" : "s")}" + (avg is { } a ? ", average " + Format.Int(a) : "");
        return Run(m.NameA, m.CountA, m.AvgA) + ". " + Run(m.NameB, m.CountB, m.AvgB);
    }

    protected override Readout? ReadoutAt(Point p)
    {
        if (layout?.Hover(p.X, p.Y) is not { } h) return null;
        return new Readout(h.Bin, h.Head, null,
                           h.Rows.Select(r => new ReadoutRow(r.Label, r.Value, r.Series >= 0 ? FillOf(r.Series) : null)).ToList(),
                           p.X, p.Y);
    }
}
