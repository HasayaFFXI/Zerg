using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using Zerg.Core;

namespace Zerg.Charts;

/// <summary>
/// The whole of a replayed file, first line to last, and the range picked
/// over it: the party's damage as a filled area under a line, smoothed a
/// little, the time of day along the bottom, and the range drawn as a
/// Timeline draws one (<see cref="LineChart"/>): outside it dimmed, inside a
/// faint light in the accent between two accent rules, its handles. Under
/// the area, a short tick at each kill, and at each of the party's deaths
/// in Crit: where the fights were.
///
/// <para><b>Dragged and keyed.</b> A handle is dragged by itself, the range
/// between them by its middle, and a drag anywhere else is a new range; the
/// pointer says which (a resize arrow at a handle, a move inside). A click
/// outside the range, not a drag, lets it go: the whole file again.
/// With the keyboard, Left and Right move the handle in hand a second (ten
/// with Shift), and Up and Down change hands; the handle in hand has a short
/// cap of the accent while the strip has the keyboard. Each move goes to
/// the <see cref="ReplayRange"/>, which says when it has settled.</para>
///
/// <para>Painted whole when the range, the file, the size or an ink
/// changes, and at no other time: nothing here moves with the clock.</para>
/// </summary>
public sealed class RangeStrip : FrameworkElement
{
    /// <summary>The area's height and the time labels' baseline, from the
    /// top, in units; and room left over the area's highest point.</summary>
    const double BarsTall = 58, LabelsAt = 76, Headroom = 6;
    /// <summary>How wide a stretch of the file each point of the area
    /// stands for, and how many points either side are averaged into it.</summary>
    const double Pitch = 4;
    const int Smooth = 2;
    /// <summary>The area's fill, the dimming outside the range and the light
    /// inside it, as strengths of their inks.</summary>
    const double AreaStrength = 0.22, OutsideStrength = 0.6, InsideStrength = 0.08;
    /// <summary>The area's line, a handle's rule, the cap on the handle in
    /// hand, and how near the pointer must be to take a handle.</summary>
    const double LineWide = 1.5, HandleLine = 1.5, CapWide = 7, CapTall = 3, Reach = 7;
    /// <summary>The least room between two time labels.</summary>
    const double LabelRoom = 64;
    /// <summary>How far a kill's or a death's tick hangs under the bars.</summary>
    const double MarkTick = 5;

    static readonly double[] Steps = [1, 2, 5, 10, 15, 30, 60, 120, 240, 480];

    public static readonly DependencyProperty RangeProperty = DependencyProperty.Register(nameof(Range),
        typeof(ReplayRange), typeof(RangeStrip), new PropertyMetadata(null, (d, e) => ((RangeStrip)d).Follow(e)));

    static DependencyProperty Ink(string name) => DependencyProperty.Register(name, typeof(Brush), typeof(RangeStrip),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((RangeStrip)d).inks = null));

    public static readonly DependencyProperty OnInkProperty = Ink(nameof(OnInk));
    public static readonly DependencyProperty AccentProperty = Ink(nameof(Accent));
    public static readonly DependencyProperty RuleProperty = Ink(nameof(Rule));
    public static readonly DependencyProperty LabelInkProperty = Ink(nameof(LabelInk));
    public static readonly DependencyProperty SurfaceProperty = Ink(nameof(Surface));
    public static readonly DependencyProperty DeathInkProperty = Ink(nameof(DeathInk));

    public ReplayRange? Range { get => (ReplayRange?)GetValue(RangeProperty); set => SetValue(RangeProperty, value); }
    /// <summary>The area: its line, and its fill at a strength of it.</summary>
    public Brush? OnInk { get => (Brush?)GetValue(OnInkProperty); set => SetValue(OnInkProperty, value); }
    public Brush? Accent { get => (Brush?)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public Brush? Rule { get => (Brush?)GetValue(RuleProperty); set => SetValue(RuleProperty, value); }
    public Brush? LabelInk { get => (Brush?)GetValue(LabelInkProperty); set => SetValue(LabelInkProperty, value); }
    /// <summary>The strip's background: outside the range, laid over the area at a strength.</summary>
    public Brush? Surface { get => (Brush?)GetValue(SurfaceProperty); set => SetValue(SurfaceProperty, value); }
    /// <summary>A death's tick.</summary>
    public Brush? DeathInk { get => (Brush?)GetValue(DeathInkProperty); set => SetValue(DeathInkProperty, value); }

    enum Hold { None, From, To, Both, New }

    Hold held;
    /// <summary>Where a drag of the whole range was taken, and the range then.</summary>
    double grabbedAt, grabbedFrom, grabbedTo;
    /// <summary>A new range is begun only once the pointer has gone this far:
    /// a click outside the range lets it go instead.</summary>
    const double DragSlop = 4;
    /// <summary>Where a new range was pressed for, across the strip; whether
    /// the press was outside the range; and whether it has become a drag.</summary>
    double pressedX;
    bool pressedOutside, begun;
    /// <summary>The handle the keyboard moves.</summary>
    bool toInHand;
    /// <summary>The bars, kept for the replay and the count they were made for.</summary>
    (Replay Replay, int Count, double[] Bins)? bins;
    readonly Dictionary<string, DrawnText> labels = [];
    /// <summary>The fainter inks, made from the inks given when first asked.</summary>
    (Brush? Area, Brush? Outside, Brush? Inside, Pen Line, Pen Rule)? inks;

    public RangeStrip()
    {
        SetResourceReference(OnInkProperty, "Text2Brush");
        SetResourceReference(AccentProperty, "AccentBrush");
        SetResourceReference(RuleProperty, "Line2Brush");
        SetResourceReference(LabelInkProperty, "Text3Brush");
        SetResourceReference(SurfaceProperty, "Bg1Brush");
        SetResourceReference(DeathInkProperty, "CritBrush");
        Focusable = true;
        UseLayoutRounding = true;
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) InvalidateVisual();
        };
    }

    void Follow(DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ReplayRange old) old.PropertyChanged -= OnRangeChanged;
        if (e.NewValue is ReplayRange now) now.PropertyChanged += OnRangeChanged;
        bins = null;
        Redraw();
    }

    void OnRangeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ReplayRange.From) or nameof(ReplayRange.To) or nameof(ReplayRange.Replay))
            Redraw();
    }

    void Redraw()
    {
        if (IsVisible) InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, LabelsAt + 4);

    // ---------------------------------------------------------- the place

    /// <summary>The file's time at <paramref name="x"/>, and back.</summary>
    static double TimeAt(Replay r, double x, double w) => r.First + Math.Clamp(x / w, 0, 1) * r.Span;
    static double XOf(Replay r, double t, double w) => r.Span > 0 ? (t - r.First) / r.Span * w : 0;

    double Snap(double x)
    {
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        return Math.Round(x * scale) / scale;
    }

    // ------------------------------------------------------------ painting

    protected override void OnRender(DrawingContext dc)
    {
        double w = RenderSize.Width, h = RenderSize.Height;
        // Hit everywhere, ink or none.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
        if (w < 1 || Range is not { Replay: { } r } range || !(r.Span > 0)) return;

        double from = Snap(XOf(r, range.From, w)), to = Snap(XOf(r, range.To, w));
        var (area, outside, inside, line, rule) = inks ??= (Faint(OnInk, AreaStrength), Faint(Surface, OutsideStrength),
            Faint(Accent, InsideStrength), Frozen(new Pen(OnInk, LineWide) { LineJoin = PenLineJoin.Round }),
            Frozen(new Pen(Accent, HandleLine)));

        // The party's damage: a point per few units, each the average of its
        // neighbours, so the eye reads where the fighting was, not each swing.
        int count = Math.Max(2, (int)Math.Floor(w / Pitch));
        if (bins is not { } kept || !ReferenceEquals(kept.Replay, r) || kept.Count != count)
            bins = kept = (r, count, Smoothed(r.Bins(count)));
        double most = kept.Bins.Max();
        if (most > 0)
        {
            double pitch = w / count, room = BarsTall - Headroom;
            Point At(int i) => new((i + 0.5) * pitch, BarsTall - kept.Bins[i] / most * room);
            var fill = new StreamGeometry();
            var top = new StreamGeometry();
            using (var f = fill.Open())
            using (var t = top.Open())
            {
                f.BeginFigure(new Point(0, BarsTall), true, true);
                f.LineTo(new Point(0, At(0).Y), false, false);
                t.BeginFigure(new Point(0, At(0).Y), false, false);
                for (int i = 0; i < count; i++)
                {
                    f.LineTo(At(i), false, false);
                    t.LineTo(At(i), true, true);
                }
                f.LineTo(new Point(w, At(count - 1).Y), false, false);
                f.LineTo(new Point(w, BarsTall), false, false);
                t.LineTo(new Point(w, At(count - 1).Y), true, true);
            }
            fill.Freeze();
            top.Freeze();
            dc.DrawGeometry(area, null, fill);
            dc.DrawGeometry(null, line, top);
        }

        // The range, as a Timeline draws one: the rest dimmed, a light inside.
        if (from > 0) dc.DrawRectangle(outside, null, new Rect(0, 0, from, BarsTall));
        if (to < w) dc.DrawRectangle(outside, null, new Rect(to, 0, w - to, BarsTall));
        if (to > from) dc.DrawRectangle(inside, null, new Rect(from, 0, to - from, BarsTall));

        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double hair = 1 / scale;
        dc.DrawRectangle(Rule, null, new Rect(0, BarsTall, w, hair));

        // The kills, then the deaths over them, hung from the rule.
        void Ticks(IReadOnlyList<double> at, Brush? ink, double wide)
        {
            foreach (var t in at)
                if (t >= r.First && t <= r.Last)
                    dc.DrawRectangle(ink, null, new Rect(Snap(XOf(r, t, w)) - wide / 2, BarsTall + hair, wide, MarkTick));
        }
        Ticks(r.Kills, LabelInk, hair);
        Ticks(r.Deaths, DeathInk, Math.Max(hair, Math.Round(1.5 * scale) / scale));

        Labels(dc, r, w);

        // The handles last, over everything; the one in hand capped while
        // the strip has the keyboard.
        Handle(dc, rule, from, !toInHand && IsKeyboardFocused);
        Handle(dc, rule, to, toInHand && IsKeyboardFocused);
    }

    void Handle(DrawingContext dc, Pen rule, double x, bool inHand)
    {
        dc.DrawLine(rule, new Point(x, 0), new Point(x, BarsTall));
        if (inHand) dc.DrawRoundedRectangle(Accent, null, new Rect(x - CapWide / 2, 0, CapWide, CapTall), 1, 1);
    }

    /// <summary>Each bin the average of itself and <see cref="Smooth"/> either side.</summary>
    static double[] Smoothed(double[] bins)
    {
        var smooth = new double[bins.Length];
        for (int i = 0; i < bins.Length; i++)
        {
            double sum = 0;
            int n = 0;
            for (int j = Math.Max(0, i - Smooth); j <= Math.Min(bins.Length - 1, i + Smooth); j++, n++) sum += bins[j];
            smooth[i] = sum / n;
        }
        return smooth;
    }

    static Pen Frozen(Pen pen)
    {
        pen.Freeze();
        return pen;
    }

    /// <summary>Times of day along the bottom, on a round step that leaves
    /// room between them, each centred on its moment.</summary>
    void Labels(DrawingContext dc, Replay r, double w)
    {
        double minutes = r.Span / 60_000;
        double step = Steps.FirstOrDefault(s => minutes / s * LabelRoom <= w, Steps[^1]);
        if (!Format.TryLocal(r.First, out var start)) return;
        // The first whole step of local time at or after the first line.
        var at = start.DateTime.Date.AddMinutes(Math.Ceiling(start.TimeOfDay.TotalMinutes / step) * step);
        static double Ms(DateTime local) =>
            new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeMilliseconds();
        var face = new Typeface((FontFamily)FindResource("SmallFont"), FontStyles.Normal, FontWeights.Normal,
                                FontStretches.Normal);
        double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (labels.Count > 200) Forget();
        for (; Ms(at) < r.Last; at = at.AddMinutes(step))
        {
            double x = XOf(r, Ms(at), w);
            var text = at.ToString("HH':'mm", CultureInfo.InvariantCulture);
            if (!labels.TryGetValue(text, out var drawn))
                labels[text] = drawn = new DrawnText(text, face, 10, LabelInk ?? Brushes.Gray, dip);
            double left = Math.Clamp(x - drawn.Width / 2, 0, Math.Max(0, w - drawn.Width));
            drawn.Draw(dc, Snap(left), LabelsAt - drawn.Baseline);
        }
    }

    void Forget()
    {
        foreach (var t in labels.Values) t.Dispose();
        labels.Clear();
    }

    static Brush? Faint(Brush? ink, double strength)
    {
        if (ink == null) return null;
        var b = ink.Clone();
        b.Opacity *= strength;
        b.Freeze();
        return b;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == LabelInkProperty) Forget();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        Forget();
        InvalidateVisual();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        InvalidateVisual();
    }

    // ------------------------------------------------------------- pointer

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (Range is not { Replay: { } r } range) return;
        Focus();
        double w = RenderSize.Width, x = e.GetPosition(this).X;
        double from = XOf(r, range.From, w), to = XOf(r, range.To, w);
        // The nearer handle, if either is in reach; then the middle, unless
        // the range is the whole file, which has nowhere to be carried: a
        // press there starts a new range, as one outside a range does.
        held = Math.Abs(x - from) <= Reach && Math.Abs(x - from) <= Math.Abs(x - to) ? Hold.From
             : Math.Abs(x - to) <= Reach ? Hold.To
             : x > from && x < to && !Whole(range, r) ? Hold.Both
             : Hold.New;
        grabbedAt = TimeAt(r, x, w);
        (grabbedFrom, grabbedTo) = (range.From, range.To);
        (pressedX, pressedOutside, begun) = (x, x < from || x > to, held != Hold.New);
        toInHand = held == Hold.To || held == Hold.New;
        // Marked before the capture, which moves the mouse before it returns.
        if (!CaptureMouse()) held = Hold.None;
        e.Handled = true;
        Move(x, settled: false);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (held != Hold.None)
        {
            Move(e.GetPosition(this).X, settled: false);
            return;
        }
        if (Range is not { Replay: { } r } range) return;
        double w = RenderSize.Width, x = e.GetPosition(this).X;
        bool near = Math.Abs(x - XOf(r, range.From, w)) <= Reach || Math.Abs(x - XOf(r, range.To, w)) <= Reach;
        Cursor = near ? Cursors.SizeWE
               : x > XOf(r, range.From, w) && x < XOf(r, range.To, w) && !Whole(range, r) ? Cursors.SizeAll
               : Cursors.Cross;
    }

    /// <summary>The range holds every line of the file.</summary>
    static bool Whole(ReplayRange range, Replay r) => range.From <= r.First && range.To >= r.Last;

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (held == Hold.None) return;
        // A click outside the range, not a drag, lets it go: the whole file.
        if (!begun && pressedOutside && Range is { Replay: { } r } range) range.Set(r.First, r.Last, settled: true);
        else Move(e.GetPosition(this).X, settled: true);
        held = Hold.None;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        // Taken away mid-drag: the range stands where it was last moved to.
        if (held == Hold.None || Range is not { } range) return;
        held = Hold.None;
        range.Set(range.From, range.To, settled: true);
    }

    void Move(double x, bool settled)
    {
        if (Range is not { Replay: { } r } range) return;
        // A new range waits until the press has become a drag.
        if (!begun)
        {
            if (Math.Abs(x - pressedX) < DragSlop) return;
            begun = true;
        }
        double t = TimeAt(r, x, RenderSize.Width);
        switch (held)
        {
            case Hold.From: range.Set(t, Math.Max(t + Session.Second, grabbedTo), settled); break;
            case Hold.To: range.Set(Math.Min(grabbedFrom, t - Session.Second), t, settled); break;
            case Hold.New: range.Set(grabbedAt, t, settled); break;
            case Hold.Both:
                double d = t - grabbedAt, length = grabbedTo - grabbedFrom;
                // Never past either end; a range longer than the file (its
                // handles stand on whole seconds) stays where it was.
                double a = Math.Clamp(grabbedFrom + d, r.First, Math.Max(r.First, r.Last - length));
                range.Set(a, a + length, settled);
                break;
        }
    }

    // ------------------------------------------------------------ keyboard

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Range is not { Replay: not null } range) return;
        double by = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10 * Session.Second : Session.Second;
        switch (e.Key)
        {
            case Key.Left or Key.Right:
                double d = e.Key == Key.Left ? -by : by;
                if (toInHand) range.Set(range.From, Math.Max(range.From + Session.Second, range.To + d), settled: true);
                else range.Set(Math.Min(range.From + d, range.To - Session.Second), range.To, settled: true);
                break;
            // A control using arrows keeps all four.
            case Key.Up or Key.Down:
                toInHand = !toInHand;
                InvalidateVisual();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    /// <summary>One picture to UI Automation, with the range as its help text.</summary>
    sealed class Peer(RangeStrip owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
        protected override string GetClassNameCore() => nameof(RangeStrip);

        protected override string GetHelpTextCore() =>
            owner.Range is { Replay: not null } r ? $"From {r.FromText} to {r.ToText}, {r.LengthText} long" : "";
    }
}
