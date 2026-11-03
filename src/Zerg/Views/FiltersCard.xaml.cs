using System.Windows.Controls;

namespace Zerg.Views;

/// <summary>
/// The Damage section's Filters pane: By target and By damage type, each a
/// tab (<see cref="TallyCard"/>), over one parse. Which is shown is the
/// view model's <c>FiltersBy</c>. It can float, tabs and all.
/// </summary>
public partial class FiltersCard : UserControl
{
    public FiltersCard() => InitializeComponent();
}
