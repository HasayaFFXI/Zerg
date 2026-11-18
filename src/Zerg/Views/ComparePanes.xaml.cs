using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Zerg.Views;

/// <summary>
/// The Compare section's seven panes: a <see cref="SplitPanel"/> with its
/// panes written into it, so it stands in the main window's page where the
/// Damage and Healing sections' panels stand, and is arranged, measured and
/// (in a window too short for it) scrolled by the same code as they are.
/// The bands over it are <see cref="CompareSection"/>. Its data context is
/// the <see cref="CompareViewModel"/>.
///
/// <para>A row's menu is made when it is asked for, from what the row is
/// then: Overview's "Match with…", "Unmatch", "Show in Actions" and
/// "Isolate"; an action's "Compare with…", run → character → action.</para>
/// </summary>
public partial class ComparePanes : SplitPanel
{
    public ComparePanes() => InitializeComponent();

    CompareViewModel? Model => DataContext as CompareViewModel;

    void OnBarMenu(object sender, ContextMenuEventArgs e)
    {
        e.Handled = true;
        if (sender is not FrameworkElement { DataContext: CompareRow row } at || Model is not { } m) return;
        if (!row.IsLine) return;
        var menu = Menu();
        if (m.MatchesFor(row) is { Count: > 0 } others)
        {
            var match = Item("Match with…");
            foreach (var other in others)
            {
                string with = other;
                match.Items.Add(Item(m.NameOf(with), () => m.Match(row, with)));
            }
            menu.Items.Add(match);
        }
        if (m.IsMatched(row)) menu.Items.Add(Item("Unmatch", () => m.Unmatch(row)));
        if (menu.Items.Count > 0) menu.Items.Add(new Separator());
        if (m.IsDamage) menu.Items.Add(Item("Show in Actions", () => m.ShowInActions(row)));
        menu.Items.Add(Item("Isolate", () => m.Isolate(row)));
        Open(menu, at);
    }

    void OnActionMenu(object sender, ContextMenuEventArgs e)
    {
        e.Handled = true;
        if (sender is not FrameworkElement { DataContext: CompareRow row } at || Model is not { } m) return;
        if (!row.IsLine || row.Owner.Length == 0) return;
        var with = Item("Compare with…");
        foreach (var runB in new[] { false, true })
        {
            var run = Item(m.RunName(runB));
            foreach (var (b, who, actions) in m.PairChoices())
            {
                if (b != runB || actions.Count == 0) continue;
                var character = Item(m.NameOf(who));
                foreach (var action in actions)
                {
                    bool r = b;
                    string w = who, a = action;
                    character.Items.Add(Item(a, () => m.CompareWith(row, r, w, a)));
                }
                run.Items.Add(character);
            }
            if (run.Items.Count > 0) with.Items.Add(run);
        }
        if (with.Items.Count == 0) return;
        var menu = Menu();
        menu.Items.Add(with);
        Open(menu, at);
    }

    static ContextMenu Menu() => new();

    static MenuItem Item(string header, Action? act = null)
    {
        var item = new MenuItem { Header = header };
        if (act != null) item.Click += (_, _) => act();
        return item;
    }

    static void Open(ContextMenu menu, FrameworkElement at)
    {
        menu.PlacementTarget = at;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }
}
