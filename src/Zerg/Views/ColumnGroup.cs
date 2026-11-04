using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Zerg.Core.Layout;

namespace Zerg.Views;

/// <summary>
/// A column group the player has hidden from the Overview's Columns
/// menu. The table says once which groups are hidden
/// (<see cref="HiddenProperty"/>, inherited: "ranged,pet"), and each cell
/// of a group says which it belongs to (<see cref="OfProperty"/>); a cell
/// whose group is hidden is collapsed, and a collapsed cell takes its
/// column with it (<see cref="Cells"/>).
///
/// <para>Collapsed as a local value, which outranks the style trigger that
/// sheds the same cell in a narrow pane; shown again, the local value is
/// cleared and the trigger has it back. So a cell must have no
/// <c>Visibility</c> of its own.</para>
/// </summary>
public static class ColumnGroup
{
    /// <summary>The group this cell belongs to: a key of <see cref="CharacterColumns.Groups"/>.</summary>
    public static readonly DependencyProperty OfProperty = DependencyProperty.RegisterAttached("Of", typeof(string),
        typeof(ColumnGroup), new PropertyMetadata(null, Apply));

    /// <summary>The groups hidden, comma-separated.</summary>
    public static readonly DependencyProperty HiddenProperty = DependencyProperty.RegisterAttached("Hidden", typeof(string),
        typeof(ColumnGroup), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, Apply));

    public static string? GetOf(DependencyObject d) => (string?)d.GetValue(OfProperty);
    public static void SetOf(DependencyObject d, string? value) => d.SetValue(OfProperty, value);
    public static string? GetHidden(DependencyObject d) => (string?)d.GetValue(HiddenProperty);
    public static void SetHidden(DependencyObject d, string? value) => d.SetValue(HiddenProperty, value);

    static void Apply(DependencyObject d, DependencyPropertyChangedEventArgs _)
    {
        if (d is not UIElement cell || GetOf(cell) is not { } group) return;
        if (Groups(GetHidden(cell)).Contains(group))
            cell.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        else if (cell.ReadLocalValue(UIElement.VisibilityProperty) != DependencyProperty.UnsetValue)
            cell.ClearValue(UIElement.VisibilityProperty);
    }

    internal static string[] Groups(string? hidden) =>
        (hidden ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>
/// The columns the Overview table needs room for (<see cref="Shed.FullProperty"/>,
/// or with "Less" as the parameter <see cref="Shed.LessProperty"/>), from
/// whether its rows are shaded and the groups hidden:
/// <see cref="CharacterColumns"/>.
/// </summary>
public sealed class CharacterRoom : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        bool shaded = values.Length < 1 || values[0] is not false;
        var hidden = ColumnGroup.Groups(values.Length > 1 ? values[1] as string : null);
        return parameter as string == "Less" ? CharacterColumns.Less(shaded, hidden) : CharacterColumns.Full(shaded, hidden);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
