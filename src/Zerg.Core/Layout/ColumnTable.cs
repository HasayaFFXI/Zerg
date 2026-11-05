namespace Zerg.Core.Layout;

/// <summary>One column of a table the player can move and hide.</summary>
/// <param name="Key">Its key, as settings.json keeps it and its heading sorts by ("damage").</param>
/// <param name="Name">What the Columns menu calls it ("Damage %"); under a group, its own word ("Acc").</param>
/// <param name="Group">The group it belongs to, by key ("weaponskill"); null for a column of its own.</param>
/// <param name="Fixed">It stays where it is and is always shown: the name a row begins with, a blank
/// column that ends the row, a column the table never shows.</param>
public sealed record TableColumn(string Key, string Name, string? Group = null, bool Fixed = false);

/// <summary>
/// What moves as one when a heading is dragged, and is one line (or one
/// group of lines) of the Columns menu: a column of its own, or a group
/// and every column in it.
/// </summary>
/// <param name="Key">The column's key, or the group's.</param>
/// <param name="Name">The column's name, or the group's.</param>
/// <param name="Columns">The places of its columns in the table as written, in order.</param>
/// <param name="IsGroup">A group, which the menu lists with its columns under it.</param>
public sealed record ColumnUnit(string Key, string Name, IReadOnlyList<int> Columns, bool IsGroup = false);

/// <summary>
/// A table's columns as written (in the XAML, in this order), and what the
/// player may do with them: move a column, or a group of columns as one, to
/// another place among the others; hide any column. The first columns and
/// the last may be fixed: they keep their places and are always shown.
///
/// <para>The player's choice is two lists, as settings.json keeps them: the
/// order of the units (<see cref="Units"/>, by key) and the columns hidden
/// (by key). Neither is trusted: <see cref="Repair"/> makes any list a
/// whole order, and <see cref="Lay"/> what the table draws from it.</para>
/// </summary>
public sealed class ColumnTable
{
    readonly Dictionary<string, int> place = [];
    readonly Dictionary<string, ColumnUnit> units = [];

    /// <param name="key">The table's key in settings.json.</param>
    /// <param name="columns">Every column, in the order the table is written.</param>
    /// <param name="groups">Each group's name, by its key.</param>
    public ColumnTable(string key, IReadOnlyList<TableColumn> columns, IReadOnlyDictionary<string, string>? groups = null)
    {
        Key = key;
        All = columns;
        for (int i = 0; i < columns.Count; i++) place.Add(columns[i].Key, i);

        // The units, in the table's own order. A group's columns stand
        // together where they are written.
        var list = new List<ColumnUnit>();
        for (int i = 0; i < columns.Count; i++)
        {
            var c = columns[i];
            if (c.Fixed) continue;
            if (c.Group is not { } g)
            {
                list.Add(new ColumnUnit(c.Key, c.Name, [i]));
                continue;
            }
            if (list.Count > 0 && list[^1].Key == g) continue;
            var members = new List<int>();
            for (int j = i; j < columns.Count && columns[j].Group == g && !columns[j].Fixed; j++) members.Add(j);
            list.Add(new ColumnUnit(g, groups?.GetValueOrDefault(g) ?? g, members, IsGroup: true));
        }
        Installed = list.Select(u => u.Key).ToArray();
        foreach (var u in list) units.Add(u.Key, u);

        // The units fill the run between the fixed columns at the start and
        // those at the end: nothing fixed stands among them.
        int first = columns.TakeWhile(c => c.Fixed).Count();
        int last = columns.Count - columns.Reverse().TakeWhile(c => c.Fixed).Count();
        if (columns.Skip(first).Take(Math.Max(0, last - first)).Any(c => c.Fixed))
            throw new ArgumentException("A fixed column stands among the movable ones", nameof(columns));
        Lead = first;
        Default = Lay(null, null);
    }

    /// <summary>The table's key in settings.json ("bars").</summary>
    public string Key { get; }

    /// <summary>Every column, in the order the table is written.</summary>
    public IReadOnlyList<TableColumn> All { get; }

    /// <summary>The units, in the table's own order, by key.</summary>
    public IReadOnlyList<string> Installed { get; }

    /// <summary>How many fixed columns the table begins with.</summary>
    public int Lead { get; }

    /// <summary>The table as installed: its own order, nothing hidden.</summary>
    public ColumnLayout Default { get; }

    /// <summary>The unit of this key; null for a key the table has not got.</summary>
    public ColumnUnit? Unit(string key) => units.GetValueOrDefault(key);

    /// <summary>The unit the column at this place (as written) belongs to; null for a fixed column.</summary>
    public ColumnUnit? UnitOf(int column)
    {
        if (column < 0 || column >= All.Count || All[column].Fixed) return null;
        return units.GetValueOrDefault(All[column].Group ?? All[column].Key);
    }

    /// <summary>The place of the column of this key as written; -1 for one the table has not got.</summary>
    public int PlaceOf(string key) => place.GetValueOrDefault(key, -1);

    /// <summary>
    /// <paramref name="order"/> made a whole order of the units: each once,
    /// a key the table has not got (or a column's key inside a group) let
    /// go, and a unit it leaves out put back after the one before it in the
    /// table's own order, so a column a newer build adds turns up where it
    /// is written. Null is the table's own order.
    /// </summary>
    public IReadOnlyList<string> Repair(IEnumerable<string>? order)
    {
        if (order is null) return Installed;
        var kept = new List<string>();
        foreach (var k in order)
            if (k is not null && units.ContainsKey(k) && !kept.Contains(k)) kept.Add(k);
        for (int i = 0; i < Installed.Count; i++)
        {
            var k = Installed[i];
            if (kept.Contains(k)) continue;
            int at = 0;
            for (int j = i - 1; j >= 0; j--)
            {
                int before = kept.IndexOf(Installed[j]);
                if (before >= 0) { at = before + 1; break; }
            }
            kept.Insert(at, k);
        }
        return kept;
    }

    /// <summary>
    /// <paramref name="order"/> with the unit <paramref name="unit"/> taken
    /// out and put back at <paramref name="to"/>: its place among the others
    /// once it is out (0, before them all; their count, after). The order is
    /// repaired first; a key that is not a unit changes nothing.
    /// </summary>
    public IReadOnlyList<string> Move(IEnumerable<string>? order, string unit, int to)
    {
        var list = Repair(order).ToList();
        if (!list.Remove(unit)) return list;
        list.Insert(Math.Clamp(to, 0, list.Count), unit);
        return list;
    }

    /// <summary>The columns of <paramref name="keys"/>: a column's own key, or
    /// a group's for every column in it. Keys the table has not got name none.</summary>
    public IEnumerable<int> Columns(IEnumerable<string> keys)
    {
        foreach (var k in keys)
        {
            if (k is null) continue;
            if (units.TryGetValue(k, out var u) && u.IsGroup)
                foreach (var c in u.Columns) yield return c;
            else if (place.TryGetValue(k, out int c) && !All[c].Fixed) yield return c;
        }
    }

    /// <summary>What the table draws: its columns in <paramref name="order"/>
    /// (repaired), with those <paramref name="hidden"/> names not shown. A
    /// fixed column is always shown, however it is named.</summary>
    public ColumnLayout Lay(IEnumerable<string>? order, IEnumerable<string>? hidden)
    {
        var visual = new List<int>(All.Count);
        for (int i = 0; i < Lead; i++) visual.Add(i);
        foreach (var k in Repair(order)) visual.AddRange(units[k].Columns);
        for (int i = visual.Count; i < All.Count; i++) visual.Add(i);

        var shown = new bool[All.Count];
        Array.Fill(shown, true);
        foreach (var c in Columns(hidden ?? [])) shown[c] = false;
        return new ColumnLayout(this, [.. visual], shown);
    }
}

/// <summary>
/// A table's columns as the player has them: where each column (as written)
/// stands, and whether it is shown. Made by <see cref="ColumnTable.Lay"/>;
/// immutable, so a table is given a new one on every change.
/// </summary>
public sealed class ColumnLayout
{
    readonly int[] visual;
    readonly int[] slot;
    readonly bool[] shown;

    internal ColumnLayout(ColumnTable table, int[] visual, bool[] shown)
    {
        Table = table;
        this.visual = visual;
        this.shown = shown;
        slot = new int[visual.Length];
        for (int i = 0; i < visual.Length; i++) slot[visual[i]] = i;
    }

    public ColumnTable Table { get; }

    /// <summary>How many columns the table is written with.</summary>
    public int Count => visual.Length;

    /// <summary>The column (as written) that stands at <paramref name="position"/>, from the left.</summary>
    public int At(int position) => visual[position];

    /// <summary>Where the column written at <paramref name="column"/> stands, from the left.</summary>
    public int SlotOf(int column) => slot[column];

    /// <summary>The column written at <paramref name="column"/> is shown. A place past the table's columns is.</summary>
    public bool Shown(int column) => column < 0 || column >= shown.Length || shown[column];

    /// <summary>The column of this key is shown; a group is while any column in it is.</summary>
    public bool Shows(string key) => Table.Columns([key]).Any(Shown);

    /// <summary>The keys of the columns hidden, as settings.json keeps them.</summary>
    public IReadOnlyList<string> Hidden =>
        [.. Enumerable.Range(0, shown.Length).Where(c => !shown[c]).Select(c => Table.All[c].Key)];

    /// <summary>The units in the order they stand, by key.</summary>
    public IReadOnlyList<string> Order
    {
        get
        {
            var keys = new List<string>();
            foreach (int c in visual)
                if (Table.UnitOf(c) is { } u && (keys.Count == 0 || keys[^1] != u.Key)) keys.Add(u.Key);
            return keys;
        }
    }

    /// <summary>
    /// The key of the shown column that stands straight after the fixed
    /// columns the table begins with; null when there is none. A row whose
    /// name runs on over the column after it (a target's, over Job) asks.
    /// </summary>
    public string? AfterLead
    {
        get
        {
            for (int p = Table.Lead; p < visual.Length; p++)
                if (shown[visual[p]]) return Table.All[visual[p]].Key;
            return null;
        }
    }

    /// <summary>The same order as installed, and nothing hidden.</summary>
    public bool IsDefault => Order.SequenceEqual(Table.Installed) && shown.All(s => s);

    /// <summary>
    /// The widths <paramref name="widths"/> (written as <see cref="Layout.Columns"/>
    /// reads them, a column for each of the table's) with the hidden columns'
    /// made 0: what the table needs room for. Order does not change it.
    /// </summary>
    public string Needs(string? widths) =>
        Layout.Columns.Zeroed(widths, Enumerable.Range(0, shown.Length).Where(c => !shown[c]));
}
