namespace Zerg.Core;

/// <summary>
/// Sorts in place by a figure, keeping equal elements in the order they
/// were in: a list of characters with the same total stays in the order they
/// first acted, every time it is counted. <see cref="List{T}.Sort()"/> is not
/// stable, and <see cref="Enumerable.OrderBy{TSource, TKey}(IEnumerable{TSource}, Func{TSource, TKey})"/> is.
/// </summary>
internal static class Sorting
{
    public static void SortBy<T>(this List<T> list, Func<T, double> key) => Put(list, list.OrderBy(key));

    public static void SortByDescending<T>(this List<T> list, Func<T, double> key) =>
        Put(list, list.OrderByDescending(key));

    static void Put<T>(List<T> list, IEnumerable<T> sorted)
    {
        if (list.Count < 2) return;
        var copy = sorted.ToArray();
        for (int i = 0; i < copy.Length; i++) list[i] = copy[i];
    }
}
