using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace Zerg.Views;

/// <summary>
/// A table's Columns button and its menu (<see cref="TableColumns.Choices"/>):
/// made again each time it opens, so its lines stand in the table's order.
/// Each line is a toggle bound to its column (UI Automation toggles it
/// without a click); a group's line is ticked while all of its columns are
/// shown, has a dash while some are, and shows them all, or hides them all.
/// The menu stays open while lines are pressed. Its last line puts the
/// table back as installed.
/// </summary>
public partial class ColumnsButton : UserControl
{
    public static readonly DependencyProperty TableProperty = DependencyProperty.Register(nameof(Table), typeof(TableColumns),
        typeof(ColumnsButton), new PropertyMetadata(null));

    public TableColumns? Table { get => (TableColumns?)GetValue(TableProperty); set => SetValue(TableProperty, value); }

    readonly ContextMenu menu;
    List<ColumnChoice> lines = [];
    long shutAt = -1000;
    /// <summary>The press now on the button is the one that shut its menu.</summary>
    bool shutByPress;

    public ColumnsButton()
    {
        InitializeComponent();
        menu = (ContextMenu)Press.FindResource("Menu");
        // Asked of the property, not of Closed, which comes late or never.
        DependencyPropertyDescriptor.FromProperty(ContextMenu.IsOpenProperty, typeof(ContextMenu)).AddValueChanged(menu, (_, _) =>
        {
            if (menu.IsOpen) return;
            shutAt = Environment.TickCount64;
            Let();
        });
        Press.PreviewMouseLeftButtonDown += (_, _) => shutByPress = Environment.TickCount64 - shutAt < 100;
    }

    /// <summary>The menu, under its button; a press that shut it does not open it again.</summary>
    void OnPress(object sender, RoutedEventArgs e)
    {
        bool shut = shutByPress;
        shutByPress = false;
        if (menu.IsOpen)
        {
            menu.IsOpen = false;
            return;
        }
        if (shut || Table is not { } table) return;
        Fill(table);
        menu.PlacementTarget = Press;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    void Fill(TableColumns table)
    {
        Let();
        menu.Items.Clear();
        lines = table.Choices();
        ColumnChoice.Kind? last = null;
        foreach (var line in lines)
        {
            // A group stands between rules, apart from the lines round it.
            if (last is not null && (line.Is == ColumnChoice.Kind.Group
                                     || (line.Is == ColumnChoice.Kind.Column && last == ColumnChoice.Kind.Member)))
                menu.Items.Add(new Separator());
            var item = new MenuItem { Header = line.Name, IsCheckable = true, StaysOpenOnClick = true, DataContext = line };
            item.SetBinding(MenuItem.IsCheckedProperty, new Binding(nameof(ColumnChoice.Shown)) { Mode = BindingMode.TwoWay });
            AutomationProperties.SetName(item, line.Spoken);
            if (line.Is == ColumnChoice.Kind.Group)
            {
                item.FontWeight = FontWeights.SemiBold;
                item.SetBinding(ColumnsMenu.PartialProperty, new Binding(nameof(ColumnChoice.Partial)));
            }
            if (line.Is == ColumnChoice.Kind.Member) ColumnsMenu.SetIndent(item, true);
            menu.Items.Add(item);
            last = line.Is;
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Restore default columns", Command = table.RestoreCommand });
    }

    /// <summary>The lines let go of the table they keep up with.</summary>
    void Let()
    {
        foreach (var line in lines) line.Dispose();
        lines = [];
    }
}

/// <summary>What a line of a Columns menu wears (its template is <c>Themes/Controls.xaml</c>'s ZMenuItem).</summary>
public static class ColumnsMenu
{
    /// <summary>A group's line, some of whose columns are shown and some not: a dash for its tick.</summary>
    public static readonly DependencyProperty PartialProperty = DependencyProperty.RegisterAttached("Partial", typeof(bool),
        typeof(ColumnsMenu), new PropertyMetadata(false));

    /// <summary>A column's line under its group's, a step in.</summary>
    public static readonly DependencyProperty IndentProperty = DependencyProperty.RegisterAttached("Indent", typeof(bool),
        typeof(ColumnsMenu), new PropertyMetadata(false));

    public static bool GetPartial(DependencyObject d) => (bool)d.GetValue(PartialProperty);
    public static void SetPartial(DependencyObject d, bool value) => d.SetValue(PartialProperty, value);
    public static bool GetIndent(DependencyObject d) => (bool)d.GetValue(IndentProperty);
    public static void SetIndent(DependencyObject d, bool value) => d.SetValue(IndentProperty, value);
}
