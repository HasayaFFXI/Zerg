namespace Zerg.Core;

/// <summary>
/// Overcure: the HP a cure would have healed past what the target was
/// missing. The heal lines carry what landed, never the missing HP, so it is
/// worked out from the cures alone: what each caster's spell heals when
/// nothing cuts it short is learned from the amounts that keep recurring, and
/// a cast that healed less than that was cut short by the target's HP.
///
/// <para>A cure's full amount (LandSandBoat <c>getCureFinal</c>) is
/// <c>floor(floor(base × dayWeather) × 2 under Divine Seal)</c>, times the
/// target's own cure-received gear. Day and weather are not in the file and
/// the game day is not worked out from the clock (a server may keep its own),
/// so every amount the server's sums allow counts as whole. Nothing else is
/// modelled (an obi, Iridescence, Rapture, Healer's Roll).</para>
/// <list type="number">
/// <item>Day/weather: a Light cure (a Cure or Curaga by magic) may carry one
/// of the server's sums (weather none, ±0.10 or ±0.25, plus day none or
/// ±0.10, bonuses first, kept to 0..1.4). Other heals have none.</item>
/// <item>A target's gear: what they get over what others get from the
/// same spell, the median over the casters and spells that reached them;
/// 1 when nothing says otherwise.</item>
/// <item>Bases, per caster and spell: amounts that recur (three times on two
/// people, twice in one cast, or three times and 15 % of one person's). One
/// that is a day/weather or sealed amount of a commoner one (landed at least
/// half as often) is not a base. A day/weather step may do that only if it
/// shows in two casters' or spells' cures; in one alone it is a coincidence
/// and the amount a base of its own.</item>
/// <item>Divine Seal doubles a whole cast: a cast with a result above every
/// unsealed amount and on twice one is sealed, every target of it.</item>
/// <item>A cast on any full amount (any base, any day/weather, sealed or
/// not, times the target's gear) or above all of them is whole: no
/// overcure.</item>
/// <item>A cast below them was cut short. It is charged against the plain
/// base, never a day/weather amount: the base's own amount on that target
/// above what it healed, less what it healed. With two bases (the caster's
/// cure changed), each is weighed by its share of the whole casts: the
/// target's own, and everyone else's worth <see cref="Prior"/> casts.</item>
/// </list>
/// <para>A spell with no base (cast once, or never twice alike) has no
/// overcure: a dash, and what it healed is left out of Overcure %.</para>
/// </summary>
public sealed class Overcure
{
    /// <summary>How many casts everyone else's casts of a spell are worth
    /// when weighing one target's bases.</summary>
    public const double Prior = 5;

    /// <summary>The cures day and weather move: Light.</summary>
    static readonly HashSet<string> Light = new(StringComparer.Ordinal)
    {
        "Cure", "Cure II", "Cure III", "Cure IV", "Cure V", "Cure VI",
        "Curaga", "Curaga II", "Curaga III", "Curaga IV", "Curaga V", "Cura", "Cura II", "Cura III",
    };

    /// <summary>The server's sum of a weather and a day term, in its order
    /// (bonuses then penalties), kept to 0..1.4.</summary>
    static double DayWeather(double weather, double day)
    {
        double dw = 1.0;
        if (weather > 0) dw += weather;
        if (day > 0) dw += day;
        if (weather < 0) dw += weather;
        if (day < 0) dw += day;
        return Math.Min(Math.Max(dw, 0.0), 1.4);
    }

    /// <summary>Every day/weather multiplier, 1 first, the rest ascending.</summary>
    static readonly double[] Multipliers = BuildMultipliers();

    static double[] BuildMultipliers()
    {
        var set = new SortedSet<double>();
        foreach (var w in new[] { 0, .1, .25, -.1, -.25 })
            foreach (var d in new[] { 0, .1, -.1 })
                set.Add(DayWeather(w, d));
        return [1.0, .. set.Where(m => m != 1.0)];
    }

    static readonly double[] Plain = [1.0];

    readonly record struct Spell(string Caster, string Name);
    readonly record struct Hit(int Base, double Mult);

    /// <summary>One caster's spell on one person, worked out.</summary>
    sealed class OnTarget
    {
        /// <summary>The bases' odds on this person; empty with nothing to charge against.</summary>
        public Dictionary<int, double> Odds = [];
        public double Gear = 1;
        /// <summary>The base most casts landed on, on this person.</summary>
        public double? Base;
        /// <summary>The others, on this person, ascending.</summary>
        public List<double> Others = [];
        /// <summary>Every full amount on this person, unsealed and sealed
        /// (<see cref="Amounts"/>), and the largest of each.</summary>
        public HashSet<int> Plain = [], Sealed = [];
        public int PlainTop, SealedTop;
    }

    readonly Dictionary<Spell, List<int>> bases = [];
    /// <summary>Per spell, the base most whole casts landed on.</summary>
    readonly Dictionary<Spell, int> shown = [];
    readonly Dictionary<(Spell, double, int), Dictionary<int, Hit>> amounts = [];
    readonly Dictionary<Spell, double[]> mults = [];
    readonly Dictionary<string, double> gear = new(StringComparer.Ordinal);
    readonly HashSet<(Spell, double)> sealedUses = [];
    readonly Dictionary<(Spell, string), OnTarget> on = [];

    /// <summary>Of a fit renamed (<see cref="Renamed"/>): the fit, what each
    /// renamed heal was charged, and the names each new caster stands for.</summary>
    Overcure? fitted;
    Dictionary<HealEvent, double?>? charged;
    Dictionary<string, List<string>>? called;

    /// <summary>No heals: nothing known, nothing charged.</summary>
    public static Overcure None { get; } = new();

    Overcure() { }

    /// <summary>The same bases for heals credited to other names after the
    /// fit (Compare's rows by job, or a character matched by hand):
    /// <paramref name="renamed"/>[i] is <paramref name="heals"/>[i] under
    /// another caster's name. Each is charged as its own caster's heal was,
    /// so two characters of one job are never measured against one base. A
    /// new caster's base is their characters' where they share one, else none.</summary>
    public Overcure Renamed(IReadOnlyList<HealEvent> heals, IReadOnlyList<HealEvent> renamed)
    {
        var o = new Overcure
        {
            fitted = this,
            charged = new Dictionary<HealEvent, double?>(ReferenceEqualityComparer.Instance),
            called = new Dictionary<string, List<string>>(StringComparer.Ordinal),
        };
        for (int i = 0; i < heals.Count; i++)
        {
            o.charged[renamed[i]] = Of(heals[i]);
            if (!o.called.TryGetValue(renamed[i].Actor, out var names)) o.called[renamed[i].Actor] = names = [];
            if (!names.Contains(heals[i].Actor)) names.Add(heals[i].Actor);
        }
        return o;
    }

    /// <summary>Works out every caster's bases from <paramref name="heals"/>
    /// (credited, as <see cref="Healing.Filter"/> gives them): the whole
    /// session's, so a range or a filter is charged against the same bases.</summary>
    public static Overcure Fit(IReadOnlyList<HealEvent> heals)
    {
        var o = new Overcure();
        if (heals.Count == 0) return o;
        var groups = new Dictionary<Spell, List<HealEvent>>();
        foreach (var h in heals)
        {
            var k = new Spell(h.Actor, h.Action);
            if (!groups.TryGetValue(k, out var list)) groups[k] = list = [];
            list.Add(h);
        }
        foreach (var (k, rows) in groups)
            o.mults[k] = Light.Contains(rows[0].Spell) && rows[0].Via == "magic" && !rows[0].IsPets ? Multipliers : Plain;

        // A target's gear, from bases read on the raw amounts.
        var ratios = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        foreach (var (k, rows) in groups)
        {
            var raw = o.FindBases(rows, k, Hp, null);
            if (raw.Count == 0) continue;
            var per = new Dictionary<string, Dictionary<int, int>>(StringComparer.Ordinal);
            foreach (var r in rows)
                if (Hp(r) > 0) Bump(Of(per, r.Target), Hp(r));
            foreach (var (t, c) in per)
            {
                var rec = c.Where(x => x.Value >= 3).Select(x => x.Key).ToList();
                if (rec.Count == 0) continue;
                int v = rec.Max();
                int near = raw.MinBy(b => Math.Abs(b - v));
                double q = (double)v / near;
                if (q < 0.8 || q > 1.35) continue;
                if (o.mults[k].Any(m => m != 1.0 && Math.Abs(q - m) < 0.005)) continue;
                if (!ratios.TryGetValue(t, out var list)) ratios[t] = list = [];
                for (int i = 0; i < c[v]; i++) list.Add(q);
            }
        }
        foreach (var (t, q) in ratios) o.gear[t] = Median(q);

        // Bases on gear-free amounts, then again letting only the day/weather
        // steps that show in two casters' or spells' cures explain one away.
        int Norm(HealEvent r) => (int)Math.Round(r.Hp / o.Gear(r.Target), MidpointRounding.ToEven);
        foreach (var (k, rows) in groups) o.bases[k] = o.FindBases(rows, k, Norm, null);
        o.amounts.Clear();
        var landedBy = new Dictionary<double, HashSet<Spell>>();
        foreach (var (k, rows) in groups)
            foreach (var r in rows)
                if (o.Amounts(k, o.Gear(r.Target), 1).TryGetValue(Hp(r), out var hit) && hit.Mult != 1.0)
                {
                    if (!landedBy.TryGetValue(hit.Mult, out var set)) landedBy[hit.Mult] = set = [];
                    set.Add(k);
                }
        var seen = landedBy.Where(x => x.Value.Count >= 2).Select(x => x.Key).ToHashSet();
        foreach (var (k, rows) in groups) o.bases[k] = o.FindBases(rows, k, Norm, seen);
        o.amounts.Clear();

        // Divine Seal, by whole cast.
        foreach (var (k, rows) in groups)
        {
            if (o.bases[k].Count == 0) continue;
            foreach (var r in rows)
            {
                if (r.Use is not double u) continue;
                var g = o.Gear(r.Target);
                if (Hp(r) > o.Amounts(k, g, 1).Keys.Max() && o.Amounts(k, g, 2).ContainsKey(Hp(r)))
                    o.sealedUses.Add((k, u));
            }
        }

        // Each base's odds, per person: the whole casts that landed on it.
        foreach (var (k, rows) in groups)
        {
            var B = o.bases[k];
            var all = new Dictionary<int, double>();
            var own = new Dictionary<string, Dictionary<int, double>>(StringComparer.Ordinal);
            foreach (var r in rows)
            {
                Of(own, r.Target);
                if (o.IsSealed(k, r) || o.Whole(k, r) is not { } hit) continue;
                all[hit.Base] = all.GetValueOrDefault(hit.Base) + 1;
                var mine = own[r.Target];
                mine[hit.Base] = mine.GetValueOrDefault(hit.Base) + 1;
            }
            int shown = B.Count == 0 ? 0 : B.MaxBy(b => (all.GetValueOrDefault(b), b));
            if (B.Count > 0) o.shown[k] = shown;
            foreach (var (t, mine) in own)
            {
                double nRest = B.Sum(b => all.GetValueOrDefault(b) - mine.GetValueOrDefault(b));
                var pb = B.ToDictionary(b => b, b => mine.GetValueOrDefault(b)
                    + (nRest > 0 ? Prior * (all.GetValueOrDefault(b) - mine.GetValueOrDefault(b)) / nRest : 0));
                double tot = pb.Values.Sum();
                double g = o.Gear(t);
                var plain = o.Amounts(k, g, 1);
                var sealed_ = o.Amounts(k, g, 2);
                o.on[(k, t)] = new OnTarget
                {
                    Plain = [.. plain.Keys], Sealed = [.. sealed_.Keys],
                    PlainTop = plain.Count > 0 ? plain.Keys.Max() : 0,
                    SealedTop = sealed_.Count > 0 ? sealed_.Keys.Max() : 0,
                    Gear = g,
                    Odds = tot > 0 ? pb.Where(x => x.Value > 0).ToDictionary(x => x.Key, x => x.Value / tot) : [],
                    Base = B.Count == 0 ? null : Math.Round(shown * g, MidpointRounding.ToEven),
                    Others = B.Where(b => b != shown).Select(b => Math.Round(b * g, MidpointRounding.ToEven)).ToList(),
                };
            }
        }
        // Only Fit asks for amounts: what is asked after is read off each person's.
        o.amounts.Clear();
        return o;
    }

    /// <summary>What one heal line overcured: 0 for one that landed whole,
    /// and null for one with nothing known to charge it against (its spell
    /// has no base), which Overcure % leaves out.</summary>
    public double? Of(HealEvent h)
    {
        if (charged != null) return charged.TryGetValue(h, out var c) ? c : null;
        var k = new Spell(h.Actor, h.Action);
        if (!on.TryGetValue((k, h.Target), out var t) || t.Odds.Count == 0) return null;
        bool doubled = IsSealed(k, h);
        int f = doubled ? 2 : 1;
        int hp = Hp(h);
        // On a full amount, or above every one: whole.
        if ((doubled ? t.Sealed : t.Plain).Contains(hp) || hp > (doubled ? t.SealedTop : t.PlainTop)) return 0;
        // Against the plain base on this person, never a day/weather amount.
        double w = 0, sum = 0;
        var cands = new Dictionary<int, double>();
        // Each base on this person as its Base cell shows it.
        foreach (var (b, p) in t.Odds) cands[f * (int)Math.Round(b * t.Gear, MidpointRounding.ToEven)] = p;
        foreach (var (v, p) in cands)
            if (v > hp)
            {
                w += p;
                sum += v * p;
            }
        return w > 0 ? sum / w - hp : 0;
    }

    /// <summary>The base a caster's spell is measured against on
    /// <paramref name="target"/> (their gear in it), or with no target the
    /// plain one; null with none.</summary>
    public double? Base(string caster, string spell, string? target = null)
    {
        if (fitted != null)
        {
            double? one = null;
            foreach (var n in called!.GetValueOrDefault(caster) ?? [])
                if (fitted.Base(n, spell, target) is double theirs)
                {
                    if (one is double o && o != theirs) return null;
                    one = theirs;
                }
            return one;
        }
        var k = new Spell(caster, spell);
        if (target != null) return on.TryGetValue((k, target), out var t) ? t.Base : null;
        return shown.TryGetValue(k, out var b) ? b : null;
    }

    /// <summary>The caster's other bases for the spell, as <see cref="Base"/>.</summary>
    public IReadOnlyList<double> Others(string caster, string spell, string? target = null)
    {
        if (fitted != null)
        {
            if (Base(caster, spell, target) is not double shownHere) return [];
            return (called!.GetValueOrDefault(caster) ?? []).SelectMany(n => fitted.Others(n, spell, target))
                                                             .Where(b => b != shownHere).Distinct().Order().ToList();
        }
        var k = new Spell(caster, spell);
        if (target != null) return on.TryGetValue((k, target), out var t) ? t.Others : [];
        if (!shown.TryGetValue(k, out var s)) return [];
        return bases[k].Where(b => b != s).Select(b => (double)b).ToList();
    }

    /// <summary>What a Base cell's pointer is told: the other bases, or null
    /// with none ("Also 196").</summary>
    public static string? Also(IReadOnlyList<double> others) =>
        others.Count == 0 ? null : "Also " + string.Join(", ", others.Select(Format.Int));

    double Gear(string target) => gear.GetValueOrDefault(target, 1.0);

    bool IsSealed(Spell k, HealEvent r) => r.Use is double u && sealedUses.Contains((k, u));

    /// <summary>The base and day/weather multiplier a cast landed on whole, or null.</summary>
    Hit? Whole(Spell k, HealEvent r) =>
        Amounts(k, Gear(r.Target), IsSealed(k, r) ? 2 : 1).TryGetValue(Hp(r), out var hit) ? hit : null;

    /// <summary>Every full amount a cast of <paramref name="k"/> can land on,
    /// on a person with gear <paramref name="g"/>, <paramref name="f"/> 2
    /// when sealed: the amount, and the base and multiplier it is. A plain
    /// base comes first, so a cast on one is that base and not another's
    /// day/weather amount.</summary>
    Dictionary<int, Hit> Amounts(Spell k, double g, int f)
    {
        if (amounts.TryGetValue((k, g, f), out var known)) return known;
        var out_ = amounts[(k, g, f)] = new Dictionary<int, Hit>();
        if (!bases.TryGetValue(k, out var B)) return out_;
        foreach (var m in mults[k])
            foreach (var b in B)
            {
                double lv = f * Math.Floor(b * m);
                out_.TryAdd((int)Math.Floor(lv * g), new Hit(b, m));
                out_.TryAdd((int)Math.Round(lv * g, MidpointRounding.ToEven), new Hit(b, m));
            }
        return out_;
    }

    /// <summary>The amounts that recur in <paramref name="rows"/>, and how
    /// often each landed.</summary>
    static (HashSet<int> Recurring, Dictionary<int, int> Count) Recurring(List<HealEvent> rows, Func<HealEvent, int> val)
    {
        var count = new Dictionary<int, int>();
        var people = new Dictionary<int, HashSet<string>>();
        var per = new Dictionary<string, Dictionary<int, int>>(StringComparer.Ordinal);
        var byUse = new Dictionary<double, Dictionary<int, int>>();
        foreach (var r in rows)
        {
            int v = val(r);
            if (v <= 0) continue;
            Bump(count, v);
            if (!people.TryGetValue(v, out var who)) people[v] = who = new HashSet<string>(StringComparer.Ordinal);
            who.Add(r.Target);
            Bump(Of(per, r.Target), v);
            if (r.Use is double u) Bump(Of(byUse, u), v);
        }
        var rec = count.Where(x => x.Value >= 3 && people[x.Key].Count >= 2).Select(x => x.Key).ToHashSet();
        foreach (var c in byUse.Values)
            foreach (var (v, n) in c)
                if (n >= 2) rec.Add(v);
        foreach (var c in per.Values)
        {
            int all = c.Values.Sum();
            foreach (var (v, n) in c)
                if (n >= 3 && n >= 0.15 * all) rec.Add(v);
        }
        return (rec, count);
    }

    /// <summary>The bases of <paramref name="k"/>: recurring amounts that
    /// no commoner one explains as its day/weather or sealed amount, with
    /// only the multipliers in <paramref name="allowed"/> (all, if null).</summary>
    List<int> FindBases(List<HealEvent> rows, Spell k, Func<HealEvent, int> val, HashSet<double>? allowed)
    {
        var (rec, count) = Recurring(rows, val);
        var ms = mults[k].Where(m => m != 1.0 && (allowed is null || allowed.Contains(m))).ToArray();
        int N(int v) => count.GetValueOrDefault(v);
        // b explains a only if b landed at least half as often: two casts in
        // three are on the base, before capping.
        bool Explains(int b, int a) => N(b) >= 0.5 * N(a)
            && (ms.Any(m => a == (int)Math.Floor(b * m)) || a == 2 * b || ms.Any(m => a == 2 * (int)Math.Floor(b * m)));
        var keep = new HashSet<int>(rec);
        // Least common first: an amount another explains goes, unless they
        // explain each other and it is the commoner.
        foreach (var a in rec.OrderBy(a => N(a)).ThenBy(a => a))
            foreach (var b in keep.ToList())
                if (b != a && keep.Contains(a) && Explains(b, a) && (!Explains(a, b) || N(a) <= N(b)))
                    keep.Remove(a);
        return keep.Order().ToList();
    }

    static int Hp(HealEvent h) => double.IsNaN(h.Hp) ? 0 : (int)h.Hp;

    static void Bump<TKey>(Dictionary<TKey, int> d, TKey k) where TKey : notnull => d[k] = d.GetValueOrDefault(k) + 1;

    static TValue Of<TKey, TValue>(Dictionary<TKey, TValue> d, TKey k) where TKey : notnull where TValue : new() =>
        d.TryGetValue(k, out var v) ? v : d[k] = new TValue();

    static double Median(List<double> q)
    {
        var s = q.Order().ToList();
        int n = s.Count;
        return n % 2 == 1 ? s[n / 2] : (s[n / 2 - 1] + s[n / 2]) / 2;
    }
}
