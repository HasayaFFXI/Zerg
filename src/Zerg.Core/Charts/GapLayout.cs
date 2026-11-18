namespace Zerg.Core.Charts;

/// <summary>One point of the gap line, in the chart's own units.</summary>
public readonly record struct GapPoint(double X, double Y);

/// <summary>A mark under the time axis, or beside the value axis.</summary>
public readonly record struct GapTick(double At, string Label);

/// <summary>
/// Where Compare's Gap view puts things: B − A over the one clock both runs
/// share, the zero line across the middle and the scale even about it, so
/// a run ahead and a run behind read the same size. Each stretch of line
/// is a list of points; a run that has ended lifts the pen.
/// </summary>
public sealed class GapLayout
{
    public IReadOnlyList<IReadOnlyList<GapPoint>> Stretches { get; private init; } = [];
    public double ZeroY { get; private init; }
    public double Left { get; private init; }
    public double Right { get; private init; }
    public double Top { get; private init; }
    public double Bottom { get; private init; }
    public IReadOnlyList<GapTick> Times { get; private init; } = [];
    public IReadOnlyList<GapTick> Values { get; private init; } = [];

    /// <summary>The gutter the value labels stand in, and the strip the time labels do.</summary>
    public const double Gutter = 44, Strip = 18, Pad = 8;

    static readonly double[] Steps = [5_000, 10_000, 15_000, 30_000, 60_000, 120_000, 300_000, 600_000, 900_000, 1_800_000];

    public static GapLayout Compute(IReadOnlyList<double> times, IReadOnlyList<double> gap, double width, double height)
    {
        double left = Gutter, right = Math.Max(left + 1, width - Pad), top = Pad, bottom = Math.Max(top + 1, height - Strip);
        double end = 0, most = 0;
        for (int i = 0; i < times.Count && i < gap.Count; i++)
        {
            if (!double.IsFinite(gap[i])) continue;
            end = Math.Max(end, times[i]);
            most = Math.Max(most, Math.Abs(gap[i]));
        }
        if (end <= 0) end = times.Count > 0 ? times[^1] : 1000;
        if (end <= 0) end = 1000;
        double scale = Nice(most);
        double zero = (top + bottom) / 2;
        double X(double t) => left + (right - left) * Math.Clamp(t / end, 0, 1);
        double Y(double v) => zero - (bottom - top) / 2 * Math.Clamp(v / scale, -1, 1);

        var stretches = new List<IReadOnlyList<GapPoint>>();
        var line = new List<GapPoint>();
        for (int i = 0; i < times.Count && i < gap.Count; i++)
        {
            if (!double.IsFinite(gap[i]))
            {
                if (line.Count > 0) stretches.Add(line);
                line = [];
                continue;
            }
            line.Add(new GapPoint(X(times[i]), Y(gap[i])));
        }
        if (line.Count > 0) stretches.Add(line);

        double step = Steps.FirstOrDefault(s => end / s <= 6, Steps[^1]);
        var ticks = new List<GapTick>();
        for (double t = 0; t <= end + 1; t += step) ticks.Add(new GapTick(X(t), Format.Elapsed(t)));

        return new GapLayout
        {
            Stretches = stretches, ZeroY = zero, Left = left, Right = right, Top = top, Bottom = bottom, Times = ticks,
            Values = [new GapTick(Y(scale), "+" + Format.Compact(scale)), new GapTick(zero, "0"), new GapTick(Y(-scale), "−" + Format.Compact(scale))],
        };
    }

    /// <summary>A round figure at or over <paramref name="v"/>: 1, 2 or 5 of a power of ten.</summary>
    public static double Nice(double v)
    {
        if (!(v > 0)) return 1;
        double p = Math.Pow(10, Math.Floor(Math.Log10(v)));
        foreach (var m in new[] { 1d, 2, 5, 10 }) if (m * p >= v) return m * p;
        return 10 * p;
    }
}
