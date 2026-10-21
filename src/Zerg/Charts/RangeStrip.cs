using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using Zerg.Core;

namespace Zerg.Charts;

/// <summary>
/// The whole of a replayed file, first line to last, and the range picked
/// over it: the party's damage as a bar per few units of width, the time
/// of day along the bottom, and two amber handles. Everything outside the
/// range is dimmed.
///
/// <para><b>Dragged and keyed.</b> A handle is dragged by itself, the range
/// between them by its middle, and a press anywhere else starts a new range
/// there. With the keyboard, Left and Right move the handle in hand a
/// second (ten with Shift), and Up and Down change hands. Each move goes to
/// the <see cref="ReplayRange"/>, which says when it has settled.</para>
///
/// <para>Painted whole when the range, the file, the size or an ink
/// changes, and at no other time: nothing here moves with the clock.</para>
/// </summary>
public sealed class RangeStrip : FrameworkElement
{
    /// <summary>The bars' height and the time labels' baseline, from the
    /// top, in units.</summary>
    const double BarsTall = 58, LabelsAt = 76;
    /// <summary>From one bar to the next, and a bar's width.</summary>
    const double Pitch = 4, BarWide = 3;
    /// <summary>A handle's line, its grip, and how near the pointer must be to take it.</summary>
    const double HandleLine = 2, GripWide = 8, GripTall = 20, Reach = 7;
    /// <summary>The least room between two time labels.</summary>
    const double LabelRoom = 64;

    static readonly double[] Steps = [1, 2, 5, 10, 15, 30, 60, 120, 240, 480];

    public static readonly DependencyProperty RangeProperty = DependencyProperty.Register(nameof(Range),
        typeof(ReplayRange), typeof(RangeStrip), new PropertyMetadata(null, (d, e) => ((RangeStrip)d).Follow(e)));

    static DependencyProperty Ink(string name) => DependencyProperty.Register(name, typeof(Brush), typeof(RangeStrip),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((RangeStrip)d).tint = null));

    public static readonly DependencyProperty OnInkProperty = Ink(nameof(OnInk));
    public static readonly DependencyProperty OffInkProperty = Ink(nameof(OffInk));
    public static readonly DependencyProperty AccentProperty = Ink(nameof(Accent));
    public static readonly DependencyProperty RuleProperty = Ink(nameof(Rule));
    public static readonly DependencyProperty LabelInkProperty = Ink(nameof(LabelInk));
    public static readonly DependencyProperty SurfaceProperty = Ink(nameof(Surface));

    public ReplayRange? Range { get => (ReplayRange?)GetValue(RangeProperty); set => SetValue(RangeProperty, value); }
    /// <summary>A bar inside the range, and outside it.</summary>
    public Brush? OnInk { get => (Brush?)GetValue(OnInkProperty); set => SetValue(OnInkProperty, value); }
    public Brush? OffInk { get => (Brush?)GetValue(OffInkProperty); set => SetValue(OffInkProperty, value); }
    public Brush? Accent { get => (Brush?)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public Brush? Rule { get => (Brush?)GetValue(RuleProperty); set => SetValue(RuleProperty, value); }
    public Brush? LabelInk { get => (Brush?)GetValue(LabelInkProperty); set => SetValue(LabelInkProperty, value); }
    /// <summary>What the grips are ringed with, so they stand off the bars.</summary>
    public Brush? Surface { get => (Brush?)GetValue(SurfaceProperty); set => SetValue(SurfaceProperty, value); }

    enum Hold { None, From, To, Both, New }

    Hold held;
    /// <summary>Where a drag of the whole range was taken, and the range then.</summary>
    double grabbedAt, grabbedFrom, grabbedTo;
    /// <summary>The handle the keyboard moves.</summary>
    bool toInHand;
    /// <summary>The bars, kept for the replay and the count they were made for.</summary>
    (Replay Replay, int Count, double[] Bins)? bins;
    readonly Dictionary<string, DrawnText> labels = [];
    Brush? tint;

    public RangeStrip()
    {
        SetResourceReference(OnInkProperty, "Text2Brush");
        SetResourceReference(OffInkProperty, "Text4Brush");
        SetResourceReference(AccentProperty, "AccentBrush");
        SetResourceReference(RuleProperty, "Line2Brush");
        SetResourceReference(LabelInkProperty, "Text3Brush");
        SetResourceReference(SurfaceProperty, "Bg1Brush");
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
        tint ??= Faint(Accent, 0.08);

        // The range's own stretch, under its bars.
        if (to > from) dc.DrawRectangle(tint, null, new Rect(from, 0, to - from, BarsTall));

        int count = Math.Max(1, (int)Math.Floor(w / Pitch));
        if (bins is not { } kept || !ReferenceEquals(kept.Replay, r) || kept.Count != count)
            bins = kept = (r, count, r.Bins(count));
        double most = kept.Bins.Max();
        double pitch = w / count;
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double wide = Math.Max(1, Math.Round(Math.Min(BarWide, pitch - 1 / scale) * scale)) / scale;
        for (int i = 0; i < count; i++)
        {
            double v = kept.Bins[i];
            if (!(v > 0) || !(most > 0)) continue;
            double x = Snap(i * pitch);
            double tall = Math.Max(1, Math.Round(v / most * BarsTall * scale)) / scale;
            // In the range if its middle is.
            double mid = r.First + (i + 0.5) / count * r.Span;
            bool on = mid >= range.From && mid < range.To;
            dc.DrawRectangle(on ? OnInk : OffInk, null, new Rect(x, BarsTall - tall, wide, tall));
        }
        double line = 1 / scale;
        dc.DrawRectangle(Rule, null, new Rect(0, BarsTall, w, line));

        Labels(dc, r, w);

        // The handles last, over everything; the one in hand ringed while
        // the strip has the keyboard.
        Handle(dc, from, !toInHand && IsKeyboardFocused);
        Handle(dc, to, toInHand && IsKeyboardFocused);
    }

    void Handle(DrawingContext dc, double x, bool inHand)
    {
        dc.DrawRectangle(Accent, null, new Rect(x - HandleLine / 2, -2, HandleLine, BarsTall + 4));
        var grip = new Rect(x - GripWide / 2, (BarsTall - GripTall) / 2, GripWide, GripTall);
        var ring = new Pen(Surface, 2);
        ring.Freeze();
        dc.DrawRoundedRectangle(Accent, ring, grip, 2.5, 2.5);
        if (inHand)
        {
            var focus = new Pen(LabelInk, 1);
            focus.Freeze();
            grip.Inflate(3, 3);
            dc.DrawRoundedRectangle(null, focus, grip, 4, 4);
        }
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
        // The nearer handle, if either is in reach; then the middle.
        held = Math.Abs(x - from) <= Reach && Math.Abs(x - from) <= Math.Abs(x - to) ? Hold.From
             : Math.Abs(x - to) <= Reach ? Hold.To
             : x > from && x < to ? Hold.Both
             : Hold.New;
        grabbedAt = TimeAt(r, x, w);
        (grabbedFrom, grabbedTo) = (range.From, range.To);
        toInHand = held == Hold.To || held == Hold.New;
        // Marked before the capture, which moves the mouse before it returns.
        if (!CaptureMouse()) held = Hold.None;
        e.Handled = true;
        Move(e.GetPosition(this).X, settled: false);
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
        Cursor = near ? Cursors.SizeWE : x > XOf(r, range.From, w) && x < XOf(r, range.To, w) ? Cursors.SizeAll : Cursors.Cross;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (held == Hold.None) return;
        Move(e.GetPosition(this).X, settled: true);
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
        double t = TimeAt(r, x, RenderSize.Width);
        switch (held)
        {
            case Hold.From: range.Set(t, Math.Max(t + Session.Second, grabbedTo), settled); break;
            case Hold.To: range.Set(Math.Min(grabbedFrom, t - Session.Second), t, settled); break;
            case Hold.New: range.Set(grabbedAt, t, settled); break;
            case Hold.Both:
                double d = t - grabbedAt, length = grabbedTo - grabbedFrom;
                double a = Math.Clamp(grabbedFrom + d, r.First, r.Last - length);
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
