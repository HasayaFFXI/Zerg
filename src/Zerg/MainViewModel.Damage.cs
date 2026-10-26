using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zerg.Charts;
using Zerg.Core;
using Zerg.Core.Charts;

namespace Zerg;

// The Damage section: five tiles and six cards, each drawn from one count.
public sealed partial class MainViewModel
{
    const char KeyGap = (char)1;

    // ----------------------------------------------------------------- tiles

    [ObservableProperty] private string totalText = "0";
    [ObservableProperty] private string totalNote = "";
    /// <summary>What the total is a total of: "Total damage", or with
    /// targets isolated "Damage to Kirin", "Damage to 3 targets"; with
    /// damage types, "Melee damage"; with both, "Melee damage to Kirin".</summary>
    [ObservableProperty] private string totalLabel = Targets.Heading([]);
    /// <summary>With anything isolated, every one of them by name, for the
    /// pointer: the label cuts a long name and counts several.</summary>
    [ObservableProperty] private string? totalTip;
    /// <summary>With anything isolated, what leads the note under the total:
    /// how much of all the damage this is ("48.1% of 227,587 · ").</summary>
    string totalShare = "";
    /// <summary>With targets isolated, why the party's DPS is a dash.</summary>
    [ObservableProperty] private string? dpsTip;
    [ObservableProperty] private string clockText = "00:00";
    [ObservableProperty] private string clockNote = None;
    /// <summary>The word in the tag beside the clock, which also colours the
    /// clock: the session's state, or Saved over a parse in the View section.</summary>
    [ObservableProperty] private SessionTag tag;
    [ObservableProperty] private string dpsText = "0.0";
    [ObservableProperty] private string dpsNote = None;
    [ObservableProperty] private string topName = None;
    [ObservableProperty] private string topNote = None;
    [ObservableProperty] private string? topTip;
    [ObservableProperty] private Brush topSwatch = Brushes.Transparent;
    [ObservableProperty] private bool hasTop;
    [ObservableProperty] private string bigText = "0";
    [ObservableProperty] private string bigNote = None;

    // The marks beside three of the figures. Drawn from a count, as the
    // figures' notes are, and never on the draw beat: between two events
    // they stand still, while the rate beside one of them falls.

    /// <summary>There is something for the marks to draw: the clock has
    /// started and damage has been dealt.</summary>
    [ObservableProperty] private bool hasMarks;
    /// <summary>Beside the total: the party's damage in each stretch of the session.</summary>
    [ObservableProperty] private IReadOnlyList<double> totalMark = [];
    /// <summary>How long a stretch is: "per 20 s", longer in a long session.</summary>
    [ObservableProperty] private string totalMarkCaption = BandMarks.Caption(BandMarks.ShortestBucket);
    /// <summary>Beside Party DPS: the party's DPS as it stood at the end of each stretch.</summary>
    [ObservableProperty] private IReadOnlyList<double> dpsMark = [];
    /// <summary>Beside Top DPS: every character's share, largest first, in their colour.</summary>
    [ObservableProperty] private IReadOnlyList<ShareSlice> topMark = [];

    /// <summary>
    /// What a mark is given: the list it already has when the new one says
    /// the same, so a count that changed nothing in it (a toggle, a section
    /// switched) does not redraw it.
    /// </summary>
    static IReadOnlyList<T> Kept<T>(IReadOnlyList<T> had, IReadOnlyList<T>? now) =>
        now is null ? (had.Count == 0 ? had : []) : had.SequenceEqual(now) ? had : now;

    /// <summary>
    /// The Target and Type buttons' lists, with them the By target and By
    /// damage type tables (each filter draws its own, from the same count:
    /// PickFilter.Draw), and what isolating changes in the band of
    /// figures: the total's label and what it is a share of, and, with a
    /// target isolated, the reason the rate beside it is a dash.
    /// </summary>
    void DrawTargets(Snapshot c)
    {
        TargetFilter.Draw(c.Targets, c.AllTargets, NameOf, c.TargetFigures);
        TypeFilter.Draw(c.Types, c.AllTypes, n => n);
        bool isolated = c.Isolated.Count > 0, any = isolated || c.IsolatedTypes.Count > 0;
        TotalLabel = DamageTypes.Heading(c.IsolatedTypes, c.Isolated);
        TotalTip = DamageTypes.Told(c.IsolatedTypes, c.Isolated.Select(NameOf).ToList());
        totalShare = any && c.Whole > 0
            ? Format.Num(c.Totals.Total / c.Whole * 100, 1) + "% of " + Format.Int(c.Whole) + Dot
            : "";
        DpsTip = isolated ? Targets.NoRate("session’s") : null;
    }

    void DrawTiles(Snapshot c)
    {
        var agg = c.Totals;
        int n = agg.Actors.Count;
        bool isolated = c.Isolated.Count > 0;
        TotalText = Format.Int(agg.Total);
        DpsNote = isolated ? "no rate for a target" : n > 0 ? n + " character" + (n == 1 ? "" : "s") : None;

        // Nothing before the clock starts. The rows are the ones the
        // cumulative chart is drawn from, so the bars add up to its lines.
        var pulse = Shown.Session.StartedAt != null ? BandMarks.Pulse(c.Events, c.Elapsed) : null;
        HasMarks = pulse != null;
        TotalMark = Kept(TotalMark, pulse?.Bars);
        if (pulse != null) TotalMarkCaption = BandMarks.Caption(pulse.Bucket);
        // No running rate either while a target is isolated: the mark goes.
        DpsMark = Kept(DpsMark, isolated ? null : pulse?.Running);
        TopMark = Kept(TopMark, pulse is null ? null
            : agg.Actors.Where(a => a.Total > 0).Select(a => new ShareSlice(a.Total, SwatchOf(a.Name))).ToList());

        // The count lists characters largest first, so the leader is the first.
        // The dot is the hue that character has on every chart.
        HasTop = n > 0;
        (TopSwatch, TopName, TopTip, TopNote) = n > 0
            ? Leader(agg.Actors[0].Name, agg.Actors[0].Share, agg.Total, "damage")
            : NoLeader;

        var best = c.Best;
        BigText = best != null ? Format.Int(best.Max) : "0";
        BigNote = best != null ? NameOf(best.Who) + Dot + best.What : None;
    }

    /// <summary>
    /// The figure for whoever leads a section: their swatch, their name, a
    /// tooltip with their jobs in full, and the note under it ("SAM/WAR ·
    /// 31.2% of 153,687 damage"). The job is not beside a name that is the
    /// job: it would read "SAM/WAR · SAM/WAR".
    /// </summary>
    (Brush Swatch, string Name, string? Tip, string Note) Leader(string name, double share, double total, string what)
    {
        var job = JobBadge(name);
        return (SwatchOf(name), NameOf(name),
                Roster.JobTitle(name) is { Length: > 0 } title ? title : NameOf(name),
                (job.Length > 0 ? job + Dot : "") + Format.Num(share * 100, 1) + "% of " + Format.Int(total) + " " + what);
    }

    static readonly (Brush Swatch, string Name, string? Tip, string Note) NoLeader = (Brushes.Transparent, None, null, None);

    /// <summary>What the pointer resting on a character's row is told: who,
    /// their jobs in full, and the one figure the table has no column for.</summary>
    string RowTip(string name, string? figure)
    {
        var full = Aliased(name) ? "" : Roster.JobTitle(name);
        return NameOf(name) + (full.Length > 0 ? Dot + full : "") + (figure != null ? Dot + figure : "");
    }

    // ----------------------------------------------------- cumulative damage

    [ObservableProperty] private LineModel? line;
    /// <summary>The same lines in a floating panel's colours. The very same
    /// model while the theme is dark, which is when the two agree.</summary>
    [ObservableProperty] private LineModel? panelLine;
    /// <summary>The session clock, for the chart's live edge.</summary>
    [ObservableProperty] private double edge = double.NaN;
    /// <summary>What an empty chart says: why there is nothing to draw.</summary>
    [ObservableProperty] private string emptyText = "";

    /// <summary>Who the line that stands for several is made of just now, as
    /// their names are drawn; null while there is no such line. The chart
    /// names the line "3 others" and cannot say who: the switch's tooltip
    /// does (<see cref="GroupTip"/>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GroupTip))]
    private string? groupMembers;

    /// <summary>One line as it is drawn, with the real name it is keyed by
    /// (null for the line that stands for several).</summary>
    sealed record Drawn(string? Real, LineSeries Series);

    void DrawLine(Snapshot c)
    {
        var agg = c.Totals;
        EmptyText = SessionText.Empty(Shown.Session, Viewing);

        // One line per character, and none for anyone at zero: a resisted
        // debuff makes an actor, not a line along the floor.
        var names = agg.Actors.Where(a => a.Total > 0).Select(a => a.Name).ToList();

        // The left edge is the session's zero and the right edge its clock,
        // running or held, so the axis covers the span DPS is divided by.
        double? now = Shown.Session.StartedAt != null ? c.Elapsed : null;
        var cum = Counting.Cumulative(c.Events, names, 0, now);

        var lines = cum.Series.Select(s => new Drawn(s.Name, new LineSeries(NameOf(s.Name), s.Values, ColorOf(s.Name)))).ToList();
        string? members = null;
        if (GroupSmallLines && SmallLines.Pick(agg, Roster.Owner) is { Count: > 0 } small)
        {
            var parts = lines.Where(l => small.Contains(l.Real!)).ToList();
            var sum = new double[cum.Times.Length];
            foreach (var p in parts)
                for (int i = 0; i < sum.Length; i++) sum[i] += p.Series.Values[i];
            lines.RemoveAll(parts.Contains);
            lines.Add(new Drawn(null, new LineSeries(SmallLines.Name(parts.Count), sum, default, Group: true)));
            // Largest first, as the table lists them.
            members = string.Join(", ", parts.OrderByDescending(p => p.Series.Values.Length > 0 ? p.Series.Values[^1] : 0)
                                             .Select(p => p.Series.Name));
        }
        GroupMembers = members;
        // Largest total last, so the leading line is drawn on top of the pack.
        lines = lines.OrderBy(l => l.Series.Values.Length > 0 ? l.Series.Values[^1] : 0).ToList();
        // Counting, and the edge is the clock: the chart rules its edge to
        // say so. Every change of the session's state is followed by a count.
        Line = new LineModel(cum.Times, lines.Select(l => l.Series).ToList(), cum.Live == true,
                             Running: cum.Live == true && Shown.Session.Running);
        PanelLine = AppTheme.IsDark ? Line : Line with
        {
            Series = lines.Select(l => l.Real != null ? l.Series with { Color = PanelColorOf(l.Real) } : l.Series).ToList(),
        };
    }

    // --------------------------------------------------- damage by character

    public ObservableCollection<ActorRow> Actors { get; } = [];
    [ObservableProperty] private bool hasActors;
    /// <summary>The line that closes the table: the party as a whole.</summary>
    public PartyRow Party { get; } = new();
    /// <summary>
    /// The Job column is dropped while names are hidden, not printed twice or
    /// blanked: every character but the owner already has their job where
    /// their name was, and a dash there would say "job unknown".
    /// </summary>
    [ObservableProperty] private bool showJob = true;

    /// <summary>The card as it floats: one short row per character.</summary>
    public ObservableCollection<StripRow> Strip { get; } = [];

    void DrawBars(Snapshot c)
    {
        // Nobody at zero. A character reaches the count by acting, not by
        // dealing damage (a bard whose only song was resisted is there with
        // 0), and a row of zeroes and dashes is nothing but height. Only this
        // card drops them: they keep their chip, so they can still be
        // excluded, and the actions table still shows what they did.
        // Largest first, or as the headings order it; the strip the same.
        var actors = BarsSort.Order(c.Totals.Actors.Where(a => a.Total > 0), column => BarKey(c, column));

        // The row is the bar: each is shaded to its damage out of the
        // leader's, not out of the party's. With ten to eighteen characters
        // nobody is over a fifth of the party, and bars to that scale would
        // all end inside the first two columns; the Damage % beside it
        // carries the share of the whole.
        double top = RowMarks.Largest(actors.Select(a => a.Total));

        string Tip(ActorTotals a) => RowTip(a.Name, Format.Int(a.Avg) + " avg per action");

        ShowJob = !HideNames;
        HasActors = actors.Count > 0;
        Rows.Sync(Actors, actors, a => a.Name, a => new ActorRow(a.Name), (row, a) =>
        {
            row.Total = a.Total;
            row.Shade = ShadeOf(a.Name);
            row.Fraction = RowMarks.Fraction(a.Total, top);
            row.IsOwner = a.Name == Roster.Owner;
            row.Tip = Tip(a);
            row.AccuracyRate = a.AutoAcc;
            row.WsAccuracyRate = a.WsAcc;
            row.PetAccuracyRate = a.PetAcc;
            row.Name = NameOf(a.Name);
            row.Swatch = SwatchOf(a.Name);
            row.Job = Roster.JobLabel(a.Name) is { Length: > 0 } job ? job : None;
            row.JobTip = Roster.JobTitle(a.Name);
            row.ShowJob = ShowJob;
            row.Damage = Format.Int(a.Total);
            row.Share = Percent(a.Share);
            row.Dps = Rate(a.Total, c.Elapsed / 1000);
            row.Accuracy = Percent(a.AutoAcc);
            row.WsDamage = Whole(a.WsTotal);
            row.WsAvg = Whole(a.WsAvg);
            row.WsShare = Percent(a.WsShare);
            row.WsAccuracy = Percent(a.WsAcc);
            row.ScDamage = Whole(a.ScTotal);
            row.ScShare = Percent(a.ScShare);
            row.PetDamage = Whole(a.PetTotal);
            row.PetAccuracy = Percent(a.PetAcc);
        });

        // The Party line, under the columns it sums. The split columns are
        // the party's own (Aggregate.Party): everyone's swings together,
        // the characters at zero included, not an average of the rows. The
        // number under Job is how many rows stand above it. Its DPS is the
        // party's, written by the draw beat with every row's.
        var party = c.Totals.Party;
        Party.Count = Format.Int(actors.Count);
        Party.Damage = Format.Int(c.Totals.Total);
        Party.Share = c.Totals.Total > 0 ? Percent(1) : None;
        Party.Accuracy = Percent(party.AutoAcc);
        Party.WsDamage = Whole(party.WsTotal);
        Party.WsAvg = Whole(party.WsAvg);
        Party.WsShare = Percent(party.WsShare);
        Party.WsAccuracy = Percent(party.WsAcc);
        Party.ScDamage = Whole(party.ScTotal);
        Party.ScShare = Percent(party.ScShare);
        Party.PetDamage = Whole(party.PetTotal);
        Party.PetAccuracy = Percent(party.PetAcc);

        // Floating, the card is a strip: the row shaded to the share as in
        // the table, at the strength a panel allows the colour, and the
        // three figures a glance during a fight is for. The other eleven
        // columns, DPS among them, stay on the table, so nothing in the strip
        // moves with the clock: every figure in it is a running total that
        // only a new event can change.
        Rows.Sync(Strip, actors, a => a.Name, a => new StripRow(a.Name), (row, a) =>
        {
            row.Name = NameOf(a.Name);
            row.Job = JobBadge(a.Name);
            row.Fill = PanelShadeOf(a.Name);
            row.Edge = Solid(PanelColorOf(a.Name));
            row.Fraction = RowMarks.Fraction(a.Total, top);
            row.Damage = Format.Int(a.Total);
            row.Share = Format.Num(a.Share * 100, 1) + "%";
            row.Accuracy = Percent(a.AutoAcc);
            row.IsOwner = a.Name == Roster.Owner;
            row.Tip = Tip(a);
        });
    }

    /// <summary>
    /// What each column of the Damage by character table is ordered by:
    /// what it prints, a dash being nothing. Damage % and DPS are the damage
    /// over one whole and one clock, so they go as Damage does; while a
    /// target is isolated every DPS is a dash, and the order is the table's own.
    /// </summary>
    SortKey<ActorTotals>? BarKey(Snapshot c, string column) => column switch
    {
        "name" => SortKey<ActorTotals>.Of(a => NameOf(a.Name)),
        "job" => SortKey<ActorTotals>.Of(a => Roster.JobLabel(a.Name)),
        "damage" or "share" => SortKey<ActorTotals>.Of(a => a.Total),
        "dps" => SortKey<ActorTotals>.Of(a => c.Rate(a.Total, 1)),
        "accuracy" => SortKey<ActorTotals>.Of(a => a.AutoAcc),
        "wsDamage" => SortKey<ActorTotals>.Of(a => a.WsTotal),
        "wsAvg" => SortKey<ActorTotals>.Of(a => a.WsAvg),
        "wsShare" => SortKey<ActorTotals>.Of(a => a.WsShare),
        "wsAccuracy" => SortKey<ActorTotals>.Of(a => a.WsAcc),
        "scDamage" => SortKey<ActorTotals>.Of(a => a.ScTotal),
        "scShare" => SortKey<ActorTotals>.Of(a => a.ScShare),
        "petDamage" => SortKey<ActorTotals>.Of(a => a.PetTotal),
        "petAccuracy" => SortKey<ActorTotals>.Of(a => a.PetAcc),
        _ => null,
    };

    /// <summary>What each column of a character's actions is ordered by. A
    /// use that never missed has a dash for Miss, and goes after those that did.</summary>
    static SortKey<ActionTotals>? ActionKey(string column) => column switch
    {
        "name" => SortKey<ActionTotals>.Of(x => x.Name),
        "hits" => SortKey<ActionTotals>.Of(x => x.Hits),
        "misses" => SortKey<ActionTotals>.Of(x => x.Misses != 0 ? (double?)x.Misses : null),
        "total" or "share" => SortKey<ActionTotals>.Of(x => x.Total),
        "avg" => SortKey<ActionTotals>.Of(x => x.Avg),
        "min" => SortKey<ActionTotals>.Of(x => x.Min),
        "max" => SortKey<ActionTotals>.Of(x => x.Max),
        _ => null,
    };

    // A figure with nothing to measure is a dash, never zero: a mage with no
    // weaponskills has no weaponskill accuracy, and 0% would say every one missed.
    static string Percent(double? x) => x is double v ? Format.Num(v * 100, 1) + "%" : None;
    static string Whole(double? x) => x is double v ? Format.Int(v) : None;

    // --------------------------------------------------------------- actions

    public ObservableCollection<ActionRow> Actions { get; } = [];
    [ObservableProperty] private bool hasActions;
    /// <summary>The characters whose actions are showing under their heading,
    /// by real name. Everyone starts as a heading alone: an alliance is
    /// eighteen characters, and a few hundred lines with all of them open.</summary>
    readonly HashSet<string> openActions = new(StringComparer.Ordinal);

    /// <summary>Selecting a character shows their actions under the heading;
    /// selecting them again puts the actions away.</summary>
    [RelayCommand]
    void ToggleActions(ActionRow row)
    {
        if (!row.IsHeading) return;
        if (!openActions.Remove(row.Actor)) openActions.Add(row.Actor);
        // Nothing is counted again: the last count, with more or fewer of its lines.
        if (snapshot is { } c) DrawActions(c);
    }

    void DrawActions(Snapshot c)
    {
        // One scale, shaded or not: a row's share is drawn to its value
        // over the largest of its siblings. A heading's is the character's
        // damage out of the leader's, as in the table above; an action's is
        // its total out of that character's largest action (Most).
        var items = new List<(ActorTotals Actor, ActionTotals? Action, double Most)>();
        double top = RowMarks.Largest(c.Totals.Actors.Select(a => a.Total));
        foreach (var a in c.Totals.Actors)
        {
            items.Add((a, null, top));
            if (!openActions.Contains(a.Name)) continue;
            double most = RowMarks.Largest(a.ActionList.Select(x => x.Total));
            foreach (var act in ActionsSort.Order(a.ActionList, ActionKey)) items.Add((a, act, most));
        }
        HasActions = items.Count > 0;
        var fills = FillsOf(drill);

        static string Key((ActorTotals Actor, ActionTotals? Action, double Most) i) =>
            i.Action == null ? i.Actor.Name : i.Actor.Name + KeyGap + i.Action.Name;

        Rows.Sync(Actions, items, Key, i => new ActionRow(Key(i), i.Actor.Name, i.Action?.Name), (row, i) =>
        {
            var (a, act, most) = i;
            if (act == null)
            {
                row.Name = NameOf(a.Name);
                row.Swatch = SwatchOf(a.Name);
                row.PanelSwatch = Solid(PanelColorOf(a.Name));
                row.Shade = HeadingShadeOf(a.Name);
                row.PanelShade = PanelHeadingShadeOf(a.Name);
                row.Fraction = RowMarks.Fraction(a.Total, most);
                row.Job = JobBadge(a.Name);
                row.Total = Format.Int(a.Total);
                row.Share = Format.Num(a.Share * 100, 1) + "%";
                row.Open = openActions.Contains(a.Name);
                row.Label = "Actions of " + NameOf(a.Name);
                return;
            }
            row.Name = act.Name;
            row.Hits = Format.Int(act.Hits);
            row.Misses = act.Misses != 0 ? Format.Int(act.Misses) : None;
            row.Total = Format.Int(act.Total);
            row.Avg = Format.Int(act.Avg);
            row.Min = Format.Int(act.Min);
            row.Max = Format.Int(act.Max);
            row.Share = Format.Num(a.Total != 0 ? act.Total / a.Total * 100 : 0, 1) + "%";
            row.ShareFraction = RowMarks.Fraction(act.Total, most);
            // Least to greatest with the average between, on a line as long
            // as the character's biggest hit of any action.
            (row.SpreadMin, row.SpreadAvg, row.SpreadMax) = RowMarks.Spread(act.Min, act.Avg, act.Max, a.Max);
            (row.Selected, row.Side, row.SideFill, row.PanelSideFill) = MarkOf(drill, fills, a.Name, act.Name);
            row.Label = NameOf(a.Name) + ", " + act.Name;
        });
    }

    // ------------------------------------------------------------ drill-down

    /// <summary>The action the drill-down is open on, by the character's real
    /// name, and the one set beside it.</summary>
    DrillOn? drill;

    /// <summary>The drill-down just opened: the window brings it into view.</summary>
    public event Action? DrillOpened;

    /// <summary>What the drill-down card draws (Views/DrillCard).</summary>
    public DrillView DamageDrill { get; }

    /// <summary>Selecting an action opens its drill-down; selecting it again
    /// closes it. With Ctrl, one more is set beside the open one, or taken
    /// away again.</summary>
    [RelayCommand]
    void Drill(ActionRow row)
    {
        if (row.Action == null) return;
        if (PairedByPress(ref drill, row.Actor, row.Action))
        {
            Recount();
            return;
        }
        bool same = drill is { } d && d.IsA(row.Actor, row.Action);
        drill = same ? null : new DrillOn(row.Actor, row.Action);
        Recount();
        if (drill != null)
        {
            OpenDrillPane(Zerg.Core.Layout.PaneLayouts.DamageSection, Zerg.Core.Layout.PaneLayouts.Drill);
            DrillOpened?.Invoke();
        }
    }

    [RelayCommand]
    void CloseDrill()
    {
        drill = null;
        Recount();
    }

    void DrawDrill(Snapshot c)
    {
        DescribePick(DamageDrill.Pick, drill, DrillWords.Damage);
        if (drill is not { } on)
        {
            DamageDrill.Close();
            return;
        }
        if (on.Paired)
        {
            var a = Counting.Distribution(c.Events, on.Actor, on.Action);
            var b = Counting.Distribution(c.Events, on.ActorB!, on.ActionB!);
            var (title, who) = Heading(on);
            DamageDrill.ShowPair(DrillKey(on), title,
                                 who + Dot + Versus(a.Count, b.Count) + " hits" + Dot + Versus(a.Total, b.Total) + " total damage",
                                 SwatchOf(on.Actor), a, b, CompareSheet.Sides(a, b, healing: false), SidesOf(on), NameOf);
            return;
        }

        // Folded per use, like every count: an area attack is one figure, what
        // the action hit for, not a point per target.
        var d = Counting.Distribution(c.Events, on.Actor, on.Action);
        var note = NameOf(on.Actor) + Dot + Format.Int(d.Count) + " hit" + (d.Count == 1 ? "" : "s") +
                   (d.Misses != 0 ? Dot + d.Misses.ToString(CultureInfo.InvariantCulture) + " miss" + (d.Misses == 1 ? "" : "es") : "") +
                   Dot + Format.Int(d.Total) + " total damage";
        // The same question the per-character table's Accuracy asks, so
        // drilling into "Attack" agrees with that cell.
        var tiles = DrillTiles(d, new StatTile("Accuracy", Format.Num(d.Accuracy * 100, 0) + "%"));
        var caption = DrillCaption("Damage distribution", d) +
                      (d.Crits != 0 ? Dot + Format.Num(d.CritRate * 100, 0) + "% crit" : "") +
                      (d.Bursts != 0 ? Dot + d.Bursts.ToString(CultureInfo.InvariantCulture) + " magic burst" : "");
        DamageDrill.Show(DrillKey(on), on.Action, note, SwatchOf(on.Actor), d, tiles, caption,
                         ColorOf(on.Actor), PanelColorOf(on.Actor), NameOf);
    }

    /// <summary>What a drill-down's list is of, and how it is drawn: a list
    /// with the same key may only have grown since the last count.</summary>
    string DrillKey(DrillOn on) =>
        on.Actor + KeyGap + on.Action + KeyGap + on.ActorB + KeyGap + on.ActionB + KeyGap + HideNames;

    /// <summary>The six figures over a drill-down: five of the spread, and
    /// one of the section's own.</summary>
    static IReadOnlyList<StatTile> DrillTiles(Distribution d, StatTile last) =>
    [
        new("Min", Format.Int(d.Min)),
        new("Average", Format.Int(d.Avg)),
        new("Max", Format.Int(d.Max)),
        new("Median", Format.Int(d.Median)),
        new("Std dev", Format.Int(d.Stdev)),
        last,
    ];

    /// <summary>The line under a drill-down's chart, as far as the two sections share it.</summary>
    static string DrillCaption(string what, Distribution d) =>
        what + Dot + d.Bins.Count.ToString(CultureInfo.InvariantCulture) + " bins" + Dot +
        "IQR " + Format.Int(d.Q1) + "–" + Format.Int(d.Q3) + Dot +
        "90th percentile " + Format.Int(d.P90);
}
