using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Zerg.Views;

public partial class BarsCard : UserControl
{
    readonly ContextMenu columns;
    long shutAt = -1000;
    /// <summary>The press now on the Columns button is the one that shut its menu.</summary>
    bool shutByPress;

    public BarsCard()
    {
        InitializeComponent();
        columns = (ContextMenu)ColumnsButton.FindResource("ColumnsMenu");
        // Asked of the property, not of Closed, which comes late or never.
        DependencyPropertyDescriptor.FromProperty(ContextMenu.IsOpenProperty, typeof(ContextMenu)).AddValueChanged(columns, (_, _) =>
        {
            if (!columns.IsOpen) shutAt = Environment.TickCount64;
        });
        ColumnsButton.PreviewMouseLeftButtonDown += (_, _) => shutByPress = Environment.TickCount64 - shutAt < 100;
    }

    /// <summary>The Columns menu, under its button; a press that shut it does not open it again.</summary>
    void OnColumns(object sender, RoutedEventArgs e)
    {
        bool shut = shutByPress;
        shutByPress = false;
        if (columns.IsOpen)
        {
            columns.IsOpen = false;
            return;
        }
        if (shut) return;
        columns.DataContext = ColumnsButton.DataContext;
        columns.PlacementTarget = ColumnsButton;
        columns.Placement = PlacementMode.Bottom;
        columns.IsOpen = true;
    }
}
