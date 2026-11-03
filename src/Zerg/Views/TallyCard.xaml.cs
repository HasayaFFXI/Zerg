using System.Windows;
using System.Windows.Controls;

namespace Zerg.Views;

/// <summary>
/// One of the Damage section's two side tables, a tab of its Filters pane
/// (<see cref="FiltersCard"/>): By target, or By damage type. What each
/// target took, or what each type of damage came to, over one parse,
/// largest first; every row a button that isolates what it names, exactly
/// as its line of the Target or Type button's menu does, because it is the
/// same <see cref="PickFilter"/> that is told.
///
/// <para>One card serves both. Which it is, is the filter it is given
/// (<see cref="Filter"/>): the word over its first column and its rows are
/// the filter's. The card's data context stays the view model, as every
/// card's does, for the shading setting and for what an empty table says.
/// Nothing in it moves with the clock.</para>
/// </summary>
public partial class TallyCard : UserControl
{
    public static readonly DependencyProperty FilterProperty = DependencyProperty.Register(nameof(Filter), typeof(PickFilter),
        typeof(TallyCard), new PropertyMetadata(null));

    /// <summary>The table has Acc, WS Avg and WS % columns (by target, and
    /// not by type): inherited, for the cells' style triggers, which
    /// collapse them otherwise, as they do when the room is too narrow.</summary>
    public static readonly DependencyProperty FiguresProperty = DependencyProperty.RegisterAttached("Figures", typeof(bool),
        typeof(TallyCard), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetFigures(DependencyObject d) => (bool)d.GetValue(FiguresProperty);
    public static void SetFigures(DependencyObject d, bool value) => d.SetValue(FiguresProperty, value);

    public TallyCard() => InitializeComponent();

    /// <summary>What the table lists and what its rows tell: the view
    /// model's filter by target, or its filter by damage type.</summary>
    public PickFilter? Filter { get => (PickFilter?)GetValue(FilterProperty); set => SetValue(FilterProperty, value); }
}
