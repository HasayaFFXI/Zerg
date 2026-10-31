namespace Zerg.Core;

/// <summary>One healing action inside a character.</summary>
public sealed class HealAction
{
    public string Name { get; set; } = "";
    public string Via { get; set; } = "";
    /// <summary>A pet's heal (its first heal said so).</summary>
    public bool Pet { get; set; }
    public double Total { get; set; }
    public int Casts { get; set; }
    /// <summary>The smallest heal above zero, or null with none.</summary>
    public double? Min { get; set; }
    public double Max { get; set; }
    public double Avg { get; set; }
    /// <summary>Everyone this heal reached, largest first.</summary>
    public List<HealPart> Targets { get; set; } = [];

    internal readonly HashSet<double> Uses = [];
    internal readonly OrderedDictionary<string, HealPart> ByTarget = new(StringComparer.Ordinal);
}

/// <summary>
/// A piece of the party's heals: one person a character healed (with the
/// heals that reached them in <see cref="Parts"/>), or one heal on one person;
/// or one person healed by anyone (with who healed them in <see cref="Parts"/>,
/// and under each, with what). The figures are <see cref="HealAction"/>'s,
/// over that piece alone.
/// </summary>
public sealed class HealPart
{
    /// <summary>The person, or the heal.</summary>
    public string Name { get; set; } = "";
    public double Total { get; set; }
    /// <summary>The casts that reached this person.</summary>
    public int Casts { get; set; }
    public double? Min { get; set; }
    public double Max { get; set; }
    public double Avg { get; set; }
    /// <summary>On a person a character healed: the heals on them; on a person
    /// healed, who healed them. Largest first.</summary>
    public List<HealPart> Parts { get; set; } = [];

    internal readonly HashSet<double> Uses = [];
    internal readonly OrderedDictionary<string, HealPart> ByName = new(StringComparer.Ordinal);

    internal void Add(double v, double? use)
    {
        if (v > Max) Max = v;
        if (v > 0 && (Min == null || v < Min)) Min = v;
        Total += v;
        if (use is not double u || Uses.Add(u)) Casts++;
    }

    internal static HealPart Of(OrderedDictionary<string, HealPart> by, string name) =>
        by.TryGetValue(name, out var p) ? p : by[name] = new HealPart { Name = name };

    /// <summary>The parts as listed: averages set, largest first, a tie in the
    /// order they first came.</summary>
    internal static List<HealPart> List(OrderedDictionary<string, HealPart> by)
    {
        var list = by.Values.ToList();
        foreach (var p in list)
        {
            p.Avg = p.Casts != 0 ? p.Total / p.Casts : 0;
            p.Parts = List(p.ByName);
        }
        list.SortByDescending(p => p.Total);
        return list;
    }
}

/// <summary>One character's healing. Pet heals are in <see cref="PetTotal"/> only.</summary>
public sealed class HealerTotals
{
    public string Name { get; set; } = "";
    public double Total { get; set; }
    public double PetTotal { get; set; }
    public int Casts { get; set; }
    public double Max { get; set; }
    /// <summary>Heals by name, in order of first cast.</summary>
    public OrderedDictionary<string, HealAction> Actions { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Total over casts, or null with no casts.</summary>
    public double? Avg { get; set; }
    /// <summary>Part of the party's healing, 0..1, or null when the party healed nothing.</summary>
    public double? Share { get; set; }
    public List<HealAction> ActionList { get; set; } = [];
    /// <summary>Everyone this character healed, their pet's heals included,
    /// largest first, each with the heals that reached them.</summary>
    public List<HealPart> Targets { get; set; } = [];

    internal readonly OrderedDictionary<string, HealPart> ByTarget = new(StringComparer.Ordinal);
}

/// <summary>The party's healing: every healer, largest first (pet heals included in the order).</summary>
public sealed class HealTotals
{
    public List<HealerTotals> Actors { get; set; } = [];
    public double Total { get; set; }
    public double PetTotal { get; set; }
    public int Casts { get; set; }
    public double Max { get; set; }
    public double? Avg { get; set; }
    /// <summary>Heal rows in.</summary>
    public int Lines { get; set; }
    /// <summary>Everyone healed, by anyone, pets' heals included, largest
    /// first: under each who healed them, and under that with what.</summary>
    public List<HealPart> Targets { get; set; } = [];
}

/// <summary>
/// Healing, from the addon's heal lines, which never enter the damage rows: no
/// damage figure can see them and nothing here can move one.
/// <list type="bullet">
/// <item>Healing: every heal not from a pet (spells and abilities).</item>
/// <item>Pet: every heal a pet did, credited to its owner with the pet's name
/// kept, as pet damage is.</item>
/// <item>Casts: one per action however many targets; a Curaga on five is one.</item>
/// <item>Average: total over casts.</item>
/// <item>Min / Max: one target's heal, raw. Min is the lowest above zero, so a
/// cure on a full-health target does not make every minimum read 0.</item>
/// <item>Per person (a heal's <see cref="HealAction.Targets"/>, a character's
/// <see cref="HealerTotals.Targets"/>): the same figures over what reached that
/// person alone. Casts are the casts that reached them, so a Curaga on five is
/// a cast for each of the five; Avg is what one of those casts healed them.</item>
/// <item>Per person healed (<see cref="HealTotals.Targets"/>): everything that
/// reached them, pets' heals included, then by whom and with what.</item>
/// </list>
/// <para><b>No overcure, on purpose.</b> The packet does not carry the target's
/// missing HP, so overcure could only be estimated (against the best a spell
/// had done so far), which forgives early casts and charges every cast after a
/// buffed one. It was tried and removed as not accurate enough. Don't bring it
/// back without a real source for missing HP; Max is the raw value for the
/// same reason.</para>
/// </summary>
public static class Healing
{
    /// <summary>
    /// The same scoping <see cref="Counting.Filter"/> applies to damage (session
    /// window, monsters, exclusions), so healing and damage are always measured
    /// over the same window. Copies, on the session clock, with a pet's heal
    /// credited to its owner. Skillchains do not apply.
    /// </summary>
    public static List<HealEvent> Filter(IReadOnlyList<HealEvent>? heals, FilterOptions? opts = null)
    {
        opts ??= new FilterOptions();
        var sn = opts.Session;
        var output = new List<HealEvent>();
        if (heals == null || (sn != null && sn.StartedAt == null)) return output;
        foreach (var h in heals)
        {
            double? el = null;
            if (sn != null)
            {
                el = sn.At(h.T);
                if (el == null) continue;
            }
            if (opts.Roster != null && opts.Roster.IsMob(h.Actor)) continue;
            var c = h;
            if (Counting.Credited(h.Owner, h.Actor, h.Pet) is { } by)
                c = c with { Actor = h.Owner!, By = by, Action = Counting.Credited(by, h.Action), Spell = h.Spell };
            if (opts.Actors != null && opts.Actors.TryGetValue(c.Actor, out var on) && !on) continue;
            if (el is double t) c = c with { T = t };
            output.Add(c);
        }
        return output;
    }

    /// <summary>
    /// The heals as rows the cumulative line and the distribution can read
    /// unchanged: the amount is the HP healed, and every one is a hit. A cure
    /// on a target at full health healed 0; it did not miss.
    /// </summary>
    public static List<CombatEvent> AsEvents(IEnumerable<HealEvent> heals) =>
        heals.Select(h => new CombatEvent
        {
            T = h.T, Use = h.Use, Kind = "heal", Actor = h.Actor, Action = h.Action,
            Target = h.Target, Dmg = h.Hp, Hit = true, Owner = h.Owner,
        }).ToList();

    /// <summary>
    /// What a drill-down draws for <paramref name="actor"/>'s heals: of one
    /// heal, or all of them with <paramref name="action"/> null; on everyone it
    /// reached, or on <paramref name="target"/> alone, each cast then what it
    /// healed that one person.
    /// </summary>
    public static Distribution Distribution(IEnumerable<HealEvent> heals, string actor, string? action,
                                            string? target = null) =>
        Counting.Distribution(AsEvents(target == null ? heals : heals.Where(h => h.Target == target)), actor, action);

    /// <summary>Heals (already filtered) → per character, per action. Largest first.</summary>
    public static HealTotals Totals(IReadOnlyList<HealEvent> heals)
    {
        // In the order they first healed, which is the order a tie keeps.
        var by = new OrderedDictionary<string, HealerTotals>(StringComparer.Ordinal);
        var healed = new OrderedDictionary<string, HealPart>(StringComparer.Ordinal);
        double total = 0, petTotal = 0, max = 0;
        int casts = 0;

        foreach (var h in heals)
        {
            bool pet = h.IsPets;
            if (!by.TryGetValue(h.Actor, out var a)) by[h.Actor] = a = new HealerTotals { Name = h.Actor };
            if (!a.Actions.TryGetValue(h.Action, out var act))
                a.Actions[h.Action] = act = new HealAction { Name = h.Action, Via = h.Via, Pet = pet };

            var v = h.Hp == 0 || double.IsNaN(h.Hp) ? 0 : h.Hp;
            bool newCast = h.Use is not double use || act.Uses.Add(use);

            if (v > act.Max) act.Max = v;
            if (v > 0 && (act.Min == null || v < act.Min)) act.Min = v;
            act.Total += v;
            if (newCast) act.Casts++;
            // Both ways round: the people a heal reached, and the heals that
            // reached a person, a pet's among them under its own name.
            HealPart.Of(act.ByTarget, h.Target).Add(v, h.Use);
            var on = HealPart.Of(a.ByTarget, h.Target);
            on.Add(v, h.Use);
            HealPart.Of(on.ByName, h.Action).Add(v, h.Use);
            // And from the other end: who was healed, by whom, with what.
            var got = HealPart.Of(healed, h.Target);
            got.Add(v, h.Use);
            var from = HealPart.Of(got.ByName, h.Actor);
            from.Add(v, h.Use);
            HealPart.Of(from.ByName, h.Action).Add(v, h.Use);

            if (pet)
            {
                a.PetTotal += v;
                petTotal += v;
            }
            else
            {
                a.Total += v;
                if (v > a.Max) a.Max = v;
                if (newCast) a.Casts++;
                total += v;
                if (v > max) max = v;
                if (newCast) casts++;
            }
        }

        var actors = by.Values.Select(x =>
        {
            x.Avg = x.Casts != 0 ? x.Total / x.Casts : null;
            x.Share = total != 0 ? x.Total / total : null;
            x.ActionList = x.Actions.Values.ToList();
            foreach (var y in x.ActionList)
            {
                y.Avg = y.Casts != 0 ? y.Total / y.Casts : 0;
                y.Targets = HealPart.List(y.ByTarget);
            }
            x.ActionList.SortByDescending(p => p.Total);
            x.Targets = HealPart.List(x.ByTarget);
            return x;
        }).ToList();
        actors.SortByDescending(p => p.Total + p.PetTotal);

        return new HealTotals
        {
            Actors = actors, Total = total, PetTotal = petTotal, Casts = casts, Max = max,
            Avg = casts != 0 ? total / casts : null, Lines = heals.Count, Targets = HealPart.List(healed),
        };
    }
}
