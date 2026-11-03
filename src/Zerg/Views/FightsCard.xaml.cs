using System.Windows.Controls;

namespace Zerg.Views;

/// <summary>
/// The Damage section's Fights pane: every kill of the session, newest
/// first, with the party's deaths between them (<c>MainViewModel.Fights</c>).
/// A kill is pressed to isolate that one fight. It can float, as a table.
/// </summary>
public partial class FightsCard : UserControl
{
    public FightsCard() => InitializeComponent();
}
