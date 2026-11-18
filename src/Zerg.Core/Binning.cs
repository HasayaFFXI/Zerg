namespace Zerg.Core;

/// <summary>
/// How a distribution's hits are cut into columns (the Settings page's
/// Distribution columns). RULES.md has the reasons.
/// </summary>
public enum Binning
{
    /// <summary>Freedman–Diaconis alone, the range cut into equal widths of
    /// any size: Zerg's first rule.</summary>
    Wide,
    /// <summary>At least √n columns, each the same whole number of values wide.</summary>
    Even,
    /// <summary>As Even, with at least a column for every ten hits.</summary>
    Fine,
}

/// <summary>Where a distribution's columns stand.</summary>
/// <param name="Lo">The first column's left edge.</param>
/// <param name="Width">Every column's width.</param>
/// <param name="Count">How many columns.</param>
/// <param name="Whole">The columns hold whole values: [Lo, Lo + Width), so
/// one holding 14 to 24 is [14, 25).</param>
public readonly record struct Cut(double Lo, double Width, int Count, bool Whole)
{
    /// <summary>The column <paramref name="v"/> falls in; the greatest value
    /// is in the last, not past it.</summary>
    public int At(double v) => (int)Math.Max(0, Math.Min(Count - 1, Math.Floor((v - Lo) / Width)));

    public double LoOf(int i) => Lo + i * Width;
    public double HiOf(int i) => Lo + (i + 1) * Width;

    /// <summary>The most columns one histogram has under <paramref name="rule"/>.</summary>
    public static int Most(Binning rule) => rule == Binning.Fine ? 45 : 28;

    /// <summary>The most a pair has, fewer since each holds two columns.</summary>
    public static int MostPaired(Binning rule) => rule == Binning.Fine ? 30 : 20;

    /// <summary>The fewest columns, unless the values are fewer than that.</summary>
    public const int Least = 6;

    /// <summary>
    /// Columns for <paramref name="n"/> values from <paramref name="min"/> to
    /// <paramref name="max"/> (<paramref name="max"/> &gt; <paramref name="min"/>).
    /// <paramref name="width"/> is the Freedman–Diaconis width (0: none).
    /// </summary>
    public static Cut Of(Binning rule, double min, double max, double width, int n, int most)
    {
        if (rule == Binning.Wide)
        {
            var raw = width > 0 ? Math.Ceiling((max - min) / width) : Math.Ceiling(Math.Sqrt(n));
            // A NaN or zero count means "the default", the least.
            var count = (int)Math.Max(Least, Math.Min(most, raw is 0 || double.IsNaN(raw) ? Least : raw));
            return new Cut(min, (max - min) / count, count, false);
        }

        // Freedman–Diaconis assumes one hump. An Attack has several (hits,
        // crits, pDIF bands), its quartiles span the gaps between them and the
        // width comes out wide; √n columns at least keeps a cluster apart from
        // its neighbours.
        double want = Math.Ceiling(Math.Sqrt(n));
        if (width > 0 && double.IsFinite(width)) want = Math.Max(want, Math.Ceiling((max - min) / width));
        if (rule == Binning.Fine) want = Math.Max(want, Math.Ceiling(n / 10.0));
        var asked = (int)Math.Max(Least, Math.Min(most, want));

        // Damage is whole: a column a whole number wide, from a whole edge,
        // holds as many values as its neighbours, so no column is taller for
        // holding one value more.
        double lo = Math.Floor(min), span = Math.Floor(max) - lo + 1;
        var w = Math.Max(1, Math.Ceiling(span / asked));
        return new Cut(lo, w, (int)Math.Ceiling(span / w), true);
    }

    /// <summary>What a column reads in a hover: "14 – 24" for whole values
    /// 14 to 24, "16" for a column of one value, its edges otherwise.</summary>
    public static string Range(double lo, double hi, bool whole) =>
        !whole ? Format.Int(lo) + " " + Dash + " " + Format.Int(hi)
        : hi - lo <= 1 ? Format.Int(lo)
        : Format.Int(lo) + " " + Dash + " " + Format.Int(hi - 1);

    static readonly string Dash = ((char)0x2013).ToString();   // en dash
}
