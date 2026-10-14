using System.Windows.Media;

namespace Zerg;

/// <summary>
/// Every colour Zerg paints with as a frozen brush, made once and handed to
/// whatever asks: the rows, the marks, the charts and the swatches all ask
/// here. A colour at a strength is the colour with that opacity, in whole
/// percent.
///
/// <para>Bounded: a player can try any number of colours in the picker,
/// and every one would otherwise stay for the life of the process. Past
/// <see cref="Most"/> the cache starts again; a brush already handed out is
/// still held by whatever draws with it.</para>
/// </summary>
static class Inks
{
    /// <summary>How many brushes are kept before the cache starts again:
    /// far more than any count asks for (eighteen characters, three
    /// strengths each, both themes, and the runs).</summary>
    const int Most = 1024;

    static readonly Dictionary<(Color Colour, int Percent), SolidColorBrush> Made = [];

    /// <summary>The colour, solid.</summary>
    public static SolidColorBrush Solid(Color c) => At(c, 100);

    /// <summary>The colour at <paramref name="percent"/> strength, 0 to 100.</summary>
    public static SolidColorBrush At(Color c, int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        if (Made.TryGetValue((c, percent), out var b)) return b;
        if (Made.Count >= Most) Made.Clear();
        b = new SolidColorBrush(c);
        if (percent < 100) b.Opacity = percent / 100.0;
        b.Freeze();
        return Made[(c, percent)] = b;
    }
}
