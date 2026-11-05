using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Zerg.Core.Layout;

namespace Zerg.Views;

/// <summary>
/// One row of a table: each child is a cell, laid out in the columns that
/// <see cref="Widths"/> names. A heading row and every data row given the same
/// widths line up, with none of the measuring across rows that a shared-size
/// grid does, so a few hundred rows stay cheap.
///
/// <para>How wide each column comes out is worked out in
/// <c>Zerg.Core/Layout/Columns</c>, where it is tested: a number is that
/// many units, <c>*</c> or <c>2*</c> is a share of what is left, and
/// <c>278*150</c> a share that is never under 150.</para>
///
/// <para>A collapsed cell takes its column with it, which is how a table
/// drops a whole column: collapse that cell in the heading and in every row.
/// A column with no cell at all (a row with fewer children than columns)
/// still takes its room: the blank column at the end of a table needs no
/// element to hold it open.</para>
///
/// <para>A cell may lie over several columns (<see cref="SpanProperty"/>):
/// a group's name over its columns, given the table's own widths, stands
/// over them at any width. Collapsed, it takes all of them with it.</para>
///
/// <para>The cells are written in the table's own order. Where the player
/// has moved or hidden columns (<see cref="ColumnSet"/>, inherited), each
/// cell stands where its column stands, and a cell all of whose columns
/// are hidden is collapsed, as a local value: it outranks the style trigger
/// that drops the same cell in a narrow pane, and a binding the cell had
/// is given back when it is shown again. A cell over several columns is as
/// wide as those of them that stand together with its first.</para>
/// </summary>
public sealed class Cells : Panel
{
    /// <summary>Columns, comma-separated: a number is that many units,
    /// <c>*</c> or <c>2*</c> is a share of what is left, and a second
    /// number after the star is the least that share may be.</summary>
    public static readonly DependencyProperty WidthsProperty = DependencyProperty.Register(nameof(Widths), typeof(string),
        typeof(Cells), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure,
            (d, e) =>
            {
                var cells = (Cells)d;
                cells.columns = Columns.Parse((string)e.NewValue);
                cells.Hide();
            }));

    /// <summary>How many columns a cell lies over: 1 unless said.</summary>
    public static readonly DependencyProperty SpanProperty = DependencyProperty.RegisterAttached("Span", typeof(int),
        typeof(Cells), new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsParentMeasure),
        v => (int)v >= 1);

    public static int GetSpan(DependencyObject d) => (int)d.GetValue(SpanProperty);
    public static void SetSpan(DependencyObject d, int value) => d.SetValue(SpanProperty, value);

    /// <summary>What a cell's own Visibility was before its columns were hidden: a binding or a value.</summary>
    static readonly DependencyProperty KeptProperty = DependencyProperty.RegisterAttached("Kept", typeof(object),
        typeof(Cells), new PropertyMetadata(null));

    /// <summary>Marks a cell hidden here, and nothing yet kept of its own.</summary>
    static readonly object Nothing = new();

    Column[] columns = [];

    /// <summary>Where each column (as written) was put last, left edge and width, in this row's units.</summary>
    double[] lefts = [], wides = [];

    public string Widths { get => (string)GetValue(WidthsProperty); set => SetValue(WidthsProperty, value); }

    /// <summary>The player's layout of this table; null where the columns are as written. A layout of
    /// another table (one with a different count of columns) is not this row's.</summary>
    ColumnLayout? Layout => ColumnSet.GetLayout(this) is { } l && l.Count == columns.Length ? l : null;

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property != ColumnSet.LayoutProperty) return;
        Hide();
        InvalidateMeasure();
    }

    protected override void OnVisualChildrenChanged(DependencyObject? added, DependencyObject? removed)
    {
        base.OnVisualChildrenChanged(added, removed);
        Hide();
    }

    /// <summary>Collapses the cells all of whose columns are hidden, and gives
    /// back the others what they had.</summary>
    void Hide()
    {
        var layout = Layout;
        // A row of headings is gone through with Tab in the order its
        // columns stand (ColumnDrag makes it a tab group of its own).
        bool heads = ColumnDrag.GetOn(this);
        for (int i = 0, c = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            int span = GetSpan(child);
            if (heads && c < columns.Length)
                KeyboardNavigation.SetTabIndex(child, layout?.SlotOf(c) ?? c);
            bool hidden = layout is not null && span > 0;
            for (int k = c; k < c + span && hidden; k++) hidden = !layout!.Shown(k);
            c += span;

            var kept = child.ReadLocalValue(KeptProperty);
            bool hiding = kept != DependencyProperty.UnsetValue;
            if (hidden && !hiding)
            {
                object own = child.ReadLocalValue(VisibilityProperty) is BindingExpressionBase b
                    ? b.ParentBindingBase
                    : child.ReadLocalValue(VisibilityProperty);
                child.SetValue(KeptProperty, own == DependencyProperty.UnsetValue ? Nothing : own);
                child.SetValue(VisibilityProperty, Visibility.Collapsed);
            }
            else if (!hidden && hiding)
            {
                child.ClearValue(KeptProperty);
                if (kept is BindingBase binding) BindingOperations.SetBinding(child, VisibilityProperty, binding);
                else if (kept is Visibility v) child.SetValue(VisibilityProperty, v);
                else child.ClearValue(VisibilityProperty);
            }
        }
    }

    /// <summary>The width of each column, in a row this wide.</summary>
    double[] Resolve(double width)
    {
        var w = new double[columns.Length];
        Span<bool> present = columns.Length <= 64 ? stackalloc bool[columns.Length] : new bool[columns.Length];
        present.Fill(true);
        var layout = Layout;
        if (layout is not null)
            for (int c = 0; c < present.Length; c++) present[c] = layout.Shown(c);
        for (int i = 0, c = 0; i < InternalChildren.Count && c < present.Length; i++)
        {
            var child = InternalChildren[i];
            bool there = child.Visibility != Visibility.Collapsed;
            for (int end = Math.Min(present.Length, c + GetSpan(child)); c < end; c++) present[c] &= there;
        }
        Columns.Resolve(columns, present, width, w);
        return w;
    }

    /// <summary>The left edge of each column (as written) once they stand in the layout's order.</summary>
    double[] Lefts(double[] w)
    {
        var x = new double[w.Length];
        var layout = Layout;
        double at = 0;
        for (int p = 0; p < w.Length; p++)
        {
            int c = layout?.At(p) ?? p;
            x[c] = at;
            at += w[c];
        }
        return x;
    }

    /// <summary>Where the cell over <paramref name="span"/> columns from
    /// <paramref name="c"/> stands. Its columns standing side by side (in
    /// whatever order), over all of them; otherwise from its first column's
    /// left edge, as wide as the columns of it that stand straight after.</summary>
    (double X, double Wide) Across(double[] w, double[] x, int c, int span)
    {
        if (c >= w.Length) return (Total(w), 0);
        int end = Math.Min(w.Length, c + span);
        if (end - c > 1 && Layout is { } layout)
        {
            int lo = int.MaxValue, hi = int.MinValue;
            for (int k = c; k < end; k++)
            {
                lo = Math.Min(lo, layout.SlotOf(k));
                hi = Math.Max(hi, layout.SlotOf(k));
            }
            if (hi - lo + 1 == end - c)
            {
                double l = double.MaxValue, r = double.MinValue;
                for (int k = c; k < end; k++)
                {
                    l = Math.Min(l, x[k]);
                    r = Math.Max(r, x[k] + w[k]);
                }
                return (l, r - l);
            }
        }
        double left = x[c], right = left + w[c];
        for (int k = c + 1; k < end; k++)
            if (Math.Abs(x[k] - right) < 0.01) right += w[k];
        return (left, right - left);
    }

    static double Total(double[] w)
    {
        double sum = 0;
        foreach (var v in w) sum += v;
        return sum;
    }

    protected override Size MeasureOverride(Size available)
    {
        var w = Resolve(available.Width);
        var x = Lefts(w);
        double height = 0, width = 0;
        for (int i = 0, c = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            int span = GetSpan(child);
            child.Measure(new Size(Across(w, x, c, span).Wide, available.Height));
            c += span;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        foreach (var column in w) width += column;
        return new Size(double.IsInfinity(available.Width) ? width : available.Width, height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var w = Resolve(final.Width);
        var x = Lefts(w);
        for (int i = 0, c = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            int span = GetSpan(child);
            var (left, wide) = Across(w, x, c, span);
            child.Arrange(new Rect(left, 0, wide, final.Height));
            c += span;
        }
        lefts = x;
        wides = w;
        return final;
    }

    /// <summary>The column (as written) at <paramref name="x"/> in this row, as it was last put; -1 for none.</summary>
    internal int ColumnAt(double x)
    {
        for (int c = 0; c < lefts.Length; c++)
            if (wides[c] > 0 && x >= lefts[c] && x < lefts[c] + wides[c]) return c;
        return -1;
    }

    /// <summary>Where the column (as written) <paramref name="c"/> was last put: its left edge and width.</summary>
    internal (double X, double Wide) Placed(int c) => c >= 0 && c < lefts.Length ? (lefts[c], wides[c]) : (0, 0);

    /// <summary>The cell over the column (as written) <paramref name="c"/>.</summary>
    internal UIElement? CellOver(int c)
    {
        for (int i = 0, at = 0; i < InternalChildren.Count; i++)
        {
            int span = GetSpan(InternalChildren[i]);
            if (c >= at && c < at + span) return InternalChildren[i];
            at += span;
        }
        return null;
    }
}
