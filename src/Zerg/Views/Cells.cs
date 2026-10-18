using System.Windows;
using System.Windows.Controls;
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
/// </summary>
public sealed class Cells : Panel
{
    /// <summary>Columns, comma-separated: a number is that many units,
    /// <c>*</c> or <c>2*</c> is a share of what is left, and a second
    /// number after the star is the least that share may be.</summary>
    public static readonly DependencyProperty WidthsProperty = DependencyProperty.Register(nameof(Widths), typeof(string),
        typeof(Cells), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure,
            (d, e) => ((Cells)d).columns = Columns.Parse((string)e.NewValue)));

    /// <summary>How many columns a cell lies over: 1 unless said.</summary>
    public static readonly DependencyProperty SpanProperty = DependencyProperty.RegisterAttached("Span", typeof(int),
        typeof(Cells), new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsParentMeasure),
        v => (int)v >= 1);

    public static int GetSpan(DependencyObject d) => (int)d.GetValue(SpanProperty);
    public static void SetSpan(DependencyObject d, int value) => d.SetValue(SpanProperty, value);

    Column[] columns = [];

    public string Widths { get => (string)GetValue(WidthsProperty); set => SetValue(WidthsProperty, value); }

    /// <summary>The width of each column, in a row this wide.</summary>
    double[] Resolve(double width)
    {
        var w = new double[columns.Length];
        Span<bool> present = columns.Length <= 64 ? stackalloc bool[columns.Length] : new bool[columns.Length];
        present.Fill(true);
        for (int i = 0, c = 0; i < InternalChildren.Count && c < present.Length; i++)
        {
            var child = InternalChildren[i];
            bool there = child.Visibility != Visibility.Collapsed;
            for (int end = Math.Min(present.Length, c + GetSpan(child)); c < end; c++) present[c] = there;
        }
        Columns.Resolve(columns, present, width, w);
        return w;
    }

    /// <summary>How wide the cell that begins at column <paramref name="c"/> is, and the column after it.</summary>
    static double Across(double[] w, ref int c, UIElement child)
    {
        double wide = 0;
        for (int end = c + GetSpan(child); c < end; c++)
            if (c < w.Length) wide += w[c];
        return wide;
    }

    protected override Size MeasureOverride(Size available)
    {
        var w = Resolve(available.Width);
        double height = 0, width = 0;
        for (int i = 0, c = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            child.Measure(new Size(Across(w, ref c, child), available.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }
        foreach (var column in w) width += column;
        return new Size(double.IsInfinity(available.Width) ? width : available.Width, height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var w = Resolve(final.Width);
        double x = 0;
        for (int i = 0, c = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            double wide = Across(w, ref c, child);
            child.Arrange(new Rect(x, 0, wide, final.Height));
            x += wide;
        }
        return final;
    }
}
