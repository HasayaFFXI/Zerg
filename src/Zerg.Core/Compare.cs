namespace Zerg.Core;

/// <summary>A damage type, in the order Compare lists them.</summary>
public sealed record KindLabel(string Key, string Label);

/// <summary>The best single hit of a run.</summary>
public sealed record BestHit(double Max, string Who, string What);

/// <summary>Who stands behind a Compare row: the characters, and the job(s) they were on.</summary>
public sealed class Members
{
    public List<string> Names { get; set; } = [];
    public List<string> Jobs { get; set; } = [];
}

/// <summary>One run, measured: everything Compare draws of it.</summary>
public sealed class Measurement
{
    public ImportedParse Run { get; set; } = null!;
    /// <summary><c>actor</c> or <c>job</c>: what a row is.</summary>
    public string By { get; set; } = "actor";
    /// <summary>The counted rows, on the run's clock.</summary>
    public List<CombatEvent> Events { get; set; } = [];
    /// <summary>The same rows under the name their table row has: by job, each
    /// is its character's main job. What <see cref="Agg"/> was counted from.</summary>
    public List<CombatEvent> Rows { get; set; } = [];
    public Aggregate Agg { get; set; } = new();
    public OrderedDictionary<string, Members> Members { get; set; } = new(StringComparer.Ordinal);
    /// <summary>The run's clock, frozen at its Pause, in seconds.</summary>
    public double Duration { get; set; }
    public double Total { get; set; }
    /// <summary>Null while targets are isolated: no rate is given for a
    /// target (<see cref="Snapshot.Rate"/>).</summary>
    public double? Dps { get; set; }
    /// <summary>Only the damage dealt to some targets was counted.</summary>
    public bool Isolated { get; set; }
    public double? Accuracy { get; set; }
    public double? WsTotal { get; set; }
    public int WsCount { get; set; }
    public double? WsAvg { get; set; }
    public double? WsAcc { get; set; }
    public double? ScTotal { get; set; }
    public int ScCount { get; set; }
    /// <summary>Rows with any damage.</summary>
    public int Characters { get; set; }
    public BestHit? Best { get; set; }
    /// <summary>Damage by type (a pet's rows are one type, <c>pet</c>):
    /// every type, whichever are isolated.</summary>
    public OrderedDictionary<string, double> Kinds { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Damage by target name: every target, whichever are isolated.</summary>
    public OrderedDictionary<string, double> Targets { get; set; } = new(StringComparer.Ordinal);
    public HealTotals Heal { get; set; } = new();
    /// <summary>Healing as time/amount points for the cumulative line (no pet heals).</summary>
    public List<CombatEvent> HealEvents { get; set; } = [];
    /// <summary>Every heal the run counts, pet heals included, under the name
    /// its table row has: by job, each is its healer's main job.</summary>
    public List<HealEvent> HealRows { get; set; } = [];
    /// <summary>Party healing by spell, pet heals included.</summary>
    public OrderedDictionary<string, double> HealSpells { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Party healing by who received it, pet heals included.</summary>
    public OrderedDictionary<string, double> HealTargets { get; set; } = new(StringComparer.Ordinal);
    /// <summary>The run's kills and the party's deaths (<see cref="Zerg.Core.Kills"/>).</summary>
    public KillTotals Kills { get; set; } = KillTotals.None;
    /// <summary>The kills of the targets isolated; every kill with none.</summary>
    public IReadOnlyList<Fight> Killed { get; set; } = [];
    /// <summary>Each character's delay samples, under the run's own names.</summary>
    public DelayTotals Delays { get; set; } = DelayTotals.None;
    /// <summary>Who stands behind each row's key in the run itself: a
    /// character matched with another run's is keyed by that run's name.</summary>
    public Dictionary<string, string> Original { get; set; } = new(StringComparer.Ordinal);
    /// <summary>The targets and types isolated, as <see cref="Targets.Only"/> gives them.</summary>
    public IReadOnlySet<string>? Only { get; set; }
    public IReadOnlySet<string>? OnlyTypes { get; set; }
    /// <summary>The session-clock time of the run's first counted hit, in ms:
    /// where its line starts on the cumulative chart's one clock.</summary>
    public double First { get; set; }
    /// <summary>False for the side of a comparison that holds no run yet.</summary>
    public bool Present { get; set; } = true;

    /// <summary>A row's delay in milliseconds, from its character's own
    /// samples; none by job.</summary>
    public double? DelayOf(string key) =>
        By == "job" ? null : Delays.Mean(Original.GetValueOrDefault(key, key), Only, OnlyTypes);

    /// <summary>How many times a row's characters died in the run.</summary>
    public int DeathsOf(string key) =>
        By == "job" && Members.TryGetValue(key, out var m) ? m.Names.Sum(Kills.DeathsOf)
        : Kills.DeathsOf(Original.GetValueOrDefault(key, key));

    /// <summary>A run that is not there: every figure a dash.</summary>
    public static Measurement Absent() => new() { Present = false, Run = null! };
}

/// <summary>A row present in either run; a side is null where the key was absent.</summary>
public sealed record Pair<T>(string Key, T? A, T? B) where T : class;

/// <summary>A summed figure for both runs (0 where absent).</summary>
public sealed record PairTotal(string Key, double A, double B);

/// <summary>A damage type for both runs.</summary>
public sealed record KindPair(string Key, string Label, double A, double B);

/// <summary>Two measurements, lined up.</summary>
public sealed class CompareRows
{
    /// <summary>One per character (or job), nobody at zero on both sides, largest first.</summary>
    public List<Pair<ActorTotals>> Rows { get; set; } = [];
    /// <summary>Every damage type either run dealt any of.</summary>
    public List<KindPair> Kinds { get; set; } = [];
    /// <summary>Damage by target, largest first.</summary>
    public List<PairTotal> Targets { get; set; } = [];
    public List<Pair<HealerTotals>> Heals { get; set; } = [];
    public List<PairTotal> HealSpells { get; set; } = [];
    public List<PairTotal> HealTargets { get; set; } = [];
}

/// <summary>A kill or a death on the cumulative chart: whose, and when on
/// its run's clock from the first hit, in ms.</summary>
public sealed record PaceMark(string Name, double At);

/// <summary>Both runs' running party total on one grid, each from its own
/// first hit. The shorter run is NaN past its own end.</summary>
public sealed class Pace
{
    public double[] Times { get; set; } = [];
    public double Step { get; set; }
    public double[] A { get; set; } = [];
    public double[] B { get; set; } = [];
    /// <summary>B − A at each time; NaN where either run has ended.</summary>
    public double[] Gap { get; set; } = [];
    public IReadOnlyList<PaceMark> KillsA { get; set; } = [];
    public IReadOnlyList<PaceMark> KillsB { get; set; } = [];
    public IReadOnlyList<PaceMark> DeathsA { get; set; } = [];
    public IReadOnlyList<PaceMark> DeathsB { get; set; } = [];
}

/// <summary>A histogram bin both runs share, [Lo, Hi): how many of each run's
/// hits fell in it.</summary>
public sealed record PairBin(double Lo, double Hi)
{
    public int A { get; set; }
    public int B { get; set; }
    /// <summary>It holds whole values, Lo to Hi − 1 (<see cref="Cut.Whole"/>).</summary>
    public bool Whole { get; init; }
}

/// <summary>One action of one row in both runs: each run's own hits, and the
/// two on one set of bins. A side is null where the run had no use of it.</summary>
public sealed class Spread
{
    public Distribution? A { get; set; }
    public Distribution? B { get; set; }
    public List<PairBin> Bins { get; set; } = [];
}

/// <summary>B against A: the difference, and that over A (null when A is zero
/// or either side has nothing to measure).</summary>
public sealed record Delta(double? D, double? Pct);

/// <summary>
/// Two exported parses → an A/B comparison.
///
/// <para>A run is an import, measured by exactly the <see cref="Counting.Filter"/>
/// and <see cref="Counting.Aggregate"/> the live view draws with, so a compared
/// figure is the figure Zerg prints when that file is imported. Nothing here
/// counts damage a second way; it only lines two answers up.</para>
///
/// <para>A is the baseline: every delta is B − A, every percentage that over A,
/// so a sign means the same thing in every cell.</para>
/// </summary>
public static class Compare
{
    public static readonly IReadOnlyList<KindLabel> KindLabels =
    [
        new("melee", "Melee"),
        new("ws", "Weaponskills"),
        new("skillchain", "Skillchains"),
        new("magic", "Magic"),
        new("pet", "Pet"),
        new("ability", "Job abilities"),
        new("ranged", "Ranged"),
        new("addl", "Additional effects"),
        new("reaction", "Counters & spikes"),
        new("mobtp", "Other"),
    ];

    public const string UnknownJob = Cast.UnknownJob;

    /// <summary>A character's main job in this run, or <see cref="UnknownJob"/>.
    /// Never borrowed from the other run: someone missing from the party table
    /// in one run was often on a different job in it.</summary>
    static string MainJob(Roster roster, string name)
    {
        return roster.JobOf(name) is { HasMain: true } j ? j.Main : UnknownJob;
    }

    static void Add(OrderedDictionary<string, double> map, string key, double v) =>
        map[key] = map.GetValueOrDefault(key) + v;

    /// <summary>
    /// One run, measured. <paramref name="byJob"/> re-actors every row onto its
    /// character's main job before the aggregate, so a job's accuracy, WS
    /// average and action breakdown are counted over its combined swings rather
    /// than averaged from per-character figures.
    ///
    /// <para><paramref name="targets"/> isolates targets, by name, as
    /// <see cref="Tracker.Count"/> does: every damage figure is then of the
    /// damage dealt to those, but for <see cref="Measurement.Targets"/>,
    /// which goes on listing them all. Healing and the run's clock are as
    /// they were.</para>
    ///
    /// <para><paramref name="types"/> isolates damage types the same way
    /// (<see cref="DamageTypes"/>), and <see cref="Measurement.Kinds"/> goes
    /// on listing them all. <b>Each list is of what the other filter
    /// leaves</b>: with a target isolated, the types are of the damage dealt
    /// to it; with a type isolated, the targets are of what that type dealt
    /// to each. A type takes no rate away.</para>
    /// </summary>
    /// <param name="characters">Characters isolated, by the name their row
    /// has (after <paramref name="rename"/>); null or empty for everyone.</param>
    /// <param name="rename">Characters of this run keyed by another name:
    /// a character matched with the other run's by hand ("Match with"), whose
    /// row is then the other's. Not by job.</param>
    /// <param name="scope">One character of this run, by their own name, as
    /// the whole of it; null for the whole party.</param>
    public static Measurement Measure(ImportedParse run, bool skillchains = true, bool byJob = false,
                                      IReadOnlySet<string>? targets = null, IReadOnlySet<string>? types = null,
                                      IReadOnlySet<string>? characters = null,
                                      IReadOnlyDictionary<string, string>? rename = null, string? scope = null)
    {
        var roster = run.Source.Roster;
        var sn = run.Session;
        if (byJob) rename = null;
        string Key(string name) => rename != null && rename.TryGetValue(name, out var n) ? n : name;
        var own = Own(characters, rename);
        if (scope != null)
            own = characters is { Count: > 0 } && !characters.Contains(Key(scope)) ? NoOne : new HashSet<string>([scope], StringComparer.Ordinal);

        var scoped = Counting.Filter(run.Source.Events,
            new FilterOptions { Session = sn, Roster = roster, Skillchains = skillchains, Characters = own });

        // By target and by type over every landed row, each before its own
        // filter and after the other's. Unfolded is right: both only sum,
        // and an area attack's damage belongs to each target.
        var only = Targets.Only(targets);
        var onlyTypes = Targets.Only(types);
        var byTarget = Targets.Totals(onlyTypes != null ? Counting.Filter(scoped, new FilterOptions { Types = onlyTypes }) : scoped);
        var kinds = DamageTypes.Totals(only != null ? Counting.Filter(scoped, new FilterOptions { Targets = only }) : scoped);
        if (only != null || onlyTypes != null)
            scoped = Counting.Filter(scoped, new FilterOptions { Targets = only, Types = onlyTypes });

        // The export's own clock, frozen at its Pause: the fixed denominator
        // the exporter was looking at.
        var secs = sn.Elapsed(sn.PausedAt) / 1000;

        var original = new Dictionary<string, string>(StringComparer.Ordinal);
        var members = new OrderedDictionary<string, Members>(StringComparer.Ordinal);
        void Note(string key, string name)
        {
            if (!members.TryGetValue(key, out var m)) members[key] = m = new Members();
            if (!m.Names.Contains(name)) m.Names.Add(name);
            var lab = roster.JobLabel(name);
            if (lab.Length > 0 && !m.Jobs.Contains(lab)) m.Jobs.Add(lab);
        }

        List<CombatEvent> rows;
        if (byJob)
        {
            rows = scoped.Select(e =>
            {
                var key = MainJob(roster, e.Actor);
                Note(key, e.Actor);
                return e with { Actor = key };
            }).ToList();
        }
        else if (rename is { Count: > 0 })
        {
            rows = scoped.Select(e =>
            {
                var key = Key(e.Actor);
                Note(key, e.Actor);
                original[key] = e.Actor;
                return key == e.Actor ? e : e with { Actor = key };
            }).ToList();
            scoped = rows;
        }
        else
        {
            rows = scoped;
            foreach (var e in scoped) Note(e.Actor, e.Actor);
        }

        var agg = Counting.Aggregate(rows, secs);

        // Healing over the same window, re-keyed by job the same way.
        var hl = Healing.Filter(run.Source.Heals, new FilterOptions { Session = sn, Roster = roster, Characters = own });
        // Overcure's bases, learned from the run's whole session as the
        // Healing section learns them: whoever is isolated, each heal is
        // charged against its own caster's.
        var overcure = Overcure.Fit(own == null ? hl : Healing.Filter(run.Source.Heals, new FilterOptions { Session = sn, Roster = roster }));
        var named = hl;
        if (byJob)
        {
            hl = hl.Select(h =>
            {
                var key = MainJob(roster, h.Actor);
                Note(key, h.Actor);
                return h with { Actor = key };
            }).ToList();
        }
        else if (rename is { Count: > 0 })
        {
            hl = hl.Select(h =>
            {
                var key = Key(h.Actor);
                Note(key, h.Actor);
                original[key] = h.Actor;
                return key == h.Actor ? h : h with { Actor = key };
            }).ToList();
        }
        // Renamed (by job, or matched by hand), each heal keeps its caster's charge.
        if (!ReferenceEquals(named, hl)) overcure = overcure.Renamed(named, hl);
        var heal = Healing.Totals(hl, overcure);

        // For Healing mode: the cumulative line, and party healing by spell
        // and by receiver, pet heals included in all three as in the Healing
        // column.
        var healEvents = new List<CombatEvent>();
        var healSpells = new OrderedDictionary<string, double>(StringComparer.Ordinal);
        var healTargets = new OrderedDictionary<string, double>(StringComparer.Ordinal);
        foreach (var h in hl)
        {
            healEvents.Add(new CombatEvent { T = h.T, Hit = true, Dmg = h.Hp });
            Add(healSpells, h.Spell, h.Hp);
            Add(healTargets, h.Target, h.Hp);
        }

        // The party's figures are the Party line's (Aggregate.Party), summed
        // from the same per-character splits: they agree with the table by
        // construction.
        var party = agg.Party;
        var kills = Zerg.Core.Kills.Count(run.Source, sn, own, skillchains);
        double first = double.PositiveInfinity;
        foreach (var e in scoped) if (e.Hit && e.Dmg > 0) { first = e.T; break; }
        if (double.IsInfinity(first)) first = 0;

        return new Measurement
        {
            Run = run,
            By = byJob ? "job" : "actor",
            Events = scoped,
            Rows = rows,
            Agg = agg,
            Members = members,
            Duration = secs,
            Total = agg.Total,
            Dps = only != null ? null : Counting.PerSecond(agg.Total, secs),
            Isolated = only != null,
            Accuracy = party.AutoAcc,
            WsTotal = party.WsTotal,
            WsCount = agg.Split.WsTries,
            WsAvg = party.WsAvg,
            WsAcc = party.WsAcc,
            ScTotal = party.ScTotal,
            ScCount = agg.Split.ScRows,
            Characters = agg.Actors.Count(a => a.Total > 0),
            Best = agg.Best,
            Kinds = kinds,
            Targets = byTarget,
            Heal = heal,
            HealEvents = healEvents,
            HealRows = hl,
            HealSpells = healSpells,
            HealTargets = healTargets,
            Kills = kills,
            Killed = only != null ? kills.Fights.Where(f => only.Contains(f.Target)).ToList() : kills.Fights,
            Delays = byJob ? DelayTotals.None : Zerg.Core.Delays.Count(run.Source, sn),
            Original = original,
            Only = only,
            OnlyTypes = onlyTypes,
            First = first,
        };
    }

    /// <summary>A set that holds no character's name: isolating it leaves nobody.</summary>
    static readonly IReadOnlySet<string> NoOne = new HashSet<string>(["\u0000"], StringComparer.Ordinal);

    /// <summary>The characters isolated, by the names this run has them
    /// under: a matched character's other name stands for them.</summary>
    static IReadOnlySet<string>? Own(IReadOnlySet<string>? characters, IReadOnlyDictionary<string, string>? rename)
    {
        if (characters is not { Count: > 0 } || rename is not { Count: > 0 }) return characters;
        var own = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in characters) if (!rename.ContainsKey(c)) own.Add(c);
        foreach (var (from, to) in rename) if (characters.Contains(to)) own.Add(from);
        return own.Count > 0 ? own : NoOne;
    }

    /// <summary>The keys of either map, A's first, each with what either side had.</summary>
    static List<(string Key, bool InA, T? A, bool InB, T? B)> PairUp<T>(
        OrderedDictionary<string, T> a, OrderedDictionary<string, T> b)
    {
        var keys = a.Keys.ToList();
        foreach (var k in b.Keys) if (!a.ContainsKey(k)) keys.Add(k);
        return keys.Select(k =>
        {
            bool inA = a.TryGetValue(k, out var va), inB = b.TryGetValue(k, out var vb);
            return (k, inA, va, inB, vb);
        }).ToList();
    }

    static OrderedDictionary<string, T> ByName<T>(IEnumerable<T> list, Func<T, string> name)
    {
        var m = new OrderedDictionary<string, T>(StringComparer.Ordinal);
        foreach (var x in list) m[name(x)] = x;
        return m;
    }

    static double Size(Bucket? x) => x?.Total ?? 0;
    static double Size(HealAction? x) => x?.Total ?? 0;
    /// <summary>Everything a healer put out, pet included: for ordering and the zero test.</summary>
    static double HealSize(HealerTotals? x) => x?.Total ?? 0;

    static List<Pair<T>> Pairs<T>(OrderedDictionary<string, T> a, OrderedDictionary<string, T> b) where T : class =>
        PairUp(a, b).Select(p => new Pair<T>(p.Key, p.A, p.B)).ToList();

    internal static List<PairTotal> Totals(OrderedDictionary<string, double> a, OrderedDictionary<string, double> b)
    {
        var list = PairUp(a, b).Select(p => new PairTotal(p.Key, p.A, p.B)).ToList();
        list.SortByDescending(x => Math.Max(x.A, x.B));
        return list;
    }

    /// <summary>Pieces of the healing under one name each (a spell, a person
    /// healed), each run's, largest first.</summary>
    internal static List<Pair<HealPart>> Parts(IReadOnlyList<HealPart> a, IReadOnlyList<HealPart> b)
    {
        var list = Pairs(ByName(a, x => x.Name), ByName(b, x => x.Name));
        list.SortByDescending(x => Math.Max(x.A?.Total ?? 0, x.B?.Total ?? 0));
        return list;
    }

    /// <summary>Two measurements → rows Compare can print.</summary>
    public static CompareRows Diff(Measurement ma, Measurement mb)
    {
        var rows = Pairs(ByName(ma.Agg.Actors, x => x.Name), ByName(mb.Agg.Actors, x => x.Name))
            .Where(r => Size(r.A) > 0 || Size(r.B) > 0).ToList();
        rows.SortByDescending(x => Math.Max(Size(x.A), Size(x.B)));

        var kinds = new List<KindPair>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in KindLabels)
        {
            seen.Add(k.Key);
            double a = ma.Kinds.GetValueOrDefault(k.Key), b = mb.Kinds.GetValueOrDefault(k.Key);
            if (Truthy(a) || Truthy(b)) kinds.Add(new KindPair(k.Key, k.Label, a, b));
        }
        // A type this list does not know yet still gets a row rather than vanishing.
        foreach (var r in PairUp(ma.Kinds, mb.Kinds))
            if (!seen.Contains(r.Key)) kinds.Add(new KindPair(r.Key, r.Key, r.A, r.B));

        var targets = Totals(ma.Targets, mb.Targets);

        var heals = Pairs(ByName(ma.Heal.Actors, x => x.Name), ByName(mb.Heal.Actors, x => x.Name))
            .Where(r => HealSize(r.A) > 0 || HealSize(r.B) > 0).ToList();
        heals.SortByDescending(x => Math.Max(HealSize(x.A), HealSize(x.B)));

        return new CompareRows
        {
            Rows = rows, Kinds = kinds, Targets = targets, Heals = heals,
            HealSpells = Totals(ma.HealSpells, mb.HealSpells),
            HealTargets = Totals(ma.HealTargets, mb.HealTargets),
        };
    }

    static bool Truthy(double v) => v != 0 && !double.IsNaN(v);

    /// <summary>
    /// One row's actions, paired, largest first. Only actions that did damage in
    /// at least one run: an Erase or a resisted Stun reaches the aggregate by
    /// being used, and a row of dashes for it is noise. An action that did
    /// damage in one run and none in the other stays; that zero is the news.
    /// </summary>
    public static List<Pair<ActionTotals>> Actions(Pair<ActorTotals> row)
    {
        var la = row.A != null ? ByName(row.A.ActionList, x => x.Name) : new(StringComparer.Ordinal);
        var lb = row.B != null ? ByName(row.B.ActionList, x => x.Name) : new(StringComparer.Ordinal);
        var list = Pairs(la, lb).Where(x => Size(x.A) > 0 || Size(x.B) > 0).ToList();
        list.SortByDescending(x => Math.Max(Size(x.A), Size(x.B)));
        return list;
    }

    /// <summary>
    /// One action of one row, as each run hit with it, on bins the two share so
    /// a column of A's stands beside the column of B's for the same damage.
    /// Each run's hits are <see cref="Counting.Distribution"/>'s, so its figures
    /// are the ones that file's own drill-down prints.
    ///
    /// <para>The bins run from the smallest hit of either run to the largest.
    /// Their width is the finer of the two runs' own Freedman–Diaconis widths:
    /// the wider would flatten the tighter run into a column or two. Cut by
    /// <paramref name="rule"/> as one histogram is (<see cref="Cut.Of"/>), to
    /// fewer columns than a single histogram's (<see cref="Cut.MostPaired"/>),
    /// because every bin holds two columns.</para>
    /// </summary>
    public static Spread Spread(Measurement ma, Measurement mb, string key, string action, Binning rule = Binning.Even) =>
        Spread(ma, mb, key, action, key, action, rule);

    /// <summary>
    /// The same for two actions that need not be one: A's action of one row
    /// against B's of the same row or another ("Tachi: Shoha in A against
    /// Tachi: Jinpu in B", or one character against another). Binned as one
    /// action of both runs is.
    /// </summary>
    public static Spread Spread(Measurement ma, Measurement mb, string keyA, string actionA, string keyB, string actionB,
                                Binning rule = Binning.Even)
    {
        Distribution? Of(Measurement m, string key, string action)
        {
            var d = Counting.Distribution(m.Rows, key, action, rule);
            return d.Swings > 0 ? d : null;
        }
        return Shared(Of(ma, keyA, actionA), Of(mb, keyB, actionB), rule);
    }

    /// <summary>
    /// The same for one heal of one healer: what each cast healed, summed over
    /// everyone it reached, so a Curaga on five is one figure, as it is in a
    /// file's own heal drill-down. A pet's heals are under the pet's name. A
    /// cure on a full target is a cast that healed 0, in the first bin.
    /// </summary>
    public static Spread HealSpread(Measurement ma, Measurement mb, string key, string action, Binning rule = Binning.Even) =>
        HealSpread(ma, mb, key, action, key, action, rule);

    /// <summary>The same for two heals that need not be one, as <see cref="Spread(Measurement, Measurement, string, string, string, string, Binning)"/>.</summary>
    public static Spread HealSpread(Measurement ma, Measurement mb, string keyA, string actionA, string keyB,
                                    string actionB, Binning rule = Binning.Even)
    {
        Distribution? Of(Measurement m, string key, string action)
        {
            var d = Counting.Distribution(Healing.AsEvents(m.HealRows), key, action, rule);
            return d.Swings > 0 ? d : null;
        }
        return Shared(Of(ma, keyA, actionA), Of(mb, keyB, actionB), rule);
    }

    /// <summary>
    /// Two distributions on one set of bins, whatever they are of: one action
    /// in two runs, two actions of one run, two characters' actions. The live
    /// sections' drill-down puts two actions of one session side by side with
    /// this, so a pair there is binned as a pair here is.
    /// </summary>
    public static Spread Shared(Distribution? a, Distribution? b, Binning rule = Binning.Even)
    {
        var spread = new Spread { A = a, B = b };

        double min = double.PositiveInfinity, max = double.NegativeInfinity, width = 0;
        int most = 0;
        foreach (var d in new[] { a, b })
        {
            if (d == null || d.Count == 0) continue;
            (min, max, most) = (Math.Min(min, d.Min), Math.Max(max, d.Max), Math.Max(most, d.Count));
            var w = Counting.BinWidth(d.Q1, d.Q3, d.Count);
            if (w > 0 && (width == 0 || w < width)) width = w;
        }
        if (most == 0) return spread;

        if (max > min)
        {
            var cut = Cut.Of(rule, min, max, width, most, Cut.MostPaired(rule));
            for (int i = 0; i < cut.Count; i++) spread.Bins.Add(new PairBin(cut.LoOf(i), cut.HiOf(i)) { Whole = cut.Whole });
            if (a != null) foreach (var v in a.Sorted) spread.Bins[cut.At(v)].A++;
            if (b != null) foreach (var v in b.Sorted) spread.Bins[cut.At(v)].B++;
        }
        else
        {
            // Every hit of both runs identical.
            spread.Bins.Add(new PairBin(min, min) { A = a?.Count ?? 0, B = b?.Count ?? 0 });
        }
        return spread;
    }

    /// <summary>One healer's heals, paired by name, largest first.</summary>
    public static List<Pair<HealAction>> HealActions(Pair<HealerTotals> row)
    {
        var la = row.A != null ? ByName(row.A.ActionList, x => x.Name) : new(StringComparer.Ordinal);
        var lb = row.B != null ? ByName(row.B.ActionList, x => x.Name) : new(StringComparer.Ordinal);
        var list = Pairs(la, lb);
        list.SortByDescending(x => Math.Max(Size(x.A), Size(x.B)));
        return list;
    }

    /// <summary>
    /// Both runs' running party total on one grid, so a crosshair reads both at
    /// the same elapsed instant ("where was each run at minute forty"). The grid
    /// runs to the longer run's clock; the shorter run is NaN past its own end
    /// rather than carried flat, because a flat line would claim it was still
    /// being measured, and the chart lifts the pen on NaN.
    /// <paramref name="healing"/> draws the heal points instead of the damage rows.
    /// </summary>
    public static Pace Pace(Measurement ma, Measurement mb, bool healing = false, int maxPoints = 0)
    {
        if (maxPoints == 0) maxPoints = 400;
        // Each run on its own clock from its first hit: two pulls line up
        // however long each party stood about before the first swing.
        static double Span(Measurement m) => m.Present ? Math.Max(0, m.Duration * 1000 - m.First) : 0;
        var end = Math.Max(Span(ma), Span(mb));
        if (end <= 0 || double.IsNaN(end)) end = 1000;
        var step = Math.Max(1000, Math.Ceiling(end / maxPoints / 1000) * 1000);
        int n = (int)Math.Ceiling(end / step) + 1;
        var times = new double[n];
        for (int i = 0; i < n; i++) times[i] = Math.Min(i * step, end);

        double[] Line(Measurement m)
        {
            var v = new double[n];
            if (!m.Present) { Array.Fill(v, double.NaN); return v; }
            var stop = Span(m);
            foreach (var e in healing ? m.HealEvents : m.Events)
            {
                if (!e.Hit || e.Dmg == 0 || double.IsNaN(e.Dmg)) continue;
                // Into the first sample at or after the hit, so a sample reads
                // the damage done by that instant, never a hit still to come.
                var bin = (int)Math.Min(n - 1, Math.Max(0, Math.Ceiling((e.T - m.First) / step)));
                v[bin] += e.Dmg;
            }
            double run = 0;
            for (int j = 0; j < n; j++)
            {
                run += v[j];
                // A sample belongs to the run while the bin it closes began
                // before the run's end: the bin that holds the end is drawn.
                v[j] = times[j] - step < stop ? run : double.NaN;
            }
            return v;
        }

        static List<PaceMark> Kills(Measurement m) =>
            m.Present ? m.Killed.Where(f => f.At >= m.First).Select(f => new PaceMark(f.Target, f.At - m.First)).ToList() : [];
        static List<PaceMark> Deaths(Measurement m) =>
            m.Present ? m.Kills.Deaths.Where(d => d.At >= m.First).Select(d => new PaceMark(d.Name, d.At - m.First)).ToList() : [];
        var (a, b) = (Line(ma), Line(mb));
        var gap = new double[n];
        for (int i = 0; i < n; i++) gap[i] = double.IsFinite(a[i]) && double.IsFinite(b[i]) ? b[i] - a[i] : double.NaN;
        return new Pace
        {
            Times = times, Step = step, A = a, B = b, Gap = gap,
            KillsA = Kills(ma), KillsB = Kills(mb), DeathsA = Deaths(ma), DeathsB = Deaths(mb),
        };
    }

    public static Delta Delta(double? a, double? b)
    {
        if (a is not double x || b is not double y) return new Delta(null, null);
        return new Delta(y - x, x != 0 && !double.IsNaN(x) ? (y - x) / Math.Abs(x) : null);
    }
}
