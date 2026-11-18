using System.Globalization;

namespace Zerg.Core;

/// <summary>How a change is said: as a share of A, in points, as a length of
/// time, as a count, or as the plain difference.</summary>
public enum ChangeKind
{
    /// <summary>"+12.3%", with the plain difference beside it.</summary>
    Percent,
    /// <summary>The figure is a percentage already, so the change is in points: "+2.1 pt".</summary>
    Points,
    /// <summary>"+4m 07s".</summary>
    Time,
    /// <summary>A count, by how many: "+3".</summary>
    Count,
    /// <summary>The difference as the figure itself is spelled: "−0.425".</summary>
    Plain,
}

/// <summary>Whether a change is good news, bad news, or neither.</summary>
public enum ChangeTone
{
    /// <summary>Nothing to compare: one side has no figure.</summary>
    None,
    /// <summary>No change, or a change that is neither better nor worse.</summary>
    Flat,
    Better,
    Worse,
}

/// <summary>
/// B against A, as it is printed. The sign is always in the text, so the tone
/// (a colour, wherever it is drawn) is never the only thing that says which
/// way a figure moved.
/// </summary>
/// <param name="Main">"+12.3%", "−2.1 pt", "new", "±0", or a dash.</param>
/// <param name="Sub">The plain difference under a percentage ("+1,204"), or "".</param>
public sealed record Change(string Main, string Sub, ChangeTone Tone)
{
    public static readonly Change Nothing = new(CompareSheet.Dash, "", ChangeTone.None);

    /// <summary>A figure that is not compared: no change is printed at all.</summary>
    public static readonly Change Blank = new("", "", ChangeTone.None);

    /// <summary>
    /// The change from <paramref name="a"/> to <paramref name="b"/>.
    ///
    /// <para><b>Which way it went is read off what is printed</b>: a change
    /// that prints as nothing ("0.0%", "0.0 pt", "0.0s") is "±" and flat, however
    /// far apart the two figures were before they were rounded, so a colour
    /// never says better or worse over two figures that read the same. The
    /// plain difference under a percentage takes its sign the same way, from
    /// its own text.</para>
    ///
    /// <para><b>Colour is direction alone</b>: no test of noise. Better is
    /// the way <paramref name="good"/> says.</para>
    /// </summary>
    /// <param name="format">How the plain difference is spelled.</param>
    /// <param name="good">+1 when more is better, −1 when less is, 0 when it is neither.</param>
    public static Change Of(double? a, double? b, ChangeKind kind, Func<double, string> format, int good)
    {
        if (a is not double x || b is not double y || !double.IsFinite(x) || !double.IsFinite(y)) return Nothing;
        double d = y - x, size = Math.Abs(d);
        int way = d > 0 ? 1 : d < 0 ? -1 : 0;

        string text;
        bool none;
        switch (kind)
        {
            case ChangeKind.Points:
                text = Format.Num(size * 100, 1) + " pt";
                none = Format.Round(size * 100, 1) == 0;
                break;
            case ChangeKind.Time:
                text = Format.Duration(size);
                none = Format.Round(size, 1) == 0;
                break;
            case ChangeKind.Count:
                text = Format.Int(size);
                none = size == 0;
                break;
            case ChangeKind.Plain:
                text = format(size);
                none = text == format(0);
                break;
            default:
                if (x != 0)
                {
                    var percent = Math.Abs(d / x) * 100;
                    text = Format.Num(percent, 1) + "%";
                    none = Format.Round(percent, 1) == 0;
                }
                else
                {
                    // Over a baseline of nothing there is no percentage to
                    // give: anything at all is simply new.
                    none = way == 0;
                    text = none ? "0" : "new";
                }
                break;
        }

        int dir = none ? 0 : way;
        var tone = dir == 0 || good == 0 ? ChangeTone.Flat : dir * good > 0 ? ChangeTone.Better : ChangeTone.Worse;
        var main = text == "new" ? text : Sign(dir) + text;
        if (kind != ChangeKind.Percent) return new(main, "", tone);
        var plain = format(size);
        return new(main, Sign(plain == format(0) ? 0 : way) + plain, tone);
    }

    internal static string Sign(int dir) => dir > 0 ? "+" : dir < 0 ? "−" : "±";
}

/// <summary>
/// One compared figure as a cell prints it: B's figure, the change, and A's
/// figure for the line under it. The values are kept for sorting: by B's
/// figure, or by the change.
/// </summary>
public sealed record Cell(string B, string A, Change Change, double? ValueB = null, double? ValueA = null)
{
    /// <summary>Nothing on either side.</summary>
    public static readonly Cell Blank = new(CompareSheet.Dash, CompareSheet.Dash, Change.Blank);

    /// <summary>B − A, the change sorted by; null without both.</summary>
    public double? Delta => ValueA is double a && ValueB is double b && double.IsFinite(a) && double.IsFinite(b) ? b - a : null;

    /// <summary>The change as a hover card reads it: "+44.0 · +21.6%".</summary>
    public string Spoken => Change.Sub.Length > 0 ? Change.Sub + " · " + Change.Main : Change.Main;

    public static Cell Of(double? a, double? b, Func<double, string> format, ChangeKind kind, int good) =>
        new(b is double y && double.IsFinite(y) ? format(y) : CompareSheet.Dash,
            a is double x && double.IsFinite(x) ? format(x) : CompareSheet.Dash,
            Change.Of(a, b, kind, format, good), b, a);

    /// <summary>A figure for each side, with no change printed.</summary>
    public static Cell Plain(double? a, double? b, Func<double, string> format) =>
        new(b is double y ? format(y) : CompareSheet.Dash, a is double x ? format(x) : CompareSheet.Dash, Change.Blank, b, a);

    /// <summary>Words for each side: a name, a job.</summary>
    public static Cell Words(string a, string b) => new(b, a, Change.Blank);
}

/// <summary>Which runs a line is in.</summary>
public enum RunSide { Both, AOnly, BOnly }

/// <summary>One of the band's figures.</summary>
public sealed record BandCell(string Key, string Label, Cell Cell);

/// <summary>
/// One compared line of a table: a character, an action, a target, a type,
/// a heal. Its cells are by the table's column keys.
/// </summary>
/// <param name="Label">The name as drawn.</param>
/// <param name="JobA">The character's job in A, or "" (not a character, not in A, or not reported).</param>
/// <param name="ColorJob">The job whose colour the line takes, or "" for <paramref name="Slot"/>'s.</param>
/// <param name="Fraction">B's leading figure over B's leader, 0 to 1: the row's shade.</param>
/// <param name="TickA">A's leading figure over B's leader, 0 to 1: A's tick.</param>
public sealed record CompareLine(string Key, string Label, RunSide Side, string JobA, string JobB, string ColorJob, int Slot,
                                 double Fraction, double TickA, IReadOnlyDictionary<string, Cell> Cells)
{
    /// <summary>The character changed main job between the runs.</summary>
    public bool JobChanged => JobA.Length > 0 && JobB.Length > 0 && Main(JobA) != Main(JobB);

    static string Main(string label) => label.Split('/')[0];

    /// <summary>"DRK → SAM" for a job change; otherwise the one job there is.</summary>
    public string Job => JobChanged ? Main(JobA) + " → " + Main(JobB) : JobB.Length > 0 ? JobB : JobA;

    public Cell this[string column] => Cells.GetValueOrDefault(column) ?? Cell.Blank;
}

/// <summary>A character's actions (or heals), under their heading.</summary>
public sealed record CompareGroup(CompareLine Head, IReadOnlyList<CompareLine> Lines);

/// <summary>A's action of one character against B's of the same or another: a
/// pair the player made. Keys are each run's row keys.</summary>
public sealed record CrossPair(string KeyA, string ActionA, string KeyB, string ActionB)
{
    public CrossPair Swapped => new(KeyB, ActionB, KeyA, ActionA);

    /// <summary>The pair as one key, for a row of Actions.</summary>
    public string Key => string.Join("\t", KeyA, ActionA, KeyB, ActionB);
}

/// <summary>
/// One line of Deltas: a character, or one of their actions under them.
/// <see cref="Total"/> is the line's damage (or healing) in each run and its
/// change: a character's over A as a percentage with the plain difference
/// beside it, an action's as the plain difference, an action of one run only
/// counted as nothing in the other. <see cref="Uses"/> and
/// <see cref="Average"/> are an action's: B's count and its change, B's
/// average and its change over A's. A character's change is their actions'
/// summed exactly.
/// </summary>
/// <param name="Delta">B − A, what the bar is drawn by (a character in one run
/// only: their total that run, negative for A).</param>
public sealed record DeltaLine(string Key, string Label, RunSide Side, bool JobChanged, double Delta,
                               Cell Total, Cell Uses, Cell Average, IReadOnlyList<DeltaLine> Actions);

/// <summary>Deltas: the party's total in each run, and a line per character.</summary>
/// <param name="Scale">The largest character change: it reaches a bar's end.</param>
public sealed record DeltaSheet(bool Healing, Cell Party, IReadOnlyList<DeltaLine> Lines, double Scale);

/// <summary>One monster type of the Battle Log, both runs.</summary>
public sealed record TallyLine(string Name, Cell Kills, Cell Dps, Cell Hps, Cell KillTime)
{
    public Cell this[string column] => column switch
    {
        "kills" => Kills, "dps" => Dps, "hps" => Hps, "killTime" => KillTime, _ => Cell.Blank,
    };
}

/// <summary>Two ends of a drill-down measured: the histogram, and its figures.</summary>
public sealed record CompareDrill(ActionSpread Spread, IReadOnlyList<BandCell> Figures);

/// <summary>What a comparison is of, besides the two runs.</summary>
public sealed record CompareOptions
{
    /// <summary>A line is a main job, everyone on it together.</summary>
    public bool ByJob { get; init; }
    public bool Skillchains { get; init; } = true;
    /// <summary>Draw every character as their job: a comparison has no owner.</summary>
    public bool HideNames { get; init; }
    public IReadOnlySet<string>? Targets { get; init; }
    public IReadOnlySet<string>? Types { get; init; }
    /// <summary>Characters isolated, by B's names: both runs are cut to them.</summary>
    public IReadOnlySet<string>? Characters { get; init; }
    /// <summary>A's characters matched by hand with B's: A's name → B's.</summary>
    public IReadOnlyDictionary<string, string>? Matches { get; init; }
    /// <summary>One character of a run as the whole of it, or null for the party.</summary>
    public string? ScopeA { get; init; }
    public string? ScopeB { get; init; }
}

/// <summary>
/// Everything the Compare section prints for a pair of runs, as text and
/// values: the band, the tables, Deltas, the Battle Log, the cumulative
/// lines and the drill-down's figures, for damage and for healing both.
/// Colours are the caller's.
///
/// <para>Nothing is counted here. <see cref="Compare"/> measures each run
/// with the same code the live sections use and lines the two up; this words
/// the result. A is the baseline throughout: every change is B − A, and A's
/// figure is always its whole run's. A live B is its figures so far.</para>
///
/// <para><b>Unmeasured is a dash, never zero.</b> A character missing from a
/// run has no DPS there. A total of a line that exists on the other side
/// counts the missing side as zero: against nothing, it reads "new".</para>
/// </summary>
public sealed class CompareSheet
{
    public const string Dash = "—";

    public Measurement A { get; private init; } = null!;
    public Measurement B { get; private init; } = null!;
    public CompareOptions Options { get; private init; } = new();
    public bool HasA => A.Present;
    public bool HasB => B.Present;
    /// <summary>Targets are isolated: no rate is given (<see cref="Snapshot.Rate"/>).</summary>
    public bool Isolating => Isolated.Count > 0;

    public IReadOnlyList<BandCell> DamageBand { get; private init; } = [];
    public IReadOnlyList<BandCell> HealBand { get; private init; } = [];

    public IReadOnlyList<CompareLine> Characters { get; private init; } = [];
    public CompareLine Party { get; private init; } = null!;
    public IReadOnlyList<CompareLine> Healers { get; private init; } = [];
    public CompareLine HealParty { get; private init; } = null!;

    /// <summary>Each character's actions, in the order of <see cref="Characters"/>.</summary>
    public IReadOnlyList<CompareGroup> Actions { get; private init; } = [];
    /// <summary>Each healer's heals (By caster).</summary>
    public IReadOnlyList<CompareGroup> Heals { get; private init; } = [];
    /// <summary>The party's healing by spell, and by who received it.</summary>
    public IReadOnlyList<CompareLine> HealSpells { get; private init; } = [];
    public IReadOnlyList<CompareLine> HealTargets { get; private init; } = [];

    public IReadOnlyList<TallyLine> Tally { get; private init; } = [];
    public TallyLine AllMonsters { get; private init; } = null!;

    /// <summary>Damage to each target (the Filters pane's By target), every
    /// target whichever are isolated.</summary>
    public IReadOnlyList<CompareLine> TargetLines { get; private init; } = [];
    /// <summary>Damage of each type, keyed by type.</summary>
    public IReadOnlyList<CompareLine> TypeLines { get; private init; } = [];
    public IReadOnlyList<string> Isolated { get; private init; } = [];
    public IReadOnlyList<string> IsolatedTypes { get; private init; } = [];
    public IReadOnlyList<PairTotal> TargetList { get; private init; } = [];
    public IReadOnlyList<PairTotal> TypeList { get; private init; } = [];

    public Pace DamagePace { get; private init; } = new();
    public Pace HealPace { get; private init; } = new();

    /// <summary>Every line of the character tables, by key.</summary>
    public IReadOnlyDictionary<string, Identity> Ids { get; private init; } = new Dictionary<string, Identity>();

    /// <summary>Characters of one run with no line in the other: whom a
    /// "Match with" may pair them with. A's are by A's own names.</summary>
    public IReadOnlyList<string> OnlyA { get; private init; } = [];
    public IReadOnlyList<string> OnlyB { get; private init; } = [];

    Naming names = null!;

    // ------------------------------------------------------------- spelling

    public static string Whole(double v) => Format.Int(v);
    public static string Rate(double v) => Format.Num(v, 1);
    public static string Percent(double v) => Format.Num(v * 100, 1) + "%";
    public static string Seconds(double v) => Format.Num(v, 3);
    static string Clock(double seconds) => Format.Stopwatch(seconds * 1000, tenths: true);
    static string KillClock(double seconds) => Format.Elapsed(seconds * 1000, tenths: true);

    /// <summary>A shade's or tick's length: a value out of the leader's.</summary>
    static double Share(double? v, double max) =>
        v is double x && x > 0 && double.IsFinite(x) && max > 0 ? Math.Clamp(x / max, 0.005, 1) : 0;

    // ------------------------------------------------------------- columns

    /// <summary>How a column's change is said and which way is better.</summary>
    sealed record Spec(string Key, ChangeKind Kind, int Good, Func<double, string> Format);

    static readonly Spec[] CharacterSpecs =
    [
        new("damage", ChangeKind.Percent, 1, Whole),
        new("share", ChangeKind.Points, 1, Percent),
        new("dps", ChangeKind.Percent, 1, Rate),
        new("accuracy", ChangeKind.Points, 1, Percent),
        new("crit", ChangeKind.Points, 1, Percent),
        // Lower is better: less time between rounds.
        new("delay", ChangeKind.Plain, -1, Seconds),
        new("rangedAccuracy", ChangeKind.Points, 1, Percent),
        new("rangedCrit", ChangeKind.Points, 1, Percent),
        new("wsDamage", ChangeKind.Percent, 1, Whole),
        new("wsAvg", ChangeKind.Percent, 1, Whole),
        new("wsShare", ChangeKind.Points, 1, Percent),
        new("wsAccuracy", ChangeKind.Points, 1, Percent),
        new("scDamage", ChangeKind.Percent, 1, Whole),
        new("scShare", ChangeKind.Points, 1, Percent),
        new("petDamage", ChangeKind.Percent, 1, Whole),
        new("petAccuracy", ChangeKind.Points, 1, Percent),
        // Fewer is better.
        new("deaths", ChangeKind.Count, -1, Whole),
    ];

    /// <summary>The healing tables: amounts and counts are never coloured;
    /// deaths are, fewer being better.</summary>
    static readonly Spec[] HealerSpecs =
    [
        new("healing", ChangeKind.Percent, 0, Whole),
        new("share", ChangeKind.Points, 0, Percent),
        new("hps", ChangeKind.Percent, 0, Rate),
        new("casts", ChangeKind.Count, 0, Whole),
        new("avg", ChangeKind.Percent, 0, Whole),
        new("deaths", ChangeKind.Count, -1, Whole),
    ];

    static readonly Spec[] ActionSpecs =
    [
        // A count: how much was done, not how well.
        new("hits", ChangeKind.Count, 0, Whole),
        new("accuracy", ChangeKind.Points, 1, Percent),
        new("total", ChangeKind.Percent, 1, Whole),
        new("avg", ChangeKind.Percent, 1, Whole),
        // One hit: not coloured.
        new("max", ChangeKind.Percent, 0, Whole),
    ];

    static readonly Spec[] HealSpecs =
    [
        new("casts", ChangeKind.Count, 0, Whole),
        new("total", ChangeKind.Percent, 0, Whole),
        new("avg", ChangeKind.Percent, 0, Whole),
        new("max", ChangeKind.Percent, 0, Whole),
    ];

    /// <summary>Which way is better, for a column of one of the tables: +1,
    /// −1, or 0 for a column never coloured. For tests and the tables' keys.</summary>
    public static int Better(string table, string column)
    {
        var specs = table switch
        {
            "cbars" => CharacterSpecs, "chbars" => HealerSpecs, "cactions" => ActionSpecs, "cheals" => HealSpecs,
            _ => Array.Empty<Spec>(),
        };
        if (specs.FirstOrDefault(s => s.Key == column) is { } spec) return spec.Good;
        return (table, column) switch
        {
            ("cfights", "dps") => 1,
            ("cfights", "killTime") => -1,
            ("cfilterTargets" or "cfilterTypes", "damage" or "share") => 1,
            _ => 0,
        };
    }

    static Dictionary<string, Cell> Cells(Spec[] specs, Func<string, double?> a, Func<string, double?> b)
    {
        var cells = new Dictionary<string, Cell>(StringComparer.Ordinal);
        foreach (var s in specs) cells[s.Key] = Cell.Of(a(s.Key), b(s.Key), s.Format, s.Kind, s.Good);
        return cells;
    }

    /// <summary>One character's figure for a column of Overview.</summary>
    static double? Pick(string column, ActorTotals t) => column switch
    {
        "damage" => t.Total,
        "share" => t.Share,
        "dps" => t.Dps,
        "accuracy" => t.AutoAcc,
        "crit" => t.AutoCrit,
        "delay" => t.Delay is double ms ? ms / 1000 : null,
        "rangedAccuracy" => t.RangedAcc,
        "rangedCrit" => t.RangedCrit,
        "wsDamage" => t.WsTotal,
        "wsAvg" => t.WsAvg,
        "wsShare" => t.WsShare,
        "wsAccuracy" => t.WsAcc,
        "scDamage" => t.ScTotal,
        "scShare" => t.ScShare,
        "petDamage" => t.PetTotal,
        "petAccuracy" => t.PetAcc,
        _ => null,
    };

    /// <summary>The party's figure for a column of Overview: the Party line.</summary>
    static double? Pick(string column, Measurement m) => column switch
    {
        "damage" => m.Total,
        "share" => m.Total > 0 ? 1 : null,
        "dps" => m.Dps,
        "accuracy" => m.Agg.Party.AutoAcc,
        "crit" => m.Agg.Party.AutoCrit,
        "rangedAccuracy" => m.Agg.Party.RangedAcc,
        "rangedCrit" => m.Agg.Party.RangedCrit,
        "wsDamage" => m.Agg.Party.WsTotal,
        "wsAvg" => m.Agg.Party.WsAvg,
        "wsShare" => m.Agg.Party.WsShare,
        "wsAccuracy" => m.Agg.Party.WsAcc,
        "scDamage" => m.Agg.Party.ScTotal,
        "scShare" => m.Agg.Party.ScShare,
        "petDamage" => m.Agg.Party.PetTotal,
        "petAccuracy" => m.Agg.Party.PetAcc,
        "deaths" => m.Kills.Deaths.Count,
        _ => null,
    };

    // ------------------------------------------------------------- the sheet

    /// <summary>Two runs compared. Either may be missing: its side is then
    /// dashes and nothing is compared.</summary>
    public static CompareSheet Of(ImportedParse? a, ImportedParse? b, CompareOptions? options = null)
    {
        var o = options ?? new CompareOptions();
        return Of(MeasureA(a, o), MeasureB(b, o), o);
    }

    /// <summary>Run A measured as a comparison measures it: its characters
    /// matched by hand keyed by B's names.</summary>
    public static Measurement MeasureA(ImportedParse? a, CompareOptions o)
    {
        if (a == null) return Measurement.Absent();
        var matches = o.Matches;
        // One character of each run against one of the other: A's is B's.
        if (!o.ByJob && o.ScopeA is { } sa && o.ScopeB is { } sb && sa != sb)
            matches = new Dictionary<string, string>(matches ?? new Dictionary<string, string>(), StringComparer.Ordinal) { [sa] = sb };
        return Compare.Measure(a, o.Skillchains, o.ByJob, o.Targets, o.Types, o.Characters, matches, o.ScopeA);
    }

    /// <summary>Run B measured as a comparison measures it.</summary>
    public static Measurement MeasureB(ImportedParse? b, CompareOptions o) =>
        b != null ? Compare.Measure(b, o.Skillchains, o.ByJob, o.Targets, o.Types, o.Characters, null, o.ScopeB)
                  : Measurement.Absent();

    /// <summary>The same over two runs already measured.</summary>
    public static CompareSheet Of(Measurement ma, Measurement mb, CompareOptions o)
    {
        var d = Compare.Diff(ma, mb);
        var names = new Naming(d, ma, mb, o.ByJob, o.HideNames);
        IReadOnlyList<string> isolated = Targets.Only(o.Targets) is { } only ? Targets.Sorted(only) : [];
        IReadOnlyList<string> isolatedTypes = Targets.Only(o.Types) is { } onlyTypes ? DamageTypes.Sorted(onlyTypes) : [];
        var byName = Totals(ma.Targets, mb.Targets);
        var byType = Totals(ma.Kinds, mb.Kinds);
        bool rates = isolated.Count == 0;

        var characters = CharactersOf(d, ma, mb, names, rates);
        var healers = HealersOf(d, ma, mb, names);
        var sheet = new CompareSheet
        {
            A = ma, B = mb, Options = o, names = names,
            Isolated = isolated,
            IsolatedTypes = isolatedTypes,
            TargetList = Targets.Pickable(byName.Keys).Select(n => byName[n]).ToList(),
            TypeList = DamageTypes.Sorted(byType.Keys).Select(k => byType[k]).ToList(),
            DamageBand = DamageBandOf(ma, mb, isolated, isolatedTypes),
            HealBand = HealBandOf(ma, mb, names),
            DamagePace = Compare.Pace(ma, mb),
            HealPace = Compare.Pace(ma, mb, healing: true),
            Characters = characters,
            Party = PartyOf(ma, mb, rates),
            Healers = healers,
            HealParty = HealPartyOf(ma, mb),
            Actions = d.Rows.Select(r => ActionsOf(r, ma, mb, names)).ToList(),
            Heals = d.Heals.Select(r => HealsOf(r, ma, mb, names)).ToList(),
            HealSpells = TotalLines(Compare.Totals(ma.HealSpells, mb.HealSpells), n => n, HealTotalSpecs, ma.Heal.Total, mb.Heal.Total),
            HealTargets = TotalLines(Compare.Totals(ma.HealTargets, mb.HealTargets), names.Draw, HealTotalSpecs, ma.Heal.Total, mb.Heal.Total),
            TargetLines = TotalLines(d.Targets, n => n.Length > 0 ? n : Dash, FilterSpecs, ma.Targets.Values.Sum(),
                                     mb.Targets.Values.Sum()),
            // In the order of the types' own table (Compare.KindLabels).
            TypeLines = TotalLines(d.Kinds.Select(k => new PairTotal(k.Key, k.A, k.B)).ToList(), DamageTypes.Label, FilterSpecs,
                                   ma.Kinds.Values.Sum(), mb.Kinds.Values.Sum()),
            Tally = TallyOf(ma, mb),
            AllMonsters = AllOf(ma, mb),
            Ids = names.Ids,
            OnlyA = d.Rows.Where(r => r.B == null).Select(r => ma.Original.GetValueOrDefault(r.Key, r.Key))
                     .Concat(d.Heals.Where(r => r.B == null && !d.Rows.Any(x => x.Key == r.Key)).Select(r => r.Key)).Distinct().ToList(),
            OnlyB = d.Rows.Where(r => r.A == null).Select(r => r.Key)
                     .Concat(d.Heals.Where(r => r.A == null && !d.Rows.Any(x => x.Key == r.Key)).Select(r => r.Key)).Distinct().ToList(),
        };
        return sheet;
    }

    static OrderedDictionary<string, PairTotal> Totals(OrderedDictionary<string, double> a, OrderedDictionary<string, double> b)
    {
        var map = new OrderedDictionary<string, PairTotal>(StringComparer.Ordinal);
        foreach (var t in Compare.Totals(a, b)) map[t.Key] = t;
        return map;
    }

    // ----------------------------------------------------------------- band

    static BandCell Band(string key, string label, double? a, double? b, Func<double, string> format, ChangeKind kind, int good) =>
        new(key, label, Cell.Of(a, b, format, kind, good));

    static double? Has(Measurement m, double? v) => m.Present ? v : null;

    static List<BandCell> DamageBandOf(Measurement ma, Measurement mb, IReadOnlyList<string> isolated, IReadOnlyList<string> types) =>
    [
        // Not coloured: a shorter run is not obviously a better one.
        Band("length", "Length", Has(ma, ma.Duration), Has(mb, mb.Duration), Clock, ChangeKind.Time, 0),
        Band("total", DamageTypes.Heading(types, isolated), Has(ma, ma.Total), Has(mb, mb.Total), Whole, ChangeKind.Percent, 1),
        Band("dps", "Party DPS", ma.Dps, mb.Dps, Rate, ChangeKind.Percent, 1),
        Band("accuracy", "Accuracy", ma.Accuracy, mb.Accuracy, Percent, ChangeKind.Points, 1),
        Band("crit", "Crit %", ma.Agg.Party.AutoCrit, mb.Agg.Party.AutoCrit, Percent, ChangeKind.Points, 1),
        Band("wsAvg", "WS avg", ma.WsAvg, mb.WsAvg, Whole, ChangeKind.Percent, 1),
        // A count: not coloured.
        Band("kills", "Kills", Has(ma, ma.Killed.Count), Has(mb, mb.Killed.Count), Whole, ChangeKind.Count, 0),
        Band("deaths", "Deaths", Has(ma, ma.Kills.Deaths.Count), Has(mb, mb.Kills.Deaths.Count), Whole, ChangeKind.Count, -1),
    ];

    static List<BandCell> HealBandOf(Measurement ma, Measurement mb, Naming names)
    {
        static double? Hps(Measurement m) => m.Present ? Counting.PerSecond(m.Heal.Total, m.Duration) : null;
        string Top(Measurement m) =>
            m.Present && m.Heal.Actors.Count > 0 ? names.Draw(m.Heal.Actors.MaxBy(h => h.Total)!.Name) : Dash;
        string KillsDeaths(Measurement m) =>
            m.Present ? Format.Int(m.Killed.Count) + " / " + Format.Int(m.Kills.Deaths.Count) : Dash;
        var deaths = Change.Of(Has(ma, ma.Kills.Deaths.Count), Has(mb, mb.Kills.Deaths.Count), ChangeKind.Count, Whole, -1);
        return
        [
            Band("length", "Length", Has(ma, ma.Duration), Has(mb, mb.Duration), Clock, ChangeKind.Time, 0),
            // Healing amounts are never coloured.
            Band("total", "Total healing", Has(ma, ma.Heal.Total), Has(mb, mb.Heal.Total), Whole, ChangeKind.Percent, 0),
            Band("hps", "Party HPS", Hps(ma), Hps(mb), Rate, ChangeKind.Percent, 0),
            Band("casts", "Casts", Has(ma, ma.Heal.Casts), Has(mb, mb.Heal.Casts), Whole, ChangeKind.Count, 0),
            Band("avg", "Avg / cast", ma.Heal.Avg, mb.Heal.Avg, Whole, ChangeKind.Percent, 0),
            Band("max", "Biggest heal", Has(ma, ma.Heal.Max), Has(mb, mb.Heal.Max), Whole, ChangeKind.Percent, 0),
            new("killsDeaths", "Kills / Deaths", new Cell(KillsDeaths(mb), KillsDeaths(ma), deaths,
                                                          Has(mb, mb.Kills.Deaths.Count), Has(ma, ma.Kills.Deaths.Count))),
            new("top", "Top healer", Cell.Words(Top(ma), Top(mb))),
        ];
    }

    // ---------------------------------------------------------- characters

    static RunSide SideOf(bool inA, bool inB) => inA && inB ? RunSide.Both : inA ? RunSide.AOnly : RunSide.BOnly;

    CompareLine LineFor(string key, RunSide side, double fraction, double tick, Dictionary<string, Cell> cells) =>
        Line(names, key, side, fraction, tick, cells);

    static CompareLine Line(Naming names, string key, RunSide side, double fraction, double tick, Dictionary<string, Cell> cells)
    {
        var id = names.Ids.GetValueOrDefault(key) ?? new Identity(key, names.Draw(key), "", "", "", 0, "", false);
        cells["job"] = Cell.Words(id.JobA, id.JobB);
        return new CompareLine(key, id.Label, side, id.JobA, id.JobB, id.ColorJob, id.Slot, fraction, tick, cells);
    }

    /// <summary>
    /// Overview's lines: a line per character (or job) in either run, those
    /// in both first, each by B's damage, then those in one run only.
    /// </summary>
    static List<CompareLine> CharactersOf(CompareRows d, Measurement ma, Measurement mb, Naming names, bool rates)
    {
        double leader = mb.Agg.Actors.Select(x => x.Total).DefaultIfEmpty(0).Max();
        if (leader == 0) leader = ma.Agg.Actors.Select(x => x.Total).DefaultIfEmpty(0).Max();
        var list = new List<CompareLine>();
        foreach (var r in d.Rows)
        {
            var (a, b) = (r.A, r.B);
            if (a != null) a.Delay = ma.DelayOf(r.Key);
            if (b != null) b.Delay = mb.DelayOf(r.Key);
            double? A(string c) => !ma.Present ? null : c switch
            {
                // Missing from a run, a total there is nothing; a rate or an
                // average is not measured.
                "damage" => a?.Total ?? 0,
                "deaths" => ma.DeathsOf(r.Key),
                "dps" when !rates => null,
                _ => a != null ? Pick(c, a) : null,
            };
            double? B(string c) => !mb.Present ? null : c switch
            {
                "damage" => b?.Total ?? 0,
                "deaths" => mb.DeathsOf(r.Key),
                "dps" when !rates => null,
                _ => b != null ? Pick(c, b) : null,
            };
            list.Add(Line(names, r.Key, SideOf(a != null, b != null), Share(b?.Total, leader), Share(a?.Total, leader),
                          Cells(CharacterSpecs, A, B)));
        }
        return [.. list.OrderBy(l => l.Side == RunSide.Both ? 0 : 1)];
    }

    static CompareLine PartyOf(Measurement ma, Measurement mb, bool rates)
    {
        double? A(string c) => !ma.Present || c == "dps" && !rates ? null : Pick(c, ma);
        double? B(string c) => !mb.Present || c == "dps" && !rates ? null : Pick(c, mb);
        var cells = Cells(CharacterSpecs, A, B);
        cells["job"] = Cell.Words(Format.Int(ma.Characters), Format.Int(mb.Characters));
        return new CompareLine("", "Party", RunSide.Both, "", "", "", 0, 1, ma.Total > 0 && mb.Total > 0 ? Math.Min(1, ma.Total / mb.Total) : 0, cells);
    }

    /// <summary>
    /// Overview's By target tab: under each target either run acted at, a
    /// line per character, of what they did to it. No rate under a target.
    /// Worked out when asked for.
    /// </summary>
    public IReadOnlyList<CompareGroup> TargetGroups()
    {
        var ga = A.Present ? Zerg.Core.TargetGroups.Of(A.Rows, null, null, A.OnlyTypes).ToDictionary(g => g.Name, StringComparer.Ordinal) : [];
        var gb = B.Present ? Zerg.Core.TargetGroups.Of(B.Rows, null, null, B.OnlyTypes).ToDictionary(g => g.Name, StringComparer.Ordinal) : [];
        var keys = gb.Keys.Concat(ga.Keys.Where(k => !gb.ContainsKey(k))).ToList();
        var groups = new List<CompareGroup>();
        foreach (var name in keys)
        {
            ga.TryGetValue(name, out var a);
            gb.TryGetValue(name, out var b);
            double leader = (b ?? a)!.Characters.Select(c => c.Total).DefaultIfEmpty(0).Max();
            var ca = a?.Characters.ToDictionary(c => c.Name, StringComparer.Ordinal) ?? [];
            var cb = b?.Characters.ToDictionary(c => c.Name, StringComparer.Ordinal) ?? [];
            var lines = new List<CompareLine>();
            foreach (var who in cb.Keys.Concat(ca.Keys.Where(k => !cb.ContainsKey(k))))
            {
                ca.TryGetValue(who, out var x);
                cb.TryGetValue(who, out var y);
                if ((x?.Total ?? 0) == 0 && (y?.Total ?? 0) == 0) continue;
                double? Va(string c) => !A.Present || c is "dps" or "deaths" ? null : c == "damage" ? x?.Total ?? 0 : x != null ? Pick(c, x) : null;
                double? Vb(string c) => !B.Present || c is "dps" or "deaths" ? null : c == "damage" ? y?.Total ?? 0 : y != null ? Pick(c, y) : null;
                lines.Add(LineFor(who, SideOf(x != null, y != null), Share(y?.Total, leader), Share(x?.Total, leader),
                                  Cells(CharacterSpecs, Va, Vb)));
            }
            lines.Sort((p, q) => (q["damage"].ValueB ?? 0).CompareTo(p["damage"].ValueB ?? 0));
            double whole = Math.Max(a?.Total ?? 0, b?.Total ?? 0);
            var head = new Dictionary<string, Cell>(StringComparer.Ordinal)
            {
                ["damage"] = Cell.Of(A.Present ? a?.Total ?? 0 : null, B.Present ? b?.Total ?? 0 : null, Whole, ChangeKind.Percent, 1),
            };
            groups.Add(new CompareGroup(new CompareLine(name, name, SideOf(a != null, b != null), "", "", "", 0, 0, 0, head), lines));
        }
        return groups;
    }

    // ------------------------------------------------------------- actions

    static CompareGroup ActionsOf(Pair<ActorTotals> r, Measurement ma, Measurement mb, Naming names)
    {
        double leader = 0;
        foreach (var x in Compare.Actions(r)) leader = Math.Max(leader, x.B?.Total ?? x.A?.Total ?? 0);
        var lines = Compare.Actions(r).Select(x => ActionLine(x, ma, mb, r.A?.Total, r.B?.Total, leader)).ToList();
        var head = new Dictionary<string, Cell>(StringComparer.Ordinal)
        {
            ["total"] = Cell.Of(ma.Present ? r.A?.Total ?? 0 : null, mb.Present ? r.B?.Total ?? 0 : null, Whole, ChangeKind.Percent, 1),
        };
        return new CompareGroup(Line(names, r.Key, SideOf(r.A != null, r.B != null), 0, 0, head), lines);
    }

    static CompareLine ActionLine(Pair<ActionTotals> x, Measurement ma, Measurement mb, double? wholeA, double? wholeB, double leader)
    {
        var (a, b) = (x.A, x.B);
        var cells = Cells(ActionSpecs, c => ma.Present ? ActionFigure(c, a) : null, c => mb.Present ? ActionFigure(c, b) : null);
        // The share of the character's damage, B's, with no change.
        static double? Of(ActionTotals? t, double? whole) => t != null && whole is > 0 ? t.Total / whole : null;
        cells["share"] = Cell.Plain(Of(a, wholeA), Of(b, wholeB), Percent);
        return new CompareLine(x.Key, x.Key, SideOf(a != null, b != null), "", "", "", 0,
                               Share(b?.Total, leader), Share(a?.Total, leader), cells);
    }

    /// <summary>An action's figure; a dash for an accuracy it cannot have or
    /// an average with no hit, a total of 0 when the run never used it.</summary>
    static double? ActionFigure(string column, ActionTotals? t) => column switch
    {
        "hits" => t?.Hits ?? 0,
        "accuracy" => t is { Tries: > 0 } ? t.Accuracy : null,
        "total" => t?.Total ?? 0,
        "avg" => t is { Hits: > 0 } ? t.Avg : null,
        "max" => t is { Hits: > 0 } ? t.Max : null,
        _ => null,
    };

    /// <summary>
    /// The line of a pair the player made: A's action of one character
    /// against B's of the same or another, with the figures every action
    /// line has. Its share is a dash.
    /// </summary>
    public CompareLine Cross(CrossPair p)
    {
        static ActionTotals? Of(Measurement m, string key, string action) =>
            m.Agg.Actors.FirstOrDefault(x => x.Name == key)?.Actions.GetValueOrDefault(action);
        ActionTotals? a = Of(A, p.KeyA, p.ActionA), b = Of(B, p.KeyB, p.ActionB);
        var cells = Cells(ActionSpecs, c => A.Present ? ActionFigure(c, a) : null, c => B.Present ? ActionFigure(c, b) : null);
        cells["share"] = Cell.Blank;
        return new CompareLine(p.Key, p.ActionB, SideOf(a != null, b != null), "", "", "", 0, 0, 0, cells);
    }

    /// <summary>The same for two heals.</summary>
    public CompareLine CrossHeal(CrossPair p)
    {
        static HealAction? Of(Measurement m, string key, string action) =>
            m.Heal.Actors.FirstOrDefault(x => x.Name == key)?.Actions.GetValueOrDefault(action);
        HealAction? a = Of(A, p.KeyA, p.ActionA), b = Of(B, p.KeyB, p.ActionB);
        var cells = Cells(HealSpecs, c => A.Present ? HealFigure(c, a) : null, c => B.Present ? HealFigure(c, b) : null);
        cells["share"] = Cell.Blank;
        return new CompareLine(p.Key, p.ActionB, SideOf(a != null, b != null), "", "", "", 0, 0, 0, cells);
    }

    /// <summary>What a drill-down's picker lists: a run's characters (by row
    /// key), those who did any damage (or healing) first.</summary>
    public IReadOnlyList<string> CharactersIn(bool runB, bool healing)
    {
        var m = runB ? B : A;
        return healing ? m.Heal.Actors.Where(h => h.Total > 0 || h.Casts > 0).Select(h => h.Name).ToList()
                       : m.Agg.Actors.Where(x => x.Total > 0).Select(x => x.Name).ToList();
    }

    /// <summary>A character's actions in one run, each with how many hits
    /// (or casts) it had, largest first.</summary>
    public IReadOnlyList<(string Action, int Count)> ActionsIn(bool runB, string key, bool healing)
    {
        var m = runB ? B : A;
        if (healing)
            return m.Heal.Actors.FirstOrDefault(h => h.Name == key) is { } h
                ? h.ActionList.OrderByDescending(x => x.Total).Select(x => (x.Name, x.Casts)).ToList() : [];
        return m.Agg.Actors.FirstOrDefault(x => x.Name == key) is { } t
            ? t.ActionList.Where(x => x.Total > 0).OrderByDescending(x => x.Total).Select(x => (x.Name, x.Hits)).ToList() : [];
    }

    /// <summary>
    /// A drill-down's two ends: A's action of one character against B's of
    /// the same or another, on bins the two share, with its figures. Hits
    /// and Max are not coloured; nor is anything of a heal.
    /// </summary>
    public CompareDrill Drill(CrossPair p, bool healing)
    {
        var s = healing ? Compare.HealSpread(A, B, p.KeyA, p.ActionA, p.KeyB, p.ActionB)
                        : Compare.Spread(A, B, p.KeyA, p.ActionA, p.KeyB, p.ActionB);
        var (a, b) = (s.A, s.B);
        int good = healing ? 0 : 1;
        static double? Of(Distribution? d, Func<Distribution, double> pick) => d is { Count: > 0 } ? pick(d) : null;
        BandCell Figure(string key, string label, Func<Distribution, double> pick, int better) =>
            Band(key, label, Of(a, pick), Of(b, pick), Whole, ChangeKind.Percent, better);
        var figures = new List<BandCell>
        {
            Band("hits", healing ? "Casts" : "Hits", A.Present ? a?.Count ?? 0 : null, B.Present ? b?.Count ?? 0 : null, Whole,
                 ChangeKind.Count, 0),
            Figure("avg", "Average", d => d.Avg, good),
            Figure("median", "Median", d => d.Median, good),
            Figure("p90", "90th pct", d => d.P90, good),
            Figure("max", "Max", d => d.Max, 0),
        };
        if (!healing)
            figures.Add(Band("crit", "Crit rate", Of(a, d => d.CritRate), Of(b, d => d.CritRate), Percent, ChangeKind.Points, 1));
        var spread = new ActionSpread(s.Bins, a?.Count ?? 0, b?.Count ?? 0, Of(a, d => d.Avg), Of(b, d => d.Avg), [], "",
                                      healing ? "cast" : "hit");
        return new CompareDrill(spread, figures);
    }

    // ------------------------------------------------------------- healing

    static List<CompareLine> HealersOf(CompareRows d, Measurement ma, Measurement mb, Naming names)
    {
        double leader = mb.Heal.Actors.Select(x => x.Total).DefaultIfEmpty(0).Max();
        if (leader == 0) leader = ma.Heal.Actors.Select(x => x.Total).DefaultIfEmpty(0).Max();
        var list = new List<CompareLine>();
        foreach (var r in d.Heals)
        {
            var (a, b) = (r.A, r.B);
            double? Of(Measurement m, HealerTotals? h, string c) => !m.Present ? null : c switch
            {
                "healing" => h?.Total ?? 0,
                "share" => h?.Share,
                "hps" => Counting.PerSecond(h?.Total ?? 0, m.Duration),
                "casts" => h?.Casts ?? 0,
                "avg" => h?.Avg,
                "deaths" => m.DeathsOf(r.Key),
                _ => null,
            };
            list.Add(Line(names, r.Key, SideOf(a != null, b != null), Share(b?.Total, leader), Share(a?.Total, leader),
                          Cells(HealerSpecs, c => Of(ma, a, c), c => Of(mb, b, c))));
        }
        return [.. list.OrderBy(l => l.Side == RunSide.Both ? 0 : 1)];
    }

    static CompareLine HealPartyOf(Measurement ma, Measurement mb)
    {
        static double? Of(Measurement m, string c) => !m.Present ? null : c switch
        {
            "healing" => m.Heal.Total,
            "share" => m.Heal.Total > 0 ? 1 : null,
            "hps" => Counting.PerSecond(m.Heal.Total, m.Duration),
            "casts" => m.Heal.Casts,
            "avg" => m.Heal.Avg,
            "deaths" => m.Kills.Deaths.Count,
            _ => null,
        };
        var cells = Cells(HealerSpecs, c => Of(ma, c), c => Of(mb, c));
        cells["job"] = Cell.Words(Format.Int(ma.Heal.Actors.Count), Format.Int(mb.Heal.Actors.Count));
        return new CompareLine("", "Party", RunSide.Both, "", "", "", 0, 1, 0, cells);
    }

    static double? HealFigure(string column, HealAction? h) => column switch
    {
        "casts" => h?.Casts ?? 0,
        "total" => h?.Total ?? 0,
        "avg" => h is { Casts: > 0 } ? h.Avg : null,
        "max" => h is { Casts: > 0 } ? h.Max : null,
        _ => null,
    };

    static CompareGroup HealsOf(Pair<HealerTotals> r, Measurement ma, Measurement mb, Naming names)
    {
        var pairs = Compare.HealActions(r);
        double leader = pairs.Select(x => x.B?.Total ?? x.A?.Total ?? 0).DefaultIfEmpty(0).Max();
        var lines = pairs.Select(x =>
        {
            var cells = Cells(HealSpecs, c => ma.Present ? HealFigure(c, x.A) : null, c => mb.Present ? HealFigure(c, x.B) : null);
            static double? Of(HealAction? h, double? whole) => h != null && whole is > 0 ? h.Total / whole : null;
            cells["share"] = Cell.Plain(Of(x.A, r.A?.Total), Of(x.B, r.B?.Total), Percent);
            return new CompareLine(x.Key, x.Key, SideOf(x.A != null, x.B != null), "", "", "", 0,
                                   Share(x.B?.Total, leader), Share(x.A?.Total, leader), cells);
        }).ToList();
        var head = new Dictionary<string, Cell>(StringComparer.Ordinal)
        {
            ["total"] = Cell.Of(ma.Present ? r.A?.Total ?? 0 : null, mb.Present ? r.B?.Total ?? 0 : null, Whole, ChangeKind.Percent, 0),
        };
        return new CompareGroup(Line(names, r.Key, SideOf(r.A != null, r.B != null), 0, 0, head), lines);
    }

    static readonly Spec[] HealTotalSpecs =
    [
        new("total", ChangeKind.Percent, 0, Whole),
    ];

    static readonly Spec[] FilterSpecs =
    [
        new("damage", ChangeKind.Percent, 1, Whole),
    ];

    /// <summary>Totals under one name each (a spell, a target, a type), with
    /// each side's share of its whole.</summary>
    static List<CompareLine> TotalLines(IReadOnlyList<PairTotal> rows, Func<string, string> draw, Spec[] specs,
                                        double wholeA, double wholeB)
    {
        double leader = rows.Select(r => r.B).DefaultIfEmpty(0).Max();
        if (leader == 0) leader = rows.Select(r => r.A).DefaultIfEmpty(0).Max();
        var key = specs[0].Key;
        int good = specs[0].Good;
        return rows.Select(r =>
        {
            var cells = Cells(specs, c => c == key ? r.A : null, c => c == key ? r.B : null);
            cells["share"] = Cell.Of(wholeA > 0 ? r.A / wholeA : null, wholeB > 0 ? r.B / wholeB : null, Percent, ChangeKind.Points, good);
            return new CompareLine(r.Key, draw(r.Key), SideOf(r.A > 0, r.B > 0), "", "", "", 0,
                                   Share(r.B, leader), Share(r.A, leader), cells);
        }).ToList();
    }

    // -------------------------------------------------------------- Deltas

    /// <summary>
    /// Deltas: the party's total damage (or healing) in each run, and a line
    /// per character: their totals and the change, and under them each of
    /// their actions' change in total, its uses and its average.
    ///
    /// <para>Totals, never rates: what each did more or less, with the
    /// clock beside them on the band. An action of one run only is counted
    /// as nothing in the other, so a character's change is their actions'
    /// summed, exactly, and so are the printed figures (totals are whole
    /// numbers).</para>
    ///
    /// <para>A character in one run only, or on another main job, has no
    /// actions under them: their actions are not one set of actions done
    /// more or less.</para>
    /// </summary>
    public DeltaSheet Deltas(bool healing)
    {
        int good = healing ? 0 : 1;
        var lines = new List<DeltaLine>();
        foreach (var g in healing ? Heals : Actions)
        {
            var head = g.Head;
            // The figures behind the line, from the run's own totals.
            var parts = new List<(string Name, int nA, double tA, int nB, double tB)>();
            if (healing)
            {
                var ha = A.Heal.Actors.FirstOrDefault(h => h.Name == head.Key);
                var hb = B.Heal.Actors.FirstOrDefault(h => h.Name == head.Key);
                foreach (var x in Compare.HealActions(new Pair<HealerTotals>(head.Key, ha, hb)))
                    parts.Add((x.Key, x.A?.Casts ?? 0, x.A?.Total ?? 0, x.B?.Casts ?? 0, x.B?.Total ?? 0));
            }
            else
            {
                var ta = A.Agg.Actors.FirstOrDefault(t => t.Name == head.Key);
                var tb = B.Agg.Actors.FirstOrDefault(t => t.Name == head.Key);
                foreach (var x in Compare.Actions(new Pair<ActorTotals>(head.Key, ta, tb)))
                    parts.Add((x.Key, x.A?.Hits ?? 0, x.A?.Total ?? 0, x.B?.Hits ?? 0, x.B?.Total ?? 0));
            }
            double sumA = 0, sumB = 0;
            var actions = new List<DeltaLine>();
            foreach (var (name, nA, tA, nB, tB) in parts)
            {
                sumA += tA;
                sumB += tB;
                var side = nA == 0 ? RunSide.BOnly : nB == 0 ? RunSide.AOnly : RunSide.Both;
                actions.Add(new DeltaLine(name, name, side, false, tB - tA,
                    Cell.Of(tA, tB, Whole, ChangeKind.Plain, good),
                    Cell.Of(nA, nB, Whole, ChangeKind.Count, 0),
                    Cell.Of(nA > 0 ? tA / nA : null, nB > 0 ? tB / nB : null, Whole, ChangeKind.Percent, good), []));
            }
            actions.Sort((p, q) => Math.Abs(q.Delta).CompareTo(Math.Abs(p.Delta)));
            bool both = head.Side == RunSide.Both;
            double? a = head.Side == RunSide.BOnly ? null : sumA, b = head.Side == RunSide.AOnly ? null : sumB;
            lines.Add(new DeltaLine(head.Key, head.Label, head.Side, head.JobChanged, sumB - sumA,
                Cell.Of(a, b, Whole, ChangeKind.Percent, good), Cell.Blank, Cell.Blank,
                both && !head.JobChanged ? actions : []));
        }

        // Those in both runs first, the biggest change first; then the rest.
        lines = [.. lines.OrderBy(l => l.Side != RunSide.Both ? 1 : 0).ThenByDescending(l => Math.Abs(l.Delta))];
        double scale = lines.Select(l => Math.Abs(l.Delta)).DefaultIfEmpty(0).Max();
        double? Party(Measurement m) => !m.Present ? null : healing ? m.Heal.Total : m.Total;
        var party = Cell.Of(Party(A), Party(B), Whole, ChangeKind.Percent, good);
        return new DeltaSheet(healing, party, lines, scale);
    }

    // ---------------------------------------------------------- Battle Log

    /// <summary>
    /// The Battle Log: each kind of monster either run killed, with how many
    /// each killed, the party's rate on that kind (its kills' damage over
    /// their lengths) and the average kill time. A kind one run never
    /// killed reads 0 kills there, and dashes.
    /// </summary>
    static List<TallyLine> TallyOf(Measurement ma, Measurement mb)
    {
        var names = new List<string>();
        foreach (var f in mb.Kills.Fights.Concat(ma.Kills.Fights)) if (!names.Contains(f.Target)) names.Add(f.Target);
        var lines = names.Select(n => TallyLineOf(n, ma, mb)).ToList();
        return [.. lines.OrderByDescending(l => l.Kills.ValueB ?? 0).ThenByDescending(l => l.Kills.ValueA ?? 0)
                        .ThenBy(l => l.Name, StringComparer.Ordinal)];
    }

    static TallyLine TallyLineOf(string name, Measurement ma, Measurement mb)
    {
        static List<Fight> Of(Measurement m, string n) => m.Kills.Fights.Where(f => f.Target == n).ToList();
        var (fa, fb) = (Of(ma, name), Of(mb, name));
        static double? Rate(List<Fight> fights, Func<Fight, double> amount)
        {
            double sum = 0, ms = 0;
            foreach (var f in fights)
                if (f.Length is double l && l > 0) { sum += amount(f); ms += l; }
            return ms > 0 ? sum / (ms / 1000) : null;
        }
        static double HealedIn(Measurement m, Fight f)
        {
            if (f.Length is not double l) return 0;
            double from = f.At - l, sum = 0;
            foreach (var h in m.HealEvents) if (h.T >= from && h.T <= f.At) sum += h.Dmg;
            return sum;
        }
        static double? Time(List<Fight> fights) => KillTotals.AverageLength(fights) is double ms ? ms / 1000 : null;
        return new TallyLine(name,
            Cell.Of(ma.Present ? fa.Count : null, mb.Present ? fb.Count : null, Whole, ChangeKind.Count, 0),
            Cell.Of(Rate(fa, f => f.Damage), Rate(fb, f => f.Damage), CompareSheet.Rate, ChangeKind.Percent, 1),
            Cell.Of(Rate(fa, f => HealedIn(ma, f)), Rate(fb, f => HealedIn(mb, f)), CompareSheet.Rate, ChangeKind.Percent, 0),
            // Fewer seconds is the good way.
            Cell.Of(Time(fa), Time(fb), KillClock, ChangeKind.Time, -1));
    }

    static TallyLine AllOf(Measurement ma, Measurement mb) =>
        new("All monsters",
            Cell.Of(ma.Present ? ma.Kills.Fights.Count : null, mb.Present ? mb.Kills.Fights.Count : null, Whole, ChangeKind.Count, 0),
            Cell.Blank, Cell.Blank, Cell.Blank);

    // ------------------------------------------------------- live drill-down

    /// <summary>
    /// Two actions of one session (or one parse), side by side: what the
    /// live sections' drill-down draws when a second action is picked.
    /// A side with no swings is dashes.
    /// </summary>
    /// <param name="healing">The two are heals: casts, and no accuracy.</param>
    public static ActionSpread Sides(Distribution? a, Distribution? b, bool healing)
    {
        static Distribution? Swung(Distribution? d) => d is { Swings: > 0 } ? d : null;
        var s = Compare.Shared(Swung(a), Swung(b));
        static string W(double? x) => x is double v ? Whole(v) : Dash;
        static string P(double? x) => x is double v ? Percent(v) : Dash;
        if (healing)
            return Worded(s, [new SpreadLine("Casts", "One per cast, however many people it reached", W(s.A?.Count),
                                             W(s.B?.Count), Change.Of(s.A?.Count, s.B?.Count, ChangeKind.Percent,
                                                                      Format.Int, 0))],
                          "Healed per cast", "cast", "heal");
        double? x = s.A?.Accuracy, y = s.B?.Accuracy;
        var hits = new SpreadLine("Hits", "Uses that dealt damage", W(s.A?.Count), W(s.B?.Count),
                                  Change.Of(s.A?.Count, s.B?.Count, ChangeKind.Percent, Format.Int, 0));
        var accuracy = new SpreadLine("Accuracy", "Swings that landed", P(x), P(y),
                                      Change.Of(x, y, ChangeKind.Points, Percent, 1));
        return Worded(s, [hits, accuracy], "Damage distribution", "hit", "action");
    }

    /// <param name="lead">The lines that lead the figures: how many there were, and how many landed.</param>
    /// <param name="title">What the chart is of, to open its caption.</param>
    /// <param name="unit">What one of the things counted is called.</param>
    /// <param name="side">What a side is: "action", "heal".</param>
    static ActionSpread Worded(Spread s, SpreadLine[] lead, string title, string unit, string side)
    {
        var (a, b) = (s.A, s.B);
        static double? Of(Distribution? d, Func<Distribution, double> pick) => d != null && d.Count != 0 ? pick(d) : null;
        static string W(double? x) => x is double v ? Whole(v) : Dash;
        SpreadLine Line(string label, Func<Distribution, double> pick, int good, string? tip = null)
        {
            double? x = Of(a, pick), y = Of(b, pick);
            return new SpreadLine(label, tip, W(x), W(y), Change.Of(x, y, ChangeKind.Percent, Format.Int, good));
        }

        var lines = new List<SpreadLine>(lead)
        {
            Line("Min", d => d.Min, 1),
            Line("Median", d => d.Median, 1, "Half the " + unit + "s were under this, half over"),
            Line("Average", d => d.Avg, 1),
            Line("90th pct", d => d.P90, 1, "Nine " + unit + "s in ten were under this"),
            Line("Max", d => d.Max, 1),
            // Neutral: a tighter spread is steadier, not stronger.
            Line("Std dev", d => d.Stdev, 0, "How far a " + unit + " typically falls from the average"),
        };
        if ((a?.Crits ?? 0) != 0 || (b?.Crits ?? 0) != 0)
        {
            double? x = Of(a, d => d.CritRate), y = Of(b, d => d.CritRate);
            lines.Add(new SpreadLine("Crit rate", "Hits that were critical", x is double p ? Percent(p) : Dash,
                                     y is double q ? Percent(q) : Dash, Change.Of(x, y, ChangeKind.Points, Percent, 1)));
        }

        const string Dot = " · ";
        var caption = s.Bins.Count == 0 ? "" :
            title + Dot + s.Bins.Count.ToString(CultureInfo.InvariantCulture) +
            (s.Bins.Count == 1 ? " bin" : " bins") + Dot + "a column is its share of that " + side + "’s " + unit + "s";
        return new ActionSpread(s.Bins, a?.Count ?? 0, b?.Count ?? 0, Of(a, d => d.Avg), Of(b, d => d.Avg), lines, caption,
                                unit);
    }

    // --------------------------------------------------------------- naming

    /// <summary>A name as drawn: a line's label, or a job standing in for a
    /// name while names are hidden.</summary>
    public string Draw(string name) => names.Draw(name);

    /// <summary>
    /// Who each line is, worked out once over the damage lines and then the
    /// healers who dealt none, so a healer who also dealt damage has the same
    /// colour and the same stand-in label in both tables.
    ///
    /// <para>By character, the job is read off whichever run the character
    /// was in, B first: the newer run is the one being judged. By job, the
    /// key is the job. A job is never borrowed from the other run: someone
    /// the party table did not report in one run was often on another job
    /// in it.</para>
    /// </summary>
    sealed class Naming
    {
        readonly Dictionary<string, Identity> ids = new(StringComparer.Ordinal);
        readonly Dictionary<string, int> used = new(StringComparer.Ordinal);
        readonly Dictionary<string, string> strangers = new(StringComparer.Ordinal);
        readonly Roster? ra, rb;
        readonly Measurement ma;
        readonly bool hide;

        public IReadOnlyDictionary<string, Identity> Ids => ids;

        public Naming(CompareRows d, Measurement ma, Measurement mb, bool byJob, bool hideNames)
        {
            (this.ma, ra, rb, hide) = (ma, ma.Present ? ma.Run.Source.Roster : null, mb.Present ? mb.Run.Source.Roster : null, hideNames);

            var rows = d.Rows.Select(r => (r.Key, InA: r.A != null, InB: r.B != null)).ToList();
            var seen = rows.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
            rows.AddRange(d.Heals.Where(r => !seen.Contains(r.Key)).Select(r => (r.Key, r.A != null, r.B != null)));

            for (int i = 0; i < rows.Count; i++)
            {
                var (key, inA, inB) = rows[i];
                if (byJob)
                {
                    var who = new List<string>();
                    foreach (var m in new[] { ma, mb })
                        if (m.Members.TryGetValue(key, out var mm))
                            foreach (var n in mm.Names) if (!who.Contains(n)) who.Add(n);
                    ids[key] = new Identity(key, key, "", "", key == Compare.UnknownJob ? "" : key, i,
                        hideNames ? who.Count.ToString(CultureInfo.InvariantCulture) + " character" + (who.Count == 1 ? "" : "s")
                                  : string.Join(", ", who),
                        false);
                    continue;
                }

                // A's own name for the character, where they were matched by hand.
                var own = ma.Original.GetValueOrDefault(key, key);
                string jobA = inA && ra != null ? ra.JobLabel(own) : "", jobB = inB && rb != null ? rb.JobLabel(key) : "";
                var main = Main(inB ? rb?.JobOf(key) : null) ?? Main(inA ? ra?.JobOf(own) : null) ?? "";
                var label = hideNames ? Alias(jobB.Length > 0 ? jobB : jobA) : own != key ? own + " → " + key : key;
                ids[key] = new Identity(key, label, jobA, jobB, main, i, "", hideNames);
            }
        }

        static string? Main(JobInfo? j) => j is { HasMain: true } ? j.Main : null;

        /// <summary>A job standing in for a name, numbered from the second
        /// character on it: "SAM/WAR", "SAM/WAR 2".</summary>
        string Alias(string job)
        {
            var label = job.Length > 0 ? job : Compare.UnknownJob;
            int k = used[label] = used.GetValueOrDefault(label) + 1;
            return k > 1 ? label + " " + k.ToString(CultureInfo.InvariantCulture) : label;
        }

        /// <summary>
        /// A name met outside the tables' own lines, as drawn: who a heal
        /// landed on. Someone with a line is drawn as their line is. While
        /// names are hidden everyone else is their job as well, numbered on
        /// from the lines.
        /// </summary>
        public string Draw(string name)
        {
            if (ids.TryGetValue(name, out var id)) return id.Label;
            if (!hide || name.Length == 0) return name;
            if (!strangers.TryGetValue(name, out var alias))
            {
                var job = rb?.JobLabel(name) ?? "";
                strangers[name] = alias = Alias(job.Length > 0 ? job : ra?.JobLabel(name) ?? "");
            }
            return alias;
        }
    }

    // ----------------------------------------------------------------- runs

    /// <summary>When a run began, how long it measured, and how many players it saw.</summary>
    public static RunSummary Describe(ImportedParse run)
    {
        var sn = run.Session;
        double started = sn.StartedAt ?? 0;
        int party = run.Source.Roster.Kinds.Count(k => k.Value == "player");
        return new RunSummary(
            Format.Day(started) + " " + Format.Clock(started),
            Format.Stopwatch(sn.Elapsed(sn.PausedAt), tenths: true),
            party.ToString(CultureInfo.InvariantCulture) + " character" + (party == 1 ? "" : "s"),
            run.Skipped > 0 ? Format.Int(run.Skipped) + " unreadable record" + (run.Skipped == 1 ? "" : "s") + " skipped" : "");
    }

    /// <summary>
    /// The session being measured, as the text of an exported parse, or null
    /// before its clock has started. A snapshot: the live session goes on
    /// counting and this copy does not. A running clock is frozen at
    /// <paramref name="now"/>, as a Pause would freeze it.
    ///
    /// <para>Through the export format on purpose. Read back with
    /// <see cref="ParseFile.Import"/>, it measures exactly as it would had it
    /// been saved to a file and opened.</para>
    /// </summary>
    public static string? Snapshot(EventReader reader, Session session, string? file, double now)
    {
        if (session.StartedAt == null) return null;
        var frozen = session.Clone();
        frozen.PausedAt ??= now;
        return ParseFile.Export(reader, frozen, t => frozen.At(t) != null, file, now);
    }
}

/// <summary>One figure of an action's hits, both runs: the least, the median.</summary>
public sealed record SpreadLine(string Label, string? Tip, string A, string B, Change Change);

/// <summary>
/// How one action's hits are spread on each side: the bins the chart draws,
/// and the figures beside it. A side's average is null where it has no hit.
/// </summary>
/// <param name="HitsA">How many hits (of a heal, casts) A's columns are shares of.</param>
/// <param name="Unit">What one of them is called: "hit", or "cast".</param>
public sealed record ActionSpread(IReadOnlyList<PairBin> Bins, int HitsA, int HitsB, double? AvgA, double? AvgB,
                                  IReadOnlyList<SpreadLine> Lines, string Caption, string Unit);

/// <summary>
/// Who a line is: what it is called, the job (or jobs) behind it, and which
/// colour marks it.
/// </summary>
/// <param name="Label">The name as drawn: the real one, or the job standing in for it.</param>
/// <param name="JobA">The character's job in run A, or "" (not in the run, or never reported).</param>
/// <param name="ColorJob">The job whose colour the line takes, or "" for the fallback colour of <paramref name="Slot"/>.</param>
/// <param name="Members">By job: who was on it, or how many while names are hidden.</param>
/// <param name="Hidden">The label is a job standing in for a name.</param>
public sealed record Identity(string Key, string Label, string JobA, string JobB, string ColorJob, int Slot,
                              string Members, bool Hidden);

/// <summary>What a run says about itself.</summary>
public sealed record RunSummary(string Started, string Length, string Party, string Skipped);
