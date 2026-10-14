using System.Globalization;
using System.Text.Json.Nodes;
using Zerg.Core.Charts;

namespace Zerg.Core.Tests;

/// <summary>
/// What is read, and what is refused: a field of the wrong JSON type makes
/// its line unreadable rather than coerced, a parse whose clock cannot be
/// right is refused, and what is kept between counts counts as a count from
/// scratch would.
/// </summary>
public class ReadingTests
{
    static readonly HashSet<string> Nobody = [];

    static string Melee(string dmg) =>
        "{\"t\":10,\"seq\":1,\"use\":1,\"kind\":\"melee\",\"actor\":\"Hasaya\",\"actorKind\":\"player\"," +
        "\"action\":\"Attack\",\"actionId\":1,\"target\":\"Goblin\",\"targetKind\":\"mob\",\"dmg\":" + dmg +
        ",\"hit\":true,\"crit\":false,\"burst\":false,\"msg\":1}";

    // ------------------------------------------------------- the contract

    [Theory]
    [InlineData("\"123\"")]
    [InlineData("\"0x1F\"")]
    [InlineData("true")]
    [InlineData("[5]")]
    [InlineData("\" 12 \"")]
    [InlineData("\"1e3\"")]
    [InlineData("1e400")]
    [InlineData("{}")]
    public void Damage_that_is_not_a_number_is_no_damage(string dmg)
    {
        var r = new EventReader();
        Assert.Null(r.Feed(Melee(dmg)));
        Assert.Empty(r.Events);
        Assert.Equal(1, r.Unreadable);
        Assert.Empty(r.Roster.Kinds);   // nothing of it was believed
    }

    [Fact]
    public void Damage_that_is_a_number_is_read_as_it_is()
    {
        var r = new EventReader();
        Assert.Equal(123, r.Feed(Melee("123"))!.Dmg);
        Assert.Equal(1000, r.Feed(Melee("1e3"))!.Dmg);
        Assert.Equal(0, r.Unreadable);
    }

    [Theory]
    [InlineData("{\"t\":10,\"kind\":7,\"actor\":\"Hasaya\",\"actorKind\":\"player\",\"dmg\":5,\"hit\":true}")]
    [InlineData("{\"t\":10,\"kind\":\"melee\",\"actor\":[\"Hasaya\"],\"actorKind\":\"player\",\"dmg\":5,\"hit\":true}")]
    [InlineData("{\"t\":10,\"kind\":\"melee\",\"actor\":\"Hasaya\",\"actorKind\":\"player\",\"dmg\":5,\"hit\":1}")]
    [InlineData("{\"t\":\"10\",\"kind\":\"melee\",\"actor\":\"Hasaya\",\"actorKind\":\"player\",\"dmg\":5,\"hit\":true}")]
    [InlineData("{\"kind\":\"melee\",\"actor\":\"Hasaya\",\"actorKind\":\"player\",\"dmg\":5,\"hit\":true}")]
    [InlineData("{\"t\":-1,\"kind\":\"melee\",\"actor\":\"Hasaya\",\"actorKind\":\"player\",\"dmg\":5,\"hit\":true}")]
    [InlineData("{\"t\":3e11,\"kind\":\"melee\",\"actor\":\"Hasaya\",\"actorKind\":\"player\",\"dmg\":5,\"hit\":true}")]
    [InlineData("{\"kind\":\"heal\",\"actor\":\"Mage\",\"actorKind\":\"player\",\"hp\":\"80\",\"t\":10}")]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    public void A_line_the_contract_does_not_allow_is_skipped_and_counted(string line)
    {
        var t = new Tracker();
        t.Follow("Hasaya_2026.07.30.jsonl");
        t.Start(0);
        t.Feed([line, Lines.Hit(11, "Tank", 50)]);

        var c = t.Count(Nobody, true, 20_000);
        // A kind that is a number used to be a row that counted; a time
        // past what a clock can show used to throw on every beat.
        Assert.Equal(50, c.Totals.Total);
        Assert.Equal(11_000, t.Session.StartedAt);
        Assert.Empty(c.Heals);
        Assert.Equal(1, t.Reader.Unreadable);
        Assert.Equal(2, t.Lines);
    }

    [Fact]
    public void Blank_lines_and_the_addons_own_notices_are_not_unreadable()
    {
        var r = new EventReader();
        r.Feed("");
        r.Feed("   ");
        // The startup probe has fields of every type; none of them is ours.
        r.Feed("{\"kind\":\"meta\",\"v\":\"0.3.0\",\"t\":1785434520,\"chunkData\":\"string\",\"actionId\":\"x\",\"dmg\":[1]}");
        Assert.Equal(0, r.Unreadable);
        Assert.Equal(3, r.LineNo);
    }

    [Fact]
    public void A_field_that_is_null_is_a_field_that_is_missing()
    {
        var r = new EventReader();
        var e = r.Feed("{\"t\":10,\"kind\":\"melee\",\"actor\":\"Hasaya\",\"actorKind\":\"player\",\"use\":null," +
                       "\"dmg\":null,\"hit\":null,\"owner\":null,\"action\":null}")!;
        Assert.Null(e.Use);
        Assert.Equal(0, e.Dmg);
        Assert.False(e.Hit);
        Assert.Null(e.Owner);
        Assert.Equal(EventReader.AdditionalEffect, e.Action);
        Assert.Equal(0, r.Unreadable);
    }

    [Fact]
    public void Every_line_of_the_test_fixture_reads()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Parses", "MNK.zerg");
        var parse = ParseFile.Import(File.ReadAllText(path));
        Assert.Equal(0, parse.Source.Unreadable);
        Assert.Equal(0, parse.Skipped);
    }

    // ------------------------------------------------------ a parse's clock

    static JsonObject Doc()
    {
        var t = new Tracker();
        t.Follow("Hasaya_2026.07.30.jsonl");
        t.Start(8_000);
        t.Feed([Lines.Hit(10, "Hasaya", 100), Lines.Hit(40, "Hasaya", 300)]);
        t.Count(Nobody, true, 12_000);
        t.TogglePause(20_000);
        t.TogglePause(30_000);
        t.TogglePause(50_000);
        return JsonNode.Parse(t.Export(60_000)!)!.AsObject();
    }

    static ImportError Refused(JsonObject doc) =>
        Assert.Throws<ParseImportException>(() => ParseFile.Import(doc.ToJsonString())).Reason;

    [Fact]
    public void A_start_no_clock_can_show_is_no_session()
    {
        var doc = Doc();
        doc["session"]!["startedAt"] = 3e14;
        doc["session"]!["pausedAt"] = 3e14 + 1000;
        Assert.Equal(ImportError.NoSession, Refused(doc));
    }

    [Fact]
    public void Pauses_are_read_in_order_whatever_order_they_are_written_in()
    {
        var doc = Doc();
        var spans = doc["session"]!["spans"]!.AsArray();
        spans.Add(new JsonObject { ["from"] = 12_000.0, ["to"] = 14_000.0 });   // before the one there is
        var parse = ParseFile.Import(doc.ToJsonString());
        Assert.Equal([new PauseSpan(12_000, 14_000), new PauseSpan(20_000, 30_000)], parse.Session.Spans);
    }

    [Fact]
    public void Pauses_that_overlap_or_lie_outside_the_session_are_no_session()
    {
        var doc = Doc();
        doc["session"]!["spans"]!.AsArray().Add(new JsonObject { ["from"] = 25_000.0, ["to"] = 35_000.0 });
        Assert.Equal(ImportError.NoSession, Refused(doc));

        doc = Doc();
        doc["session"]!["spans"]!.AsArray().Add(new JsonObject { ["from"] = 1_000.0, ["to"] = 2_000.0 });
        Assert.Equal(ImportError.NoSession, Refused(doc));

        doc = Doc();
        doc["session"]!["spans"]!.AsArray().Add(new JsonObject { ["from"] = 40_500.0, ["to"] = 99_000.0 });
        Assert.Equal(ImportError.NoSession, Refused(doc));
    }

    [Fact]
    public void A_version_that_is_not_a_number_is_not_a_parse()
    {
        var doc = Doc();
        doc["version"] = "1";
        Assert.Equal(ImportError.NotAParse, Refused(doc));
        doc["version"] = 2;
        Assert.Equal(ImportError.TooNew, Refused(doc));
    }

    [Fact]
    public void A_time_too_far_off_to_name_a_file_by_is_left_out_of_the_name()
    {
        Assert.Equal("Hasaya_parse.zerg", ParseFile.SuggestedName("Hasaya", 3e14));
        Assert.Equal("parse.zerg", ParseFile.SuggestedName(null, double.NaN));
    }

    [Fact]
    public void An_export_writes_names_as_they_are()
    {
        var t = new Tracker();
        t.Follow("Hasaya_2026.07.30.jsonl");
        t.Start(0);
        t.Feed([Lines.Hit(10, "Hasaya", 100, target: "Ébène <b> \\\"x\\\"")]);
        t.Count(Nobody, true, 12_000);
        t.TogglePause(20_000);
        var text = t.Export(20_000)!;

        Assert.Contains("\"target\":\"Ébène <b> \\\"x\\\"\"", text);
        Assert.Equal("Ébène <b> \"x\"", ParseFile.Import(text).Source.Events[0].Target);
    }

    // ----------------------------------------------------- kept between counts

    /// <summary>A pull with a pause, a pet, a heal and a name that is
    /// found to be a player only after its first rows were read.</summary>
    static string[] Pull() =>
    [
        Lines.Job("Hasaya", "SAM", "WAR"),
        Lines.Hit(10, "Hasaya", 100), Lines.Hit(11, "Stranger", 40, actorKind: "other"),
        Lines.Hit(12, "Fluffy", 60, actorKind: "pet", owner: "Beast"), Lines.Heal(13, "Mage", 80),
        Lines.Hit(14, "Hasaya", 30, kind: "skillchain", action: "Light"),
        Lines.Hit(21, "Hasaya", 200), Lines.Job("Stranger", "WAR"),
        Lines.Hit(22, "Stranger", 70, actorKind: "player"), Lines.Heal(23, "Mage", 90),
        Lines.Hit(31, "Goblin", 9, actorKind: "mob", target: "Hasaya", targetKind: "player"),
        Lines.Hit(32, "Hasaya", 300),
    ];

    static (double Total, string Listed, double Heal, double Elapsed) Counted(Snapshot c) =>
        (c.Totals.Total, string.Join(", ", c.Listed), c.Healing.Total, c.Elapsed);

    [Fact]
    public void A_session_counted_as_it_grows_counts_as_one_counted_once()
    {
        var lines = Pull();
        var step = new Tracker();
        step.Follow("Hasaya_2026.07.30.jsonl");
        step.Start(5_000);
        for (int i = 0; i < lines.Length; i++)
        {
            step.Feed([lines[i]]);
            step.Count(Nobody, true, (10 + i) * 1000.0);
            if (i == 6) step.TogglePause(18_000);
            if (i == 7) step.TogglePause(20_000);
        }

        var once = new Tracker();
        once.Follow("Hasaya_2026.07.30.jsonl");
        once.Start(5_000);
        once.Feed(lines[..7]);
        once.Count(Nobody, true, 16_000);
        once.TogglePause(18_000);
        once.TogglePause(20_000);
        once.Feed(lines[7..]);

        foreach (var skillchains in new[] { true, false })
            foreach (var excluded in new[] { Nobody, new HashSet<string> { "Beast" } })
                Assert.Equal(Counted(once.Count(excluded, skillchains, 40_000)),
                             Counted(step.Count(excluded, skillchains, 40_000)));
    }

    [Fact]
    public void A_snapshot_does_not_grow_with_the_session()
    {
        var t = new Tracker();
        t.Follow("Hasaya_2026.07.30.jsonl");
        t.Start(0);
        t.Feed([Lines.Hit(10, "Hasaya", 100)]);
        var first = t.Count(Nobody, true, 11_000);
        t.Feed([Lines.Hit(12, "Hasaya", 100)]);
        t.Count(Nobody, true, 13_000);
        Assert.Single(first.Events);
    }

    [Fact]
    public void The_chips_are_in_the_order_the_table_is()
    {
        var c = new Tracker();
        c.Follow("Hasaya_2026.07.30.jsonl");
        c.Start(0);
        // A use over two targets that landed on one, a miss, and a tie kept
        // in the order of first action.
        c.Feed([
            Lines.Hit(10, "Tank", 0, hit: false), Lines.Hit(10, "Mage", 50), Lines.Hit(11, "Hasaya", 50),
            "{\"t\":12,\"seq\":1,\"use\":900,\"kind\":\"ws\",\"actor\":\"Tank\",\"actorKind\":\"player\",\"action\":\"Spin\"," +
            "\"target\":\"A\",\"targetKind\":\"mob\",\"dmg\":0,\"hit\":false}",
            "{\"t\":12,\"seq\":2,\"use\":900,\"kind\":\"ws\",\"actor\":\"Tank\",\"actorKind\":\"player\",\"action\":\"Spin\"," +
            "\"target\":\"B\",\"targetKind\":\"mob\",\"dmg\":60,\"hit\":true}",
        ]);
        var all = c.Count(Nobody, true, 20_000);
        Assert.Equal(all.Totals.Actors.Select(a => a.Name), Counting.Ranked(all.Events));
        Assert.Equal(["Tank", "Mage", "Hasaya"], all.Listed);
        // With someone excluded the chips still list everyone, in that order.
        Assert.Equal(["Tank", "Mage", "Hasaya"], c.Count(new HashSet<string> { "Tank" }, true, 20_000).Listed);
    }

    [Fact]
    public void An_idle_session_keeps_its_slots_and_none_of_its_rows()
    {
        var t = new Tracker();
        t.Follow("Hasaya_2026.07.30.jsonl");
        t.Feed([Lines.Hit(10, "Tank", 50), Lines.Heal(11, "Mage", 80), Lines.Hit(12, "Hasaya", 60)]);

        Assert.Empty(t.Reader.Events);
        Assert.Empty(t.Reader.Heals);
        Assert.Equal(2, t.Reader.Rows);
        Assert.Equal(3, t.Lines);
        Assert.Equal(0, t.Cast.Slots["Hasaya"]);
        Assert.Equal(1, t.Cast.Slots["Tank"]);

        // Armed, they are kept: the next counted one is the zero.
        t.Start(15_000);
        t.Feed([Lines.Hit(16, "Mage", 70)]);
        Assert.Single(t.Reader.Events);
        Assert.Equal(16_000, t.Count(Nobody, true, 20_000).Events.Count is 1 ? t.Session.StartedAt : null);
        Assert.Equal(2, t.Cast.Slots["Mage"]);
    }

    // ------------------------------------------------------------- charts

    sealed record Line(string Name, double[] Values, bool Group = false) : ILineSeries;

    [Fact]
    public void The_crosshair_finds_the_live_edge_by_its_time()
    {
        // An even grid of ten seconds, and the edge half a second past the last bin.
        double[] times = [0, 10_000, 20_000, 30_000, 30_500];
        var l = LineLayout.Compute(800, 300, times, [new Line("A", [0, 1, 2, 3, 3])], false, false, (s, _) => s.Length * 7);
        // Three quarters of the way across is 22,875 ms: the 20 s bin, not
        // the fourth of five samples by count (30 s).
        var x = l.Plot.X + 0.75 * l.Plot.W;
        Assert.Equal(2, l.Hover(x, l.Plot.Y + 10)!.Index);
        Assert.Equal(4, l.Hover(l.Plot.Right, l.Plot.Y + 10)!.Index);
        Assert.Equal(0, l.Hover(l.Plot.X - 5, l.Plot.Y + 10)!.Index);
    }

    [Fact]
    public void A_chart_is_summed_up_over_the_session_clock_not_a_time_of_day()
    {
        var s = LineLayout.Summary([0, 12_000, 24_000],
            [("Hasaya", [0, 500, 1200]), ("Tank", [0, 900, 1000]), ("Gone", [0, double.NaN, double.NaN])]);
        Assert.Equal("3 lines over 00:24. Highest: Hasaya, 1,200", s);
        Assert.Equal("No data", LineLayout.Summary([], []));
        Assert.Equal("1 line over 1:00:00", LineLayout.Summary([3_600_000], [("Gone", [double.NaN])]));
    }

    [Fact]
    public void Unnamed_damage_is_exact()
    {
        Assert.Equal(0, Targets.Unnamed([new TargetTotal("A", 10)], 10));
        Assert.Equal(0.25, Targets.Unnamed([new TargetTotal("A", 10)], 10.25));
        Assert.Equal(0, Targets.Unnamed([new TargetTotal("A", 10)], 9));
    }

    [Fact]
    public void A_level_is_written_as_a_number()
    {
        var r = new EventReader();
        r.Feed(Lines.Job("Hasaya", "SAM", "WAR"));
        Assert.Equal("SAM75 / WAR37", r.Roster.JobTitle("Hasaya"));
        Assert.Equal(75.ToString(CultureInfo.InvariantCulture), r.Roster.JobOf("Hasaya")!.MainLevel.ToString(CultureInfo.InvariantCulture));
    }
}
