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
    /// <summary>MP spent on it: each cast's list cost, once however many it
    /// reached (<see cref="ManaCost"/>); 0 for a heal that costs none.</summary>
    public double Mp { get; set; }
    /// <summary>What the casts that cost MP healed.</summary>
    public double CostedHp { get; set; }
    /// <summary>HP healed per MP spent, or null with none spent.</summary>
    public double? PerMp { get; set; }
    /// <summary>Everyone this heal reached, largest first.</summary>
    public List<HealPart> Targets { get; set; } = [];
    /// <summary>What it overcured (<see cref="Zerg.Core.Overcure"/>): what its
    /// cures would have healed past what their targets were missing.</summary>
    public double Overcure { get; set; }
    /// <summary>What the heals Overcure could be measured on healed.</summary>
    public double CuredHp { get; set; }
    /// <summary>Overcure out of what those heals would have healed whole,
    /// 0..1; null when none could be measured.</summary>
    public double? OvercurePct { get; set; }
    /// <summary>The amount its overcure is measured against (the caster's
    /// base for the spell), or null with none.</summary>
    public double? Base { get; set; }
    /// <summary>The caster's other bases for it ("Also 196"), or null.</summary>
    public string? Also { get; set; }

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
    /// <summary>MP spent on this person: each cast that reached them charged
    /// its cost shared evenly among everyone it reached, so a Curaga II on
    /// five is 24 MP on each.</summary>
    public double Mp { get; set; }
    /// <summary>What the casts that cost MP healed this person.</summary>
    public double CostedHp { get; set; }
    /// <summary>HP healed per MP spent, or null with none spent.</summary>
    public double? PerMp { get; set; }
    /// <summary>On a person a character healed: the heals on them; on a person
    /// healed, who healed them. Largest first.</summary>
    public List<HealPart> Parts { get; set; } = [];
    /// <summary>What it overcured (<see cref="Zerg.Core.Overcure"/>): what its
    /// cures would have healed past what their targets were missing.</summary>
    public double Overcure { get; set; }
    /// <summary>What the heals Overcure could be measured on healed.</summary>
    public double CuredHp { get; set; }
    /// <summary>Overcure out of what those heals would have healed whole,
    /// 0..1; null when none could be measured.</summary>
    public double? OvercurePct { get; set; }
    /// <summary>On one heal on one person: the amount its overcure is
    /// measured against, their gear in it. Under a heal (a person it
    /// reached) only when that differs from the heal's own. Else null.</summary>
    public double? Base { get; set; }
    /// <summary>With <see cref="Base"/>, the caster's other bases, or null.</summary>
    public string? Also { get; set; }

    internal readonly HashSet<double> Uses = [];
    internal readonly OrderedDictionary<string, HealPart> ByName = new(StringComparer.Ordinal);

    /// <param name="v">What it healed this person.</param>
    /// <param name="use">The cast it belongs to.</param>
    /// <param name="mp">This person's part of the cast's MP, or null for a heal that costs none.</param>
    /// <param name="oc">What it overcured, or null when that cannot be measured.</param>
    internal void Add(double v, double? use, double? mp, double? oc)
    {
        if (oc is double o)
        {
            Overcure += o;
            CuredHp += v;
        }
        if (v > Max) Max = v;
        if (v > 0 && (Min == null || v < Min)) Min = v;
        Total += v;
        bool cast = use is not double u || Uses.Add(u);
        if (cast) Casts++;
        if (mp is not double m) return;
        CostedHp += v;
        // Once a cast, as Casts: a second result on the same person is not charged again.
        if (cast) Mp += m;
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
            p.PerMp = ManaCost.PerMp(p.CostedHp, p.Mp);
            p.OvercurePct = Healing.OvercurePct(p.Overcure, p.CuredHp);
            p.Parts = List(p.ByName);
        }
        list.SortByDescending(p => p.Total);
        return list;
    }
}

/// <summary>One character's healing, their pet's included; <see cref="PetTotal"/>
/// is the pet's part of it.</summary>
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
    /// <summary>MP spent: each cast's list cost, once; a pet's Blood Pacts are this character's MP.</summary>
    public double Mp { get; set; }
    /// <summary>What the casts that cost MP healed.</summary>
    public double CostedHp { get; set; }
    /// <summary>HP healed per MP spent, over the heals that cost MP, or null with none spent.</summary>
    public double? PerMp { get; set; }
    public List<HealAction> ActionList { get; set; } = [];
    /// <summary>Everyone this character healed, their pet's heals included,
    /// largest first, each with the heals that reached them.</summary>
    public List<HealPart> Targets { get; set; } = [];
    /// <summary>What it overcured (<see cref="Zerg.Core.Overcure"/>): what its
    /// cures would have healed past what their targets were missing.</summary>
    public double Overcure { get; set; }
    /// <summary>What the heals Overcure could be measured on healed.</summary>
    public double CuredHp { get; set; }
    /// <summary>Overcure out of what those heals would have healed whole,
    /// 0..1; null when none could be measured.</summary>
    public double? OvercurePct { get; set; }

    internal readonly OrderedDictionary<string, HealPart> ByTarget = new(StringComparer.Ordinal);
}

/// <summary>The party's healing, pets' included: every healer, largest first.</summary>
public sealed class HealTotals
{
    public List<HealerTotals> Actors { get; set; } = [];
    public double Total { get; set; }
    public double PetTotal { get; set; }
    public int Casts { get; set; }
    public double Max { get; set; }
    public double? Avg { get; set; }
    /// <summary>The party's MP spent, and what those casts healed.</summary>
    public double Mp { get; set; }
    public double CostedHp { get; set; }
    /// <summary>HP healed per MP spent, or null with none spent.</summary>
    public double? PerMp { get; set; }
    /// <summary>Heal rows in.</summary>
    public int Lines { get; set; }
    /// <summary>What it overcured (<see cref="Zerg.Core.Overcure"/>): what its
    /// cures would have healed past what their targets were missing.</summary>
    public double Overcure { get; set; }
    /// <summary>What the heals Overcure could be measured on healed.</summary>
    public double CuredHp { get; set; }
    /// <summary>Overcure out of what those heals would have healed whole,
    /// 0..1; null when none could be measured.</summary>
    public double? OvercurePct { get; set; }

    /// <summary>Everyone healed, by anyone, pets' heals included, largest
    /// first: under each who healed them, and under that with what.</summary>
    public List<HealPart> Targets { get; set; } = [];
    /// <summary>The party's healing by spell (<see cref="HealEvent.Spell"/>),
    /// whoever cast it, largest first. A cast counted once, its MP once.</summary>
    public List<HealPart> Spells { get; set; } = [];
}

/// <summary>
/// Healing, from the addon's heal lines, which never enter the damage rows: no
/// damage figure can see them and nothing here can move one.
/// <list type="bullet">
/// <item>Healing: every heal, spells, abilities and a pet's, which is credited
/// to its owner with the pet's name kept, as pet damage is.</item>
/// <item>Pet: the part of that a pet did, said apart as well.</item>
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
/// <item>MP Spent and HP/MP: each cast's list cost (<see cref="ManaCost"/>),
/// once however many it reached; HP/MP is what the casts that cost MP
/// healed over that MP, so a free heal (Chakra, a wyvern's breath) moves
/// neither. On a person, a cast's cost is shared evenly among everyone it
/// reached.</item>
/// <item>Overcure: estimated (<see cref="Zerg.Core.Overcure"/>), against each
/// caster's base for the spell, learned from the session's cures; the
/// packet does not carry the target's missing HP. Max is the raw value.</item>
/// </list>
/// </summary>
public static class Healing
{
    /// <summary>
    /// The same scoping <see cref="Counting.Filter"/> applies to damage (session
    /// window, monsters, the characters isolated), so healing and damage are always measured
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
            if (!Counting.Counts(opts.Characters, c.Actor)) continue;
            // Who it healed, and with what: asked of each heal, so a Curaga
            // that reached an isolated target and four others is the one heal
            // on that target.
            if (opts.Targets is { Count: > 0 } to && !to.Contains(c.Target)) continue;
            if (opts.Spells is { Count: > 0 } sp && !sp.Contains(c.Spell)) continue;
            if (el is double t) c = c with { T = t };
            output.Add(c);
        }
        return output;
    }

    /// <summary>The label over the healing total: "Total healing" with
    /// nothing isolated; otherwise what is, as the damage total's says it:
    /// "Healing by Sylviane", "Cure IV healing by Sylviane to Hasaya",
    /// "Healing of 2 spells by 2 casters to 3 targets" (a long name cut).</summary>
    /// <param name="by">The casters isolated, as they are drawn.</param>
    /// <param name="to">The targets isolated (the people healed), as they are drawn.</param>
    /// <param name="spells">The spells isolated.</param>
    public static string Heading(IReadOnlyCollection<string> by, IReadOnlyCollection<string>? to = null,
                                 IReadOnlyCollection<string>? spells = null)
    {
        to ??= [];
        spells ??= [];
        if (by.Count == 0 && to.Count == 0 && spells.Count == 0) return "Total healing";
        var what = spells.Count == 0 ? "Healing"
            : spells.Count == 1 ? Targets.Short(spells.First(), Targets.LabelLength) + " healing"
            : "Healing of " + Format.Int(spells.Count) + " spells";
        return what + (by.Count == 0 ? ""
            : by.Count == 1 ? " by " + Targets.Short(by.First(), Targets.LabelLength)
            : " by " + Format.Int(by.Count) + " casters") + (to.Count == 0 ? ""
            : to.Count == 1 ? " to " + Targets.Short(to.First(), Targets.LabelLength)
            : " to " + Format.Int(to.Count) + " targets");
    }

    /// <summary>The same in a sentence, with every name, for the pointer;
    /// null with nothing isolated.</summary>
    public static string? Told(IReadOnlyCollection<string> by, IReadOnlyCollection<string>? to = null,
                               IReadOnlyCollection<string>? spells = null) =>
        by.Count == 0 && to is not { Count: > 0 } && spells is not { Count: > 0 } ? null
        : "Only " + (spells is { Count: > 0 } ? "the " + string.Join(", ", spells) + " healing" : "the healing") +
          (by.Count > 0 ? " done by " + string.Join(", ", by) : "") +
          (to is { Count: > 0 } ? (by.Count > 0 ? " to " : " done to ") + string.Join(", ", to) : "");

    /// <summary>What each spell healed (<see cref="HealEvent.Spell"/>), a
    /// pet's included: the Spell button's list.</summary>
    public static Dictionary<string, double> BySpell(IEnumerable<HealEvent> heals)
    {
        var by = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var h in heals)
            by[h.Spell] = by.GetValueOrDefault(h.Spell) + (double.IsNaN(h.Hp) ? 0 : h.Hp);
        return by;
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
                                            string? target = null, Binning rule = Binning.Even) =>
        Counting.Distribution(AsEvents(target == null ? heals : heals.Where(h => h.Target == target)), actor, action, rule);

    /// <summary>Overcure out of what the measured heals would have healed
    /// whole, or null with none measured.</summary>
    internal static double? OvercurePct(double overcure, double cured) =>
        overcure + cured > 0 ? overcure / (overcure + cured) : null;

    /// <summary>Heals (already filtered) → per character, per action. Largest first.</summary>
    /// <param name="overcure">The session's bases (<see cref="Overcure.Fit"/>),
    /// which every heal's overcure is measured against; none without.</param>
    /// <param name="reach">With targets isolated, every heal of the casts
    /// <paramref name="heals"/> came from, whoever it reached: a cast's MP is
    /// shared among everyone it reached, so a Curaga II on five charged to
    /// one of them alone is a fifth of its cost. Null: the heals are whole
    /// casts (nothing is isolated, or only casters or spells, which keep
    /// a cast whole).</param>
    public static HealTotals Totals(IReadOnlyList<HealEvent> heals, Overcure? overcure = null,
                                    IReadOnlyList<HealEvent>? reach = null)
    {
        // In the order they first healed, which is the order a tie keeps.
        var by = new OrderedDictionary<string, HealerTotals>(StringComparer.Ordinal);
        var healed = new OrderedDictionary<string, HealPart>(StringComparer.Ordinal);
        var spells = new OrderedDictionary<string, HealPart>(StringComparer.Ordinal);
        double total = 0, petTotal = 0, max = 0, mpAll = 0, costedAll = 0, ocAll = 0, curedAll = 0;
        int casts = 0;
        var reached = Reached(reach ?? heals);
        // How many of those people these heals are of: all of them, but for
        // targets isolated.
        var here = reach is null ? reached : Reached(heals);

        foreach (var h in heals)
        {
            bool pet = h.IsPets;
            if (!by.TryGetValue(h.Actor, out var a)) by[h.Actor] = a = new HealerTotals { Name = h.Actor };
            if (!a.Actions.TryGetValue(h.Action, out var act))
                a.Actions[h.Action] = act = new HealAction { Name = h.Action, Via = h.Via, Pet = pet };

            var v = h.Hp == 0 || double.IsNaN(h.Hp) ? 0 : h.Hp;
            bool newCast = h.Use is not double use || act.Uses.Add(use);
            // The cast's MP, and each person's part of it: shared evenly
            // among everyone the cast reached.
            var cost = ManaCost.Of(h);
            int all = h.Use is double u && reached.TryGetValue(u, out var n) ? n : 1;
            double? each = cost is double c ? c / all : null;
            // What the cast is charged here, once: the parts of the people
            // counted, which is all of it unless targets are isolated.
            if (cost is double whole && h.Use is double uu && here.TryGetValue(uu, out var m) && m < all)
                cost = whole * m / all;

            double? oc = overcure?.Of(h);
            if (oc is double o)
            {
                act.Overcure += o;
                act.CuredHp += v;
                a.Overcure += o;
                a.CuredHp += v;
                ocAll += o;
                curedAll += v;
            }

            if (v > act.Max) act.Max = v;
            if (v > 0 && (act.Min == null || v < act.Min)) act.Min = v;
            act.Total += v;
            if (newCast) act.Casts++;
            // Both ways round: the people a heal reached, and the heals that
            // reached a person, a pet's among them under its own name.
            HealPart.Of(act.ByTarget, h.Target).Add(v, h.Use, each, oc);
            var on = HealPart.Of(a.ByTarget, h.Target);
            on.Add(v, h.Use, each, oc);
            HealPart.Of(on.ByName, h.Action).Add(v, h.Use, each, oc);
            // And from the other end: who was healed, by whom, with what.
            var got = HealPart.Of(healed, h.Target);
            got.Add(v, h.Use, each, oc);
            var from = HealPart.Of(got.ByName, h.Actor);
            from.Add(v, h.Use, each, oc);
            HealPart.Of(from.ByName, h.Action).Add(v, h.Use, each, oc);
            // And by spell, whoever cast it: the cast's whole MP, once.
            HealPart.Of(spells, h.Spell).Add(v, h.Use, cost, oc);

            // A pet's heal is its owner's healing, as its damage is their
            // damage; Pet Healing says how much of it was the pet's.
            a.Total += v;
            if (v > a.Max) a.Max = v;
            if (newCast) a.Casts++;
            total += v;
            if (v > max) max = v;
            if (newCast) casts++;
            if (pet)
            {
                a.PetTotal += v;
                petTotal += v;
            }
            // MP once a cast, however many it reached; what it healed, all of it.
            if (cost is double mp)
            {
                act.CostedHp += v;
                a.CostedHp += v;
                costedAll += v;
                if (newCast)
                {
                    act.Mp += mp;
                    a.Mp += mp;
                    mpAll += mp;
                }
            }
        }

        var actors = by.Values.Select(x =>
        {
            x.Avg = x.Casts != 0 ? x.Total / x.Casts : null;
            x.Share = total != 0 ? x.Total / total : null;
            x.PerMp = ManaCost.PerMp(x.CostedHp, x.Mp);
            x.OvercurePct = OvercurePct(x.Overcure, x.CuredHp);
            x.ActionList = x.Actions.Values.ToList();
            foreach (var y in x.ActionList)
            {
                y.Avg = y.Casts != 0 ? y.Total / y.Casts : 0;
                y.PerMp = ManaCost.PerMp(y.CostedHp, y.Mp);
                y.OvercurePct = OvercurePct(y.Overcure, y.CuredHp);
                y.Targets = HealPart.List(y.ByTarget);
                if (overcure is null) continue;
                // The base a heal is measured against, and on a person under
                // it theirs only where their gear makes it another.
                y.Base = overcure.Base(x.Name, y.Name);
                y.Also = Overcure.Also(overcure.Others(x.Name, y.Name));
                foreach (var p in y.Targets)
                    if (overcure.Base(x.Name, y.Name, p.Name) is double b && b != y.Base)
                        (p.Base, p.Also) = (b, Overcure.Also(overcure.Others(x.Name, y.Name, p.Name)));
            }
            x.ActionList.SortByDescending(p => p.Total);
            x.Targets = HealPart.List(x.ByTarget);
            // One heal on one person, By caster: its base on them.
            if (overcure is not null)
                foreach (var p in x.Targets)
                    foreach (var q in p.Parts)
                        (q.Base, q.Also) = (overcure.Base(x.Name, q.Name, p.Name),
                                            Overcure.Also(overcure.Others(x.Name, q.Name, p.Name)));
            return x;
        }).ToList();
        actors.SortByDescending(p => p.Total);

        var people = HealPart.List(healed);
        // One heal on one person, By target: its base on them.
        if (overcure is not null)
            foreach (var p in people)
                foreach (var from in p.Parts)
                    foreach (var q in from.Parts)
                        (q.Base, q.Also) = (overcure.Base(from.Name, q.Name, p.Name),
                                            Overcure.Also(overcure.Others(from.Name, q.Name, p.Name)));
        return new HealTotals
        {
            Actors = actors, Total = total, PetTotal = petTotal, Casts = casts, Max = max,
            Avg = casts != 0 ? total / casts : null, Lines = heals.Count, Targets = people, Spells = HealPart.List(spells),
            Mp = mpAll, CostedHp = costedAll, PerMp = ManaCost.PerMp(costedAll, mpAll),
            Overcure = ocAll, CuredHp = curedAll, OvercurePct = OvercurePct(ocAll, curedAll),
        };
    }

    /// <summary>How many people each cast reached, by its use: what its MP
    /// is shared among, person by person. A second result on the same
    /// person is not a second person.</summary>
    static Dictionary<double, int> Reached(IReadOnlyList<HealEvent> heals)
    {
        var people = new HashSet<(double, string)>();
        var count = new Dictionary<double, int>();
        foreach (var h in heals)
            if (h.Use is double u && people.Add((u, h.Target)))
                count[u] = count.GetValueOrDefault(u) + 1;
        return count;
    }
}
