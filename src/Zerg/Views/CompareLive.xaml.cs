using System.Windows.Controls;

namespace Zerg.Views;

/// <summary>
/// The live compare panel: the live run's rates so far against the other
/// run's final ones. Only ever in a floating panel
/// (<see cref="PanelSet.CompareLive"/>). Its data context is the
/// <see cref="CompareViewModel"/>.
/// </summary>
public partial class CompareLive : UserControl
{
    public CompareLive() => InitializeComponent();
}
