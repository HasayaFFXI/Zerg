using Zerg.Core.Charts;

namespace Zerg.Core.Tests;

/// <summary>
/// How a distribution is cut into columns (Cut, Binning): the three rules on
/// made-up values, and on the real parse in Parses/MNK.zerg, where a melee
/// Attack has several humps and Freedman–Diaconis alone lumps them together.
/// </summary>
public class BinningTests
{
    static readonly Snapshot Mnk = Tracker.Of(ParseFile.Import(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Parses", "MNK.zerg")))).Count(new HashSet<string>(), true);

    static Distribution Of(string actor, string action, Binning rule) =>
        Counting.Distribution(Mnk.Events, actor, action, rule);

    static void Even(IReadOnlyList<Bin> bins)
    {
        var w = bins[0].Hi - bins[0].Lo;
        Assert.Equal(Math.Floor(w), w);
        Assert.All(bins, b =>
        {
            Assert.True(b.Whole);
            Assert.Equal(Math.Floor(b.Lo), b.Lo);
            Assert.Equal(w, b.Hi - b.Lo);
        });
    }

    [Fact]
    public void Wide_is_the_first_rule_freedman_diaconis_from_the_least_hit()
    {
        // Derezz's Attack: 467 hits from 3 to 226, Q1 16 and Q3 89, so FD's
        // width is 18.6 and the first column takes everything from 3 to 21.
        var d = Of("Derezz", "Attack", Binning.Wide);
        Assert.Equal((467, 3d, 226d, 12), (d.Count, d.Min, d.Max, d.Bins.Count));
        Assert.Equal((3d, 226d), (d.Bins[0].Lo, d.Bins[^1].Hi));
        Assert.Equal(178, d.Bins[0].Count);
        Assert.All(d.Bins, b => Assert.False(b.Whole));
    }

    [Fact]
    public void Even_keeps_derezzs_lowest_cluster_out_of_one_column()
    {
        var d = Of("Derezz", "Attack", Binning.Even);
        // √467 columns, 11 values each, from 3: the 47 hits of exactly 16
        // are no longer in the column of every hit from 3 up.
        Assert.Equal(21, d.Bins.Count);
        Even(d.Bins);
        Assert.Equal((3d, 14d, 86), (d.Bins[0].Lo, d.Bins[0].Hi, d.Bins[0].Count));
        Assert.Equal((14d, 25d, 97), (d.Bins[1].Lo, d.Bins[1].Hi, d.Bins[1].Count));
        Assert.True(d.Bins.Max(b => b.Count) < 0.25 * d.Count);
        // The last column is a whole width too, so the axis ends past the greatest hit.
        Assert.Equal(234d, d.Bins[^1].Hi);
        Assert.Equal(d.Count, d.Bins.Sum(b => b.Count));
    }

    [Fact]
    public void Fine_gives_a_column_for_every_ten_hits_up_to_its_cap()
    {
        var d = Of("Derezz", "Attack", Binning.Fine);
        // 467 hits want 47 columns; 45 at most, five values each.
        Assert.Equal(45, d.Bins.Count);
        Even(d.Bins);
        Assert.Equal((13d, 18d, 87), (d.Bins[2].Lo, d.Bins[2].Hi, d.Bins[2].Count));
        Assert.Equal((88d, 93d, 43), (d.Bins[17].Lo, d.Bins[17].Hi, d.Bins[17].Count));
        Assert.Equal(d.Count, d.Bins.Sum(b => b.Count));
    }

    [Fact]
    public void Even_columns_hold_as_many_values_each_so_no_sawtooth()
    {
        // Hasaya's Offhand Attack: FD's width is 4.7, so columns held 4 and 5
        // values in turn. Whole widths of 5; the real spike at 48 to 52 stays.
        var wide = Of("Hasaya", "Offhand Attack", Binning.Wide);
        Assert.Equal(4.71, wide.Bins[0].Hi - wide.Bins[0].Lo, 2);

        var d = Of("Hasaya", "Offhand Attack", Binning.Even);
        Assert.Equal(27, d.Bins.Count);
        Even(d.Bins);
        Assert.Equal(5d, d.Bins[0].Hi - d.Bins[0].Lo);
        var top = d.Bins.MaxBy(b => b.Count)!;
        Assert.Equal((48d, 53d, 209), (top.Lo, top.Hi, top.Count));
    }

    [Fact]
    public void Even_never_cuts_finer_than_one_value()
    {
        // Three values: three columns of one, not six of a half.
        var c = Cut.Of(Binning.Even, 100, 102, 0, 40, Cut.Most(Binning.Even));
        Assert.Equal(new Cut(100, 1, 3, true), c);
        Assert.Equal(2, c.At(102));

        // The FD width wins where it asks for more columns than √n.
        var fd = Cut.Of(Binning.Even, 0, 99, 5, 16, Cut.Most(Binning.Even));
        Assert.Equal((0d, 5d, 20), (fd.Lo, fd.Width, fd.Count));
        // And never more than the cap: 28 asked of 1,000 values, 36 wide.
        var capped = Cut.Of(Binning.Even, 0, 999, 1, 50, Cut.Most(Binning.Even));
        Assert.Equal((36d, 28), (capped.Width, capped.Count));
    }

    [Fact]
    public void Fine_wants_a_column_for_every_ten_and_wide_for_every_fd_width()
    {
        Assert.Equal(30, Cut.Of(Binning.Fine, 0, 299, 0, 300, 45).Count);
        Assert.Equal(18, Cut.Of(Binning.Even, 0, 299, 0, 300, 28).Count);   // √300, 17 wide
        var wide = Cut.Of(Binning.Wide, 10, 70, 10, 100, 28);
        Assert.Equal(new Cut(10, 10, 6, false), wide);
        Assert.Equal(5, wide.At(70));
    }

    [Fact]
    public void A_pair_is_cut_by_the_same_rule_to_fewer_columns()
    {
        var a = Of("Derezz", "Attack", Binning.Even);
        var b = Of("Squez", "Attack", Binning.Even);
        // 2 to 226 is 225 values: 20 columns asked, 12 wide, so 19 of them.
        var s = Compare.Shared(a, b);
        Assert.Equal(19, s.Bins.Count);
        Assert.All(s.Bins, x => Assert.True(x.Whole));
        Assert.Equal((2d, 14d), (s.Bins[0].Lo, s.Bins[0].Hi));
        Assert.Equal((a.Count, b.Count), (s.Bins.Sum(x => x.A), s.Bins.Sum(x => x.B)));

        // 30 asked, 8 wide: 29.
        Assert.Equal(29, Compare.Shared(a, b, Binning.Fine).Bins.Count);
        Assert.All(Compare.Shared(a, b, Binning.Wide).Bins, x => Assert.False(x.Whole));
    }

    [Fact]
    public void A_whole_column_reads_its_values_in_a_hover()
    {
        Assert.Equal("14 – 24", Cut.Range(14, 25, whole: true));
        Assert.Equal("16", Cut.Range(16, 17, whole: true));
        Assert.Equal("3 – 22", Cut.Range(3, 21.6, whole: false));

        var d = Of("Derezz", "Attack", Binning.Even);
        var l = HistogramLayout.Compute(438, 170, d.Bins, d.Avg, d.Count);
        Assert.Equal("14 – 24", l.Hover(l.Plot.X + l.Slot * 1.5, l.Plot.Y + 10)!.Head);
    }
}
