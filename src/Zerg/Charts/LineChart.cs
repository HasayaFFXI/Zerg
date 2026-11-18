using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Zerg.Core;
using Zerg.Core.Charts;

namespace Zerg.Charts;

/// <summary>
/// The cumulative chart: one line per series on a shared time grid, with end
/// markers, end labels and a crosshair that reads every line at one instant.
///
/// <para>Five layers, in two kinds. Two <b>stand</b> between counts: under the
/// lines, the value axis (gridlines, their labels, the baseline); over them,
/// what is at the lines' ends (the rule that says the session is counting,
/// the markers, the names and the lines back to their markers). They are
/// drawn when the data, the size, the inks or the DPI change, and not when
/// the live edge steps: none of them depends on the time at the edge. On a
/// floating panel their text has a dark outline (<see cref="Chart.Halo"/>),
/// which costs nothing between counts for that reason. Two
/// <b>move</b> with the edge, at the draw frequency: the lines themselves,
/// kept as a bitmap between redraws, and the time axis's labels. The
/// crosshair is a layer of its own, drawn when the pointer reaches another
/// grid time.</para>
///
/// <para>The party's deaths and kills are marked where they fell
/// (<see cref="LineModel.Deaths"/>, <see cref="LineModel.Kills"/>): a death
/// is an X on its character's line, the line dashed and dimmed from it to
/// their next action, and a tick on the time axis; a kill is a fainter tick.
/// All of it is placed by the time axis, so it moves with the edge, on the
/// lines' layer and the axis's.</para>
///
/// <para><b>A range is picked by dragging across it</b> (a Timeline): while
/// <see cref="PickCommand"/> is set, a drag over the plot hands it the
/// stretch dragged (<see cref="ClockRange"/>), and <see cref="Picked"/> is
/// drawn lit in the accent with the rest dimmed. An edge of the range is
/// taken by pressing on it and moves alone, the other where it was; the
/// range is carried by its middle, as long as it was, as a replay's range
/// is; a click outside the range, not a drag, lets it go
/// (<see cref="ResetCommand"/>). The range has a layer of its own over the
/// lines, placed by the time axis as they are, so it moves
/// with the edge; while a drag is under way it is drawn from the drag alone,
/// and handed on as it goes (<see cref="RangePick"/>, not settled), so what
/// it cuts can be counted as it moves, as a replay's range is. Let go, it is
/// handed on settled; called off (Esc, the capture lost), the range picked
/// before it is handed back.</para>
/// </summary>
/// <summary>A range dragged across a Timeline (<see cref="LineChart.PickCommand"/>):
/// settled once let go, before that where the drag has got to.</summary>
public sealed record RangePick(ClockRange Range, bool Settled);

public sealed class LineChart : Chart
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(LineModel),
        typeof(LineChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender,
            // New data states its own edge; one stepped to earlier belongs to the old data.
            (d, _) => ((LineChart)d).edge = double.NaN));

    public static readonly DependencyProperty EndLabelsProperty = DependencyProperty.Register(nameof(EndLabels), typeof(bool),
        typeof(LineChart), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EdgeProperty = DependencyProperty.Register(nameof(Edge), typeof(double),
        typeof(LineChart), new FrameworkPropertyMetadata(double.NaN, (d, e) =>
        {
            // Not AffectsRender: that redraws a chart nobody can see (its
            // section is off screen, the window is in the tray), and at the
            // draw frequency that cost as much as drawing the one on screen.
            // A chart is drawn again when it comes back into view.
            var chart = (LineChart)d;
            chart.edge = (double)e.NewValue;
            if (chart.IsVisible) chart.InvalidateVisual();
        }));

    public static readonly DependencyProperty PickedProperty = DependencyProperty.Register(nameof(Picked), typeof(ClockRange),
        typeof(LineChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PickCommandProperty = DependencyProperty.Register(nameof(PickCommand),
        typeof(ICommand), typeof(LineChart), new PropertyMetadata(null));

    public static readonly DependencyProperty ResetCommandProperty = DependencyProperty.Register(nameof(ResetCommand),
        typeof(ICommand), typeof(LineChart), new PropertyMetadata(null));

    public static readonly DependencyProperty AccentProperty = RegisterInk(nameof(Accent), typeof(LineChart));
    public static readonly DependencyProperty LiveInkProperty = RegisterInk(nameof(LiveInk), typeof(LineChart));
    public static readonly DependencyProperty LeaderInkProperty = RegisterInk(nameof(LeaderInk), typeof(LineChart));
    public static readonly DependencyProperty DeathInkProperty = RegisterInk(nameof(DeathInk), typeof(LineChart));

    /// <summary>How strong the rule at a counting session's edge is drawn.</summary>
    const double LiveStrength = 0.55;
    /// <summary>How strong a line is drawn between a death and its character's next action.</summary>
    const double DownStrength = 0.45;
    /// <summary>Half an X's width; how tall a death's or a kill's tick stands over the baseline.</summary>
    const double CrossReach = 3.5, MarkTick = 6;
    /// <summary>How strong the dimming outside a range is; the light inside it;
    /// a range's edges.</summary>
    const double OutsideStrength = 0.6, InsideStrength = 0.08, RangeEdge = 1.5;
    /// <summary>How far the pointer must go before a press is a drag, not a click.</summary>
    const double DragSlop = 4;
    /// <summary>How near an edge of the range the pointer must be to take it.</summary>
    const double Reach = 7;

    readonly DrawingVisual back = new(), axis = new();
    readonly DrawingVisual lines = new() { CacheMode = new BitmapCache { SnapsToDevicePixels = true } };
    readonly DrawingVisual range = new(), front = new(), crosshair = new();
    double edge = double.NaN;
    /// <summary>What a press took hold of: a new range from where it was
    /// pressed, an edge of the range, or the range by its middle.</summary>
    enum Grip { New, Edge, Middle }

    /// <summary>A drag under way: what it holds, where it was pressed, on
    /// the clock and across the chart, and where it has got to. An edge taken
    /// is dragged from the other, which stays: At is that edge, To the one in
    /// hand. Live once it has gone far enough to be a drag, not a click, and
    /// so handed on as it goes; Before, the range picked when it began.</summary>
    (Grip Hold, double At, double X, double To, bool Live, ClockRange? Before)? dragging;
    LineLayout? layout;
    LineHover? hover;

    // What the standing layers were last drawn for. While all of it holds, a
    // redraw is the edge stepping, and they are left alone.
    LineModel? drawnModel;
    double drawnW, drawnH;
    bool drawnCompact, drawnEndLabels;
    string? drawnEmpty;
    int drawnEpoch = -1;

    // The model's grid with the edge in its last place: one copy for as long
    // as the model lasts, written to on every step.
    double[]? stepped;
    LineModel? steppedOf;

    public LineChart()
    {
        Layers(back, axis, lines, range, front, crosshair);
        SetResourceReference(AccentProperty, "AccentBrush");
        SetResourceReference(LiveInkProperty, "LiveBrush");
        SetResourceReference(LeaderInkProperty, "Line3Brush");
        SetResourceReference(DeathInkProperty, "CritBrush");
    }

    public LineModel? Model { get => (LineModel?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <summary>Name every line at its end, in the margin right of the plot,
    /// in place of a legend; and draw the line that leads stronger than the
    /// pack.</summary>
    public bool EndLabels { get => (bool)GetValue(EndLabelsProperty); set => SetValue(EndLabelsProperty, value); }

    /// <summary>
    /// The live edge: the session clock, in milliseconds. Setting it moves a
    /// live model's last grid time there and redraws, so the lines run flat
    /// through a quiet stretch instead of standing still and then lurching.
    /// Step it at the draw frequency (<see cref="DrawRate"/>), never per
    /// frame. It is ignored for a model that is not live, and forgotten when
    /// the model changes.
    /// </summary>
    public double Edge { get => (double)GetValue(EdgeProperty); set => SetValue(EdgeProperty, value); }

    /// <summary>The range picked, on the session clock: lit, and the rest
    /// of the plot dimmed. Null: none.</summary>
    public ClockRange? Picked { get => (ClockRange?)GetValue(PickedProperty); set => SetValue(PickedProperty, value); }

    /// <summary>Given the stretch dragged across the plot, a <see cref="RangePick"/>,
    /// as the drag goes and once more when it is let go.
    /// Null: the chart cannot be dragged across (a floating panel's).</summary>
    public ICommand? PickCommand { get => (ICommand?)GetValue(PickCommandProperty); set => SetValue(PickCommandProperty, value); }

    /// <summary>A click outside the range: let it go.</summary>
    public ICommand? ResetCommand { get => (ICommand?)GetValue(ResetCommandProperty); set => SetValue(ResetCommandProperty, value); }

    /// <summary>A range's edges, and the light inside it.</summary>
    public Brush Accent { get => Get(AccentProperty); set => SetValue(AccentProperty, value); }

    /// <summary>The rule at the right-hand edge while the session is counting.</summary>
    public Brush LiveInk { get => Get(LiveInkProperty); set => SetValue(LiveInkProperty, value); }
    /// <summary>The thin line from a marker to a name that was moved clear of its neighbours.</summary>
    public Brush LeaderInk { get => Get(LeaderInkProperty); set => SetValue(LeaderInkProperty, value); }
    /// <summary>A death: its X and its tick.</summary>
    public Brush DeathInk { get => Get(DeathInkProperty); set => SetValue(DeathInkProperty, value); }

    Brush InkOf(LineSeries s) => s.Group ? Muted : Solid(s.Color);

    protected override void Paint(double w, double h)
    {
        var m = Model;
        if (m == null || w < 1 || h < 1)
        {
            layout = null;
            drawnModel = null;
            Clear(back);
            Clear(axis);
            Clear(lines);
            Clear(range);
            Clear(front);
            return;
        }

        var times = Grid(m);
        bool compact = Compact, endLabels = EndLabels;
        var empty = EmptyText;

        if (layout != null && ReferenceEquals(m, drawnModel) && w == drawnW && h == drawnH && compact == drawnCompact
            && endLabels == drawnEndLabels && empty == drawnEmpty && Epoch == drawnEpoch)
        {
            // The edge stepped, and nothing else: the time axis is worked
            // out again and the two layers that follow it are drawn.
            if (layout.Empty) return;
            layout.Retime(times);
        }
        else
        {
            // The model is the caller's and may be drawn by another chart too,
            // so the edge is in a copy of its grid (Grid), never in the model.
            layout = LineLayout.Compute(w, h, times, m.Series, compact, endLabels,
                                        (s, bold) => Text(s, LabelSize, bold, bold ? Ink : Ink2).Width);
            (drawnModel, drawnW, drawnH, drawnCompact, drawnEndLabels, drawnEmpty, drawnEpoch) =
                (m, w, h, compact, endLabels, empty, Epoch);
            if (layout.Empty)
            {
                Clear(axis);
                Clear(lines);
                Clear(range);
                Clear(front);
                using var e = back.RenderOpen();
                DrawEmpty(e, w, h, "No damage yet");
                return;
            }
            DrawBack(layout);
            DrawFront(layout, m);
        }
        DrawAxis(layout);
        DrawLines(layout, m, times);
        DrawRange(layout);
    }

    // ------------------------------------------------------------- the range

    /// <summary>The range over the lines: the drag under way, or the range
    /// picked. Outside it the plot is dimmed in the chart's surface; inside,
    /// a faint light in the accent between two accent rules. Placed by the
    /// time axis, so it is drawn whenever the lines are.</summary>
    void DrawRange(LineLayout l)
    {
        using var dc = range.RenderOpen();
        var (from, to) = dragging is { } d && Dragged(l, d) is { } r ? (r.From, r.To)
                       : Picked is { } p ? (p.From, p.To) : (double.NaN, double.NaN);
        if (double.IsNaN(from) || l.Empty) return;
        var plot = l.Plot;
        double x0 = Math.Clamp(SnapLine(l.Px(from)), plot.X, plot.Right);
        double x1 = Math.Clamp(SnapLine(l.Px(to)), plot.X, plot.Right);
        var outside = Faint(Surface, OutsideStrength, 12);
        if (x0 > plot.X) dc.DrawRectangle(outside, null, new Rect(plot.X, plot.Y, x0 - plot.X, plot.H));
        if (x1 < plot.Right) dc.DrawRectangle(outside, null, new Rect(x1, plot.Y, plot.Right - x1, plot.H));
        if (x1 > x0) dc.DrawRectangle(Faint(Accent, InsideStrength, 13), null, new Rect(x0, plot.Y, x1 - x0, plot.H));
        var rule = CachedPen(Accent, 14, () => new Pen(Accent, RangeEdge));
        dc.DrawLine(rule, new Point(x0, plot.Y), new Point(x0, plot.Bottom));
        dc.DrawLine(rule, new Point(x1, plot.Y), new Point(x1, plot.Bottom));
    }

    /// <summary>A brush at a strength, kept with the pens (its kind a pen
    /// kind no pen uses), so a theme change makes it again.</summary>
    Brush Faint(Brush ink, double strength, int kind) => CachedPen(ink, kind, () =>
    {
        var faint = ink.Clone();
        faint.Opacity *= strength;
        faint.Freeze();
        return new Pen(faint, 0);
    }).Brush;

    /// <summary>A time on the session clock at a point across the plot,
    /// inside its span, to the tenth of a second.</summary>
    double TimeAt(LineLayout l, double x)
    {
        double t = l.T0 + (x - l.Plot.X) / l.Plot.W * (l.T1 - l.T0);
        return Math.Clamp(Math.Round(t / 100) * 100, l.T0, l.T1);
    }

    /// <summary>The range a drag has made so far: from where it was pressed
    /// to where it is, or, carried by its middle, the range it began with
    /// moved as far as the pointer has, never past either end of the plot.</summary>
    static ClockRange? Dragged(LineLayout l, (Grip Hold, double At, double X, double To, bool Live, ClockRange? Before) d)
    {
        if (d.Hold != Grip.Middle || d.Before is not { } b) return ClockRange.Of(d.At, d.To);
        double from = Math.Clamp(b.From + d.To - d.At, l.T0, Math.Max(l.T0, l.T1 - b.Length));
        return new ClockRange(from, from + b.Length);
    }

    /// <summary>The range picked, carried by its middle at a point across the
    /// plot: inside it, clear of its edges, and short of the whole plot,
    /// which has nowhere to be carried (a press there starts a new range).</summary>
    bool MiddleAt(LineLayout l, double x) =>
        Picked is { } p && x > l.Px(p.From) && x < l.Px(p.To) && (p.From > l.T0 || p.To < l.T1);

    /// <summary>The edge of the range picked within reach of a point across
    /// the plot, the nearer if both are: the edge in hand, and the other.</summary>
    (double Held, double Other)? EdgeAt(LineLayout l, double x)
    {
        if (Picked is not { } p) return null;
        double from = Math.Abs(l.Px(p.From) - x), to = Math.Abs(l.Px(p.To) - x);
        if (Math.Min(from, to) > Reach) return null;
        return to <= from ? (p.To, p.From) : (p.From, p.To);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (PickCommand == null || layout is not { Empty: false } l) return;
        var p = e.GetPosition(this);
        var plot = l.Plot;
        if (p.X < plot.X || p.X > plot.Right || p.Y < plot.Y || p.Y > plot.Bottom + LineLayout.TickLength) return;
        double at = TimeAt(l, p.X);
        // Marked before the capture: capturing raises a move before it returns.
        dragging = EdgeAt(l, p.X) is { } edge ? (Grip.Edge, edge.Other, p.X, edge.Held, false, Picked)
                 : MiddleAt(l, p.X) ? (Grip.Middle, at, p.X, at, false, Picked)
                 : (Grip.New, at, p.X, at, false, Picked);
        HoverAt(null);
        if (!CaptureMouse())
        {
            dragging = null;
            return;
        }
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragging is not { } d || layout is not { } l)
        {
            // The pointer says what a press would take: an edge, or the range.
            double px = e.GetPosition(this).X;
            var cursor = PickCommand == null || layout is not { Empty: false } at ? Cursors.Cross
                       : EdgeAt(at, px) != null ? Cursors.SizeWE
                       : MiddleAt(at, px) ? Cursors.SizeAll : Cursors.Cross;
            if (Cursor != cursor) Cursor = cursor;
            base.OnMouseMove(e);
            return;
        }
        double x = e.GetPosition(this).X;
        var to = TimeAt(l, x);
        bool live = d.Live || Math.Abs(x - d.X) >= DragSlop;
        if (to == d.To && live == d.Live) return;
        dragging = d = d with { To = to, Live = live };
        DrawRange(l);
        // Handed on as it goes: the section counts it while it moves.
        if (live && Dragged(l, d) is { Length: > 0 } picked) Pick(picked, settled: false);
    }

    void Pick(ClockRange range, bool settled)
    {
        var pick = new RangePick(range, settled);
        if (PickCommand is { } command && command.CanExecute(pick)) command.Execute(pick);
    }

    void Reset()
    {
        if (ResetCommand is { } reset && reset.CanExecute(null)) reset.Execute(null);
    }

    /// <summary>A drag called off: the range it began with, back.</summary>
    void PutBack(ClockRange? before)
    {
        if (before is { } b) Pick(b, settled: true);
        else Reset();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (dragging is not { } d) return;
        dragging = null;
        // Let go of the capture after the drag is over, so losing it does
        // not call the drag off.
        ReleaseMouseCapture();
        e.Handled = true;
        double x = e.GetPosition(this).X;
        var picked = layout is { } held ? Dragged(held, d) : ClockRange.Of(d.At, d.To);
        bool moved = d.Live || Math.Abs(x - d.X) >= DragSlop;
        if (moved && picked is { Length: > 0 }) Pick(picked, settled: true);
        // Dragged back onto itself: nothing picked, the range as it was.
        else if (moved) PutBack(d.Before);
        // A click outside the range lets it go; inside it, or on an edge,
        // it leaves the range as it was.
        else if (d.Hold == Grip.New && Picked is { } p && !p.Holds(d.At)) Reset();
        if (layout is { } l) DrawRange(l);
        HoverAt(e.GetPosition(this));
    }

    /// <summary>The capture taken away mid-drag (another window, Esc): the
    /// drag is called off, and the range is as it was before it.</summary>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (dragging is not { } d) return;
        dragging = null;
        if (d.Live) PutBack(d.Before);
        if (layout is { } l) DrawRange(l);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && dragging != null) ReleaseMouseCapture();
    }

    /// <summary>The model's grid, with its last time at the edge when the
    /// model is live and the edge has been stepped to somewhere it can be.</summary>
    double[] Grid(LineModel m)
    {
        var times = m.Times;
        if (!m.Live || times.Length == 0 || double.IsNaN(edge) || edge == times[^1]
            || (times.Length >= 2 && edge < times[^2]))
            return times;
        if (stepped == null || !ReferenceEquals(steppedOf, m) || stepped.Length != times.Length)
        {
            stepped = (double[])times.Clone();
            steppedOf = m;
        }
        stepped[^1] = edge;
        return stepped;
    }

    /// <summary>Under the lines: the value axis. It stands between counts.</summary>
    void DrawBack(LineLayout l)
    {
        var plot = l.Plot;
        using var dc = back.RenderOpen();
        var grid = Hairline(GridInk);
        foreach (var t in l.YTicks)
        {
            var y = SnapLine(t.Pos);
            dc.DrawLine(grid, new Point(plot.X, y), new Point(plot.Right, y));
            var text = Text(t.Label, AxisSize, false, Muted);
            Label(dc, text, plot.X - LineLayout.YLabelGap - text.Width, y - text.Height / 2);
        }
        var floor = SnapLine(l.AxisY);
        dc.DrawLine(Hairline(AxisInk), new Point(plot.X, floor), new Point(plot.Right, floor));
    }

    /// <summary>The time axis: a tick and a label per round time. It moves
    /// with the edge, at the draw frequency, so its labels are drawn plain
    /// (Draw, not Label): on a floating panel they are the one piece of the
    /// chart's text without an outline, and the one piece drawn on a beat.</summary>
    void DrawAxis(LineLayout l)
    {
        var plot = l.Plot;
        using var dc = axis.RenderOpen();
        var pen = Hairline(AxisInk);
        var floor = SnapLine(l.AxisY);
        foreach (var t in l.XTicks)
        {
            var x = SnapLine(t.Pos);
            dc.DrawLine(pen, new Point(x, floor), new Point(x, floor + LineLayout.TickLength));
            // The first begins at the plot's edge instead of hanging out
            // under the value axis's labels.
            var text = Text(t.Label, AxisSize, false, Muted);
            Draw(dc, text, Math.Max(plot.X, t.Pos - text.Width / 2), plot.Bottom + LineLayout.XLabelGap);
        }

        // Kills, faint, then deaths over them: a short tick up from the
        // baseline at each, so the time ticks under it stay readable.
        if (Model is not { } m) return;
        var kill = Hairline(Muted);
        foreach (var k in m.Kills ?? [])
            if (Within(l, k.At) is double x)
                dc.DrawLine(kill, new Point(x, floor - MarkTick), new Point(x, floor));
        var death = CachedPen(DeathInk, 8, () => new Pen(DeathInk, 1.5));
        foreach (var d in m.Deaths ?? [])
            if (Within(l, d.At) is double x)
                dc.DrawLine(death, new Point(x, floor - MarkTick), new Point(x, floor));
    }

    /// <summary>Where a time is across the plot, on a pixel centre, or null
    /// when it is outside the plot's span.</summary>
    double? Within(LineLayout l, double t) => t < l.T0 || t > l.T1 ? null : SnapLine(l.Px(t));

    /// <summary>The last grid time at or before <paramref name="t"/>, by its index; -1 for none.</summary>
    static int AtOrBefore(double[] times, double t)
    {
        int i = Array.BinarySearch(times, t);
        return i >= 0 ? i : ~i - 1;
    }

    /// <summary>The lines. They move with the edge. A stretch from one of
    /// the party's deaths to their next action is dashed and dimmed, and
    /// the death an X on the line.</summary>
    void DrawLines(LineLayout l, LineModel m, double[] times)
    {
        using var dc = lines.RenderOpen();
        var deaths = m.Deaths ?? [];
        for (int i = 0; i < m.Series.Count; i++)
        {
            var s = m.Series[i];
            var down = deaths.Where(d => d.Series == i).ToList();
            // Down between two grid times: after a death, before the next action.
            bool Down(double t0, double t1) => down.Any(d => t0 >= d.At && t1 <= (d.Next ?? double.PositiveInfinity));
            var g = new StreamGeometry();
            var dashed = down.Count > 0 ? new StreamGeometry() : null;
            using (var ctx = g.Open())
            using (var dash = dashed?.Open())
            {
                bool pen = false;
                int n = Math.Min(times.Length, s.Values.Length);
                Point last = default;
                for (int j = 0; j < n; j++)
                {
                    if (!double.IsFinite(s.Values[j])) { pen = false; continue; }
                    var p = new Point(l.Px(times[j]), l.Py(s.Values[j]));
                    if (pen && dash != null && Down(times[j - 1], times[j]))
                    {
                        dash.BeginFigure(last, false, false);
                        dash.LineTo(p, true, true);
                        ctx.BeginFigure(p, false, false);
                    }
                    else if (pen) ctx.LineTo(p, true, true);
                    else ctx.BeginFigure(p, false, false);
                    pen = true;
                    last = p;
                }
            }
            g.Freeze();
            var ink = InkOf(s);
            dc.DrawGeometry(null, LinePen(ink, s.Group, lead: i == l.Leader), g);
            if (dashed != null)
            {
                dashed.Freeze();
                dc.DrawGeometry(null, DownPen(ink), dashed);
            }
        }

        // The X at each death, on its character's line where the line
        // stood then, ringed in the chart's surface as a marker is.
        var ring = CachedPen(Surface, 9, () => new Pen(Surface, 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
        var cross = CachedPen(DeathInk, 10, () => new Pen(DeathInk, 1.75) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
        foreach (var d in deaths)
        {
            if (d.Series is not int si || si >= m.Series.Count || d.At < l.T0 || d.At > l.T1) continue;
            int j = AtOrBefore(times, d.At);
            var values = m.Series[si].Values;
            double v = j >= 0 && j < values.Length && double.IsFinite(values[j]) ? values[j] : 0;
            double x = l.Px(d.At), y = l.Py(v), r = CrossReach;
            foreach (var pen in new[] { ring, cross })
            {
                dc.DrawLine(pen, new Point(x - r, y - r), new Point(x + r, y + r));
                dc.DrawLine(pen, new Point(x - r, y + r), new Point(x + r, y - r));
            }
        }
    }

    /// <summary>A line's own colour, dimmed and dashed: its character is
    /// down, as far as the file can say.</summary>
    Pen DownPen(Brush ink) => CachedPen(ink, 11, () =>
    {
        var faint = ink.Clone();
        faint.Opacity *= DownStrength;
        faint.Freeze();
        return new Pen(faint, 1.5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            DashCap = PenLineCap.Round,
            DashStyle = new DashStyle([2, 2.5], 0),
        };
    });

    /// <summary>Over the lines: what is at their ends. It stands between counts.</summary>
    void DrawFront(LineLayout l, LineModel m)
    {
        var plot = l.Plot;
        using var dc = front.RenderOpen();

        // The edge of a session that is counting.
        if (m.Running)
        {
            var x = SnapLine(plot.Right);
            dc.DrawLine(FaintHairline(LiveInk, LiveStrength), new Point(x, plot.Y), new Point(x, plot.Bottom));
        }

        foreach (var mark in l.Markers)
            dc.DrawEllipse(InkOf(m.Series[mark.Series]), Ring, new Point(mark.X, mark.Y),
                           LineLayout.MarkerRadius, LineLayout.MarkerRadius);

        var leader = Hairline(LeaderInk);
        foreach (var label in l.Labels)
        {
            if (label.Elbow is { } e)
            {
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(e.X0, e.Y0), false, false);
                    ctx.LineTo(new Point(e.X1, e.Y1), true, true);
                    ctx.LineTo(new Point(e.X2, e.Y1), true, true);
                }
                g.Freeze();
                dc.DrawGeometry(null, leader, g);
            }

            // The leader's name in the strongest ink; a total, where there
            // is one after the name, in the quietest.
            var text = Text(label.Text, LabelSize, label.Lead, label.Lead ? Ink : Ink2);
            Label(dc, text, label.X, label.Y - text.Height / 2);
            if (label.Total.Length > 0)
            {
                var total = Text(label.Total, LabelSize, false, Muted);
                Label(dc, total, label.TotalRight - total.Width, label.Y - total.Height / 2);
            }
        }
    }

    protected override string Summary()
    {
        if (Model is not { Times.Length: > 0, Series.Count: > 0 } m) return Nothing("No data");
        var summary = LineLayout.Summary(m.Times, m.Series.Select(s => (s.Name, s.Values)).ToList());
        int kills = m.Kills?.Count ?? 0, deaths = m.Deaths?.Count ?? 0;
        return kills + deaths == 0 ? summary
            : summary + ". " + Format.Int(kills) + (kills == 1 ? " kill, " : " kills, ") +
              Format.Int(deaths) + (deaths == 1 ? " death" : " deaths");
    }

    protected override Readout? ReadoutAt(Point p)
    {
        hover = layout?.Hover(p.X, p.Y);
        if (hover == null || Model is not { } m) return null;
        var rows = hover.Rows.Select(r => new ReadoutRow(r.Label, r.Value, InkOf(m.Series[r.Series]))).ToList();
        // What fell in the stretch the crosshair reads: from halfway back to
        // the grid time before it to halfway on to the one after.
        var times = Grid(m);
        int i = hover.Index;
        double from = i > 0 ? (times[i - 1] + times[i]) / 2 : double.NegativeInfinity;
        double to = i < times.Length - 1 ? (times[i] + times[i + 1]) / 2 : double.PositiveInfinity;
        foreach (var k in m.Kills ?? [])
            if (k.At > from && k.At <= to)
                rows.Add(new ReadoutRow(k.Named + " killed", k.Length is double len ? "in " + Format.Elapsed(len, tenths: true) : ""));
        foreach (var d in m.Deaths ?? [])
            if (d.At > from && d.At <= to)
                rows.Add(new ReadoutRow(d.Name + " died", d.Next is double next
                    ? "acted again " + Format.Elapsed(next) : "not acted since", DeathInk));
        return new Readout(hover.Index, hover.Head, null, rows, hover.AnchorX, p.Y);
    }

    protected override void OnHover(Readout? readout)
    {
        using var dc = crosshair.RenderOpen();
        if (readout == null || hover == null || layout == null || Model is not { } m) return;
        var plot = layout.Plot;
        var x = SnapLine(hover.X);
        dc.DrawLine(DashedHairline(Muted), new Point(x, plot.Y), new Point(x, plot.Bottom));
        foreach (var d in hover.Dots)
            dc.DrawEllipse(InkOf(m.Series[d.Series]), Ring, new Point(x, d.Y), LineLayout.MarkerRadius, LineLayout.MarkerRadius);
    }
}
