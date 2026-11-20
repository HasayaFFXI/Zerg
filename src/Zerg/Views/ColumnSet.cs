using System.Windows;
using System.Windows.Data;
using Zerg.Core.Layout;

namespace Zerg.Views;

/// <summary>
/// A table's columns as the player has them, said once on the element the
/// table stands in (<c>v:ColumnSet.Table="{Binding ActionsColumns}"</c>)
/// and inherited by everything in it as a <see cref="ColumnLayout"/>:
/// every row of the table (<see cref="Cells"/>) stands its cells in the
/// layout's order and collapses those of the columns hidden; the element
/// that says whether the columns fit (<see cref="Shed"/>) needs room only
/// for the columns shown; a heading row that can be dragged
/// (<see cref="ColumnDrag"/>) finds the table to move its columns in.
///
/// <para>A table inside another (a drill-down under an action) sets
/// <c>v:ColumnSet.Layout="{x:Null}"</c> on itself, so that its own rows do
/// not take the outer table's layout.</para>
///
/// <para>In a floating panel (<see cref="Float.On"/>) the table draws from
/// the panel's own columns (<see cref="TableColumns.Floating"/>), and its
/// headings move those; a table whose columns are chosen only while it
/// floats (<see cref="TableColumns.PanelOnly"/>) stands as written in the
/// main window.</para>
/// </summary>
public static class ColumnSet
{
    /// <summary>The table's columns. Not inherited: the element it is set on
    /// binds <see cref="LayoutProperty"/> to them, which is.</summary>
    public static readonly DependencyProperty TableProperty = DependencyProperty.RegisterAttached("Table", typeof(TableColumns),
        typeof(ColumnSet), new PropertyMetadata(null, (d, _) => Bind(d)));

    /// <summary>The element a table is set on came into a panel, or left one.</summary>
    internal static void Refloat(DependencyObject d)
    {
        if (GetTable(d) is not null) Bind(d);
    }

    static void Bind(DependencyObject d)
    {
        var set = GetTable(d);
        if (Effective(d, set) is { } table)
            BindingOperations.SetBinding(d, LayoutProperty,
                new Binding(nameof(TableColumns.Layout)) { Source = table, Mode = BindingMode.OneWay });
        else if (set is not null)
        {
            // Stands as written: said outright, so an outer table's is not taken.
            BindingOperations.ClearBinding(d, LayoutProperty);
            SetLayout(d, null);
        }
        else
            d.ClearValue(LayoutProperty);
    }

    /// <summary>What a table set as <paramref name="set"/> draws from where
    /// <paramref name="d"/> is: a panel's own columns in a panel; none for
    /// a table whose columns are chosen only there, in the main window.</summary>
    static TableColumns? Effective(DependencyObject d, TableColumns? set) =>
        set is null ? null : Float.GetOn(d) ? set.Floating : set.PanelOnly ? null : set;

    /// <summary>What the table draws: inherited by every row of it.</summary>
    public static readonly DependencyProperty LayoutProperty = DependencyProperty.RegisterAttached("Layout", typeof(ColumnLayout),
        typeof(ColumnSet), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, (d, _) =>
        {
            if (d is not FrameworkElement el) return;
            if (Shed.GetFull(el) is not null) Shed.Decide(el);
            if (GetFit(el) is not null) Refit(el);
        }));

    /// <summary>
    /// The table's widths (as <see cref="Cells.Widths"/> is written), on a
    /// table that drops the columns it has no room for rather than squeeze
    /// or scroll: a strip in a floating panel. The columns shown that do
    /// not fit go from the end of the row, the last standing first
    /// (<see cref="ColumnLayout.Overflow"/>), so the player's order says
    /// what stays. <see cref="FitMarginProperty"/> is what the rows keep
    /// beside their columns.
    /// </summary>
    public static readonly DependencyProperty FitProperty = DependencyProperty.RegisterAttached("Fit", typeof(string),
        typeof(ColumnSet), new PropertyMetadata(null, (d, e) =>
        {
            if (d is not FrameworkElement el) return;
            el.SizeChanged -= OnFitSize;
            if (e.NewValue is string) el.SizeChanged += OnFitSize;
            Refit(el);
        }));

    public static readonly DependencyProperty FitMarginProperty = DependencyProperty.RegisterAttached("FitMargin",
        typeof(double), typeof(ColumnSet), new PropertyMetadata(0.0));

    /// <summary>The columns (by place as written) a <see cref="FitProperty"/>
    /// table has no room for: inherited by its rows, which leave them out
    /// as they leave out the hidden ones.</summary>
    public static readonly DependencyProperty DroppedProperty = DependencyProperty.RegisterAttached("Dropped",
        typeof(int[]), typeof(ColumnSet), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    public static string? GetFit(DependencyObject d) => (string?)d.GetValue(FitProperty);
    public static void SetFit(DependencyObject d, string? value) => d.SetValue(FitProperty, value);
    public static double GetFitMargin(DependencyObject d) => (double)d.GetValue(FitMarginProperty);
    public static void SetFitMargin(DependencyObject d, double value) => d.SetValue(FitMarginProperty, value);
    public static int[]? GetDropped(DependencyObject d) => (int[]?)d.GetValue(DroppedProperty);
    public static void SetDropped(DependencyObject d, int[]? value) => d.SetValue(DroppedProperty, value);

    static void OnFitSize(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) Refit((FrameworkElement)sender);
    }

    /// <summary>What the table has no room for now; told to its rows only when that changes.</summary>
    static void Refit(FrameworkElement el)
    {
        int[] over = GetFit(el) is { } widths && GetLayout(el) is { } layout
            ? [.. layout.Overflow(widths, el.ActualWidth - GetFitMargin(el)).Order()]
            : [];
        var now = GetDropped(el) ?? [];
        if (over.SequenceEqual(now)) return;
        if (over.Length == 0) el.ClearValue(DroppedProperty);
        else SetDropped(el, over);
    }

    public static TableColumns? GetTable(DependencyObject d) => (TableColumns?)d.GetValue(TableProperty);
    public static void SetTable(DependencyObject d, TableColumns? value) => d.SetValue(TableProperty, value);
    public static ColumnLayout? GetLayout(DependencyObject d) => (ColumnLayout?)d.GetValue(LayoutProperty);
    public static void SetLayout(DependencyObject d, ColumnLayout? value) => d.SetValue(LayoutProperty, value);

    /// <summary>The table <paramref name="d"/> stands in: the nearest element
    /// up the tree that <see cref="TableProperty"/> is set on, and the columns
    /// it draws from there; null where it stands as written.</summary>
    internal static (FrameworkElement Holder, TableColumns Table)? Find(DependencyObject? d)
    {
        for (; d is not null; d = System.Windows.Media.VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d))
            if (d is FrameworkElement el && GetTable(el) is { } set)
                return Effective(el, set) is { } table ? (el, table) : null;
        return null;
    }
}
