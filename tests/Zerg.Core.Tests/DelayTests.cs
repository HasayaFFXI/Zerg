using System.Globalization;

namespace Zerg.Core.Tests;

public class DelayTests
{
    static readonly HashSet<string> Nobody = [];
    const double Zero = 1_000_000;
    static int use = 80_000;

    static string Bool(bool b) => b ? "true" : "false";
    static string Id(double? id) => id is double i ? FormattableString.Invariant($",\"targetId\":{i}") : "";

    /// <summary>One round of auto-attacks: <paramref name="swings"/> rows of
    /// one packet, at <paramref name="ms"/> past the zero.</summary>
    static string[] Round(double ms, string actor = "Hasaya", string target = "Tiger", double? id = 1,
                          int swings = 1, string action = "Attack", string? owner = null) =>
        Enumerable.Range(1, swings).Select(seq => FormattableString.Invariant(
            $"{{\"t\":{Zero + ms},\"seq\":{seq},\"use\":{++use},\"kind\":\"melee\",\"actor\":\"{actor}\",\"actorKind\":\"{(owner != null ? "pet" : "player")}\",\"action\":\"{action}\",\"actionId\":0,\"target\":\"{target}\",\"targetKind\":\"mob\"{Id(id)},\"dmg\":100,\"hit\":true,\"crit\":false,\"burst\":false,\"msg\":1{(owner != null ? $",\"owner\":\"{owner}\",\"pet\":\"{actor}\"" : "")}}}"))
        .ToArray();

    /// <summary>One of the character's own actions at a monster.</summary>
    static string Act(double ms, string kind = "ws", string actor = "Hasaya", string target = "Tiger", double? id = 1) =>
        FormattableString.Invariant(
            $"{{\"t\":{Zero + ms},\"seq\":1,\"use\":{++use},\"kind\":\"{kind}\",\"actor\":\"{actor}\",\"actorKind\":\"player\",\"action\":\"Tachi: Gekko\",\"actionId\":1,\"target\":\"{target}\",\"targetKind\":\"mob\"{Id(id)},\"dmg\":900,\"hit\":true,\"crit\":false,\"burst\":false,\"msg\":185}}");

    /// <summary>Utsusemi on oneself: a support row.</summary>
    static string Utsusemi(double ms, string actor = "Hasaya") =>
        FormattableString.Invariant(
            $"{{\"t\":{Zero + ms},\"seq\":1,\"use\":{++use},\"kind\":\"magic\",\"actor\":\"{actor}\",\"actorKind\":\"player\",\"action\":\"Utsusemi: Ni\",\"actionId\":339,\"target\":\"{actor}\",\"targetKind\":\"player\",\"dmg\":0,\"hit\":true,\"crit\":false,\"burst\":false,\"msg\":230}}");

    static string Cure(double ms, string actor = "Hasaya") =>
        FormattableString.Invariant(
            $"{{\"kind\":\"heal\",\"t\":{Zero + ms},\"seq\":1,\"use\":{++use},\"via\":\"magic\",\"actor\":\"{actor}\",\"actorKind\":\"player\",\"action\":\"Cure III\",\"actionId\":3,\"target\":\"{actor}\",\"targetKind\":\"player\",\"hp\":190,\"msg\":7}}");

    static string Kill(double ms, string target = "Tiger", double? id = 1) =>
        FormattableString.Invariant(
            $"{{\"kind\":\"death\",\"t\":{Zero + ms},\"seq\":1,\"actor\":\"Hasaya\",\"actorKind\":\"player\",\"target\":\"{target}\",\"targetKind\":\"mob\"{Id(id)},\"msg\":6}}");

    static string Died(double ms, string who = "Hasaya") =>
        string.Create(CultureInfo.InvariantCulture,
            $"{{\"kind\":\"death\",\"t\":{Zero + ms},\"seq\":1,\"target\":\"{who}\",\"targetKind\":\"player\",\"msg\":97}}");

    static Tracker Fed(params IEnumerable<string>[] lines)
    {
        var t = new Tracker();
        t.Follow("Hasaya_2026.11.04.jsonl");
        t.Start(Zero);
        t.Feed(lines.SelectMany(l => l).ToList());
        return t;
    }

    static string[] L(params string[] lines) => lines;

    static Snapshot Count(Tracker t, IReadOnlySet<string>? characters = null, IReadOnlySet<string>? targets = null,
                          IReadOnlySet<string>? types = null, Fight? fight = null) =>
        t.Count(characters ?? Nobody, true, Zero + 600_000, targets, types, fight);

    static double? DelayOf(Snapshot s, string name = "Hasaya") => s.Characters.Single(a => a.Name == name).Delay;
    static List<double> Samples(Snapshot s, string name = "Hasaya") =>
        s.Delays.ByActor.TryGetValue(name, out var l) ? l.Select(x => x.Ms).ToList() : [];

    [Fact]
    public void A_round_is_one_packet_however_many_swings_it_had()
    {
        // A triple attack, a dual-wielder's offhand and a kick are rows of
        // one round: counted as swings each would put a 0 between them.
        var t = Fed(Round(0, swings: 3), Round(3000, swings: 2), Round(6000), Round(6000, action: "Kick Attack"),
                    Round(9000, action: "Offhand Attack"));
        var s = Count(t);
        Assert.Equal(new[] { 3000.0, 3000, 3000 }, Samples(s));
        Assert.Equal(3000, DelayOf(s));
    }

    [Fact]
    public void One_round_is_no_sample_and_no_figure()
    {
        // The first round only starts a sample: the file has no engage.
        var s = Count(Fed(Round(0, swings: 2)));
        Assert.Null(DelayOf(s));
        Assert.Empty(Samples(s));
    }

    [Fact]
    public void A_sample_whose_target_died_is_discarded_even_at_the_same_spawn()
    {
        var t = Fed(Round(0), Round(3000),
                    L(Kill(3800)),
                    // The same spawn point, respawned: the same id, another life.
                    Round(6400), Round(9400));
        Assert.Equal(new[] { 3000.0, 3000 }, Samples(Count(t)));
    }

    [Fact]
    public void A_target_killed_by_someone_else_discards_too()
    {
        var t = Fed(Round(0), Round(3000), Round(3000, actor: "Kanaria"),
                    L(Kill(3900)),
                    Round(7000, target: "Crab", id: 2), Round(10_000, target: "Crab", id: 2));
        Assert.Equal(new[] { 3000.0, 3000 }, Samples(Count(t)));
    }

    [Fact]
    public void A_sample_across_two_targets_is_discarded_with_no_death_seen()
    {
        var t = Fed(Round(0), Round(3000), Round(6000, target: "Crab", id: 2), Round(9000, target: "Crab", id: 2));
        Assert.Equal(new[] { 3000.0, 3000 }, Samples(Count(t)));

        // Two of one name are two targets by their ids.
        var u = Fed(Round(0), Round(3000), Round(6000, id: 7), Round(9000, id: 7));
        Assert.Equal(new[] { 3000.0, 3000 }, Samples(Count(u)));

        // With no id, the name is all there is.
        var v = Fed(Round(0, id: null), Round(3000, id: null), Round(6000, id: null));
        Assert.Equal(new[] { 3000.0, 3000 }, Samples(Count(v)));
    }

    [Fact]
    public void A_sample_the_character_died_in_is_discarded()
    {
        var t = Fed(Round(0), Round(3000), Round(6000), L(Died(6500)), Round(9000), Round(12_000));
        Assert.Equal(new[] { 3000.0, 3000, 3000 }, Samples(Count(t)));
    }

    [Fact]
    public void A_weaponskill_between_two_rounds_is_in_the_sample()
    {
        var t = Fed(Round(0), Round(3000), Round(6000), L(Act(7000)), Round(11_000));
        var s = Count(t);
        Assert.Equal(new[] { 3000.0, 3000, 5000 }, Samples(s));
        Assert.Equal(11_000 / 3.0, DelayOf(s)!.Value, 9);
        Assert.Equal(1, s.Delays.ByActor["Hasaya"][2].Actions);
    }

    [Fact]
    public void A_sample_past_its_ceiling_is_discarded()
    {
        // Twice the typical sample: asleep, out of range, disengaged; the
        // file never says which.
        var t = Fed(Round(0), Round(3000), Round(6000), Round(9000), Round(15_001), Round(18_001));
        Assert.Equal(new[] { 3000.0, 3000, 3000, 3000 }, Samples(Count(t)));

        // Exactly twice is kept.
        var u = Fed(Round(0), Round(3000), Round(6000), Round(12_000));
        Assert.Equal(new[] { 3000.0, 3000, 6000 }, Samples(Count(u)));
    }

    [Fact]
    public void Each_own_action_between_raises_the_ceiling_by_one_typical_sample()
    {
        // 7 s with nothing between: past 2 × 3 s.
        var bare = Fed(Round(0), Round(3000), Round(6000), Round(13_000));
        Assert.Equal(new[] { 3000.0, 3000 }, Samples(Count(bare)));

        // With an action between, 3 × 3 s: a spell, a buff, a heal or a
        // ranged attack as much as a weaponskill or an ability.
        foreach (var act in new[] { Act(8000), Act(8000, "ability"), Act(8000, "magic"), Act(8000, "ranged"),
                                    Utsusemi(8000), Cure(8000) })
        {
            var t = Fed(Round(0), Round(3000), Round(6000), L(act), Round(13_000));
            Assert.Equal(new[] { 3000.0, 3000, 7000 }, Samples(Count(t)));
        }

        // An area weaponskill is one action, however many rows it wrote.
        var aoe = Fed(Round(0), Round(3000), Round(6000), L(Act(8000), Act(8000, target: "Crab", id: 2)), Round(16_000));
        Assert.Equal(new[] { 3000.0, 3000 }, Samples(Count(aoe)));
        var two = Fed(Round(0), Round(3000), Round(6000), L(Act(8000), Act(9000, "ability")), Round(16_000));
        Assert.Equal(new[] { 3000.0, 3000, 10_000 }, Samples(Count(two)));
    }

    [Fact]
    public void The_typical_sample_is_the_lower_median_of_the_clean_ones()
    {
        // Two samples, one of them held up: the shorter is typical.
        var t = Fed(Round(0), Round(3000), Round(33_000));
        Assert.Equal(new[] { 3000.0 }, Samples(Count(t)));

        // A sample with a weaponskill in it is not typical of a swing.
        var u = Fed(Round(0), Round(3000), L(Act(4000)), Round(9000), L(Act(10_000)), Round(15_000),
                    L(Act(16_000)), Round(21_000));
        Assert.Equal(new[] { 3000.0, 6000, 6000, 6000 }, Samples(Count(u)));
    }

    [Fact]
    public void Rounds_outside_the_session_or_across_a_pause_are_no_sample()
    {
        var t = new Tracker();
        t.Follow("Hasaya_2026.11.04.jsonl");
        t.Start(Zero);
        t.Feed([.. Round(0), .. Round(3000), .. Round(6000), .. Round(9000)]);
        t.Count(Nobody, true, Zero + 9000);
        t.TogglePause(Zero + 9500);
        t.TogglePause(Zero + 10_000);
        // 3 s of wall clock with half a second of pause in it.
        t.Feed([.. Round(12_000), .. Round(15_000)]);
        var s = t.Count(Nobody, true, Zero + 16_000);
        Assert.Equal(new[] { 3000.0, 3000, 3000, 3000 }, Samples(s));
        Assert.Equal(1, s.Delays.ByActor["Hasaya"].Count(x => x.From.T == Zero + 12_000));
    }

    [Fact]
    public void A_pets_swings_are_not_its_owners_rounds()
    {
        var t = Fed(Round(0), Round(1000, actor: "Fluffikins", owner: "Hasaya"), Round(3000),
                    Round(4000, actor: "Fluffikins", owner: "Hasaya"), Round(6000));
        var s = Count(t);
        Assert.Equal(new[] { 3000.0, 3000 }, Samples(s));
        Assert.False(s.Delays.ByActor.ContainsKey("Fluffikins"));
    }

    [Fact]
    public void Rounds_are_paired_before_anything_is_isolated()
    {
        // Kirin, the adds, Kirin again: isolated, Kirin's rounds must not
        // be joined across the adds into one long sample.
        var t = Fed(Round(0, target: "Kirin", id: 9), Round(3000, target: "Kirin", id: 9),
                    Round(6000, target: "Crab", id: 2), Round(9000, target: "Crab", id: 2), Round(13_000, target: "Crab", id: 2),
                    Round(15_000, target: "Kirin", id: 9), Round(19_000, target: "Kirin", id: 9));
        var kirin = new HashSet<string> { "Kirin" };
        Assert.Equal(3500, DelayOf(Count(t, targets: kirin)));
        Assert.Equal(3500, DelayOf(Count(t, targets: new HashSet<string> { "Crab" })));
        Assert.Equal(3500, DelayOf(Count(t)));
    }

    [Fact]
    public void Types_without_melee_leave_no_figure()
    {
        var t = Fed(Round(0), Round(3000), L(Act(4000)), Round(8000));
        Assert.Null(DelayOf(Count(t, types: new HashSet<string> { "ws" })));
        Assert.Equal(4000, DelayOf(Count(t, types: new HashSet<string> { "ws", "melee" })));
    }

    [Fact]
    public void One_fight_isolated_is_its_own_samples()
    {
        var t = Fed(Round(0), Round(3000), L(Kill(3800)), Round(6400), Round(10_400), L(Kill(11_000)));
        var fights = Count(t).Kills.Fights;
        Assert.Equal(3000, DelayOf(Count(t, fight: fights[0])));
        Assert.Equal(4000, DelayOf(Count(t, fight: fights[1])));
    }

    [Fact]
    public void Isolating_characters_leaves_each_row_its_own_figure()
    {
        var t = Fed(Round(0), Round(0, actor: "Kanaria"), Round(3000), Round(4000, actor: "Kanaria"), Round(6000));
        var s = Count(t, characters: new HashSet<string> { "Hasaya" });
        Assert.Equal(3000, DelayOf(s));
        Assert.Equal(4000, DelayOf(s, "Kanaria"));
    }

    [Fact]
    public void By_target_a_characters_delay_is_of_their_samples_at_it()
    {
        var t = Fed(Round(0), Round(3000), Round(6000, target: "Crab", id: 2), Round(10_000, target: "Crab", id: 2));
        var groups = Count(t).TargetGroups;
        Assert.Equal(3000, groups.Single(g => g.Name == "Tiger").Characters.Single().Delay);
        Assert.Equal(4000, groups.Single(g => g.Name == "Crab").Characters.Single().Delay);
    }

    [Fact]
    public void A_parse_exported_and_opened_again_reads_the_same_delay()
    {
        var t = Fed(Round(0), Round(3000), L(Act(4000)), Round(8000), L(Kill(8500)),
                    Round(12_000, target: "Crab", id: 2), Round(15_500, target: "Crab", id: 2));
        var before = DelayOf(Count(t));
        t.TogglePause(Zero + 20_000);
        var text = t.Export(Zero + 21_000)!;
        var opened = Tracker.Of(ParseFile.Import(text));
        Assert.Equal(before, opened.Count(Nobody, true).Characters.Single().Delay);
        Assert.Equal(11_500 / 3.0, before!.Value, 9);
    }
}
