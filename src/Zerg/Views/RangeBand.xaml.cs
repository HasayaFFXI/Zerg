using System.Windows.Controls;
using System.Windows.Input;

namespace Zerg.Views;

/// <summary>
/// A replay's range, under its tab's band: the two handles' times (typed,
/// or dragged on the strip), what the range holds, the switch that leaves
/// the gaps between pulls out, the pulls to crop to, and Export. While the
/// file is still being read, how far it has got. Its data context is the
/// tab's <see cref="ReplayRange"/>.
/// </summary>
public partial class RangeBand : UserControl
{
    public RangeBand() => InitializeComponent();

    ReplayRange? Range => DataContext as ReplayRange;

    void OnTimeKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        Commit((TextBox)sender);
        e.Handled = true;
    }

    void OnTimeLeft(object sender, KeyboardFocusChangedEventArgs e) => Commit((TextBox)sender);

    /// <summary>A typed time is given to the range; one that names no time
    /// in the file is put back to the handle's.</summary>
    void Commit(TextBox box)
    {
        if (Range is not { } range) return;
        bool from = ReferenceEquals(box, FromBox);
        if (box.Text == (from ? range.FromText : range.ToText)) return;
        range.Type(from, box.Text);
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
    }
}
