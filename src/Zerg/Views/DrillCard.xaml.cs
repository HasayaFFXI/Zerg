using System.Windows;
using System.Windows.Controls;

namespace Zerg.Views;

public partial class DrillCard : UserControl
{
    /// <summary>How wide two actions' chart and figures must have to stand
    /// side by side: the figures' 326 and a chart no narrower than them.</summary>
    const double Beside = 660, FiguresWidth = 326;

    public DrillCard() => InitializeComponent();

    /// <summary>
    /// Two actions' figures beside their chart in a wide pane, under it in a
    /// narrow one. Only the figures' place changes: nothing is made again.
    /// </summary>
    void OnPairSize(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged) return;
        bool beside = e.NewSize.Width >= Beside;
        if (beside == (FiguresColumn.Width.Value > 0)) return;
        FiguresColumn.Width = new GridLength(beside ? FiguresWidth : 0);
        Grid.SetColumn(PairFigures, beside ? 1 : 0);
        Grid.SetRow(PairFigures, beside ? 0 : 1);
        PairFigures.Margin = beside ? new Thickness(0, 10, 14, 4) : new Thickness(12, 8, 14, 4);
    }
}
