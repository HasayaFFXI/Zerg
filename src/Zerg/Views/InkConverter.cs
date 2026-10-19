using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Zerg.Views;

/// <summary>
/// The letter's ink on a badge filled with the bound brush: a side's tag in
/// a live drill-down, whose colour is a character's and not a run's
/// (<see cref="AppTheme.InkOn"/>). "Panel" as the parameter takes the dark
/// set whatever the theme, as a floating panel does.
/// </summary>
public sealed class InkConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is SolidColorBrush b ? Inks.Solid(AppTheme.InkOn(b.Color, parameter is "Panel" || AppTheme.IsDark)) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
