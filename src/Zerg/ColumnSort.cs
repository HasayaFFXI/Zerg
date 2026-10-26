using CommunityToolkit.Mvvm.ComponentModel;
using Zerg.Core;

namespace Zerg;

/// <summary>
/// One table's order, as its column headings set it (<c>Views/SortHead</c>)
/// and settings.json keeps it, under the table's key. Whoever draws the
/// table lists its rows through <see cref="Order"/>, and draws it again,
/// from the last count, when it is <see cref="Changed"/>: a new order is a
/// redraw, never a count.
///
/// <para>A floating panel's form of the table is listed in the same order:
/// it is drawn from the same rows.</para>
/// </summary>
public sealed partial class ColumnSort : ObservableObject
{
    readonly Settings settings;

    public ColumnSort(string table, Settings settings)
    {
        Table = table;
        this.settings = settings;
        by = settings.Sorts.TryGetValue(table, out var kept) ? TableOrder.Parse(kept) : null;
    }

    /// <summary>The table's key in settings.json.</summary>
    public string Table { get; }

    /// <summary>The column the table is listed by and which way; null for the table's own order.</summary>
    [ObservableProperty] private TableOrder? by;

    /// <summary>A heading was pressed: the table is to be listed again.</summary>
    public event Action? Changed;

    /// <summary>The heading of <paramref name="column"/> was pressed.</summary>
    /// <param name="words">The column is of words, and goes A to Z first.</param>
    public void Press(string column, bool words)
    {
        By = TableOrder.Press(By, column, words);
        if (By is { } now) settings.Sorts[Table] = now.ToString();
        else settings.Sorts.Remove(Table);
        settings.Save();
        Log.Write("sort " + Table + " " + (By?.ToString() ?? "as listed"));
        Changed?.Invoke();
    }

    /// <summary><paramref name="rows"/>, in the table's own order, listed in this one.</summary>
    /// <param name="keyOf">What a column of the table is ordered by; null for one it does not have.</param>
    public List<T> Order<T>(IEnumerable<T> rows, Func<string, SortKey<T>?> keyOf) => Ordering.Apply(rows, By, keyOf);
}
