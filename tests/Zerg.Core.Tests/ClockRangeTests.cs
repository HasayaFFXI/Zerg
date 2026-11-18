using Zerg.Core.Charts;

namespace Zerg.Core.Tests;

/// <summary>A range picked on a Timeline: what it cuts, and its clock.</summary>
public class ClockRangeTests
{
    static readonly HashSet<string> Nobody = [];

    /// <summary>Started at 1,000 s: Hasaya at 0, 5, 10, 15 and 20 s of the
    /// session clock, Tank at 7, a cure at 3 and at 12.</summary>
    static Tracker Fought()
    {
        var t = new Tracker();
        t.Follow("Hasaya_2026.07.30.jsonl");
        t.Start(1_000_000);
        t.Feed([
            Lines.Hit(1000, "Hasaya", 100),
            Lines.Heal(1003, "Kanaria", 40),
            Lines.Hit(1005, "Hasaya", 100),
            Lines.Hit(1007, "Tank", 50),
            Lines.Hit(1010, "Hasaya", 100),
            Lines.Heal(1012, "Kanaria", 60),
            Lines.Hit(1015, "Hasaya", 100),
            Lines.Hit(1020, "Hasaya", 100),
        ]);
        return t;
    }

    [Fact]
    public void Dragged_either_way_round_it_is_one_range()
    {
        Assert.Equal(new ClockRange(4000, 16000), ClockRange.Of(16000, 4000));
        Assert.True(new ClockRange(4000, 16000).Holds(16000));
        Assert.False(new ClockRange(4000, 16000).Holds(16001));
    }

    [Fact]
    public void A_range_counts_only_its_rows_over_its_first_row_to_its_last()
    {
        var t = Fought();
        var c = t.Count(Nobody, true, 1_030_000, range: new ClockRange(4000, 16000));
        // 5, 7, 10 and 15 s: 350 over the ten seconds from 5 to 15.
        Assert.Equal(350, c.Totals.Total);
        Assert.Equal(10_000, c.Elapsed);
        Assert.Equal(new ClockRange(5000, 15000), c.DamageRange);
        Assert.Equal(35, c.Rate(c.Totals.Total, c.Elapsed / 1000));
        // The rows keep their times on the session clock.
        Assert.Equal([5000, 7000, 10000, 15000], c.Events.Select(e => e.T));
        // The Timeline stays whole, and ends at the session's clock.
        Assert.Equal(6, c.Timeline.Count);
        Assert.Equal(30_000, c.Clock);
    }

    [Fact]
    public void With_no_range_nothing_changes()
    {
        var c = Fought().Count(Nobody, true, 1_030_000);
        Assert.Equal(550, c.Totals.Total);
        Assert.Equal(30_000, c.Elapsed);
        Assert.Equal(30_000, c.HealElapsed);
        Assert.Same(c.Events, c.Timeline);
        Assert.Null(c.DamageRange);
    }

    [Fact]
    public void A_range_no_row_falls_in_counts_nothing()
    {
        var c = Fought().Count(Nobody, true, 1_030_000, range: new ClockRange(21_000, 29_000));
        Assert.Equal(0, c.Totals.Total);
        Assert.Equal(0, c.Elapsed);
        Assert.Null(c.DamageRange);
        Assert.Equal(6, c.Timeline.Count);
    }

    [Fact]
    public void Each_section_s_range_moves_only_its_own_figures()
    {
        var t = Fought();
        // Damage's range: the healing is the session's.
        var d = t.Count(Nobody, true, 1_030_000, range: new ClockRange(4000, 16000));
        Assert.Equal(100, d.Healing.Total);
        Assert.Equal(30_000, d.HealElapsed);

        // Healing's: 2 to 11 s is measured from the hit at 5 to the one at
        // 10, so the cure at 3 is before it; the damage is the session's.
        var h = t.Count(Nobody, true, 1_030_000, healRange: new ClockRange(2000, 13000));
        Assert.Equal(550, h.Totals.Total);
        Assert.Equal(30_000, h.Elapsed);
        Assert.Equal(new ClockRange(5000, 10000), h.HealRange);
        Assert.Equal(5000, h.HealElapsed);
        Assert.Equal(0, h.Healing.Total);
        Assert.Equal(2, h.HealTimeline.Count);

        // 0 to 13 s ends at the hit at 10: the cure at 3 is in it, the one
        // at 12 after its last row, as a heal after a session's end is.
        var wide = t.Count(Nobody, true, 1_030_000, healRange: new ClockRange(0, 13000));
        Assert.Equal(40, wide.Healing.Total);
        Assert.Equal(10_000, wide.HealElapsed);
    }

    [Fact]
    public void A_range_is_measured_over_everyone_whoever_is_isolated()
    {
        // Tank isolated: the range still runs from Hasaya's row at 5 s to 15 s.
        var c = Fought().Count(new HashSet<string> { "Tank" }, true, 1_030_000, range: new ClockRange(4000, 16000));
        Assert.Equal(50, c.Totals.Total);
        Assert.Equal(10_000, c.Elapsed);
        Assert.Single(c.Timeline);
    }

    [Fact]
    public void A_range_exported_while_the_session_runs_opens_as_the_range_read()
    {
        var t = Fought();
        var c = t.Count(Nobody, true, 1_030_000, range: new ClockRange(4000, 16000));
        var parse = ParseFile.Import(t.ExportRange(new ClockRange(4000, 16000), true, 1_030_000)!.Value.Text);

        // From the hit at 5 s to the one at 15: the cure at 12 goes too.
        Assert.Equal((1_005_000d, 1_015_000d), (parse.Session.StartedAt, parse.Session.PausedAt));
        Assert.Equal([100d, 50d, 100d, 100d], parse.Source.Events.Select(e => e.Dmg));
        Assert.Equal([60d], parse.Source.Heals.Select(h => h.Hp));

        var opened = Tracker.Of(parse).Count(Nobody, true);
        Assert.Equal(c.Totals.Total, opened.Totals.Total);
        Assert.Equal(c.Elapsed, opened.Elapsed);
    }

    [Fact]
    public void A_range_exported_keeps_the_pauses_inside_it()
    {
        var t = Fought();
        t.Count(Nobody, true, 1_008_000);
        t.TogglePause(1_008_000);
        t.TogglePause(1_009_000);
        // The second's pause puts the hit at 10 s on the clock at 9, and 15 at 14.
        var c = t.Count(Nobody, true, 1_030_000, range: new ClockRange(4000, 14000));
        Assert.Equal(9_000, c.Elapsed);

        var parse = ParseFile.Import(t.ExportRange(new ClockRange(4000, 14000), true, 1_030_000)!.Value.Text);
        Assert.Equal([new PauseSpan(1_008_000, 1_009_000)], parse.Session.Spans);
        var opened = Tracker.Of(parse).Count(Nobody, true);
        Assert.Equal(350, opened.Totals.Total);
        Assert.Equal(9_000, opened.Elapsed);
    }

    [Fact]
    public void A_range_with_no_row_in_it_exports_nothing() =>
        Assert.Null(Fought().ExportRange(new ClockRange(21_000, 29_000), true, 1_030_000));

    [Fact]
    public void The_band_s_bars_start_at_the_range()
    {
        var c = Fought().Count(Nobody, true, 1_030_000, range: new ClockRange(4000, 16000));
        var p = BandMarks.Pulse(c.Events, c.Elapsed, c.DamageRange!.From)!;
        Assert.Equal(350, p.Bars.Sum());
        Assert.Single(p.Bars);
        Assert.Equal(35, p.Running[0]);
    }
}
