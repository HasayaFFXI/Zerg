using System.Globalization;

namespace Zerg.Core;

/// <summary>
/// How a table is listed once a player has pressed one of its column
/// headings: by that column, one way or the other. No order at all (null)
/// is the table's own, which each table says for itself: the largest first,
/// mostly, and the newest first in a drill-down's list of hits.
///
/// <para><b>A press goes round three ways</b> (<see cref="Press"/>): a column
/// of figures is listed largest first, then smallest first, then in the
/// table's own order again; a column of words A to Z, then Z to A, then
/// the table's own. Pressing another column starts that one at its first.</para>
///
/// <para>A table's order is a setting, kept by the table's key in
/// settings.json as <see cref="ToString"/> writes it ("damage desc").</para>
/// </summary>
/// <param name="Column">The column's key, as the table names it ("damage", "name").</param>
/// <param name="Descending">Largest first (or Z to A).</param>
public readonly record struct TableOrder(string Column, bool Descending)
{
    /// <summary>
    /// The order after the heading of <paramref name="column"/> is pressed
    /// with the table in order <paramref name="now"/>.
    /// </summary>
    /// <param name="words">The column is of words, which go A to Z first:
    /// a name is looked for, where a figure is ranked.</param>
    public static TableOrder? Press(TableOrder? now, string column, bool words)
    {
        bool first = !words;
        if (now is not { } was || was.Column != column) return new TableOrder(column, first);
        return was.Descending == first ? new TableOrder(column, !first) : null;
    }

    /// <summary>As settings.json keeps it: the column and "asc" or "desc".</summary>
    public override string ToString() => Column + (Descending ? " desc" : " asc");

    /// <summary>
    /// What <see cref="ToString"/> wrote, or null for anything else: a
    /// file edited by hand costs that table its order and nothing more.
    /// Whether the table has such a column is the table's to say.
    /// </summary>
    public static TableOrder? Parse(string? text)
    {
        var parts = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return null;
        return parts[1].ToLower(CultureInfo.InvariantCulture) switch
        {
            "desc" => new TableOrder(parts[0], true),
            "asc" => new TableOrder(parts[0], false),
            _ => null,
        };
    }
}

/// <summary>
/// What one column of a table is ordered by: a figure, or words. Null
/// (a figure that is <c>NaN</c>, words that are empty) is a cell with
/// nothing in it, drawn as a dash.
/// </summary>
public sealed class SortKey<T>
{
    SortKey(Func<T, double?>? figure, Func<T, string?>? words) => (Figure, Words) = (figure, words);

    public Func<T, double?>? Figure { get; }
    public Func<T, string?>? Words { get; }

    public static SortKey<T> Of(Func<T, double?> figure) => new(figure, null);
    public static SortKey<T> Of(Func<T, string?> words) => new(null, words);
}

/// <summary>
/// Lists a table's rows in a <see cref="TableOrder"/>.
///
/// <para><b>Rows that come out equal keep the table's own order</b>, so
/// characters with no weaponskills stay in the order of their damage, and a
/// list ordered by the same column twice comes out the same both times.</para>
///
/// <para><b>A dash goes last, whichever way</b>: a mage with no weaponskill
/// accuracy is not the least accurate, and listed first by smallest, the
/// figures asked for would be under a run of dashes.</para>
///
/// <para>Words go as <see cref="Targets.SortedBy"/> has them: without regard
/// to case, then by it.</para>
/// </summary>
public static class Ordering
{
    /// <summary>
    /// <paramref name="rows"/>, which are in the table's own order, in
    /// <paramref name="order"/>: as they are while there is none, or while
    /// <paramref name="keyOf"/> knows no such column (a settings file from
    /// a build whose table had one).
    /// </summary>
    public static List<T> Apply<T>(IEnumerable<T> rows, TableOrder? order, Func<string, SortKey<T>?> keyOf)
    {
        if (order is not { } o || keyOf(o.Column) is not { } key) return [.. rows];
        return key.Figure != null ? By(rows, key.Figure, o.Descending) : By(rows, key.Words!, o.Descending);
    }

    public static List<T> By<T>(IEnumerable<T> rows, Func<T, double?> figure, bool descending)
    {
        var keyed = rows.Select(r => (Row: r, Key: figure(r) is double v && !double.IsNaN(v) ? v : (double?)null)).ToList();
        var with = keyed.Where(x => x.Key != null);
        var sorted = descending ? with.OrderByDescending(x => x.Key!.Value) : with.OrderBy(x => x.Key!.Value);
        return [.. sorted.Select(x => x.Row), .. keyed.Where(x => x.Key == null).Select(x => x.Row)];
    }

    public static List<T> By<T>(IEnumerable<T> rows, Func<T, string?> words, bool descending)
    {
        var keyed = rows.Select(r => (Row: r, Key: words(r) is { Length: > 0 } w ? w : null)).ToList();
        var with = keyed.Where(x => x.Key != null);
        var sorted = descending
            ? with.OrderByDescending(x => x.Key, StringComparer.OrdinalIgnoreCase).ThenByDescending(x => x.Key, StringComparer.Ordinal)
            : with.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Key, StringComparer.Ordinal);
        return [.. sorted.Select(x => x.Row), .. keyed.Where(x => x.Key == null).Select(x => x.Row)];
    }
}
