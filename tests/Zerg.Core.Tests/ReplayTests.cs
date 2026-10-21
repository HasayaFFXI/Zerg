namespace Zerg.Core.Tests;

/// <summary>An event file read whole, cropped to a range and exported.</summary>
public class ReplayTests
{
    static readonly HashSet<string> Nobody = [];

    /// <summary>Two fights 100 s apart, a monster swinging between them, and
    /// a heal before the first: one is 100–110 s, the other 210–215 s.</summary>
    static Replay TwoFights() => Replay.Read(
    [
        Lines.Job("Hasaya", "SAM", "WAR"),
        Lines.Heal(95, "Mage", 80),
        Lines.Hit(100, "Hasaya", 300, target: "Crab"),
        Lines.Hit(104, "Tank", 0, hit: false, target: "Crab"),
        Lines.Hit(109, "Hasaya", 200, kind: "ws", action: "Tachi: Gekko", target: "Crab"),
        Lines.Hit(150, "Crab", 999, actorKind: "mob", target: "Hasaya", targetKind: "player"),
        Lines.Hit(210, "Tank", 50, target: "Goblin"),
        Lines.Hit(212, "Hasaya", 40, target: "Crab"),
        Lines.Hit(214, "Hasaya", 70, target: "Goblin"),
        "not json",
    ], "Hasaya-20261020222434.jsonl");

    [Fact]
    public void A_replay_reads_every_line_through_the_live_reader()
    {
        var r = TwoFights();
        Assert.Equal(10, r.Lines);
        Assert.Equal(1, r.Reader.Unreadable);
        Assert.Equal(7, r.Reader.Events.Count);
        Assert.Single(r.Reader.Heals);
        Assert.Equal("Hasaya", r.Reader.Roster.Owner);
        Assert.Equal("SAM/WAR", r.Reader.Roster.JobLabel("Hasaya"));
        // From the first record on the clock to the end of the last one's second.
        Assert.Equal((95_000d, 215_000d), (r.First, r.Last));
        // The monster's swing is no party damage.
        Assert.Equal(660, r.Damage);
    }

    [Fact]
    public void A_file_with_no_party_rows_has_no_damage_and_no_range()
    {
        var r = Replay.Read([Lines.Job("Hasaya", "SAM"), Lines.Hit(5, "Crab", 10, actorKind: "mob", target: "Hasaya",
                                                                  targetKind: "player")], "x.jsonl");
        Assert.Equal(0, r.Damage);
        Assert.Null(r.Crop(r.First, r.Last));
    }

    [Fact]
    public void A_crop_is_the_session_start_and_pause_would_have_made()
    {
        var r = TwoFights();
        // Armed mid-second, before the fight: the zero is the first party row,
        // the end the last party row's second, not the 200 s the range ran to.
        var s = r.Crop(97_400, 200_000)!;
        Assert.Equal(97_000, s.ArmedAt);
        Assert.Equal(100_000, s.StartedAt);
        Assert.Equal(200_000, s.PausedAt);
        Assert.Equal(110_000, s.EndedAt);
        Assert.Equal(10_000, s.Elapsed(1e12));

        // Nothing the party did falls in it: no clock.
        Assert.Null(r.Crop(120_000, 200_000));
        // A range raised to the second holds a row stamped inside it.
        Assert.Equal(215_000, r.Crop(205_000, 214_200)!.EndedAt);
    }

    [Fact]
    public void A_range_is_one_clock_with_its_quiet_stretches_on_it()
    {
        var r = TwoFights();
        var whole = r.Crop(r.First, r.Last)!;
        Assert.Empty(whole.Spans);

        var t = Tracker.Replaying("f.jsonl");
        t.Load(r, whole);
        var c = t.Count(Nobody, true, 1e12);
        // From the first party row to the end of the last one's second, the
        // hundred seconds between the fights included.
        Assert.Equal(115_000, c.Elapsed);
        Assert.Equal(660, c.Totals.Total);
        // The heal before the zero is outside the range, as it would be live.
        Assert.Empty(c.Heals);
    }

    [Fact]
    public void A_cropped_replay_exports_an_ordinary_parse_of_the_range_alone()
    {
        var r = TwoFights();
        var t = Tracker.Replaying("Hasaya-20261020222434.jsonl");
        Assert.False(t.CanExport);                 // not read yet
        t.Load(r, r.Crop(205_000, r.Last));
        Assert.True(t.CanExport);

        var parse = ParseFile.Import(t.Export(1e12)!);
        Assert.Equal([50d, 40d, 70d], parse.Source.Events.Select(e => e.Dmg));
        Assert.Empty(parse.Source.Heals);
        Assert.Equal((210_000d, 215_000d), (parse.Session.StartedAt, parse.Session.PausedAt));
        Assert.Equal("Hasaya-20261020222434.jsonl", parse.File);
        Assert.Equal("SAM/WAR", parse.Source.Roster.JobLabel("Hasaya"));

        // Counted again from the file, it is what the replay showed.
        var shown = t.Count(Nobody, true, 1e12);
        var again = Tracker.Of(parse).Count(Nobody, true, 1e12);
        Assert.Equal(shown.Totals.Total, again.Totals.Total);
        Assert.Equal(shown.Elapsed, again.Elapsed);
    }

    [Fact]
    public void A_replay_is_read_only_whatever_is_pressed()
    {
        var r = TwoFights();
        var t = Tracker.Replaying("f.jsonl");
        t.Load(r, r.Crop(r.First, r.Last));
        var before = t.Session;
        t.Start(1e12);
        t.TogglePause(1e12);
        t.Feed([Lines.Hit(300, "Hasaya", 5)]);
        Assert.Same(before, t.Session);
        Assert.Equal(7, t.Reader.Events.Count);

        // A tracker that is no replay takes no crop.
        var live = new Tracker();
        live.Crop(r.Crop(r.First, r.Last));
        Assert.Null(live.Session.StartedAt);
    }

    [Fact]
    public void Bins_are_the_party_damage_in_equal_stretches_of_the_file()
    {
        var r = TwoFights();     // 95–215 s: six bins of 20 s
        Assert.Equal([500d, 0, 0, 0, 0, 160], r.Bins(6));
        Assert.Equal(r.Damage, r.Bins(7).Sum());
        Assert.Empty(r.Bins(0));
    }

    [Fact]
    public void A_typed_time_of_day_is_found_in_the_file_whatever_day_it_falls_on()
    {
        // 23:59:00 to 00:01:00 local, a file across midnight.
        var start = new DateTime(2026, 10, 20, 23, 59, 0);
        double ms = new DateTimeOffset(start, TimeZoneInfo.Local.GetUtcOffset(start)).ToUnixTimeMilliseconds();
        var r = Replay.Read([Lines.Hit(ms / 1000, "Hasaya", 1), Lines.Hit(ms / 1000 + 119, "Hasaya", 1)], "x.jsonl");

        Assert.Equal(ms + 30_000, r.TimeOf("23:59:30"));
        Assert.Equal(ms + 90_000, r.TimeOf("0:00:30"));
        Assert.Equal(ms + 60_000, r.TimeOf(" 00:00 "));
        Assert.Null(r.TimeOf("12:00:00"));     // not in the file
        Assert.Null(r.TimeOf("25:00"));
        Assert.Null(r.TimeOf("soon"));
    }
}
