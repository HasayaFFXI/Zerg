using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zerg.Core.Layout;

namespace Zerg;

/// <summary>
/// One table's columns as the player has them: in what order, and which
/// hidden. A heading dragged to another place (<c>Views/ColumnDrag</c>)
/// moves its column, or its group; the table's Columns menu
/// (<c>Views/ColumnsButton</c>) shows and hides them. settings.json keeps
/// both under the table's key.
///
/// <para>The table draws from <see cref="Layout"/>, given to it once
/// (<c>Views/ColumnSet.Table</c>): a change is a new layout, which every
/// row of the table lays itself out by again. Never a count, never a new
/// row.</para>
///
/// <para><b>A floating panel's columns are its own</b>
/// (<see cref="Floating"/>, kept in settings.json's <c>panelColumns</c>),
/// so a slim panel over the game takes nothing from the main window. Until
/// the player moves or hides one there, a panel's table stands as the main
/// window's does and follows it; once they have, it keeps its own, and its
/// menu's last line puts it back to following. A strip (the Overview and
/// Healing by character, floating) is a table of its own
/// (<see cref="Strip"/>), and so is a table that has columns to choose only
/// while it floats (<see cref="PanelOnly"/>): those two start as installed.</para>
/// </summary>
public sealed partial class TableColumns : ObservableObject
{
    readonly Settings settings;
    /// <summary>Where this one is kept: settings.json's <c>columns</c> or <c>panelColumns</c>.</summary>
    readonly Dictionary<string, ColumnsKept> store;
    /// <summary>Its key there: its table's, or the docked table's for a panel's.</summary>
    readonly string key;
    /// <summary>A panel's twin of the main window's table: the table it stands as until it has its own.</summary>
    readonly TableColumns? follows;
    /// <summary>A twin has columns of its own, which the player set in the panel.</summary>
    bool own;

    /// <summary>A table of the main window, and its panel's twin, which follows it until it is changed.</summary>
    public TableColumns(ColumnTable table, Settings settings)
        : this(table, settings, settings.Columns, table.Key, null)
    {
        Floating = new TableColumns(table, settings, settings.PanelColumns, table.Key, this);
    }

    TableColumns(ColumnTable table, Settings settings, Dictionary<string, ColumnsKept> store, string key, TableColumns? follows)
    {
        Table = table;
        this.settings = settings;
        this.store = store;
        this.key = key;
        this.follows = follows;
        Floating = this;
        store.TryGetValue(key, out var kept);
        own = kept is not null;
        layout = kept is not null ? table.Lay(kept.Order, kept.Hidden)
               : follows is not null ? Mirror()
               : table.Default;
        if (follows is not null) follows.Changed += Follow;
    }

    /// <summary>A table of the main window whose floating form is a strip,
    /// <paramref name="strip"/>, with columns of its own from the start.</summary>
    public static TableColumns Strip(ColumnTable table, ColumnTable strip, Settings settings) =>
        new(table, settings, settings.Columns, table.Key, null)
        {
            Floating = new TableColumns(strip, settings, settings.PanelColumns, table.Key, null),
        };

    /// <summary>A table whose columns are chosen only while it floats; in
    /// the main window it stands as written (<see cref="PanelOnly"/>).</summary>
    public static TableColumns OnlyFloating(ColumnTable table, Settings settings) =>
        new(table, settings, settings.PanelColumns, table.Key, null) { PanelOnly = true };

    public ColumnTable Table { get; }

    /// <summary>The table's columns while it floats: a panel's own. A table
    /// that has columns only while it floats is its own.</summary>
    public TableColumns Floating { get; private init; }

    /// <summary>Its columns are chosen only while it floats: in the main
    /// window the table stands as written, with no menu and no drag.</summary>
    public bool PanelOnly { get; private init; }

    /// <summary>What the table draws.</summary>
    [ObservableProperty] private ColumnLayout layout;

    /// <summary>Something about the columns changed: the Columns menu's lines read themselves again.</summary>
    public event Action? Changed;

    /// <summary>The Columns menu's last line: what it puts the table back to.</summary>
    public string RestoreWords => follows is not null ? "Same as the main window" : "Restore default columns";

    /// <summary>The unit <paramref name="unit"/> was dropped at <paramref name="to"/>, its place among the others.</summary>
    public void Move(string unit, int to)
    {
        var order = Table.Move(Layout.Order, unit, to);
        if (order.SequenceEqual(Layout.Order)) return;
        Set(Table.Lay(order, Layout.Hidden), $"moved {unit}");
    }

    /// <summary>The columns of <paramref name="keys"/> (a group's key for all of
    /// its columns) are shown, or hidden.</summary>
    public void Show(IEnumerable<string> keys, bool shown)
    {
        var hidden = Layout.Hidden.ToHashSet();
        foreach (int c in Table.Columns(keys))
        {
            if (shown) hidden.Remove(Table.All[c].Key);
            else hidden.Add(Table.All[c].Key);
        }
        if (hidden.SetEquals(Layout.Hidden)) return;
        Set(Table.Lay(Layout.Order, hidden), shown ? "shown" : "hidden");
    }

    /// <summary>The table's own order, every column shown; a panel's twin
    /// back to following the main window's table.</summary>
    [RelayCommand]
    void Restore()
    {
        if (follows is not null)
        {
            if (!own) return;
            own = false;
            store.Remove(key);
            Layout = Mirror();
            Saved("follows the main window");
            return;
        }
        if (Layout.IsDefault) return;
        Set(Table.Default, "restored");
    }

    /// <summary>The main window's table as this one's would stand.</summary>
    ColumnLayout Mirror() => Table.Lay(follows!.Layout.Order, follows.Layout.Hidden);

    /// <summary>The main window's table changed: a twin with none of its own changes with it.</summary>
    void Follow()
    {
        if (own) return;
        Layout = Mirror();
        Changed?.Invoke();
    }

    void Set(ColumnLayout now, string why)
    {
        Layout = now;
        // A twin's own is kept even when it is as installed: it no longer follows.
        if (follows is not null) own = true;
        if (now.IsDefault && follows is null) store.Remove(key);
        else store[key] = new ColumnsKept { Order = [.. now.Order], Hidden = [.. now.Hidden] };
        Saved(why);
    }

    void Saved(string why)
    {
        settings.Save();
        var where = ReferenceEquals(store, settings.PanelColumns) ? "panel " : "";
        Log.Write($"columns {where}{key} {why}: [{string.Join(",", Layout.Order)}] hidden [{string.Join(",", Layout.Hidden)}]");
        Changed?.Invoke();
    }

    /// <summary>
    /// The lines of the Columns menu, in the order the units stand: a
    /// column of its own, or a group's heading (its "select all") with a
    /// line for each of its columns under it. Each line keeps up with the
    /// table until it is let go.
    /// </summary>
    public List<ColumnChoice> Choices()
    {
        var lines = new List<ColumnChoice>();
        foreach (var key in Layout.Order)
        {
            var unit = Table.Unit(key)!;
            if (!unit.IsGroup)
            {
                lines.Add(new ColumnChoice(this, unit.Name, [key], ColumnChoice.Kind.Column));
                continue;
            }
            lines.Add(new ColumnChoice(this, unit.Name, [key], ColumnChoice.Kind.Group));
            foreach (int c in unit.Columns)
                lines.Add(new ColumnChoice(this, Table.All[c].Name, [Table.All[c].Key], ColumnChoice.Kind.Member)
                {
                    GroupName = unit.Name,
                });
        }
        return lines;
    }
}

/// <summary>One line of a table's Columns menu (<see cref="TableColumns.Choices"/>).</summary>
public sealed partial class ColumnChoice : ObservableObject, IDisposable
{
    public enum Kind { Column, Group, Member }

    readonly TableColumns table;
    readonly string[] keys;

    internal ColumnChoice(TableColumns table, string name, string[] keys, Kind kind)
    {
        this.table = table;
        this.keys = keys;
        Name = name;
        Is = kind;
        table.Changed += Read;
    }

    public string Name { get; }
    public Kind Is { get; }

    /// <summary>A column under a group: the group's name, which a reader hears with its own.</summary>
    public string? GroupName { get; init; }

    /// <summary>The menu line's name to a reader: "Weaponskill Acc", "Ranged (all)".</summary>
    public string Spoken => Is switch
    {
        Kind.Group => Name + " (all)",
        Kind.Member => GroupName + " " + Name,
        _ => Name,
    };

    /// <summary>Every column of the line is shown. Ticked, they all are; unticked, none is.</summary>
    public bool Shown
    {
        get => table.Table.Columns(keys).All(table.Layout.Shown);
        set => table.Show(keys, value);
    }

    /// <summary>A group of which some columns are shown and some are not.</summary>
    public bool Partial
    {
        get
        {
            var cols = table.Table.Columns(keys).ToList();
            return cols.Any(table.Layout.Shown) && !cols.All(table.Layout.Shown);
        }
    }

    void Read()
    {
        OnPropertyChanged(nameof(Shown));
        OnPropertyChanged(nameof(Partial));
    }

    public void Dispose() => table.Changed -= Read;

    public override string ToString() => Spoken;
}
