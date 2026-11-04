namespace Zerg.Core;

/// <summary>
/// One target of the Overview pane's By target tab: what the characters who
/// count dealt it, the split columns of that damage, and every character's
/// own figures against it.
/// </summary>
/// <param name="Name">The target's name; never "" (<see cref="Targets.Pickable"/>).</param>
/// <param name="Total">What it took from the characters who count: those
/// isolated, or everyone. The same figure as its line of By target.</param>
/// <param name="Party">The split columns of that damage, as the Party line
/// reads them with this target alone isolated.</param>
/// <param name="Characters">Every character who acted at it, isolated or
/// not, largest first, each with their share of what it took from everyone.</param>
public sealed record TargetGroup(string Name, double Total, SplitFigures Party, IReadOnlyList<ActorTotals> Characters);

/// <summary>
/// The damage grouped by target: the per-character table again under
/// each target's name.
///
/// <para>A row is asked its target before its use is folded, as isolating
/// and <see cref="Counting.ByTarget"/> ask it, so a weaponskill that reached
/// three targets is one use at each, of what it dealt to that one. A
/// character's figures under a target are then exactly what their row of
/// the per-character table reads with that target alone isolated.</para>
/// </summary>
public static class TargetGroups
{
    /// <summary>
    /// Every target something landed on or was tried at, in the order they
    /// were first acted at; rows that name no target are not grouped
    /// (there is nothing to pick). <paramref name="events"/> are everyone's:
    /// the characters' lines list everyone, as the per-character table
    /// does, and the target's own figures are of <paramref name="who"/>
    /// alone, as its line of By target is.
    /// </summary>
    /// <param name="who">The characters isolated, or null for everyone.</param>
    public static List<TargetGroup> Of(IReadOnlyList<CombatEvent> events, IReadOnlySet<string>? who)
    {
        var rows = new OrderedDictionary<string, List<CombatEvent>>(StringComparer.Ordinal);
        foreach (var e in events)
        {
            if (e.Target.Length == 0) continue;
            if (!rows.TryGetValue(e.Target, out var list)) rows[e.Target] = list = [];
            list.Add(e);
        }
        var groups = new List<TargetGroup>(rows.Count);
        foreach (var (name, list) in rows)
        {
            var everyone = Counting.Aggregate(list);
            var theirs = who is null ? everyone
                : Counting.Aggregate(list.Where(e => who.Contains(e.Actor)).ToList());
            groups.Add(new TargetGroup(name, theirs.Total, theirs.Party, everyone.Actors));
        }
        return groups;
    }
}
