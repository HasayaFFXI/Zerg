namespace Zerg.Core;

/// <summary>
/// One support action inside a character: how many times it was cast, and
/// on how many of those it reached it took effect (Hits) or had none
/// (Misses). Casts count uses; Hits and Misses count the people reached.
/// </summary>
public sealed record SupportAction(string Name, int Casts, int Hits, int Misses, IReadOnlyList<SupportTarget> Targets)
{
    /// <summary>Hits out of everyone reached; null when it reached nobody.</summary>
    public double? HitRate => Support.Rate(Hits, Misses);
}

/// <summary>
/// One person an action reached: how many of its casts reached them, and on
/// how many of those it took effect or had none. A pet reached is a person
/// of its own, by its own name.
/// </summary>
public sealed record SupportTarget(string Name, int Casts, int Hits, int Misses)
{
    public double? HitRate => Support.Rate(Hits, Misses);
}

/// <summary>One character's support: their casts, hits and misses in all,
/// and by action, most casts first.</summary>
public sealed record SupportCaster(string Name, int Casts, int Hits, int Misses, IReadOnlyList<SupportAction> Actions)
{
    public double? HitRate => Support.Rate(Hits, Misses);
}

/// <summary>The party's support: every caster, most casts first.</summary>
public sealed record SupportTotals(IReadOnlyList<SupportCaster> Casters, int Casts)
{
    public static SupportTotals None { get; } = new([], 0);
}

/// <summary>
/// Support, from the rows <see cref="EventReader"/> files apart from the
/// damage (<see cref="EventReader.IsSupport"/>): buffs, status removals, an
/// avatar's wards. What is measured is how often each was cast and, of the
/// people it reached, on how many it took effect. A buff has no amount to
/// add up or divide: there is no total, no average and no rate of one, and
/// it starts no clock.
/// </summary>
public static class Support
{
    /// <summary>
    /// The support rows that count: <see cref="Counting.Filter"/>'s
    /// scoping, as heals have it (the session window, monsters,
    /// exclusions), a pet's credited to its owner with its own name kept
    /// ("Titan: Earthen Ward").
    /// </summary>
    public static List<CombatEvent> Filter(IReadOnlyList<CombatEvent>? rows, FilterOptions? opts = null) =>
        rows == null ? [] : Counting.Filter(rows, opts);

    /// <summary>Hits out of hits and misses, or null when there are neither.</summary>
    public static double? Rate(int hits, int misses) =>
        hits + misses > 0 ? (double)hits / (hits + misses) : null;

    /// <summary>
    /// Rows (already filtered) → casts, hits and misses per character, per
    /// action. A cast is one use however many it reached: a Protectra on six
    /// is one. Each row is one person reached, a hit when it took effect on
    /// them and a miss when it had none: that Protectra, taking on four, is
    /// four hits and two misses. Under each action, the same for each person
    /// it reached, most hits first. Ties keep the order of first cast, or of
    /// first reached.
    /// </summary>
    public static SupportTotals Totals(IReadOnlyList<CombatEvent> rows)
    {
        var by = new OrderedDictionary<string, OrderedDictionary<string, HashSet<double>>>(StringComparer.Ordinal);
        var counts = new Dictionary<(string, string), (int Casts, int Hits, int Misses)>();
        // Per action, per person reached, in the order first reached; and
        // the uses that reached each, so a cast is counted once per person.
        var reached = new Dictionary<(string, string), OrderedDictionary<string, (int Casts, int Hits, int Misses)>>();
        var reachedBy = new HashSet<(string, string, string, double)>();
        foreach (var e in rows)
        {
            if (!by.TryGetValue(e.Actor, out var actions)) by[e.Actor] = actions = new(StringComparer.Ordinal);
            if (!actions.TryGetValue(e.Action, out var uses)) actions[e.Action] = uses = [];
            var key = (e.Actor, e.Action);
            var n = counts.GetValueOrDefault(key);
            // A row with no use is a cast of its own.
            if (e.Use is not double use || uses.Add(use)) n.Casts++;
            if (e.Hit) n.Hits++;
            else n.Misses++;
            counts[key] = n;

            if (!reached.TryGetValue(key, out var people)) reached[key] = people = new(StringComparer.Ordinal);
            var p = people.GetValueOrDefault(e.Target);
            if (e.Use is not double u || reachedBy.Add((e.Actor, e.Action, e.Target, u))) p.Casts++;
            if (e.Hit) p.Hits++;
            else p.Misses++;
            people[e.Target] = p;
        }

        var casters = new List<SupportCaster>();
        foreach (var (name, actions) in by)
        {
            var list = actions.Keys.Select(a =>
            {
                var n = counts[(name, a)];
                var targets = reached[(name, a)].Select(t => new SupportTarget(t.Key, t.Value.Casts, t.Value.Hits, t.Value.Misses))
                    .ToList();
                targets.SortByDescending(t => t.Hits);
                return new SupportAction(a, n.Casts, n.Hits, n.Misses, targets);
            }).ToList();
            list.SortByDescending(a => a.Casts);
            casters.Add(new SupportCaster(name, list.Sum(a => a.Casts), list.Sum(a => a.Hits), list.Sum(a => a.Misses),
                list));
        }
        casters.SortByDescending(c => c.Casts);
        return new SupportTotals(casters, casters.Sum(c => c.Casts));
    }
}
