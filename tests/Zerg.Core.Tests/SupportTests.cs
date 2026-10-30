using System.Globalization;

namespace Zerg.Core.Tests;

public class SupportTests
{
    static readonly HashSet<string> Nobody = [];

    /// <summary>One target of a buff, as the addon writes it: a magic row
    /// from one of ours to one of ours that dealt nothing.</summary>
    static string Buff(double t, string actor, string action, string target, int use, string? owner = null,
                       double dmg = 0, bool hit = false)
    {
        var pet = owner != null ? $",\"owner\":\"{owner}\",\"pet\":\"{actor}\"" : "";
        return string.Create(CultureInfo.InvariantCulture,
            $"{{\"t\":{t},\"seq\":1,\"use\":{use},\"kind\":\"{(owner != null ? "pet" : "magic")}\",\"actor\":\"{actor}\"," +
            $"\"actorKind\":\"{(owner != null ? "pet" : "player")}\",\"action\":\"{action}\",\"actionId\":46," +
            $"\"target\":\"{target}\",\"targetKind\":\"player\",\"dmg\":{dmg},\"hit\":{(hit ? "true" : "false")}," +
            $"\"crit\":false,\"burst\":false,\"msg\":75{pet}}}");
    }

    static Tracker Armed(double at = 20_000)
    {
        var t = new Tracker();
        t.Follow("Hasaya_2026.07.30.jsonl");
        t.Start(at);
        return t;
    }

    [Fact]
    public void A_row_from_one_of_ours_to_one_of_ours_is_support_not_damage()
    {
        var r = new EventReader();
        Assert.Null(r.Feed(Buff(10, "Mage", "Protect IV", "Tank", 900)));
        Assert.NotNull(r.Feed(Lines.Hit(11, "Mage", 0, kind: "magic", action: "Slow", hit: false)));

        Assert.Equal("Protect IV", Assert.Single(r.Support).Action);
        Assert.Equal("Slow", Assert.Single(r.Events).Action);
        Assert.Equal(0, r.Unreadable);
    }

    [Fact]
    public void Support_does_not_start_the_clock()
    {
        var t = Armed();
        t.Feed([Buff(21, "Mage", "Protect IV", "Tank", 901), Buff(22, "Mage", "Haste", "Tank", 902)]);
        var c = t.Count(Nobody, true, 30_000);
        Assert.False(c.Latched);
        Assert.Equal(0, c.Support.Casts);

        // The first swing is the zero, and the buffs before it fall outside.
        t.Feed([Lines.Hit(25, "Tank", 100), Buff(26, "Mage", "Regen", "Tank", 903)]);
        c = t.Count(Nobody, true, 30_000);
        Assert.True(c.Latched);
        Assert.Equal(25_000, t.Session.StartedAt);
        Assert.Equal(1, c.Support.Casts);
        Assert.Equal("Regen", Assert.Single(Assert.Single(c.Support.Casters).Actions).Name);
    }

    [Fact]
    public void A_buff_written_with_its_own_figure_as_damage_is_no_damage()
    {
        // An older addon wrote Protect as dealing 40 to the one it covered.
        var t = Armed();
        t.Feed([Lines.Hit(21, "Tank", 100), Buff(22, "Mage", "Protect", "Tank", 904, dmg: 40, hit: true)]);
        var c = t.Count(Nobody, true, 30_000);

        Assert.Equal(100, c.Totals.Total);
        Assert.Equal(new[] { "Tank" }, c.Totals.Actors.Select(a => a.Name));
        Assert.Equal(1, c.Support.Casts);
    }

    [Fact]
    public void A_cast_is_one_use_however_many_it_reached_and_a_pets_is_its_owners()
    {
        var t = Armed();
        t.Feed([
            Lines.Hit(21, "Tank", 100),
            Buff(22, "Mage", "Protectra", "Tank", 910), Buff(22, "Mage", "Protectra", "Mage", 910),
            Buff(22, "Mage", "Protectra", "Smn", 910),
            Buff(23, "Mage", "Haste", "Tank", 911), Buff(24, "Mage", "Haste", "Smn", 912),
            Buff(25, "Titan", "#564", "Tank", 913, owner: "Smn"), Buff(25, "Titan", "#564", "Mage", 913, owner: "Smn"),
        ]);
        var s = t.Count(Nobody, true, 30_000).Support;

        Assert.Equal(4, s.Casts);
        Assert.Equal(new[] { "Mage", "Smn" }, s.Casters.Select(c => c.Name));
        var mage = s.Casters[0];
        Assert.Equal(3, mage.Casts);
        // Most first.
        Assert.Equal(new[] { ("Haste", 2), ("Protectra", 1) }, mage.Actions.Select(a => (a.Name, a.Casts)));
        Assert.Equal(("Titan: #564", 1), (s.Casters[1].Actions[0].Name, s.Casters[1].Actions[0].Casts));
    }

    [Fact]
    public void Hits_and_misses_count_the_people_reached_and_casts_count_uses()
    {
        var t = Armed();
        t.Feed([
            Lines.Hit(21, "Tank", 100),
            // One Protectra reaching three: it took on two and not on the third.
            Buff(22, "Mage", "Protectra", "Tank", 960, hit: true), Buff(22, "Mage", "Protectra", "Mage", 960, hit: true),
            Buff(22, "Mage", "Protectra", "Smn", 960),
            // Two Erases, one finding nothing to remove.
            Buff(23, "Mage", "Erase", "Tank", 961, hit: true), Buff(24, "Mage", "Erase", "Tank", 962),
        ]);
        var mage = Assert.Single(t.Count(Nobody, true, 30_000).Support.Casters);

        Assert.Equal((3, 3, 2), (mage.Casts, mage.Hits, mage.Misses));
        Assert.Equal(0.6, mage.HitRate!.Value, 12);
        var protectra = mage.Actions.Single(a => a.Name == "Protectra");
        Assert.Equal((1, 2, 1), (protectra.Casts, protectra.Hits, protectra.Misses));
        Assert.Equal(2 / 3.0, protectra.HitRate!.Value, 12);
        var erase = mage.Actions.Single(a => a.Name == "Erase");
        Assert.Equal((2, 1, 1), (erase.Casts, erase.Hits, erase.Misses));
        Assert.Equal(0.5, erase.HitRate!.Value, 12);

        // Per person reached: most hits first, ties in the order reached.
        Assert.Equal(new[] { ("Tank", 1, 1, 0), ("Mage", 1, 1, 0), ("Smn", 1, 0, 1) },
            protectra.Targets.Select(p => (p.Name, p.Casts, p.Hits, p.Misses)));
        var tank = Assert.Single(erase.Targets);
        Assert.Equal(("Tank", 2, 1, 1), (tank.Name, tank.Casts, tank.Hits, tank.Misses));
        Assert.Equal(0.5, tank.HitRate!.Value, 12);
    }

    [Fact]
    public void A_cast_reaching_someone_twice_is_one_of_their_casts()
    {
        // The same use on the same person twice (two results) is one cast that
        // reached them, and two of their hits and misses.
        var t = Armed();
        t.Feed([Lines.Hit(21, "Tank", 100), Buff(22, "Mage", "Haste", "Tank", 970, hit: true), Buff(22, "Mage", "Haste", "Tank", 970)]);
        var p = Assert.Single(Assert.Single(Assert.Single(t.Count(Nobody, true, 30_000).Support.Casters).Actions).Targets);
        Assert.Equal((1, 1, 1), (p.Casts, p.Hits, p.Misses));
    }

    [Fact]
    public void A_rate_of_nothing_reached_is_none()
    {
        Assert.Null(Support.Rate(0, 0));
        Assert.Equal(1.0, Support.Rate(3, 0));
    }

    [Fact]
    public void An_excluded_characters_support_is_not_counted()
    {
        var t = Armed();
        t.Feed([Lines.Hit(21, "Tank", 100), Buff(22, "Mage", "Haste", "Tank", 920)]);

        Assert.Equal(0, t.Count(new HashSet<string> { "Mage" }, true, 30_000).Support.Casts);
        Assert.Equal(1, t.Count(Nobody, true, 30_000).Support.Casts);
    }

    [Fact]
    public void Support_ends_no_session()
    {
        var t = Armed();
        t.Feed([Lines.Hit(21, "Tank", 100), Buff(28, "Mage", "Haste", "Tank", 930)]);
        t.Count(Nobody, true, 29_000);
        t.TogglePause(35_000);

        // The end is the second the last swing landed in, not the buff's.
        Assert.Equal(22_000, t.Session.EndedAt);
    }

    [Fact]
    public void An_export_carries_support_back_among_the_events_and_it_is_filed_apart_again()
    {
        var t = Armed();
        t.Feed([Lines.Hit(21, "Tank", 100), Buff(21, "Mage", "Haste", "Tank", 940), Lines.Hit(22, "Tank", 50)]);
        t.Count(Nobody, true, 23_000);
        t.TogglePause(25_000);

        var parse = ParseFile.Import(t.Export(40_000)!);
        Assert.Equal(0, parse.Skipped);
        Assert.Equal(2, parse.Source.Events.Count);
        Assert.Equal("Haste", Assert.Single(parse.Source.Support).Action);

        var c = Tracker.Of(parse).Count(Nobody, true);
        Assert.Equal(150, c.Totals.Total);
        Assert.Equal(1, c.Support.Casts);
    }

    [Fact]
    public void A_parse_saved_before_support_was_filed_apart_counts_it_as_support()
    {
        // Older exports have buffs among the damage rows, as the file did.
        var t = Armed();
        t.Feed([Lines.Hit(21, "Tank", 100)]);
        t.Count(Nobody, true, 23_000);
        t.TogglePause(25_000);
        var text = t.Export(40_000)!;
        var buff = Buff(21, "Mage", "Protect", "Tank", 950, dmg: 40, hit: true);
        text = text.Replace("\"events\": [\n", "\"events\": [\n    " + buff + ",\n");

        var c = Tracker.Of(ParseFile.Import(text)).Count(Nobody, true);
        Assert.Equal(100, c.Totals.Total);
        Assert.Equal(1, c.Support.Casts);
    }
}
