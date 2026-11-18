using System.Globalization;
using Zerg.Core.Layout;

namespace Zerg.Core.Tests;

/// <summary>What the Compare section prints for a pair of runs.</summary>
public class CompareTests
{
    static readonly HashSet<string> Nobody = [];

    /// <summary>
    /// A run made the way a live side is taken: lines fed to a tracker armed
    /// at 0, its clock frozen at <paramref name="end"/> seconds, and the lot
    /// read back through the export format.
    /// </summary>
    static ImportedParse Run(double end, params string[] lines)
    {
        var t = new Tracker();
        t.Follow("Hasaya_2026.07.30.jsonl");
        t.Start(0);
        t.Feed(lines);
        t.Count(Nobody, true, end * 1000);
        return ParseFile.Import(CompareSheet.Snapshot(t.Reader, t.Session, t.File, end * 1000)!);
    }

    static CompareSheet Sheet(ImportedParse? a, ImportedParse? b, CompareOptions? o = null) => CompareSheet.Of(a, b, o);

    static string Death(double t, string who) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{{\"kind\":\"death\",\"t\":{t * 1000},\"seq\":1,\"target\":\"{who}\",\"targetKind\":\"player\",\"msg\":97}}");

    static string Kill(double t, string target, double id) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{{\"kind\":\"death\",\"t\":{t * 1000},\"seq\":1,\"actor\":\"Hasaya\",\"actorKind\":\"player\",\"target\":\"{target}\",\"targetKind\":\"mob\",\"targetId\":{id},\"msg\":6}}");

    static string Swing(double t, string actor, double dmg, string target, double id) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{{\"t\":{t * 1000},\"seq\":1,\"use\":{(int)(t * 10)},\"kind\":\"melee\",\"actor\":\"{actor}\",\"actorKind\":\"player\",\"action\":\"Attack\",\"actionId\":0,\"target\":\"{target}\",\"targetKind\":\"mob\",\"targetId\":{id},\"dmg\":{dmg},\"hit\":true,\"crit\":false,\"burst\":false,\"msg\":1}}");

    /// <summary>A printed signed figure read back: "−10.0" is −10.</summary>
    static double Read(string text) =>
        double.Parse(text.Replace("−", "-").Replace("±", "").Replace("+", "").Replace(",", ""), CultureInfo.InvariantCulture);

    // ----------------------------------------------------------- a change

    [Fact]
    public void A_change_carries_its_sign_in_the_text()
    {
        Assert.Equal(new Change("+50.0%", "+50", ChangeTone.Better), Change.Of(100, 150, ChangeKind.Percent, Format.Int, 1));
        Assert.Equal(new Change("−25.0%", "−50", ChangeTone.Worse), Change.Of(200, 150, ChangeKind.Percent, Format.Int, 1));
        Assert.Equal(new Change("±0.0%", "±0", ChangeTone.Flat), Change.Of(5, 5, ChangeKind.Percent, Format.Int, 1));
    }

    [Fact]
    public void Less_can_be_the_better_way()
    {
        Assert.Equal(ChangeTone.Better, Change.Of(200, 150, ChangeKind.Percent, Format.Int, -1).Tone);
        Assert.Equal(ChangeTone.Worse, Change.Of(100, 150, ChangeKind.Percent, Format.Int, -1).Tone);
    }

    [Fact]
    public void A_change_that_prints_as_zero_is_grey_whatever_its_sign()
    {
        Assert.Equal(new Change("±0.0%", "±0.0", ChangeTone.Flat),
                     Change.Of(2606.50, 2606.52, ChangeKind.Percent, n => Format.Num(n, 1), 1));
        Assert.Equal(new Change("±0.0%", "+2", ChangeTone.Flat), Change.Of(2_606_500, 2_606_502, ChangeKind.Percent, Format.Int, 1));
        Assert.Equal(new Change("±0.0 pt", "", ChangeTone.Flat), Change.Of(0.9, 0.9004, ChangeKind.Points, Format.Int, 1));
        Assert.Equal(new Change("+0.1 pt", "", ChangeTone.Better), Change.Of(0.9, 0.9006, ChangeKind.Points, Format.Int, 1));
        Assert.Equal(new Change("±0.0s", "", ChangeTone.Flat), Change.Of(60, 60.04, ChangeKind.Time, Format.Int, 1));
        Assert.Equal(new Change("±0.000", "", ChangeTone.Flat), Change.Of(3.1001, 3.1003, ChangeKind.Plain, CompareSheet.Seconds, -1));
        Assert.Equal(new Change("new", "+5", ChangeTone.Better), Change.Of(0, 5, ChangeKind.Percent, Format.Int, 1));
        Assert.Equal(new Change("±0", "±0", ChangeTone.Flat), Change.Of(0, 0, ChangeKind.Percent, Format.Int, 1));
    }

    [Fact]
    public void A_plain_change_is_spelled_as_its_figure()
    {
        // Delay: less is better, a plain number of seconds.
        Assert.Equal(new Change("−0.425", "", ChangeTone.Better), Change.Of(3.100, 2.675, ChangeKind.Plain, CompareSheet.Seconds, -1));
        Assert.Equal(new Change("+0.425", "", ChangeTone.Worse), Change.Of(2.675, 3.100, ChangeKind.Plain, CompareSheet.Seconds, -1));
    }

    [Fact]
    public void With_a_side_missing_there_is_no_change_to_state()
    {
        Assert.Equal(Change.Nothing, Change.Of(null, 5, ChangeKind.Percent, Format.Int, 1));
        Assert.Equal(Change.Nothing, Change.Of(5, null, ChangeKind.Points, Format.Int, 1));
        Assert.Equal(ChangeTone.None, Change.Nothing.Tone);
    }

    [Theory]
    // Totals, averages, rates and shares: more is better.
    [InlineData("cbars", "damage", 1)]
    [InlineData("cbars", "dps", 1)]
    [InlineData("cbars", "accuracy", 1)]
    [InlineData("cbars", "crit", 1)]
    [InlineData("cbars", "wsDamage", 1)]
    [InlineData("cbars", "wsAvg", 1)]
    [InlineData("cbars", "scDamage", 1)]
    [InlineData("cbars", "petDamage", 1)]
    // Delay and deaths: fewer is better.
    [InlineData("cbars", "delay", -1)]
    [InlineData("cbars", "deaths", -1)]
    [InlineData("cactions", "total", 1)]
    [InlineData("cactions", "avg", 1)]
    [InlineData("cactions", "accuracy", 1)]
    // Counts and one hit: never coloured.
    [InlineData("cactions", "hits", 0)]
    [InlineData("cactions", "max", 0)]
    [InlineData("cfights", "kills", 0)]
    [InlineData("cfights", "dps", 1)]
    [InlineData("cfights", "killTime", -1)]
    // Healing amounts are never coloured; deaths are.
    [InlineData("chbars", "healing", 0)]
    [InlineData("chbars", "hps", 0)]
    [InlineData("chbars", "casts", 0)]
    [InlineData("chbars", "avg", 0)]
    [InlineData("chbars", "deaths", -1)]
    [InlineData("cheals", "total", 0)]
    [InlineData("cfilterTargets", "damage", 1)]
    public void Each_column_is_coloured_by_its_own_direction(string table, string column, int better)
    {
        Assert.Equal(better, CompareSheet.Better(table, column));
    }

    // ---------------------------------------------------------------- band

    [Fact]
    public void The_band_reads_B_against_As_whole_run()
    {
        // A: 300 damage over 20 s. B: 600 over 40 s, a weaponskill, a miss and a death.
        var a = Run(30, Lines.Hit(10, "Hasaya", 100), Lines.Hit(20, "Hasaya", 200));
        var b = Run(50, Lines.Hit(10, "Hasaya", 100), Lines.Hit(20, "Hasaya", 0, hit: false),
                        Lines.Hit(30, "Hasaya", 500, kind: "ws", action: "Tachi: Gekko"), Death(31, "Hasaya"));
        var band = Sheet(a, b).DamageBand;

        Assert.Equal(["Length", "Total damage", "Party DPS", "Accuracy", "Crit %", "WS avg", "Kills", "Deaths"],
                     band.Select(c => c.Label));
        var cells = band.ToDictionary(c => c.Key, c => c.Cell);
        // Length is never coloured.
        Assert.Equal(("00:20.0", "00:40.0", "+20.0s", ChangeTone.Flat),
                     (cells["length"].A, cells["length"].B, cells["length"].Change.Main, cells["length"].Change.Tone));
        // A total is coloured by direction.
        Assert.Equal(("300", "600", "+100.0%", ChangeTone.Better),
                     (cells["total"].A, cells["total"].B, cells["total"].Change.Main, cells["total"].Change.Tone));
        Assert.Equal(("15.0", "15.0", ChangeTone.Flat), (cells["dps"].A, cells["dps"].B, cells["dps"].Change.Tone));
        Assert.Equal(("100.0%", "50.0%", "−50.0 pt", ChangeTone.Worse),
                     (cells["accuracy"].A, cells["accuracy"].B, cells["accuracy"].Change.Main, cells["accuracy"].Change.Tone));
        // A had no weaponskill: a dash, and nothing to compare.
        Assert.Equal(("—", "500", Change.Nothing), (cells["wsAvg"].A, cells["wsAvg"].B, cells["wsAvg"].Change));
        Assert.Equal(("0", "1", ChangeTone.Worse), (cells["deaths"].A, cells["deaths"].B, cells["deaths"].Change.Tone));
    }

    [Fact]
    public void A_live_run_so_far_is_set_against_As_final_figures()
    {
        var a = Run(30, Lines.Hit(10, "Hasaya", 100), Lines.Hit(20, "Hasaya", 200));     // 300 over 20 s: 15.0

        // B is still going: 10 s in, 250 dealt.
        var t = new Tracker();
        t.Follow("Hasaya_2026.07.30.jsonl");
        t.Start(0);
        t.Feed([Lines.Hit(10, "Hasaya", 100), Lines.Hit(15, "Hasaya", 150)]);
        t.Count(Nobody, true, 20_000);
        var soFar = ParseFile.Import(CompareSheet.Snapshot(t.Reader, t.Session, t.File, 20_000)!);

        var dps = Sheet(a, soFar).DamageBand.Single(c => c.Key == "dps").Cell;
        Assert.Equal(("15.0", "25.0", "+66.7%"), (dps.A, dps.B, dps.Change.Main));
        // A is its whole run whatever B's clock reads.
        Assert.Equal(15, Sheet(a, soFar).A.Dps);
    }

    [Fact]
    public void One_side_alone_is_its_own_figures_with_nothing_compared()
    {
        var b = Run(30, Lines.Hit(10, "Hasaya", 100), Lines.Hit(20, "Hasaya", 200));
        var s = Sheet(null, b);

        Assert.False(s.HasA);
        var total = s.DamageBand.Single(c => c.Key == "total").Cell;
        Assert.Equal(("—", "300", Change.Nothing), (total.A, total.B, total.Change));
        Assert.Equal(("—", "300"), (s.Characters.Single()["damage"].A, s.Characters.Single()["damage"].B));
        Assert.All(s.DamagePace.A, v => Assert.True(double.IsNaN(v)));
    }

    // ------------------------------------------------------------ characters

    [Fact]
    public void Characters_are_matched_by_name_and_those_in_one_run_go_last_with_a_tag()
    {
        var a = Run(30, Lines.Hit(10, "Hasaya", 400), Lines.Hit(11, "Olivia", 50));
        var b = Run(30, Lines.Hit(10, "Hasaya", 300), Lines.Hit(12, "Tank", 100));
        var s = Sheet(a, b);

        Assert.Equal(["Hasaya", "Tank", "Olivia"], s.Characters.Select(r => r.Key));
        Assert.Equal([RunSide.Both, RunSide.BOnly, RunSide.AOnly], s.Characters.Select(r => r.Side));
        var tank = s.Characters[1];
        // Missing from a run, a total there is nothing; a rate is not measured.
        Assert.Equal(("0", "100", "new"), (tank["damage"].A, tank["damage"].B, tank["damage"].Change.Main));
        Assert.Equal(("—", "5.0", Change.Nothing), (tank["dps"].A, tank["dps"].B, tank["dps"].Change));
        // The shade is B's damage over B's leader; the tick is A's over the same.
        // A's 400 is past B's leader: the tick stands at the end.
        Assert.Equal((1d, 1d), (s.Characters[0].Fraction, s.Characters[0].TickA));
        Assert.Equal(1 / 3d, tank.Fraction, 6);
        Assert.Equal(["Olivia"], s.OnlyA);
        Assert.Equal(["Tank"], s.OnlyB);
    }

    [Fact]
    public void Match_with_reads_As_character_as_Bs()
    {
        var a = Run(30, Lines.Job("Olivia", "SAM"), Lines.Hit(10, "Olivia", 400), Death(12, "Olivia"));
        var b = Run(30, Lines.Job("Torvald", "SAM"), Lines.Hit(10, "Torvald", 300));
        var s = Sheet(a, b, new CompareOptions { Matches = new Dictionary<string, string> { ["Olivia"] = "Torvald" } });

        var line = Assert.Single(s.Characters);
        Assert.Equal(("Torvald", RunSide.Both, "Olivia → Torvald"), (line.Key, line.Side, line.Label));
        Assert.Equal(("400", "300", "−25.0%"), (line["damage"].A, line["damage"].B, line["damage"].Change.Main));
        // Her deaths are hers, under his line.
        Assert.Equal(("1", "0", ChangeTone.Better), (line["deaths"].A, line["deaths"].B, line["deaths"].Change.Tone));
        Assert.Empty(s.OnlyA);
        // Isolating him by B's name takes her in A.
        var only = Sheet(a, b, new CompareOptions
        {
            Matches = new Dictionary<string, string> { ["Olivia"] = "Torvald" },
            Characters = new HashSet<string> { "Torvald" },
        });
        Assert.Equal("400", Assert.Single(only.Characters)["damage"].A);
    }

    [Fact]
    public void One_character_of_each_run_is_one_line()
    {
        var a = Run(30, Lines.Hit(10, "Hasaya", 400), Lines.Hit(11, "Olivia", 50));
        var b = Run(30, Lines.Hit(10, "Hasaya", 300), Lines.Hit(12, "Torvald", 100));
        var s = Sheet(a, b, new CompareOptions { ScopeA = "Hasaya", ScopeB = "Torvald" });

        var line = Assert.Single(s.Characters);
        Assert.Equal(("Torvald", "400", "100"), (line.Key, line["damage"].A, line["damage"].B));
    }

    [Fact]
    public void A_job_change_is_drawn_and_coloured_like_any_change()
    {
        var a = Run(30, Lines.Job("Hasaya", "DRK", "SAM"), Lines.Hit(10, "Hasaya", 400));
        var b = Run(30, Lines.Job("Hasaya", "SAM", "WAR"), Lines.Hit(10, "Hasaya", 500));
        var line = Sheet(a, b).Characters.Single();

        Assert.True(line.JobChanged);
        Assert.Equal("DRK → SAM", line.Job);
        Assert.Equal(ChangeTone.Better, line["damage"].Change.Tone);
        // A sub job alone is no change.
        var c = Run(30, Lines.Job("Hasaya", "SAM", "NIN"), Lines.Hit(10, "Hasaya", 500));
        Assert.False(Sheet(b, c).Characters.Single().JobChanged);
    }

    [Fact]
    public void Hiding_names_hides_every_name()
    {
        var lines = new[]
        {
            Lines.Job("Hasaya", "SAM", "WAR"), Lines.Job("Other", "SAM", "WAR"), Lines.Job("Mage", "WHM"),
            Lines.Hit(10, "Hasaya", 300), Lines.Hit(11, "Other", 200), Lines.Hit(12, "Stranger", 100),
            Lines.Heal(13, "Mage", 50, target: "Hasaya"), Lines.Heal(14, "Mage", 40, target: "Bystander"),
        };
        var run = Run(30, lines);
        var s = Sheet(run, run, new CompareOptions { HideNames = true });

        Assert.Equal("SAM/WAR", s.Ids["Hasaya"].Label);
        Assert.Equal("SAM/WAR 2", s.Ids["Other"].Label);
        Assert.Equal("Unknown job", s.Ids["Stranger"].Label);
        Assert.Equal("WHM", s.Ids["Mage"].Label);
        Assert.Equal(["SAM/WAR", "Unknown job 2"], s.HealTargets.Select(t => t.Label));
        Assert.Equal(["Hasaya", "Bystander"], Sheet(run, run).HealTargets.Select(t => t.Label));
    }

    [Fact]
    public void By_job_combines_everyone_on_a_job()
    {
        var a = Run(30, Lines.Job("Hasaya", "SAM", "WAR"), Lines.Job("Other", "SAM", "NIN"),
                        Lines.Hit(10, "Hasaya", 300), Lines.Hit(11, "Other", 200));
        var b = Run(30, Lines.Job("Hasaya", "SAM", "WAR"), Lines.Hit(10, "Hasaya", 300));
        var s = Sheet(a, b, new CompareOptions { ByJob = true });

        var sam = Assert.Single(s.Characters);
        Assert.Equal(("SAM", "500", "300"), (sam.Key, sam["damage"].A, sam["damage"].B));
        Assert.Equal("Hasaya, Other", s.Ids["SAM"].Members);
    }

    // --------------------------------------------------------------- actions

    [Fact]
    public void An_actions_list_pairs_each_action_across_the_runs()
    {
        var a = Run(30, Lines.Hit(10, "Hasaya", 100), Lines.Hit(11, "Hasaya", 0, hit: false));
        var b = Run(30, Lines.Hit(10, "Hasaya", 300), Lines.Hit(12, "Hasaya", 900, kind: "ws", action: "Tachi: Gekko"));
        var group = Sheet(a, b).Actions.Single();

        Assert.Equal(["Tachi: Gekko", "Attack"], group.Lines.Select(x => x.Key));
        var ws = group.Lines[0];
        Assert.Equal(RunSide.BOnly, ws.Side);
        Assert.Equal(("0", "900", "new"), (ws["total"].A, ws["total"].B, ws["total"].Change.Main));
        var attack = group.Lines[1];
        // Hits are a count: not coloured.
        Assert.Equal(("1", "1", ChangeTone.Flat), (attack["hits"].A, attack["hits"].B, attack["hits"].Change.Tone));
        Assert.Equal(("50.0%", "100.0%", "+50.0 pt"), (attack["accuracy"].A, attack["accuracy"].B, attack["accuracy"].Change.Main));
        Assert.Equal(new Change("+200.0%", "+200", ChangeTone.Better), attack["total"].Change);
        // The share is B's of the character's damage, with no change.
        Assert.Equal(("100.0%", "25.0%", Change.Blank), (attack["share"].A, attack["share"].B, attack["share"].Change));
    }

    [Fact]
    public void Any_action_can_be_set_against_any_other_of_either_run()
    {
        var a = Run(60, Lines.Hit(10, "Hasaya", 100), Lines.Hit(11, "Hasaya", 1000, kind: "ws", action: "Tachi: Gekko"),
                        Lines.Hit(12, "Hasaya", 1200, kind: "ws", action: "Tachi: Gekko", crit: true));
        var b = Run(60, Lines.Hit(10, "Torvald", 100), Lines.Hit(11, "Torvald", 900, kind: "ws", action: "Tachi: Jinpu"));
        var s = Sheet(a, b);
        var pair = new CrossPair("Hasaya", "Tachi: Gekko", "Torvald", "Tachi: Jinpu");

        var line = s.Cross(pair);
        Assert.Equal(("2", "1"), (line["hits"].A, line["hits"].B));
        Assert.Equal(("1,100", "900", "−18.2%", ChangeTone.Worse),
                     (line["avg"].A, line["avg"].B, line["avg"].Change.Main, line["avg"].Change.Tone));
        Assert.Equal(Cell.Blank, line["share"]);

        var drill = s.Drill(pair, healing: false);
        Assert.Equal(["Hits", "Average", "Median", "90th pct", "Max", "Crit rate"], drill.Figures.Select(f => f.Label));
        // Hits and Max are not coloured; the rest are.
        Assert.Equal(ChangeTone.Flat, drill.Figures.Single(f => f.Key == "max").Cell.Change.Tone);
        Assert.Equal(ChangeTone.Flat, drill.Figures.Single(f => f.Key == "hits").Cell.Change.Tone);
        Assert.Equal(ChangeTone.Worse, drill.Figures.Single(f => f.Key == "crit").Cell.Change.Tone);
        Assert.Equal((2, 1, 1100d, 900d), (drill.Spread.HitsA, drill.Spread.HitsB, drill.Spread.AvgA, drill.Spread.AvgB));
        // Even's columns: 900 to 1200 is 301 values, six columns of 51.
        Assert.Equal((900d, 1206d, 6), (drill.Spread.Bins[0].Lo, drill.Spread.Bins[^1].Hi, drill.Spread.Bins.Count));
        // Wide's run from the least to the greatest.
        var wide = s.Drill(pair, healing: false, Binning.Wide).Spread;
        Assert.Equal((900d, 1200d), (wide.Bins[0].Lo, wide.Bins[^1].Hi));

        // The pickers: each run's characters, and a character's actions with their hits.
        Assert.Equal(["Hasaya"], s.CharactersIn(runB: false, healing: false));
        Assert.Equal([("Tachi: Jinpu", 1), ("Attack", 1)], s.ActionsIn(runB: true, "Torvald", healing: false));
        // Swapped, the ends change places.
        Assert.Equal(new CrossPair("Torvald", "Tachi: Jinpu", "Hasaya", "Tachi: Gekko"), pair.Swapped);
    }

    // ---------------------------------------------------------------- Deltas

    static (ImportedParse A, ImportedParse B) DeltaRuns()
    {
        // A, 10 s: Attack 100 four times, Gekko 1000 once. 1,400: 140.0 a second.
        var a = Run(20, Lines.Hit(10, "Hasaya", 100), Lines.Hit(11, "Hasaya", 100), Lines.Hit(12, "Hasaya", 100),
                        Lines.Hit(13, "Hasaya", 100), Lines.Hit(14, "Hasaya", 1000, kind: "ws", action: "Tachi: Gekko"));
        // B, 10 s: Attack 120 five times, Gekko 900 twice; and Tank, in B only, 50.
        var b = Run(20, Lines.Hit(10, "Hasaya", 120), Lines.Hit(11, "Hasaya", 120), Lines.Hit(12, "Hasaya", 120),
                        Lines.Hit(13, "Hasaya", 120), Lines.Hit(14, "Hasaya", 120),
                        Lines.Hit(15, "Hasaya", 900, kind: "ws", action: "Tachi: Gekko"),
                        Lines.Hit(16, "Hasaya", 900, kind: "ws", action: "Tachi: Gekko"), Lines.Hit(17, "Tank", 50));
        return (a, b);
    }

    [Fact]
    public void Deltas_give_each_character_their_totals_and_each_action_its_uses_and_average()
    {
        var (a, b) = DeltaRuns();
        var d = Sheet(a, b).Deltas(healing: false);

        Assert.Equal(("1,400", "2,450", "+75.0%", "+1,050"), (d.Party.A, d.Party.B, d.Party.Change.Main, d.Party.Change.Sub));
        var hasaya = d.Lines[0];
        // Attack 400 → 600, Gekko 1,000 → 1,800: 1,400 → 2,400.
        Assert.Equal(("Hasaya", "1,400", "2,400", "+71.4%", "+1,000", ChangeTone.Better),
                     (hasaya.Key, hasaya.Total.A, hasaya.Total.B, hasaya.Total.Change.Main, hasaya.Total.Change.Sub,
                      hasaya.Total.Change.Tone));
        Assert.Equal(1000, hasaya.Delta);

        // The biggest change first. Gekko: twice where it was once, each 10% smaller.
        Assert.Equal(["Tachi: Gekko", "Attack"], hasaya.Actions.Select(x => x.Key));
        var gekko = hasaya.Actions[0];
        Assert.Equal(("+800", ChangeTone.Better), (gekko.Total.Change.Main, gekko.Total.Change.Tone));
        Assert.Equal(("2", "+1", ChangeTone.Flat), (gekko.Uses.B, gekko.Uses.Change.Main, gekko.Uses.Change.Tone));
        Assert.Equal(("900", "1,000", "−10.0%", ChangeTone.Worse),
                     (gekko.Average.B, gekko.Average.A, gekko.Average.Change.Main, gekko.Average.Change.Tone));
        var attack = hasaya.Actions[1];
        Assert.Equal(("+200", "5", "+1", "120", "+20.0%"),
                     (attack.Total.Change.Main, attack.Uses.B, attack.Uses.Change.Main, attack.Average.B, attack.Average.Change.Main));

        // In one run only: no figure for A, no change, no actions.
        var tank = d.Lines[1];
        Assert.Equal(("Tank", RunSide.BOnly, "—", "50", "—"), (tank.Key, tank.Side, tank.Total.A, tank.Total.B, tank.Total.Change.Main));
        Assert.Empty(tank.Actions);

        // The actions add up to the character, the characters to the party.
        Assert.Equal(Read(hasaya.Total.Change.Sub), hasaya.Actions.Sum(x => Read(x.Total.Change.Main)), 6);
        Assert.Equal(Read(d.Party.Change.Sub), d.Lines.Sum(l => l.Delta), 6);
    }

    [Fact]
    public void An_action_of_one_run_only_counts_as_nothing_in_the_other()
    {
        var a = Run(20, Lines.Hit(10, "Hasaya", 100), Lines.Hit(11, "Hasaya", 700, kind: "ws", action: "Tachi: Gekko"));
        var b = Run(20, Lines.Hit(10, "Hasaya", 100), Lines.Hit(11, "Hasaya", 900, kind: "ws", action: "Tachi: Kasha"));
        var hasaya = Sheet(a, b).Deltas(healing: false).Lines.Single();

        var gekko = hasaya.Actions.Single(x => x.Key == "Tachi: Gekko");
        Assert.Equal((RunSide.AOnly, "−700", "0", "−1", "—", "700"),
                     (gekko.Side, gekko.Total.Change.Main, gekko.Uses.B, gekko.Uses.Change.Main, gekko.Average.B, gekko.Average.A));
        var kasha = hasaya.Actions.Single(x => x.Key == "Tachi: Kasha");
        Assert.Equal((RunSide.BOnly, "+900", "1", "900", "—"),
                     (kasha.Side, kasha.Total.Change.Main, kasha.Uses.B, kasha.Average.B, kasha.Average.A));
        Assert.Equal("+200", hasaya.Total.Change.Sub);
    }

    [Fact]
    public void A_job_change_in_Deltas_is_compared_as_a_whole()
    {
        var a = Run(30, Lines.Job("Hasaya", "DRK"), Lines.Hit(10, "Hasaya", 400));
        var b = Run(30, Lines.Job("Hasaya", "SAM"), Lines.Hit(10, "Hasaya", 500));
        var line = Sheet(a, b).Deltas(healing: false).Lines.Single();

        Assert.Equal((true, "+25.0%", "+100"), (line.JobChanged, line.Total.Change.Main, line.Total.Change.Sub));
        Assert.Empty(line.Actions);
    }

    [Fact]
    public void Deltas_are_totals_on_the_targets_isolated()
    {
        var (a, b) = DeltaRuns();
        var d = Sheet(a, b, new CompareOptions { Targets = new HashSet<string> { "Goblin" } }).Deltas(healing: false);
        Assert.Equal(("1,400", "2,450"), (d.Party.A, d.Party.B));
    }

    // ------------------------------------------------------------ Battle Log

    [Fact]
    public void A_monster_one_run_never_killed_reads_0_and_dashes_there()
    {
        var a = Run(60, Swing(10, "Hasaya", 100, "Tiger", 1), Kill(20, "Tiger", 1));
        var b = Run(60, Swing(10, "Hasaya", 300, "Crab", 2), Kill(13, "Crab", 2));
        var s = Sheet(a, b);

        Assert.Equal(["Crab", "Tiger"], s.Tally.Select(l => l.Name));
        var crab = s.Tally[0];
        Assert.Equal(("0", "1"), (crab.Kills.A, crab.Kills.B));
        Assert.Equal(("—", "100.0", Change.Nothing), (crab.Dps.A, crab.Dps.B, crab.Dps.Change));
        Assert.Equal(("—", "0:03.0"), (crab.KillTime.A, crab.KillTime.B));
        var tiger = s.Tally[1];
        Assert.Equal(("1", "0", "—"), (tiger.Kills.A, tiger.Kills.B, tiger.Dps.B));
        Assert.Equal(("1", "1"), (s.AllMonsters.Kills.A, s.AllMonsters.Kills.B));
    }

    [Fact]
    public void Isolating_a_monster_cuts_both_runs_to_it_and_takes_the_rate_away()
    {
        var a = Run(60, Swing(10, "Hasaya", 100, "Tiger", 1), Kill(20, "Tiger", 1), Swing(21, "Hasaya", 50, "Crab", 2));
        var b = Run(60, Swing(10, "Hasaya", 300, "Tiger", 3), Kill(13, "Tiger", 3));
        var s = Sheet(a, b, new CompareOptions { Targets = new HashSet<string> { "Tiger" } });

        Assert.Equal(("100", "300"), (s.Characters.Single()["damage"].A, s.Characters.Single()["damage"].B));
        Assert.Equal(CompareSheet.Dash, s.DamageBand.Single(c => c.Key == "dps").Cell.B);
        // The tally still lists every kind.
        Assert.Equal(["Tiger"], s.Tally.Select(l => l.Name));
    }

    // ------------------------------------------------------------- the chart

    [Fact]
    public void The_cumulative_lines_share_one_clock_and_the_gap_is_B_less_A()
    {
        var a = Run(15, Lines.Hit(10, "Hasaya", 100), Lines.Hit(12, "Hasaya", 50));      // 5 s
        var b = Run(30, Lines.Hit(10, "Hasaya", 100), Lines.Hit(25, "Hasaya", 400));     // 20 s
        var p = Sheet(a, b).DamagePace;

        Assert.Equal((100d, 100d, 0d), (p.A[0], p.B[0], p.Gap[0]));
        int four = Array.IndexOf(p.Times, 4000d);
        Assert.Equal((150d, 100d, -50d), (p.A[four], p.B[four], p.Gap[four]));
        // Past its own end the shorter run has no figure, and so no gap.
        Assert.True(double.IsNaN(p.A[^1]));
        Assert.True(double.IsNaN(p.Gap[^1]));
        Assert.Equal(500, p.B[^1]);
    }

    [Fact]
    public void The_chart_marks_each_runs_kills_and_deaths_on_its_own_clock()
    {
        var a = Run(60, Swing(10, "Hasaya", 100, "Tiger", 1), Swing(18, "Hasaya", 100, "Tiger", 1), Kill(20, "Tiger", 1),
                        Death(25, "Olivia"));
        var p = Sheet(a, a).DamagePace;

        // A kill stands at its last hit.
        var kill = Assert.Single(p.KillsA);
        Assert.Equal(("Tiger", 8_000d), (kill.Name, kill.At));
        Assert.Equal(("Olivia", 15_000d), (p.DeathsB.Single().Name, p.DeathsB.Single().At));
    }

    // ------------------------------------------------------------- healing

    [Fact]
    public void Healing_is_compared_without_colour_but_for_deaths()
    {
        var a = Run(30, Lines.Hit(10, "Hasaya", 100), Lines.Heal(12, "Mage", 300), Lines.Heal(13, "Mage", 500));
        var b = Run(30, Lines.Hit(10, "Hasaya", 100), Lines.Heal(12, "Mage", 900, action: "Cure V"), Death(14, "Hasaya"));
        var s = Sheet(a, b);

        var mage = s.Healers.Single();
        Assert.Equal(("800", "900", ChangeTone.Flat), (mage["healing"].A, mage["healing"].B, mage["healing"].Change.Tone));
        Assert.All(s.HealBand.Where(c => c.Key != "killsDeaths"),
                   c => Assert.True(c.Cell.Change.Tone is ChangeTone.Flat or ChangeTone.None));
        Assert.Equal(ChangeTone.Worse, s.HealBand.Single(c => c.Key == "killsDeaths").Cell.Change.Tone);
        Assert.Equal(("Mage", "Mage"), (s.HealBand.Single(c => c.Key == "top").Cell.A, s.HealBand.Single(c => c.Key == "top").Cell.B));

        // Heals by spell, and a heal of A against another of B.
        Assert.Equal(["Cure V", "Cure IV"], s.HealSpells.Select(l => l.Key));
        var cross = s.CrossHeal(new CrossPair("Mage", "Cure IV", "Mage", "Cure V"));
        Assert.Equal(("400", "900", ChangeTone.Flat), (cross["avg"].A, cross["avg"].B, cross["avg"].Change.Tone));
        var drill = s.Drill(new CrossPair("Mage", "Cure IV", "Mage", "Cure V"), healing: true);
        Assert.Equal("Casts", drill.Figures[0].Label);
        Assert.All(drill.Figures, f => Assert.True(f.Cell.Change.Tone is ChangeTone.Flat or ChangeTone.None));

        // Deltas over healing: totals, casts and averages, never coloured.
        var d = s.Deltas(healing: true);
        var line = d.Lines.Single();
        Assert.Equal(("+100", ChangeTone.Flat), (line.Total.Change.Sub, line.Total.Change.Tone));
        Assert.All(line.Actions, x => Assert.True(x.Total.Change.Tone is ChangeTone.Flat or ChangeTone.None
                                                  && x.Average.Change.Tone is ChangeTone.Flat or ChangeTone.None));
    }

    // ------------------------------------------------------- live drill-down

    [Fact]
    public void Two_actions_of_one_session_are_set_side_by_side()
    {
        var t = new Tracker();
        t.Follow("Hasaya_2026.07.30.jsonl");
        t.Start(0);
        t.Feed([Lines.Hit(10, "Hasaya", 100), Lines.Hit(11, "Hasaya", 0, hit: false), Lines.Hit(12, "Hasaya", 300),
                Lines.Hit(13, "Hasaya", 1000, kind: "ws", action: "Tachi: Fudo")]);
        var c = t.Count(Nobody, true, 30_000);
        var s = CompareSheet.Sides(Counting.Distribution(c.Events, "Hasaya", "Attack"),
                                   Counting.Distribution(c.Events, "Hasaya", "Tachi: Fudo"), healing: false);

        Assert.Equal((2, 1, 200d, 1000d), (s.HitsA, s.HitsB, s.AvgA, s.AvgB));
        Assert.Equal(["Hits", "Accuracy", "Min", "Median", "Average", "90th pct", "Max", "Std dev"], s.Lines.Select(x => x.Label));
        Assert.Equal(("66.7%", "100.0%", "+33.3 pt"), (s.Lines[1].A, s.Lines[1].B, s.Lines[1].Change.Main));
        Assert.EndsWith("a column is its share of that action’s hits", s.Caption);
    }

    // ----------------------------------------------------------------- runs

    [Fact]
    public void A_run_says_when_it_began_how_long_it_ran_and_who_was_in_it()
    {
        var run = Run(75, Lines.Hit(10, "Hasaya", 100), Lines.Hit(11, "Tank", 50), Lines.Hit(12, "Goblin", 5, actorKind: "mob"));
        var info = CompareSheet.Describe(run);
        var began = DateTimeOffset.FromUnixTimeMilliseconds(10_000).ToLocalTime();

        Assert.Equal(began.ToString("yyyy'-'MM'-'dd HH':'mm':'ss", CultureInfo.InvariantCulture), info.Started);
        Assert.Equal("01:05.0", info.Length);
        Assert.Equal("2 characters", info.Party);
    }

    [Fact]
    public void The_session_on_screen_can_be_taken_as_a_run_without_touching_it()
    {
        var t = new Tracker();
        t.Follow("Hasaya_2026.07.30.jsonl");
        Assert.Null(CompareSheet.Snapshot(t.Reader, t.Session, t.File, 5_000));
        t.Start(8_000);
        t.Feed([Lines.Hit(5, "Hasaya", 999), Lines.Hit(10, "Hasaya", 100), Lines.Hit(20, "Hasaya", 200)]);
        t.Count(Nobody, true, 25_000);

        var run = ParseFile.Import(CompareSheet.Snapshot(t.Reader, t.Session, t.File, 25_000)!);

        Assert.True(t.Session.Running);
        Assert.Equal(25_000, run.Session.PausedAt);
        var m = Compare.Measure(run);
        Assert.Equal((300d, 15d, 20d), (m.Total, m.Duration, m.Dps));
        t.Feed([Lines.Hit(30, "Hasaya", 50)]);
        Assert.Equal(300, Compare.Measure(run).Total);
    }

    // --------------------------------------------------------------- layout

    [Fact]
    public void Compare_is_seven_panes_with_filters_and_the_drill_down_folded()
    {
        Assert.Equal(["cbars", "cactions", "cdeltas", "cpace", "cfights", "cfilters", "cdrill"], SplitTree.Keys(PaneLayouts.Compare));
        Assert.True(SplitTree.Leaf(PaneLayouts.Compare, "cfilters")!.Folded);
        Assert.True(SplitTree.Leaf(PaneLayouts.Compare, "cdrill")!.Folded);
    }

    [Fact]
    public void A_compare_layout_saved_before_the_redesign_is_read_as_its_panes_became()
    {
        var old = """{"Compare":{"split":"columns","share":0.5,"first":{"pane":"cactors"},"second":{"split":"rows","share":0.3,"first":{"pane":"cpace"},"second":{"split":"rows","share":0.5,"first":{"pane":"ckinds"},"second":{"pane":"ctargets"}}}}}""";
        var tree = PaneLayouts.Read(System.Text.Json.JsonDocument.Parse(old).RootElement, "Compare");
        var keys = SplitTree.Keys(tree);

        Assert.Equal(7, keys.Count);
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.DoesNotContain("cactors", keys);
        Assert.Equal("cbars", keys[0]);
    }

    [Fact]
    public void Compares_tables_hide_some_columns_as_installed()
    {
        var bars = Tables.CompareBars;
        Assert.Equal("cbars", bars.Key);
        Assert.False(bars.Default.Shows("share"));
        Assert.False(bars.Default.Shows("ranged"));
        Assert.True(bars.Default.Shows("wsAvg"));
        Assert.False(bars.Default.Shows("wsDamage"));
        Assert.True(bars.Default.Shows("delay"));
        Assert.True(bars.Default.IsDefault);
        Assert.False(bars.Lay(null, null).IsDefault);
    }
}
