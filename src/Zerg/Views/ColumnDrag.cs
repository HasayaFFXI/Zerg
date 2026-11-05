using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Zerg.Core.Layout;

namespace Zerg.Views;

/// <summary>
/// A row of column headings whose columns are moved by dragging them
/// (<c>v:ColumnDrag.On="True"</c> on the headings' <see cref="Cells"/>).
/// A press and release on a heading is still the heading's own (a sort);
/// once the pointer has gone a few units sideways it is a drag: the column
/// is dimmed down the whole table, its name follows the pointer, and an
/// accent bar stands where it will go. Let go, the column goes there
/// (<see cref="TableColumns.Move"/>). A column in a group carries its group,
/// and a group's name (in the row over the headings) does too; nothing
/// goes inside a group. The first column, the name, stays put.
///
/// <para>Not in a floating panel, which is moved by hand from anywhere in
/// it. Losing the pointer (another window, Alt+Tab) drops the drag and
/// moves nothing.</para>
/// </summary>
public static class ColumnDrag
{
    public static readonly DependencyProperty OnProperty = DependencyProperty.RegisterAttached("On", typeof(bool),
        typeof(ColumnDrag), new PropertyMetadata(false, (d, e) =>
        {
            if (d is not Cells heads) return;
            heads.PreviewMouseLeftButtonDown -= Down;
            heads.PreviewMouseMove -= Moved;
            heads.PreviewMouseLeftButtonUp -= Up;
            heads.LostMouseCapture -= Lost;
            if (e.NewValue is not true) return;
            // Ink everywhere, so the room between two headings' words is a heading too.
            heads.Background ??= Brushes.Transparent;
            // Tab goes through the headings in the order they stand (Cells sets each one's index).
            KeyboardNavigation.SetTabNavigation(heads, KeyboardNavigationMode.Local);
            heads.PreviewMouseLeftButtonDown += Down;
            heads.PreviewMouseMove += Moved;
            heads.PreviewMouseLeftButtonUp += Up;
            heads.LostMouseCapture += Lost;
        }));

    public static bool GetOn(DependencyObject d) => (bool)d.GetValue(OnProperty);
    public static void SetOn(DependencyObject d, bool value) => d.SetValue(OnProperty, value);

    /// <summary>How far sideways the pointer goes before a press is a drag.</summary>
    const double Threshold = 6;

    sealed class Drag(Cells heads, FrameworkElement holder, TableColumns table, ColumnUnit unit, double start)
    {
        public readonly Cells Heads = heads;
        public readonly FrameworkElement Holder = holder;
        public readonly TableColumns Table = table;
        public readonly ColumnUnit Unit = unit;
        public readonly double Start = start;
        public bool Begun;
        public int To = -1;
        public Marker? Marker;
    }

    /// <summary>The press or drag under way; one at a time.</summary>
    static Drag? now;

    static void Down(object sender, MouseButtonEventArgs e)
    {
        var heads = (Cells)sender;
        now = null;
        if (Float.GetOn(heads) || ColumnSet.Find(heads) is not { } found) return;
        var layout = ColumnSet.GetLayout(heads);
        if (layout is null || layout.Table != found.Table.Table) return;
        var x = e.GetPosition(heads).X;
        if (found.Table.Table.UnitOf(heads.ColumnAt(x)) is not { } unit) return;
        now = new Drag(heads, found.Holder, found.Table, unit, x);
    }

    static void Moved(object sender, MouseEventArgs e)
    {
        if (now is not { } d || d.Heads != sender) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            End(d);
            return;
        }
        double x = e.GetPosition(d.Heads).X;
        if (!d.Begun)
        {
            if (Math.Abs(x - d.Start) < Threshold) return;
            // Begun before the capture, which raises a move of its own.
            d.Begun = true;
            if (!d.Heads.CaptureMouse())
            {
                now = null;
                return;
            }
            Mouse.OverrideCursor = Cursors.SizeWE;
            if (AdornerLayer.GetAdornerLayer(d.Holder) is { } layer)
            {
                d.Marker = new Marker(d.Holder);
                layer.Add(d.Marker);
            }
        }
        Aim(d, x);
        e.Handled = true;
    }

    static void Up(object sender, MouseButtonEventArgs e)
    {
        if (now is not { } d || d.Heads != sender) return;
        now = null;
        if (!d.Begun) return;
        e.Handled = true;
        int to = d.To;
        End(d);
        if (to >= 0) d.Table.Move(d.Unit.Key, to);
    }

    static void Lost(object sender, MouseEventArgs e)
    {
        if (now is { Begun: true } d && d.Heads == sender)
        {
            now = null;
            End(d);
        }
    }

    static void End(Drag d)
    {
        if (now == d) now = null;
        if (d.Marker is { } m) AdornerLayer.GetAdornerLayer(d.Holder)?.Remove(m);
        d.Marker = null;
        Mouse.OverrideCursor = null;
        if (d.Heads.IsMouseCaptured) d.Heads.ReleaseMouseCapture();
    }

    /// <summary>Where the column would go with the pointer at <paramref name="x"/>,
    /// and the marks that say so.</summary>
    static void Aim(Drag d, double x)
    {
        var layout = ColumnSet.GetLayout(d.Heads);
        if (layout is null) return;
        var order = layout.Order;
        var others = order.Where(k => k != d.Unit.Key).ToList();

        // The others as they stand and can be seen: a unit hidden or
        // dropped in a narrow pane is no place to aim at.
        double? Left(string key) => Extent(d.Heads, layout.Table.Unit(key)!)?.Left;
        string? before = null;
        double bar = double.NaN;
        foreach (var key in others)
        {
            if (Extent(d.Heads, layout.Table.Unit(key)!) is not { } ext) continue;
            if ((ext.Left + ext.Right) / 2 > x)
            {
                before = key;
                bar = ext.Left;
                break;
            }
            bar = ext.Right;
        }
        d.To = before is null ? others.Count : others.IndexOf(before);
        if (double.IsNaN(bar)) bar = Left(d.Unit.Key) ?? 0;

        if (d.Marker is not { } m || Extent(d.Heads, d.Unit) is not { } own) return;
        var to = d.Heads.TranslatePoint(new Point(0, 0), d.Holder);
        m.Show(to.X + own.Left, to.X + own.Right, to.X + bar, to.X + x, to.Y + d.Heads.ActualHeight / 2, d.Unit.Name);
    }

    /// <summary>The left and right edges of what is seen of <paramref name="unit"/> in the headings; null when none is.</summary>
    static (double Left, double Right)? Extent(Cells heads, ColumnUnit unit)
    {
        double l = double.MaxValue, r = double.MinValue;
        foreach (int c in unit.Columns)
        {
            var (x, wide) = heads.Placed(c);
            if (wide <= 0) continue;
            l = Math.Min(l, x);
            r = Math.Max(r, x + wide);
        }
        return l <= r ? (l, r) : null;
    }

    /// <summary>
    /// What a drag draws over its table: the column being moved dimmed from
    /// top to bottom, the bar where it will go, and its name under the
    /// pointer. In the theme's inks; never hit.
    /// </summary>
    sealed class Marker : Adorner
    {
        double from, until, bar, pointer, middle;
        string name = "";

        public Marker(UIElement table) : base(table)
        {
            IsHitTestVisible = false;
        }

        public void Show(double from, double until, double bar, double pointer, double middle, string name)
        {
            (this.from, this.until, this.bar, this.pointer, this.middle, this.name) = (from, until, bar, pointer, middle, name);
            InvalidateVisual();
        }

        Brush Ink(string key) => AdornedElement is FrameworkElement el && el.TryFindResource(key) is Brush b ? b : Brushes.Gray;

        protected override void OnRender(DrawingContext dc)
        {
            var size = AdornedElement.RenderSize;
            if (until > from)
            {
                dc.PushOpacity(0.6);
                dc.DrawRectangle(Ink("Bg1Brush"), null, new Rect(from, 0, until - from, size.Height));
                dc.Pop();
            }
            dc.DrawRectangle(Ink("AccentBrush"), null, new Rect(Math.Clamp(bar - 1, 0, Math.Max(0, size.Width - 2)), 0, 2, size.Height));

            if (string.IsNullOrEmpty(name)) return;
            var text = new FormattedText(name, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(TextElement.GetFontFamily(AdornedElement), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                11, Ink("Text1Brush"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            double w = text.Width + 16, h = 20;
            var box = new Rect(pointer - w / 2, middle - h / 2, w, h);
            dc.DrawRoundedRectangle(Ink("Bg4Brush"), new Pen(Ink("Line3Brush"), 1), box, 4, 4);
            dc.DrawText(text, new Point(box.X + 8, box.Y + (h - text.Height) / 2));
        }
    }
}
