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
/// </summary>
public static class ColumnSet
{
    /// <summary>The table's columns. Not inherited: the element it is set on
    /// binds <see cref="LayoutProperty"/> to them, which is.</summary>
    public static readonly DependencyProperty TableProperty = DependencyProperty.RegisterAttached("Table", typeof(TableColumns),
        typeof(ColumnSet), new PropertyMetadata(null, (d, e) =>
        {
            if (e.NewValue is TableColumns table)
                BindingOperations.SetBinding(d, LayoutProperty,
                    new Binding(nameof(TableColumns.Layout)) { Source = table, Mode = BindingMode.OneWay });
            else
                BindingOperations.ClearBinding(d, LayoutProperty);
        }));

    /// <summary>What the table draws: inherited by every row of it.</summary>
    public static readonly DependencyProperty LayoutProperty = DependencyProperty.RegisterAttached("Layout", typeof(ColumnLayout),
        typeof(ColumnSet), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, (d, _) =>
        {
            if (d is FrameworkElement el && Shed.GetFull(el) is not null) Shed.Decide(el);
        }));

    public static TableColumns? GetTable(DependencyObject d) => (TableColumns?)d.GetValue(TableProperty);
    public static void SetTable(DependencyObject d, TableColumns? value) => d.SetValue(TableProperty, value);
    public static ColumnLayout? GetLayout(DependencyObject d) => (ColumnLayout?)d.GetValue(LayoutProperty);
    public static void SetLayout(DependencyObject d, ColumnLayout? value) => d.SetValue(LayoutProperty, value);

    /// <summary>The table <paramref name="d"/> stands in: the nearest element
    /// up the tree that <see cref="TableProperty"/> is set on, and its columns.</summary>
    internal static (FrameworkElement Holder, TableColumns Table)? Find(DependencyObject? d)
    {
        for (; d is not null; d = System.Windows.Media.VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d))
            if (d is FrameworkElement el && GetTable(el) is { } table) return (el, table);
        return null;
    }
}
