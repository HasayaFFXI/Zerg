using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Shapes;

namespace Zerg.Views;

/// <summary>
/// A pick button for one side of a pair (Views/PairPicker.xaml): in the
/// live sections' drill-down, which action is set beside the one it was
/// opened on; in Compare, under an opened action, which action of each run.
/// Its data context is a <see cref="PairPick"/>.
///
/// <para><b>A line is chosen, not ticked.</b> Choosing one runs its pick
/// and shuts the menu; the tick is on the side's action as it stands, the
/// next time the menu opens.</para>
/// </summary>
public partial class PairPicker : UserControl
{
    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(PairPicker), new PropertyMetadata(false));

    readonly ContextMenu menu;
    long shutAt = -1000;
    /// <summary>The press now on the button is the one that shut the menu.</summary>
    bool shutByPress;

    public PairPicker()
    {
        InitializeComponent();
        menu = (ContextMenu)FindResource("Menu");
        // Asked of the property, not of Closed, which comes late or never
        // (as for the Target button: Views/PickButton).
        DependencyPropertyDescriptor.FromProperty(ContextMenu.IsOpenProperty, typeof(ContextMenu)).AddValueChanged(menu, (_, _) =>
        {
            IsOpen = menu.IsOpen;
            if (!menu.IsOpen) shutAt = Environment.TickCount64;
        });
        Open.PreviewMouseLeftButtonDown += (_, _) => shutByPress = Environment.TickCount64 - shutAt < 100;
    }

    /// <summary>The menu is open: the button wears its pressed face.</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); private set => SetValue(IsOpenProperty, value); }

    void OnOpen(object sender, RoutedEventArgs e)
    {
        bool shut = shutByPress;
        shutByPress = false;
        if (DataContext is not PairPick pick) return;
        if (menu.IsOpen)
        {
            menu.IsOpen = false;
            return;
        }
        if (shut) return;

        Fill(pick);
        if (menu.Items.Count == 0) return;
        menu.PlacementTarget = Open;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    void Fill(PairPick pick)
    {
        menu.Items.Clear();
        var groups = pick.Groups();
        bool others = false;
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g.Options.Count == 0) continue;
            if (i == 0)
            {
                foreach (var o in g.Options) menu.Items.Add(Line(o));
                continue;
            }
            // The pair's own character is ruled off from everyone else.
            if (!others && menu.Items.Count > 0) menu.Items.Add(new Separator());
            others = true;
            var who = new MenuItem { Header = g.Who, Icon = Swatch(g) };
            AutomationProperties.SetName(who, g.Who);
            foreach (var o in g.Options) who.Items.Add(Line(o));
            menu.Items.Add(who);
        }
    }

    MenuItem Line(PairOption o)
    {
        var name = new TextBlock { Text = o.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var note = new TextBlock
        {
            Text = o.Note, Margin = new Thickness(18, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 11,
        };
        note.SetResourceReference(TextBlock.ForegroundProperty, "Text3Brush");
        Typography.SetNumeralAlignment(note, FontNumeralAlignment.Tabular);
        var header = new DockPanel { MinWidth = 150 };
        DockPanel.SetDock(note, Dock.Right);
        header.Children.Add(note);
        header.Children.Add(name);

        var item = new MenuItem { Header = header, IsChecked = o.Ticked };
        AutomationProperties.SetName(item, o.Name);
        item.Click += (_, _) =>
        {
            menu.IsOpen = false;
            o.Pick();
        };
        return item;
    }

    static FrameworkElement? Swatch(PairGroup g)
    {
        if (g.Swatch == null) return null;
        return new Rectangle { Width = 8, Height = 8, RadiusX = 2, RadiusY = 2, Fill = g.Swatch, HorizontalAlignment = HorizontalAlignment.Center };
    }
}
