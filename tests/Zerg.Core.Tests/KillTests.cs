using System.Globalization;

namespace Zerg.Core.Tests;

public class KillTests
{
    static readonly HashSet<string> Nobody = [];
    static int use = 50_000;

    /// <summary>A party member's swing at a monster with an id, as the addon
    /// now writes every row aimed at one.</summary>
    static string Swing(double t, string actor, double dmg, string target, double? id, bool hit = true) =>
        FormattableString.Invariant(
            $"{{\"t\":{t * 1000},\"seq\":1,\"use\":{++use},\"kind\":\"melee\",\"actor\":\"{actor}\",\"actorKind\":\"player\",\"action\":\"Attack\",\"actionId\":0,\"target\":\"{target}\",\"targetKind\":\"mob\"{Id(id)},\"dmg\":{dmg},\"hit\":{(hit ? "true" : "false")},\"crit\":false,\"burst\":false,\"msg\":1}}");

    /// <summary>A monster's death line: the party member who killed it, its id.</summary>
    static string Kill(double t, string target, double? id, string killer = "Hasaya", int msg = 6) =>
        FormattableString.Invariant(
            $"{{\"kind\":\"death\",\"t\":{t * 1000},\"seq\":1,\"actor\":\"{killer}\",\"actorKind\":\"player\",\"target\":\"{target}\",\"targetKind\":\"mob\"{Id(id)},\"msg\":{msg}}}");

    static string Id(double? id) => id is double i ? FormattableString.Invariant($",\"targetId\":{i}") : "";

    /// <summary>A party member's death line: who died, and nothing else.</summary>
    static string Died(double t, string who) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{{\"kind\":\"death\",\"t\":{t * 1000},\"seq\":1,\"target\":\"{who}\",\"targetKind\":\"player\",\"msg\":97}}");

    static Tracker Armed(double at = 1_000_000)
    {
        var t = new Tracker();
        t.Follow("Hasaya_2026.07.30.jsonl");
        t.Start(at);
        return t;
    }

    [Fact]
    public void The_code_is_the_last_three_hex_digits_as_XIUI_shows_them()
    {
        Assert.Equal("002", Kills.Code(17195010));   // 0x1066002
        Assert.Equal("0CB", Kills.Code(0x10660CB));
        Assert.Equal("Tiger [00B]", Kills.Named("Tiger", 0x106600B));
        Assert.Equal("Tiger (10)", Kills.Counted("Tiger", 10));
        Assert.Equal("Goblin Leecher", Kills.Counted("Goblin Leecher", 0));
    }

    [Fact]
    public void A_death_line_is_filed_apart_and_is_never_a_row()
    {
        var r = new EventReader();
        // A monster's names its killer: as a row it would be Hasaya's miss.
        Assert.Null(r.Feed(Kill(10, "Tiger", 17195010)));
        Assert.Null(r.Feed(Died(11, "Olivia")));
        Assert.Empty(r.Events);
        Assert.Equal(2, r.Deaths.Count);
        Assert.Equal((true, 17195010.0, 10_000.0), (r.Deaths[0].IsKill, r.Deaths[0].TargetId!.Value, r.Deaths[0].T));
        Assert.Equal(("Olivia", false, (double?)null), (r.Deaths[1].Target, r.Deaths[1].IsKill, r.Deaths[1].TargetId));
        Assert.Equal(0, r.Unreadable);

        // One with nobody dead, or no time, breaks the contract.
        r.Feed("{\"kind\":\"death\",\"t\":12000,\"targetKind\":\"player\",\"msg\":97}");
        r.Feed("{\"kind\":\"death\",\"target\":\"Olivia\",\"targetKind\":\"player\",\"msg\":97}");
        Assert.Equal(2, r.Unreadable);
    }

    [Fact]
    public void Deaths_start_no_clock()
    {
        var t = Armed();
        t.Feed([Died(1001, "Olivia"), Kill(1002, "Tiger", 1)]);
        Assert.False(t.Count(Nobody, true, 1_010_000).Latched);
    }

    [Fact]
    public void The_death_line_splits_one_spawn_into_its_fights()
    {
        var t = Armed();
        t.Feed([
            Swing(1000, "Hasaya", 100, "Tiger", 2),
            Swing(1010, "Kanaria", 200, "Tiger", 2),
            Kill(1011, "Tiger", 2),
            // The same spawn point, respawned: the same id, a new fight.
            Swing(1100, "Hasaya", 300, "Tiger", 2),
            Swing(1130, "Hasaya", 0, "Tiger", 2, hit: false),
            Kill(1131, "Tiger", 2),
        ]);
        var k = t.Count(Nobody, true, 1_200_000).Kills;

        Assert.Equal(2, k.Fights.Count);
        var (a, b) = (k.Fights[0], k.Fights[1]);
        Assert.Equal(("Tiger [002]", 11_000.0, 300.0, 10_000.0), (a.Named, a.Length!.Value, a.Damage, a.At));
        Assert.Equal(("Tiger [002]", 31_000.0, 300.0, 130_000.0), (b.Named, b.Length!.Value, b.Damage, b.At));
        Assert.Equal(300 / 11.0, a.Dps!.Value, 9);
        Assert.Equal(2, k.KillsOf("Tiger"));
        Assert.Equal(21_000, KillTotals.AverageLength(k.Fights));
    }

    [Fact]
    public void Two_of_one_name_fought_at_once_are_two_fights_by_their_ids()
    {
        var t = Armed();
        t.Feed([
            Swing(1000, "Hasaya", 100, "Tiger", 6),
            Swing(1002, "Kanaria", 500, "Tiger", 11),
            Swing(1020, "Hasaya", 100, "Tiger", 6),
            Kill(1021, "Tiger", 6),
            Swing(1050, "Kanaria", 500, "Tiger", 11),
            Kill(1051, "Tiger", 11),
        ]);
        var f = t.Count(Nobody, true, 1_100_000).Kills.Fights;
        Assert.Equal(new[] { ("006", 200.0, 21_000.0), ("00B", 1000.0, 49_000.0) },
                     f.Select(x => (x.Code, x.Damage, x.Length!.Value)));
    }

    [Fact]
    public void Rows_and_kills_without_an_id_are_no_fight()
    {
        // The addon writes an id on every line aimed at a monster; one
        // without is matched to nothing, by name or otherwise.
        var t = Armed();
        t.Feed([Swing(1000, "Hasaya", 100, "Crab", null), Swing(1005, "Hasaya", 50, "Crab", null), Kill(1006, "Crab", 77),
                Swing(1010, "Hasaya", 70, "Crab", 78), Kill(1012, "Crab", null)]);
        var c = t.Count(Nobody, true, 1_100_000);
        var f = Assert.Single(c.Kills.Fights);
        Assert.Equal(("Crab [04D]", 77d, 0d, (double?)null), (f.Named, f.Id, f.Damage, f.Length));
        Assert.Equal(220, c.Totals.Total);                    // the damage is still counted
    }

    [Fact]
    public void A_kill_is_in_the_session_when_its_killing_blow_is()
    {
        var t = Armed();
        t.Feed([Swing(1000, "Hasaya", 100, "Tiger", 2), Swing(1010, "Hasaya", 100, "Tiger", 2)]);
        t.Count(Nobody, true, 1_011_000);
        // Paused after the last swing: the clock is snapped back to it.
        // The death line comes two seconds later, after that end.
        t.TogglePause(1_015_000);
        t.Feed([Kill(1012, "Tiger", 2)]);
        var c = t.Count(Nobody, true, 1_020_000);
        Assert.Equal(10_000, c.Elapsed);
        var f = Assert.Single(c.Kills.Fights);
        Assert.Equal(10_000, f.At);
        Assert.Equal(12_000, f.Length);

        // And so it goes with its blow into an export, and back.
        var back = Tracker.Of(ParseFile.Import(t.Export(1_030_000)!));
        Assert.Single(back.Count(Nobody, true).Kills.Fights);
    }

    [Fact]
    public void A_kill_before_the_zero_is_not_the_sessions()
    {
        var t = Armed();
        t.Feed([Kill(999, "Tiger", 2), Swing(1000, "Hasaya", 100, "Tiger", 3)]);
        Assert.Empty(t.Count(Nobody, true, 1_010_000).Kills.Fights);
    }

    [Fact]
    public void A_party_death_runs_to_the_next_thing_they_did()
    {
        var t = Armed();
        t.Feed([
            Swing(1000, "Hasaya", 100, "Tiger", 2),
            Swing(1001, "Olivia", 100, "Tiger", 2),
            Died(1005, "Olivia"),
            Died(1006, "Kanaria"),
            // Healed is not acting: a heal Olivia received ends nothing.
            Lines.Heal(1010, "Kanaria", 300, target: "Olivia"),
            Swing(1030, "Olivia", 100, "Tiger", 2),
        ]);
        var d = t.Count(Nobody, true, 1_040_000).Kills.Deaths;
        Assert.Equal(new[] { ("Olivia", 5_000.0, (double?)30_000), ("Kanaria", 6_000.0, (double?)10_000) },
                     d.Select(x => (x.Name, x.At, x.Next)));

        // With a character isolated, only theirs is listed.
        var off = t.Count(new HashSet<string> { "Kanaria" }, true, 1_040_000).Kills;
        Assert.Equal("Kanaria", Assert.Single(off.Deaths).Name);
        Assert.Equal(0, off.DeathsOf("Olivia"));
    }

    [Fact]
    public void A_party_death_before_the_zero_or_after_the_end_is_not_the_sessions()
    {
        var t = Armed();
        t.Feed([Died(999, "Olivia"), Swing(1000, "Hasaya", 100, "Tiger", 2)]);
        t.Count(Nobody, true, 1_001_000);
        t.TogglePause(1_005_000);
        t.Feed([Died(1003, "Kanaria")]);
        Assert.Empty(t.Count(Nobody, true, 1_010_000).Kills.Deaths);
    }

    [Fact]
    public void One_fight_isolated_counts_its_rows_alone_and_gives_no_rate()
    {
        var t = Armed();
        t.Feed([
            Swing(1000, "Hasaya", 100, "Tiger", 2),
            Kill(1001, "Tiger", 2),
            Swing(1010, "Hasaya", 400, "Tiger", 2),
            Swing(1012, "Hasaya", 1000, "Crab", 9),
            Kill(1020, "Tiger", 2),
        ]);
        var all = t.Count(Nobody, true, 1_030_000);
        var second = all.Kills.Fights[1];
        var c = t.Count(Nobody, true, 1_030_000, fight: second);

        Assert.Equal(400, c.Totals.Total);
        Assert.Null(c.Rate(400, 30));
        Assert.Equal(second, Assert.Single(c.Killed));
        Assert.Null(c.KillRate(30));
        Assert.Equal(["Tiger"], c.Scope);
        Assert.Equal(" [002]", c.Tag("Tiger"));
    }

    [Fact]
    public void A_fight_isolated_beside_a_target_counts_the_rows_of_either()
    {
        var t = Armed();
        t.Feed([
            Swing(1000, "Hasaya", 100, "Tiger", 2),
            Kill(1001, "Tiger", 2),
            Swing(1010, "Hasaya", 400, "Tiger", 2),
            Swing(1012, "Hasaya", 1000, "Crab", 9),
            Kill(1014, "Crab", 9),
            Kill(1020, "Tiger", 2),
        ]);
        var first = t.Count(Nobody, true, 1_030_000).Kills.Fights[0];
        var c = t.Count(Nobody, true, 1_030_000, targets: new HashSet<string> { "Crab" }, fight: first);

        Assert.Equal(1100, c.Totals.Total);
        Assert.Equal(2, c.Killed.Count);
        Assert.Equal(["Crab", "Tiger"], c.Scope);
        Assert.Equal((" (1)", " [002]"), (c.Tag("Crab"), c.Tag("Tiger")));
        // Kills an hour stand again: a name is isolated.
        Assert.Equal(2 / (30 / 3600.0), c.KillRate(30)!.Value, 9);
    }

    [Fact]
    public void Compare_reads_each_runs_kills_kill_time_and_deaths()
    {
        ImportedParse Run(params string[] lines)
        {
            var t = Armed();
            t.Feed(lines);
            t.Count(Nobody, true, 1_060_000);
            return ParseFile.Import(CompareSheet.Snapshot(t.Reader, t.Session, t.File, 1_060_000)!);
        }
        var a = Run(Swing(1000, "Hasaya", 100, "Tiger", 2), Kill(1020, "Tiger", 2));
        var b = Run(Swing(1000, "Hasaya", 100, "Tiger", 2), Kill(1010, "Tiger", 2),
                    Swing(1012, "Hasaya", 100, "Tiger", 2), Died(1015, "Olivia"), Kill(1022, "Tiger", 2));

        var s = CompareSheet.Of(a, b, byJob: false, skillchains: true, hideNames: false);
        var tiles = s.DamageTiles.ToDictionary(t => t.Label);

        Assert.Equal(("0:20", "0:10", "−10s"), (tiles["Avg kill time"].A, tiles["Avg kill time"].B, tiles["Avg kill time"].Change.Main));
        Assert.Equal(ChangeTone.Better, tiles["Avg kill time"].Change.Tone);
        Assert.Equal(("0", "1", "+1", ChangeTone.Worse),
                     (tiles["Deaths"].A, tiles["Deaths"].B, tiles["Deaths"].Change.Main, tiles["Deaths"].Change.Tone));
        var tiger = Assert.Single(s.Targets);
        Assert.Equal(("Tiger (1 · 2)", "0:20", "0:10"), (tiger.Name, tiger.KillTime!.A, tiger.KillTime.B));
    }

    [Fact]
    public void Kills_an_hour_stay_while_a_target_is_isolated()
    {
        var t = Armed();
        t.Feed([
            Swing(1000, "Hasaya", 100, "Tiger", 2), Kill(1001, "Tiger", 2),
            Swing(1002, "Hasaya", 100, "Crab", 9), Kill(1003, "Crab", 9),
            Swing(1004, "Hasaya", 100, "Tiger", 2), Kill(1005, "Tiger", 2),
        ]);
        var all = t.Count(Nobody, true, 1_036_000);
        Assert.Equal(3 / (36 / 3600.0), all.KillRate(36)!.Value, 9);

        var tigers = t.Count(Nobody, true, 1_036_000, targets: new HashSet<string> { "Tiger" });
        Assert.Null(tigers.Rate(200, 36));
        Assert.Equal(2, tigers.Killed.Count);
        Assert.Equal(2 / (36 / 3600.0), tigers.KillRate(36)!.Value, 9);
    }

    [Fact]
    public void Export_writes_deaths_and_ids_as_the_addon_did()
    {
        var t = Armed();
        t.Feed([
            Swing(1000, "Hasaya", 100, "Tiger", 17195010),
            Died(1002, "Olivia"),
            Swing(1003, "Hasaya", 100, "Tiger", 17195010),
            Kill(1004, "Tiger", 17195010),
            Swing(1005, "Hasaya", 1, "Crab", 9),
        ]);
        t.Count(Nobody, true, 1_006_000);
        t.TogglePause(1_007_000);
        var text = t.Export(1_008_000)!;
        Assert.Contains("\"targetKind\":\"mob\",\"targetId\":17195010,\"dmg\":100", text);
        Assert.Contains("{\"kind\":\"death\",\"t\":1002000,\"seq\":1,\"target\":\"Olivia\",\"targetKind\":\"player\",\"msg\":97}", text);
        Assert.Contains("{\"kind\":\"death\",\"t\":1004000,\"seq\":1,\"actor\":\"Hasaya\",\"actorKind\":\"player\"," +
                        "\"target\":\"Tiger\",\"targetKind\":\"mob\",\"targetId\":17195010,\"msg\":6}", text);

        var parse = ParseFile.Import(text);
        Assert.Equal(0, parse.Skipped);
        var k = Tracker.Of(parse).Count(Nobody, true).Kills;
        Assert.Equal("Tiger [002]", Assert.Single(k.Fights).Named);
        Assert.Equal("Olivia", Assert.Single(k.Deaths).Name);
    }
}
