using System.Globalization;

namespace Zerg.Core.Tests;

public class EnfeebleTests
{
    static readonly HashSet<string> Nobody = [];

    /// <summary>One monster's result of an enfeeble, as the addon writes it:
    /// its own kind of line, with <c>hit</c> and no <c>dmg</c>.</summary>
    static string Enfeeble(double t, string actor, string action, string target, int use, bool hit,
                           string? owner = null, int msg = 236)
    {
        var pet = owner != null ? $",\"owner\":\"{owner}\",\"pet\":\"{actor}\"" : "";
        return string.Create(CultureInfo.InvariantCulture,
            $"{{\"kind\":\"enfeeble\",\"t\":{t},\"seq\":1,\"use\":{use},\"via\":\"{(owner != null ? "pet" : "magic")}\"," +
            $"\"actor\":\"{actor}\",\"actorKind\":\"{(owner != null ? "pet" : "player")}\",\"action\":\"{action}\"," +
            $"\"actionId\":273,\"target\":\"{target}\",\"targetKind\":\"mob\",\"hit\":{(hit ? "true" : "false")}," +
            $"\"msg\":{msg}{pet}}}");
    }

    static Tracker Armed(double at = 20_000)
    {
        var t = new Tracker();
        t.Follow("Hasaya_2026.07.30.jsonl");
        t.Start(at);
        return t;
    }

    [Fact]
    public void An_enfeeble_line_is_filed_apart_from_the_damage_in_the_rows_shape()
    {
        var r = new EventReader();
        Assert.Null(r.Feed(Enfeeble(10, "Mage", "Sleepga", "Crab", 900, hit: false, msg: 85)));

        Assert.Empty(r.Events);
        var e = Assert.Single(r.Enfeebles);
        Assert.Equal(("magic", "Sleepga", "Crab", false, 85.0, 0.0), (e.Kind, e.Action, e.Target, e.Hit, e.Msg, e.Dmg));
        Assert.Equal(0, r.Unreadable);
    }

    [Fact]
    public void Enfeebles_start_no_clock_and_count_no_damage()
    {
        var t = Armed();
        t.Feed([Enfeeble(21, "Mage", "Sleepga", "Crab", 901, hit: true)]);
        var c = t.Count(Nobody, true, 30_000);
        Assert.False(c.Latched);
        Assert.Equal(0, c.Enfeebles.Casts);

        // The first swing is the zero, and the Sleepga before it falls outside.
        t.Feed([Lines.Hit(25, "Tank", 100), Enfeeble(26, "Mage", "Stun", "Crab", 902, hit: false, msg: 85)]);
        c = t.Count(Nobody, true, 30_000);
        Assert.True(c.Latched);
        Assert.Equal(25_000, t.Session.StartedAt);
        Assert.Equal(100, c.Totals.Total);
        Assert.Equal(new[] { "Tank" }, c.Totals.Actors.Select(a => a.Name));
        Assert.Equal("Stun", Assert.Single(Assert.Single(c.Enfeebles.Casters).Actions).Name);
        // Nor are they support.
        Assert.Equal(0, c.Support.Casts);
    }

    [Fact]
    public void Hits_and_misses_count_the_monsters_reached_and_casts_count_uses()
    {
        var t = Armed();
        t.Feed([
            Lines.Hit(21, "Tank", 100),
            // One Sleepga on three: it took on two, and the third resisted.
            Enfeeble(22, "Mage", "Sleepga", "Crab", 960, hit: true), Enfeeble(22, "Mage", "Sleepga", "Bigclaw", 960, hit: true),
            Enfeeble(22, "Mage", "Sleepga", "Snipper", 960, hit: false, msg: 284),
            // A second on one already asleep: no effect, a miss.
            Enfeeble(30, "Mage", "Sleepga", "Crab", 961, hit: false, msg: 75),
        ]);
        var mage = Assert.Single(t.Count(Nobody, true, 40_000).Enfeebles.Casters);
        var sleepga = Assert.Single(mage.Actions);

        Assert.Equal((2, 2, 2), (sleepga.Casts, sleepga.Hits, sleepga.Misses));
        Assert.Equal(0.5, sleepga.HitRate!.Value, 12);
        Assert.Equal(new[] { ("Crab", 2, 1, 1), ("Bigclaw", 1, 1, 0), ("Snipper", 1, 0, 1) },
            sleepga.Targets.Select(p => (p.Name, p.Casts, p.Hits, p.Misses)));
    }

    [Fact]
    public void A_pets_enfeeble_is_its_owners_under_its_own_name_and_an_excluded_characters_is_not_counted()
    {
        var t = Armed();
        t.Feed([
            Lines.Hit(21, "Tank", 100),
            Enfeeble(22, "Diabolos", "Nightmare", "Crab", 970, hit: true, owner: "Smn", msg: 266),
            Enfeeble(23, "Mage", "Slow", "Crab", 971, hit: true, msg: 237),
        ]);
        var e = t.Count(Nobody, true, 30_000).Enfeebles;
        Assert.Equal(2, e.Casts);
        Assert.Equal(new[] { ("Smn", "Diabolos: Nightmare"), ("Mage", "Slow") },
            e.Casters.Select(c => (c.Name, c.Actions[0].Name)));

        Assert.Equal(new[] { "Smn" },
            t.Count(new HashSet<string> { "Mage" }, true, 30_000).Enfeebles.Casters.Select(c => c.Name));
    }

    [Fact]
    public void Enfeebles_end_no_session()
    {
        var t = Armed();
        t.Feed([Lines.Hit(21, "Tank", 100), Enfeeble(28, "Mage", "Stun", "Crab", 980, hit: true)]);
        t.Count(Nobody, true, 29_000);
        t.TogglePause(35_000);

        // The end is the second the last swing landed in, not the Stun's.
        Assert.Equal(22_000, t.Session.EndedAt);
    }

    [Fact]
    public void An_export_carries_enfeebles_among_the_events_as_the_addon_wrote_them()
    {
        var t = Armed();
        t.Feed([
            Lines.Hit(21, "Tank", 100), Enfeeble(21, "Mage", "Sleepga", "Crab", 990, hit: false, msg: 85),
            Enfeeble(21, "Diabolos", "Nightmare", "Crab", 991, hit: true, owner: "Smn", msg: 266), Lines.Hit(22, "Tank", 50),
        ]);
        t.Count(Nobody, true, 23_000);
        t.TogglePause(25_000);

        var text = t.Export(40_000)!;
        Assert.Contains("{\"kind\":\"enfeeble\",\"t\":21,\"seq\":1,\"use\":990,\"via\":\"magic\",\"actor\":\"Mage\"," +
                        "\"actorKind\":\"player\",\"action\":\"Sleepga\",\"actionId\":273,\"target\":\"Crab\"," +
                        "\"targetKind\":\"mob\",\"hit\":false,\"msg\":85}", text);
        var parse = ParseFile.Import(text);
        Assert.Equal(0, parse.Skipped);
        Assert.Equal(2, parse.Source.Events.Count);
        Assert.Equal(new[] { ("magic", "Mage", null), ("pet", "Diabolos", "Smn") },
            parse.Source.Enfeebles.Select(e => (e.Kind, e.Actor, e.Owner)));

        var c = Tracker.Of(parse).Count(Nobody, true);
        Assert.Equal(150, c.Totals.Total);
        Assert.Equal(2, c.Enfeebles.Casts);
    }
}
