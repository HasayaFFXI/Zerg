namespace Zerg.Core;

/// <summary>
/// One sample of a character's delay: from one round of their auto-attacks
/// to their next, at the same target, with nothing between the two that the
/// rules discard it for (<see cref="Delays"/>).
/// </summary>
/// <param name="Actor">Whose rounds.</param>
/// <param name="From">The first row of the round the sample starts at.</param>
/// <param name="To">The first row of the round it ends at.</param>
/// <param name="Actions">How many actions of their own came between the two
/// (a weaponskill, an ability, a spell, a ranged attack), each a moment of
/// its own however many rows it wrote.</param>
public sealed record DelaySample(string Actor, CombatEvent From, CombatEvent To, int Actions)
{
    /// <summary>The time from one round to the next, in milliseconds.</summary>
    public double Ms => To.T - From.T;

    /// <summary>The target both rounds were at.</summary>
    public string Target => From.Target;
}

/// <summary>Every character's delay samples in the session, kept
/// (<see cref="Delays.Count"/>).</summary>
public sealed class DelayTotals(IReadOnlyDictionary<string, List<DelaySample>> byActor)
{
    public static readonly DelayTotals None = new(new Dictionary<string, List<DelaySample>>());

    /// <summary>The samples kept, by character, in the order they ended.</summary>
    public IReadOnlyDictionary<string, List<DelaySample>> ByActor { get; } = byActor;

    /// <summary>A character's samples that the viewer's switches leave: of
    /// the targets and the fight isolated (a sample's two rounds are at one
    /// target, so it is in or out whole), and none while melee is not among
    /// the types isolated. The characters isolated take nothing away: each
    /// row of the table is its own character's.</summary>
    public IEnumerable<DelaySample> Of(string actor, IReadOnlySet<string>? targets = null,
                                       IReadOnlySet<string>? types = null, Fight? fight = null)
    {
        if (!ByActor.TryGetValue(actor, out var all)) yield break;
        if (types is { Count: > 0 } && !types.Contains("melee")) yield break;
        bool named = targets is { Count: > 0 };
        foreach (var s in all)
        {
            if ((named || fight != null)
                && !(named && targets!.Contains(s.Target) || fight != null && fight.Holds(s.From) && fight.Holds(s.To)))
                continue;
            yield return s;
        }
    }

    /// <summary>A character's delay, in milliseconds: the average of their
    /// samples the switches leave; null with none.</summary>
    public double? Mean(string actor, IReadOnlySet<string>? targets = null,
                        IReadOnlySet<string>? types = null, Fight? fight = null)
    {
        double sum = 0;
        int n = 0;
        foreach (var s in Of(actor, targets, types, fight)) { sum += s.Ms; n++; }
        return n > 0 ? sum / n : null;
    }
}

/// <summary>
/// Delay: how long a character takes from one round of auto-attacks to the
/// next, on average. Whatever they did between two rounds (a weaponskill, an
/// ability, a spell, a ranged attack) is in it, animation and all: that is
/// time their weapon was not swinging, and the figure is meant to show it.
///
/// <para><b>A round is one packet</b>: every melee row of one character
/// with the same time. A double or triple attack, a dual-wielder's offhand,
/// a kick attack and a Zanshin are all rows of one round, and counted as
/// swings they would put a sample of 0 between them.</para>
///
/// <para><b>A sample runs from one round to the next.</b> Its first round
/// is never an engage: the file has none. A round ends one sample and
/// starts the next. A sample is discarded, and its end still starts the
/// next one, when:</para>
/// <list type="bullet">
/// <item>the first round's target died before the second (its death line
/// between them, by id), the killing round included;</item>
/// <item>the two rounds were at different targets, by id, or by name where
/// a row has none: a switch, or one that got away, with no death seen;</item>
/// <item>the character died between them;</item>
/// <item>either round is outside the session, or a pause lies between
/// them: the pause comes off the clock, but no weapon stopped for it;</item>
/// <item>it is longer than its ceiling (below).</item>
/// </list>
///
/// <para><b>The ceiling</b> is for what the file never says: a character
/// asleep, stunned, out of range, disengaged and engaged again, using an
/// item. A sample is kept up to twice the character's typical sample, and
/// once more of it for each action of their own between its rounds (a
/// weaponskill starts the swing over). Typical is the lower median of their
/// samples with no action in them, or of all of them where none is clean,
/// over the whole session, whatever the viewer isolates.</para>
///
/// <para>Asked of every row the reader holds, whatever the viewer's
/// switches, as kills are: rounds are paired before anything is isolated, or
/// a round at Kirin, a minute at the adds, and a round at Kirin again would
/// be one sample a minute long. It starts and ends no clock.</para>
/// </summary>
public static class Delays
{
    /// <summary>A sample may be this many typical samples long, and one more
    /// for each action between its rounds.</summary>
    public const double Allowance = 2;

    sealed record Round(CombatEvent First, int LastLine);

    public static DelayTotals Count(EventReader reader, Session session)
    {
        if (session.StartedAt == null) return DelayTotals.None;
        var roster = reader.Roster;

        bool Own(string actor, string kind, bool pets) => !pets && kind != "pet" && !roster.IsMob(actor);

        // Each character's rounds, in file order.
        var rounds = new Dictionary<string, List<Round>>(StringComparer.Ordinal);
        foreach (var e in reader.Events)
        {
            if (e.Kind != "melee" || !Own(e.Actor, e.ActorKind, e.IsPets)) continue;
            if (!rounds.TryGetValue(e.Actor, out var list)) rounds[e.Actor] = list = [];
            if (list.Count > 0 && list[^1].First.T == e.T) list[^1] = list[^1] with { LastLine = e.Line };
            else list.Add(new Round(e, e.Line));
        }
        if (rounds.Count == 0) return DelayTotals.None;

        // When each character acted otherwise, of their own.
        var acted = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        void Acted(string actor, double t)
        {
            if (!rounds.ContainsKey(actor)) return;
            if (!acted.TryGetValue(actor, out var list)) acted[actor] = list = [];
            list.Add(t);
        }
        foreach (var e in reader.Events)
            if (e.Kind is "ws" or "ability" or "magic" or "ranged" && Own(e.Actor, e.ActorKind, e.IsPets)) Acted(e.Actor, e.T);
        foreach (var h in reader.Heals)
            if (Own(h.Actor, h.ActorKind, h.IsPets)) Acted(h.Actor, h.T);
        foreach (var e in reader.Support)
            if (Own(e.Actor, e.ActorKind, e.IsPets)) Acted(e.Actor, e.T);
        foreach (var e in reader.Enfeebles)
            if (Own(e.Actor, e.ActorKind, e.IsPets)) Acted(e.Actor, e.T);
        foreach (var list in acted.Values) list.Sort();

        // The death lines, by monster id and by party member's name.
        var killed = new Dictionary<double, List<int>>();
        var died = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        foreach (var d in reader.Deaths)
        {
            if (d.IsKill && d.TargetId is double id)
            {
                if (!killed.TryGetValue(id, out var list)) killed[id] = list = [];
                list.Add(d.Line);
            }
            else if (d.TargetKind == "player")
            {
                if (!died.TryGetValue(d.Target, out var list)) died[d.Target] = list = [];
                list.Add(d.Line);
            }
        }

        var byActor = new Dictionary<string, List<DelaySample>>(StringComparer.Ordinal);
        foreach (var (actor, list) in rounds)
        {
            var times = acted.GetValueOrDefault(actor);
            var deaths = died.GetValueOrDefault(actor);
            var samples = new List<DelaySample>();
            for (int i = 1; i < list.Count; i++)
            {
                var (a, b) = (list[i - 1], list[i]);
                var (from, to) = (a.First, b.First);
                if (session.At(from.T) is not double at || session.At(to.T) is not double bt) continue;
                double ms = to.T - from.T;
                if (ms <= 0 || bt - at != ms) continue;   // a pause between them
                if (!SameTarget(from, to)) continue;
                if (from.TargetId is double id && Between(killed.GetValueOrDefault(id), a.LastLine, to.Line)) continue;
                if (Between(deaths, a.LastLine, to.Line)) continue;
                samples.Add(new DelaySample(actor, from, to, Moments(times, from.T, to.T)));
            }
            if (samples.Count == 0) continue;

            var clean = samples.Where(s => s.Actions == 0).Select(s => s.Ms).ToList();
            var typical = LowerMedian(clean.Count > 0 ? clean : samples.Select(s => s.Ms).ToList());
            var kept = samples.Where(s => s.Ms <= typical * (Allowance + s.Actions)).ToList();
            if (kept.Count > 0) byActor[actor] = kept;
        }
        return new DelayTotals(byActor);
    }

    /// <summary>Two rounds at one target: by id where both rows have one,
    /// by name otherwise.</summary>
    static bool SameTarget(CombatEvent a, CombatEvent b) =>
        a.TargetId is double x && b.TargetId is double y ? x == y : a.Target == b.Target;

    /// <summary>A line of <paramref name="lines"/> (in order) lies strictly
    /// between the two.</summary>
    static bool Between(List<int>? lines, int after, int before)
    {
        if (lines == null) return false;
        int i = lines.BinarySearch(after + 1);
        if (i < 0) i = ~i;
        return i < lines.Count && lines[i] < before;
    }

    /// <summary>How many different moments in <paramref name="times"/>
    /// (sorted) lie strictly between the two: one action, one moment, however
    /// many targets it reached.</summary>
    static int Moments(List<double>? times, double after, double before)
    {
        if (times == null) return 0;
        int i = times.BinarySearch(after);
        i = i < 0 ? ~i : i;
        while (i < times.Count && times[i] <= after) i++;
        int n = 0;
        double? last = null;
        for (; i < times.Count && times[i] < before; i++)
        {
            if (times[i] != last) n++;
            last = times[i];
        }
        return n;
    }

    /// <summary>The middle value, or the lower of the two middle ones: of
    /// two samples, one of them held up by something unseen, the other.</summary>
    static double LowerMedian(List<double> values)
    {
        values.Sort();
        return values[(values.Count - 1) / 2];
    }
}
