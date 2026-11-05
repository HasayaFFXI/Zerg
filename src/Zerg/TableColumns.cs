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
/// row. A floating panel's form of the table is the same table.</para>
/// </summary>
public sealed partial class TableColumns : ObservableObject
{
    readonly Settings settings;

    public TableColumns(ColumnTable table, Settings settings)
    {
        Table = table;
        this.settings = settings;
        settings.Columns.TryGetValue(table.Key, out var kept);
        layout = table.Lay(kept?.Order, kept?.Hidden);
    }

    public ColumnTable Table { get; }

    /// <summary>What the table draws.</summary>
    [ObservableProperty] private ColumnLayout layout;

    /// <summary>Something about the columns changed: the Columns menu's lines read themselves again.</summary>
    public event Action? Changed;

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

    /// <summary>The table's own order, every column shown.</summary>
    [RelayCommand]
    void Restore()
    {
        if (Layout.IsDefault) return;
        Set(Table.Default, "restored");
    }

    void Set(ColumnLayout now, string why)
    {
        Layout = now;
        if (now.IsDefault) settings.Columns.Remove(Table.Key);
        else settings.Columns[Table.Key] = new ColumnsKept { Order = [.. now.Order], Hidden = [.. now.Hidden] };
        settings.Save();
        Log.Write($"columns {Table.Key} {why}: [{string.Join(",", now.Order)}] hidden [{string.Join(",", now.Hidden)}]");
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
