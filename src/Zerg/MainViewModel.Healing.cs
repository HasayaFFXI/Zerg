using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zerg.Charts;
using Zerg.Core;
using Zerg.Core.Charts;

namespace Zerg;

// The Healing section: the Damage section's layout over the heals alone.
//
// One session, one clock, one set of filters: it is the same fight seen from
// the other side, so it is drawn from the same count as the damage cards and
// on every count, whichever section is on screen. Every figure is worked out
// in Zerg.Core (Healing.Totals); this only words and draws them.
//
// A pet's heals are their owner's healing, in the hero tile, the line and the
// bars alike, as a pet's damage is. How much was the pet's is said apart too:
// a column of its own, and in the heals table under the pet's name.
public sealed partial class MainViewModel
{
    // ----------------------------------------------------------------- tiles
    //
    // The clock is the Damage section's own (ClockText, ClockNote, Tag):
    // there is one session.

    [ObservableProperty] private string healTotalText = "0";
    [ObservableProperty] private string healTotalNote = "";
    /// <summary>"Total healing", or with something isolated "Healing by
    /// Sylviane to Hasaya"; every one of them by name for the pointer.</summary>
    [ObservableProperty] private string healTotalLabel = Healing.Heading([]);
    [ObservableProperty] private string? healTotalTip;
    /// <summary>With anything isolated, what leads the note under the total:
    /// how much of all the healing this is.</summary>
    string healTotalShare = "";
    [ObservableProperty] private string hpsText = "0.0";
    [ObservableProperty] private string hpsNote = None;
    [ObservableProperty] private string topHealerName = None;
    [ObservableProperty] private string topHealerNote = None;
    [ObservableProperty] private string? topHealerTip;
    [ObservableProperty] private Brush topHealerSwatch = Brushes.Transparent;
    [ObservableProperty] private bool hasTopHealer;
    [ObservableProperty] private string bigHealText = "0";
    [ObservableProperty] private string bigHealNote = None;

    // The same three marks as beside the damage figures, from the heals.
    [ObservableProperty] private bool hasHealMarks;
    [ObservableProperty] private IReadOnlyList<double> healTotalMark = [];
    [ObservableProperty] private string healTotalMarkCaption = BandMarks.Caption(BandMarks.ShortestBucket);
    [ObservableProperty] private IReadOnlyList<double> hpsMark = [];
    [ObservableProperty] private IReadOnlyList<ShareSlice> topHealerMark = [];

    void DrawHealing(Snapshot c)
    {
        HealEmptyText = SessionText.Empty(Shown.Session, Viewing, what: "healing");
        // The heals as rows a chart can add up: the cumulative lines and the
        // band's marks are both drawn from these.
        var own = Healing.AsEvents(c.Heals);
        DrawHealTiles(c, own);
        // The Timeline is the whole session's, whatever range is picked.
        DrawHealLine(c, HealingRange != null ? Healing.AsEvents(c.HealTimeline) : own);
        DrawHealBars(c);
        DrawHeals(c);
        DrawHealDrill(c);
        DrawSupport(c);
    }

    void DrawHealTiles(Snapshot c, IReadOnlyList<CombatEvent> own)
    {
        var h = c.Healing;
        HealTotalText = Format.Int(h.Total);
        var by = c.IsolatedHealers.Select(NameOf).ToList();
        var to = c.IsolatedHealTargets.Select(NameOf).ToList();
        HealTotalLabel = Healing.Heading(by, to, c.IsolatedSpells);
        HealTotalTip = Healing.Told(by, to, c.IsolatedSpells);
        double whole = c.HealWhole;
        healTotalShare = (by.Count > 0 || to.Count > 0 || c.IsolatedSpells.Count > 0) && whole > 0
            ? Format.Num(h.Total / whole * 100, 1) + "% of " + Format.Int(whole) + Dot
            : "";
        // Whoever healed anything, their pet's heals included.
        int n = h.Actors.Count(a => a.Total > 0);
        HpsNote = n > 0 ? n + " healer" + (n == 1 ? "" : "s") : None;

        var pulse = Shown.Session.StartedAt != null ? BandMarks.Pulse(own, c.HealElapsed, c.HealRange?.From ?? 0) : null;
        HasHealMarks = pulse != null;
        HealTotalMark = Kept(HealTotalMark, pulse?.Bars);
        if (pulse != null) HealTotalMarkCaption = BandMarks.Caption(pulse.Bucket);
        HpsMark = Kept(HpsMark, pulse?.Running);
        // Largest first, by their healing, a pet's included.
        TopHealerMark = Kept(TopHealerMark, pulse is null ? null
            : h.Actors.Where(a => a.Total > 0).OrderByDescending(a => a.Total)
               .Select(a => new ShareSlice(a.Total, SwatchOf(a.Name))).ToList());

        HasTopHealer = c.TopHealer != null;
        (TopHealerSwatch, TopHealerName, TopHealerTip, TopHealerNote) = c.TopHealer is { } top
            ? Leader(top.Name, top.Share ?? 0, h.Total, "healing")
            : NoLeader;

        var best = c.BestHeal;
        BigHealText = best != null ? Format.Int(best.Max) : "0";
        BigHealNote = best != null ? NameOf(best.Who) + Dot + best.What : None;
    }

    // ---------------------------------------------------- cumulative healing

    [ObservableProperty] private LineModel? healLine;
    /// <summary>The same lines in a floating panel's colours.</summary>
    [ObservableProperty] private LineModel? panelHealLine;
    /// <summary>What an empty healing chart says.</summary>
    [ObservableProperty] private string healEmptyText = "";

    void DrawHealLine(Snapshot c, IReadOnlyList<CombatEvent> rows)
    {
        // A line for everyone who healed anything, or whose pet did. Every healer
        // keeps their own: a party has two or three, so there is no pack to
        // fold into an "others" line as the damage chart does.
        var healers = HealingRange != null ? Healing.Totals(c.HealTimeline) : c.Healing;
        var names = healers.Actors.Where(a => a.Total > 0).Select(a => a.Name).ToList();

        double? now = Shown.Session.StartedAt != null ? c.Clock : null;
        var cum = Counting.Cumulative(rows, names, 0, now);

        // Largest total last, so the leading line is drawn on top.
        var lines = cum.Series.Select(s => new Drawn(s.Name, new LineSeries(NameOf(s.Name), s.Values, ColorOf(s.Name))))
                              .OrderBy(l => l.Series.Values.Length > 0 ? l.Series.Values[^1] : 0).ToList();
        // The same deaths and kills as the damage chart's: a healer sees
        // where the fights were and who went down.
        HealLine = new LineModel(cum.Times, lines.Select(l => l.Series).ToList(), cum.Live == true,
                                 Running: cum.Live == true && Shown.Session.Running,
                                 Deaths: ChartDeaths(c, lines.Select(l => l.Real).ToList()), Kills: ChartKills(c.TimelineKilled));
        PanelHealLine = AppTheme.IsDark ? HealLine : HealLine with
        {
            Series = lines.Select(l => l.Series with { Color = PanelColorOf(l.Real!) }).ToList(),
        };
    }

    // ------------------------------------------------------ healing overview

    // The Healing section's Overview, as the Damage section's is: a tab of
    // casters with the targets each healed under them, and a tab of targets
    // (the people healed) with their casters under them. A heading is
    // pressed to fold, a box ticked to isolate; the boxes set the caster and
    // target filters together (OverviewTicks), as the damage's set the
    // character and target filters.

    public ObservableCollection<HealerRow> Healers { get; } = [];
    [ObservableProperty] private bool hasHealers;
    /// <summary>The By target tab: each target's heading, and while it is
    /// open the casters under it.</summary>
    public ObservableCollection<HealerRow> HealTargetRows { get; } = [];
    [ObservableProperty] private bool hasHealTargetRows;
    /// <summary>The line that closes the table: the party as a whole.</summary>
    public HealPartyRow HealParty { get; } = new();

    /// <summary>The card as it floats: one short row per character.</summary>
    public ObservableCollection<HealStripRow> HealStrip { get; } = [];

    /// <summary>Something the boxes set is isolated: a caster or a target.</summary>
    [ObservableProperty] private bool healOverviewOn;
    /// <summary>What the pane says after its title: what is isolated, or
    /// what the tab is and how it is used.</summary>
    [ObservableProperty] private string healOverviewNote = "";

    /// <summary>The casters, and the targets, whose rows show under their
    /// heading, by real name. Every heading starts folded; not saved.</summary>
    readonly HashSet<string> openCasters = new(StringComparer.Ordinal), openHealTargets = new(StringComparer.Ordinal);

    void DrawHealBars(Snapshot c)
    {
        // Every caster, whoever is isolated: as in the Damage section, one
        // isolated or not is listed with their own figures, and one
        // isolated stays though they healed nothing. Largest first, or as
        // the headings order it; the strip the same.
        var picked = HealerFilter.Picked;
        var listed = c.Healers.Where(a => a.Total > 0 || picked.Contains(a.Name)).ToList();
        foreach (var n in picked)
            if (!listed.Any(a => a.Name == n)) listed.Add(new HealerTotals { Name = n });
        var actors = HealersSort.Order(listed, HealerKey);

        // The row is the bar, to the leading caster's healing.
        double top = RowMarks.Largest(actors.Select(a => a.Total));

        HasHealers = actors.Count > 0;
        DescribeHealOverview();
        DrawCasterRows(c, actors, top);
        if (HealOverviewByTarget) DrawHealTargetRows(c);

        // The Party line: the party's own figures, which the count already
        // has (HealTotals), of the casters and targets who count, and its
        // Healing % theirs out of everyone's. The number under Job is how
        // many casters above it count; its HPS is written by the draw beat.
        var h = c.Healing;
        HealParty.Count = Format.Int(actors.Count(a => !HealerFilter.Marks(a.Name, "").Dimmed));
        HealParty.Healing = Format.Int(h.Total);
        HealParty.Share = c.HealWhole > 0 ? Percent(h.Total / c.HealWhole) : None;
        HealParty.Casts = Format.Int(h.Casts);
        HealParty.Avg = Whole(h.Avg);
        HealParty.Mp = MpText(h.Mp);
        HealParty.PerMp = PerMpText(h.PerMp);
        (HealParty.Overcure, HealParty.OvercurePct) = OvercureText(h.Overcure, h.OvercurePct);
        HealParty.PetHealing = h.PetTotal != 0 ? Format.Int(h.PetTotal) : None;

        // Floating, the strip: whichever of the table's columns the panel
        // shows (Tables.HealStrip). HPS is written by the draw beat once a
        // second, as the damage strip's DPS is.
        string Tip(HealerTotals a) => RowTip(a.Name, a.Avg is double avg ? Format.Int(avg) + " avg per cast" : null);
        Rows.Sync(HealStrip, actors, a => a.Name, a => new HealStripRow(a.Name), (row, a) =>
        {
            row.Name = NameOf(a.Name);
            row.Job = JobBadge(a.Name);
            row.Fill = PanelShadeOf(a.Name);
            row.Edge = Solid(PanelColorOf(a.Name));
            row.Fraction = RowMarks.Fraction(a.Total, top);
            row.Total = a.Total;
            row.Healing = Format.Int(a.Total);
            row.Share = Percent(a.Share);
            row.Hps = HpsOf(c, a.Total);
            row.Casts = Format.Int(a.Casts);
            row.Avg = Whole(a.Avg);
            row.Mp = MpText(a.Mp);
            row.PerMp = PerMpText(a.PerMp);
            (row.Overcure, row.OvercurePct) = OvercureText(a.Overcure, a.OvercurePct);
            row.PetHealing = a.PetTotal != 0 ? Format.Int(a.PetTotal) : None;
            row.IsOwner = a.Name == Roster.Owner;
            row.Dimmed = HealerFilter.Marks(a.Name, "").Dimmed;
            row.Tip = Tip(a);
        });
    }

    /// <summary>Healing over the Healing section's clock, as a row prints
    /// it. A rate whatever is isolated: the casters were healing all through.</summary>
    static string HpsOf(Snapshot c, double total) =>
        Format.Num(Counting.PerSecond(total, c.HealElapsed / 1000) ?? double.NaN, 1);

    /// <summary>The targets a caster healed, as the rows under their heading
    /// list them, of the spells isolated: every target, isolated or not; one
    /// isolated that they reached and healed nothing stays, to be let go.</summary>
    List<HealPart> TargetsOfCaster(Snapshot c, string who)
    {
        var targets = HealTargetFilter.Picked;
        return c.HealGroups.Actors.FirstOrDefault(a => a.Name == who) is { } a
            ? a.Targets.Where(t => t.Name.Length > 0 && (t.Total > 0 || targets.Contains(t.Name))).ToList()
            : [];
    }

    /// <summary>The casters who healed a target, as the rows under its
    /// heading list them; one isolated who reached it and healed nothing stays.</summary>
    List<HealPart> CastersAt(Snapshot c, string target)
    {
        var casters = HealerFilter.Picked;
        return c.HealGroups.Targets.FirstOrDefault(p => p.Name == target) is { } p
            ? p.Parts.Where(a => a.Total > 0 || casters.Contains(a.Name)).ToList()
            : [];
    }

    /// <summary>
    /// The By caster tab: each caster's heading, pressed to show or put
    /// away their targets, and under it while it is open each target they
    /// healed. A target's row is the caster's healing on them; its Healing %
    /// is its share of the caster's, so the rows under a heading add to
    /// 100%, shaded in the caster's colour out of their largest target's.
    /// </summary>
    void DrawCasterRows(Snapshot c, List<HealerTotals> actors, double top)
    {
        var lines = new List<(string Key, HealerTotals Caster, HealPart? At, double Top, double Whole)>();
        foreach (var a in actors)
        {
            lines.Add((a.Name, a, null, top, 0));
            if (!openCasters.Contains(a.Name)) continue;
            var under = TargetsOfCaster(c, a.Name);
            double most = RowMarks.Largest(under.Select(t => t.Total));
            double whole = under.Sum(t => t.Total);
            foreach (var t in HealersSort.Order(under, column => HealPartKey(column, people: true)))
                lines.Add((a.Name + PickFilter.Rest + t.Name, a, t, most, whole));
        }

        var casters = HealerFilter.Picked;
        var targets = HealTargetFilter.Picked;
        Rows.Sync(Healers, lines, l => l.Key,
            l => l.At is { } at
                ? new HealerRow(l.Key, _ => TickCasterAt(l.Caster.Name, at.Name), _ => TickCasterAt(l.Caster.Name, at.Name))
                  { Nested = true, IsTarget = true }
                : new HealerRow(l.Key, FoldCaster, TickCaster),
            (row, l) =>
            {
                var a = l.Caster;
                row.Shade = ShadeOf(a.Name);
                row.ShowJob = !HideNames;
                if (l.At is { } t)
                {
                    bool counts = OverviewTicks.Counts(casters, targets, a.Name, t.Name);
                    row.Total = t.Total;
                    row.Fraction = RowMarks.Fraction(t.Total, l.Top);
                    row.Show(counts);
                    row.Dimmed = HealOverviewOn && !counts;
                    row.Name = NameOf(t.Name);
                    row.TickName = (counts ? "Let go of " : "Isolate ") + NameOf(a.Name) + " at " + NameOf(t.Name);
                    row.Tip = NameOf(a.Name) + " at " + NameOf(t.Name) + "\n" + Format.Int(t.Avg) + " avg per cast";
                    row.Job = "";
                    row.JobTip = "";
                    WritePart(row, c, t, l.Whole > 0 ? Percent(t.Total / l.Whole) : None);
                    return;
                }
                var tick = CasterTick(c, a.Name);
                row.Total = a.Total;
                row.Fraction = RowMarks.Fraction(a.Total, l.Top);
                row.Open = openCasters.Contains(a.Name);
                row.Show(TickOf(tick));
                row.Dimmed = HealOverviewOn && tick == TickState.None;
                row.IsOwner = a.Name == Roster.Owner;
                row.TickName = (tick == TickState.All ? "Let go of " : "Isolate ") + NameOf(a.Name);
                row.Tip = RowTip(a.Name, a.Avg is double avg ? Format.Int(avg) + " avg per cast" : null) + "\n" +
                          (row.Open ? "Select to put away their targets" : "Select to show their targets");
                row.Name = NameOf(a.Name);
                row.Swatch = SwatchOf(a.Name);
                row.Job = Roster.JobLabel(a.Name) is { Length: > 0 } job ? job : None;
                row.JobTip = Roster.JobTitle(a.Name);
                row.Healing = Format.Int(a.Total);
                row.Share = Percent(a.Share);
                row.Hps = HpsOf(c, a.Total);
                row.Casts = Format.Int(a.Casts);
                row.Avg = Whole(a.Avg);
                row.Mp = MpText(a.Mp);
                row.PerMp = PerMpText(a.PerMp);
                (row.Overcure, row.OvercurePct) = OvercureText(a.Overcure, a.OvercurePct);
                // A dash, not a zero: most characters have no pet to have healed.
                row.PetHealing = a.PetTotal != 0 ? Format.Int(a.PetTotal) : None;
            });
        HasHealers = lines.Count > 0;
    }

    /// <summary>A row's figures from a part of the healing: one caster's on
    /// one target. No pet column: the pet's part of it is not said apart here.</summary>
    static void WritePart(HealerRow row, Snapshot c, HealPart p, string share)
    {
        row.Healing = Format.Int(p.Total);
        row.Share = share;
        row.Hps = HpsOf(c, p.Total);
        row.Casts = Format.Int(p.Casts);
        row.Avg = p.Casts > 0 ? Format.Int(p.Avg) : None;
        row.Mp = MpText(p.Mp);
        row.PerMp = PerMpText(p.PerMp);
        (row.Overcure, row.OvercurePct) = OvercureText(p.Overcure, p.OvercurePct);
        row.PetHealing = None;
    }

    void FoldCaster(string key)
    {
        if (!openCasters.Remove(key)) openCasters.Add(key);
        // Nothing is counted again: the last count, with more or fewer of its lines.
        if (snapshot is { } c) DrawHealBars(c);
    }

    void FoldHealed(string name)
    {
        if (!openHealTargets.Remove(name)) openHealTargets.Add(name);
        if (snapshot is { } c) DrawHealTargetRows(c);
    }

    /// <summary>
    /// The By target tab: each target (a person healed) as a heading with
    /// everything that reached them of the spells isolated, pressed to show
    /// or put away the casters under it. A target's Healing % is its share
    /// of what everyone healed; a caster's is their share of what this
    /// target got, shaded to their healing out of its leader's.
    /// </summary>
    void DrawHealTargetRows(Snapshot c)
    {
        var targets = HealTargetFilter.Picked;
        var casters = HealerFilter.Picked;
        var groups = c.HealGroups.Targets.Where(p => p.Name.Length > 0 && (p.Total > 0 || targets.Contains(p.Name))).ToList();
        foreach (var n in targets)
            if (!groups.Any(p => p.Name == n)) groups.Add(new HealPart { Name = n });
        groups = HealersSort.Order(groups, column => HealPartKey(column, people: true));
        double topTarget = RowMarks.Largest(groups.Select(p => p.Total));

        var lines = new List<(string Key, HealPart Target, HealPart? Caster, double Top)>();
        foreach (var p in groups)
        {
            lines.Add(("t" + PickFilter.Rest + p.Name, p, null, topTarget));
            if (!openHealTargets.Contains(p.Name)) continue;
            var under = CastersAt(c, p.Name);
            double top = RowMarks.Largest(under.Select(a => a.Total));
            foreach (var a in HealersSort.Order(under, column => HealPartKey(column, people: true)))
                lines.Add(("c" + PickFilter.Rest + p.Name + PickFilter.Rest + a.Name, p, a, top));
        }

        double all = c.HealGroups.Total;
        HasHealTargetRows = lines.Count > 0;
        Rows.Sync(HealTargetRows, lines, l => l.Key,
            l => l.Caster is { } who
                ? new HealerRow(l.Key, _ => TickHealedBy(l.Target.Name, who.Name), _ => TickHealedBy(l.Target.Name, who.Name))
                  { Nested = true }
                : new HealerRow(l.Key, _ => FoldHealed(l.Target.Name), _ => TickHealed(l.Target.Name)) { IsTarget = true },
            (row, l) =>
            {
                var p = l.Target;
                row.ShowJob = !HideNames;
                if (l.Caster is not { } a)
                {
                    var tick = HealedTick(c, p.Name);
                    row.Total = p.Total;
                    row.Shade = null;
                    row.Fraction = 0;
                    row.Name = NameOf(p.Name);
                    row.Open = openHealTargets.Contains(p.Name);
                    row.Show(TickOf(tick));
                    row.Dimmed = HealOverviewOn && tick == TickState.None;
                    row.TickName = (tick == TickState.All ? "Let go of " : "Isolate ") + NameOf(p.Name);
                    row.Tip = NameOf(p.Name) + "\n" +
                              (row.Open ? "Select to put away their casters" : "Select to show who healed them");
                    row.Job = "";
                    row.JobTip = "";
                    WritePart(row, c, p, all > 0 ? Percent(p.Total / all) : None);
                    return;
                }
                bool counts = OverviewTicks.Counts(targets, casters, p.Name, a.Name);
                row.Total = a.Total;
                row.Shade = ShadeOf(a.Name);
                row.Fraction = RowMarks.Fraction(a.Total, l.Top);
                row.IsOwner = a.Name == Roster.Owner;
                row.Show(counts);
                row.Dimmed = HealOverviewOn && !counts;
                row.TickName = (counts ? "Let go of " : "Isolate ") + NameOf(a.Name) + " at " + NameOf(p.Name);
                row.Tip = RowTip(a.Name, Format.Int(a.Avg) + " avg per cast on " + NameOf(p.Name));
                row.Name = NameOf(a.Name);
                row.Swatch = SwatchOf(a.Name);
                row.Job = Roster.JobLabel(a.Name) is { Length: > 0 } job ? job : None;
                row.JobTip = Roster.JobTitle(a.Name);
                WritePart(row, c, a, p.Total > 0 ? Percent(a.Total / p.Total) : None);
            });
    }

    void DescribeHealOverview()
    {
        var casters = HealerFilter.Picked;
        var targets = HealTargetFilter.Picked;
        HealOverviewOn = casters.Count > 0 || targets.Count > 0;
        string Who() => casters.Count == 0 ? "everyone" : casters.Count == 1 ? NameOf(casters.First()) : casters.Count + " casters";
        string At() => targets.Count == 1 ? NameOf(targets.First()) : targets.Count + " targets";
        HealOverviewNote = HealOverviewOn
            ? "Isolated: " + Who() + (targets.Count > 0 ? " at " + At() : "")
            : HealOverviewByTarget
                ? "Everyone healed. Select one for who healed them, and tick to isolate"
                : "Total healed since the session started. Select a caster for their targets, and tick to isolate";
    }

    /// <summary>The Overview's Clear: every caster and every target count again.</summary>
    [RelayCommand]
    void ClearHealOverview() => SetHealPairs([], []);

    /// <summary>What the boxes set: the casters isolated and the targets
    /// isolated, both at once, then one count.</summary>
    void SetHealPairs(IEnumerable<string> casters, IEnumerable<string> targets)
    {
        HealerFilter.Set(casters);
        HealTargetFilter.Set(targets);
        Log.Write("casters " + (HealerFilter.IsOn ? string.Join(", ", HealerFilter.Picked) : "all") +
                  "; healed targets " + (HealTargetFilter.IsOn ? string.Join(", ", HealTargetFilter.Picked) : "all"));
        Recount();
    }

    /// <summary>A caster's heading box, By caster: of their targets listed under them.</summary>
    TickState CasterTick(Snapshot c, string who) =>
        OverviewTicks.Heading(HealerFilter.Picked, HealTargetFilter.Picked, who,
                              () => TargetsOfCaster(c, who).Select(t => t.Name).ToList());

    /// <summary>A target's heading box, By target: of their casters listed under them.</summary>
    TickState HealedTick(Snapshot c, string target) =>
        OverviewTicks.Heading(HealTargetFilter.Picked, HealerFilter.Picked, target,
                              () => CastersAt(c, target).Select(a => a.Name).ToList());

    void TickCaster(string who)
    {
        if (snapshot is not { } c) return;
        var (casters, targets) = OverviewTicks.PressHeading(HealerFilter.Picked, HealTargetFilter.Picked, who,
                                                            CasterTick(c, who));
        SetHealPairs(casters, targets);
    }

    void TickCasterAt(string who, string target)
    {
        if (snapshot is not { } c) return;
        var (casters, targets) = OverviewTicks.PressPair(HealerFilter.Picked, HealTargetFilter.Picked, who, target,
            TargetsOfCaster(c, who).Select(t => t.Name), CastersAt(c, target).Select(a => a.Name));
        SetHealPairs(casters, targets);
    }

    void TickHealed(string target)
    {
        if (snapshot is not { } c) return;
        var (targets, casters) = OverviewTicks.PressHeading(HealTargetFilter.Picked, HealerFilter.Picked, target,
                                                            HealedTick(c, target));
        SetHealPairs(casters, targets);
    }

    void TickHealedBy(string target, string who)
    {
        if (snapshot is not { } c) return;
        var (targets, casters) = OverviewTicks.PressPair(HealTargetFilter.Picked, HealerFilter.Picked, target, who,
            CastersAt(c, target).Select(a => a.Name), TargetsOfCaster(c, who).Select(t => t.Name));
        SetHealPairs(casters, targets);
    }

    /// <summary>What each column orders a part of the healing by: a target
    /// under a caster, a target's heading, a caster under a target. Job and
    /// Pet Healing have none to give.</summary>
    SortKey<HealPart>? HealPartKey(string column, bool people) => column switch
    {
        "name" => SortKey<HealPart>.Of(x => people ? NameOf(x.Name) : x.Name),
        "healing" or "share" or "hps" => SortKey<HealPart>.Of(x => x.Total),
        "casts" => SortKey<HealPart>.Of(x => x.Casts),
        "avg" => SortKey<HealPart>.Of(x => x.Casts > 0 ? (double?)x.Avg : null),
        "mp" => SortKey<HealPart>.Of(x => x.Mp > 0 ? (double?)x.Mp : null),
        "perMp" => SortKey<HealPart>.Of(x => x.PerMp),
        "overcure" => SortKey<HealPart>.Of(x => x.OvercurePct != null ? (double?)x.Overcure : null),
        "overcurePct" => SortKey<HealPart>.Of(x => x.OvercurePct),
        _ => null,
    };

    /// <summary>What each column of the Healing by character table is
    /// ordered by: what it prints, a dash being nothing. Healing % and HPS go
    /// as Healing does.</summary>
    SortKey<HealerTotals>? HealerKey(string column) => column switch
    {
        "name" => SortKey<HealerTotals>.Of(a => NameOf(a.Name)),
        "job" => SortKey<HealerTotals>.Of(a => Roster.JobLabel(a.Name)),
        "healing" or "share" or "hps" => SortKey<HealerTotals>.Of(a => a.Total),
        "casts" => SortKey<HealerTotals>.Of(a => a.Casts),
        "avg" => SortKey<HealerTotals>.Of(a => a.Avg),
        "mp" => SortKey<HealerTotals>.Of(a => a.Mp > 0 ? (double?)a.Mp : null),
        "perMp" => SortKey<HealerTotals>.Of(a => a.PerMp),
        "overcure" => SortKey<HealerTotals>.Of(a => a.OvercurePct != null ? (double?)a.Overcure : null),
        "overcurePct" => SortKey<HealerTotals>.Of(a => a.OvercurePct),
        "petHealing" => SortKey<HealerTotals>.Of(a => a.PetTotal != 0 ? (double?)a.PetTotal : null),
        _ => null,
    };

    /// <summary>What each column of a character's heals is ordered by.</summary>
    static SortKey<HealAction>? HealKey(string column) => column switch
    {
        "name" => SortKey<HealAction>.Of(x => x.Name),
        "casts" => SortKey<HealAction>.Of(x => x.Casts),
        "total" or "share" => SortKey<HealAction>.Of(x => x.Total),
        "avg" => SortKey<HealAction>.Of(x => x.Avg),
        "min" => SortKey<HealAction>.Of(x => x.Min),
        "max" => SortKey<HealAction>.Of(x => x.Max),
        "mp" => SortKey<HealAction>.Of(x => x.Mp > 0 ? (double?)x.Mp : null),
        "perMp" => SortKey<HealAction>.Of(x => x.PerMp),
        "base" => SortKey<HealAction>.Of(x => x.Base),
        "overcure" => SortKey<HealAction>.Of(x => x.OvercurePct != null ? (double?)x.Overcure : null),
        "overcurePct" => SortKey<HealAction>.Of(x => x.OvercurePct),
        _ => null,
    };

    /// <summary>The same for the people healed, or the heals on one person,
    /// by the name as drawn.</summary>
    SortKey<HealPart>? PartKey(string column, bool people) => column switch
    {
        "name" => SortKey<HealPart>.Of(x => people ? NameOf(x.Name) : x.Name),
        "casts" => SortKey<HealPart>.Of(x => x.Casts),
        "total" or "share" => SortKey<HealPart>.Of(x => x.Total),
        "avg" => SortKey<HealPart>.Of(x => x.Avg),
        "min" => SortKey<HealPart>.Of(x => x.Min),
        "max" => SortKey<HealPart>.Of(x => x.Max),
        "mp" => SortKey<HealPart>.Of(x => x.Mp > 0 ? (double?)x.Mp : null),
        "perMp" => SortKey<HealPart>.Of(x => x.PerMp),
        "base" => SortKey<HealPart>.Of(x => x.Base),
        "overcure" => SortKey<HealPart>.Of(x => x.OvercurePct != null ? (double?)x.Overcure : null),
        "overcurePct" => SortKey<HealPart>.Of(x => x.OvercurePct),
        _ => null,
    };

    /// <summary>MP Spent as a column prints it: a dash for none, as a heal
    /// that costs no MP (Chakra, a waltz, a wyvern's breath) has none.</summary>
    static string MpText(double mp) => mp > 0 ? Format.Int(mp) : None;

    /// <summary>HP/MP to a tenth, or a dash with no MP spent.</summary>
    static string PerMpText(double? perMp) => perMp is double x ? Format.Num(x, 1) : None;

    /// <summary>Overcure and Overcure % as the columns print them: both a
    /// dash when nothing could be measured.</summary>
    static (string, string) OvercureText(double overcure, double? pct) =>
        pct is null ? (None, None) : (Format.Int(overcure), Percent(pct));

    // ----------------------------------------------------------------- heals

    const string HealsBySpell = "Spell", HealsByCaster = "Caster", HealsByTarget = "Target";

    /// <summary>How the Heals table lists the heals: "Spell" (each
    /// character, their heals, and under a heal the people it reached),
    /// "Caster" (each character, the people they healed, and under a person
    /// the heals that reached them) or "Target" (each person healed, who
    /// healed them, and under a character with what). Saved.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HealsNote), nameof(HealsNameHead))]
    private string healsBy = HealsBySpell;

    /// <summary>The second line is a person a character healed (By caster).</summary>
    bool ByPerson => HealsBy == HealsByCaster;
    /// <summary>The headings are the people healed (By target).</summary>
    bool ByHealed => HealsBy == HealsByTarget;

    /// <summary>What the Heals pane's heading says it lists, and its first column's heading.</summary>
    public string HealsNote => HealsBy switch
    {
        HealsByCaster => "Who each character healed. Select a character to show theirs, and a person to see what healed them and drill down",
        HealsByTarget => "Who was healed. Select a person to show who healed them, and a character to see with what and drill down",
        _ => "Every cure, waltz and pet heal. Select a character to show theirs, and a heal to see who it reached and drill down",
    };
    public string HealsNameHead => HealsBy == HealsBySpell ? "Heal" : "Target";

    /// <summary>Each way keeps its own drill-down and its own open lines:
    /// switching back finds what was left open. Nothing is counted.</summary>
    partial void OnHealsByChanged(string value)
    {
        settings.HealsBy = value;
        settings.Save();
        if (snapshot is not { } c) return;
        DrawHeals(c);
        DrawHealDrill(c);
    }

    public ObservableCollection<HealRow> Heals { get; } = [];
    [ObservableProperty] private bool hasHeals;
    /// <summary>The headings whose lines are showing under them, by real
    /// name: the characters (By spell and By caster alike) and the people
    /// healed (By target). Every heading starts alone, as in the actions
    /// table.</summary>
    readonly HashSet<string> openHeals = new(StringComparer.Ordinal), openHealed = new(StringComparer.Ordinal);
    /// <summary>The second lines whose third is listed under them, in each
    /// way, by the character's real name and the pick
    /// (<see cref="HealPick"/>) with <see cref="KeyGap"/> between.</summary>
    readonly HashSet<string> openSpells = new(StringComparer.Ordinal), openPeople = new(StringComparer.Ordinal),
                             openFrom = new(StringComparer.Ordinal);

    HashSet<string> OpenHeadings => ByHealed ? openHealed : openHeals;
    HashSet<string> OpenLines => ByHealed ? openFrom : ByPerson ? openPeople : openSpells;

    /// <summary>
    /// What a heal drill-down's action is, as one word: a heal ("Cure IV"), a
    /// person healed (<see cref="KeyGap"/> and their real name), or a heal on
    /// one person (the heal, the gap, the person). A heal on one person is
    /// the same pick in every way, and so is everything one character cast
    /// on one person.
    /// </summary>
    static string HealPick(string? heal, string? target) => target == null ? heal! : heal + KeyGap + target;

    static (string? Heal, string? Target) HealParts(string pick)
    {
        int i = pick.IndexOf(KeyGap, StringComparison.Ordinal);
        return i < 0 ? (pick, null) : (i == 0 ? null : pick[..i], pick[(i + 1)..]);
    }

    /// <summary>A pick as a drill-down's heading or its pick button names it.</summary>
    string HealWords(string pick) => HealParts(pick) switch
    {
        (var heal, null) => heal!,
        (null, var who) => NameOf(who),
        var (heal, who) => heal + " on " + NameOf(who),
    };

    /// <summary>Selecting a heading shows the lines under it; selecting it
    /// again puts them away.</summary>
    [RelayCommand]
    void ToggleHeals(HealRow row)
    {
        if (!row.IsHeading) return;
        if (!OpenHeadings.Remove(row.Actor)) OpenHeadings.Add(row.Actor);
        // Nothing is counted again: the last count, with more or fewer of its lines.
        if (snapshot is { } c) DrawHeals(c);
    }

    /// <summary>A heading of the Heals table: a character, or (By target) a
    /// person healed. Its total, its share, and what pets healed, apart.</summary>
    readonly record struct HealsHead(string Name, double Total, double? Share, double Pet, double Mp, double? PerMp,
                                     double Overcure, double? OvercurePct);

    /// <summary>A line of the Heals table under a heading: whose (the
    /// character who cast it), what it is (its pick), the name it is drawn
    /// with, whether that is a person, whether it is the third line, its
    /// figures, what its share is out of (Of), and the largest beside it
    /// (Most).</summary>
    readonly record struct HealsLine(string Actor, string Pick, string Name, bool Person, bool Under, double Total,
                                     int Casts, double Avg, double? Min, double Max, double Of, double Most,
                                     double Mp, double? PerMp, double Overcure, double? OvercurePct,
                                     bool Heal = false, double? Base = null, string? Also = null);

    void DrawHeals(Snapshot c)
    {
        // A heading is shaded to its total out of the largest heading's, as
        // in the table above; a line's share is drawn to its total out of
        // the largest beside it (Most).
        var items = new List<(HealsHead Head, HealsLine? Line)>();
        var heads = OpenHeadings;
        var lines = OpenLines;
        bool healed = ByHealed;
        double top;
        if (healed)
        {
            // Everything that reached them, pets' heals included, and its
            // share of everything healed: the people add up to it.
            double everything = c.Healing.Total;
            top = RowMarks.Largest(c.Healing.Targets.Select(p => p.Total));
            foreach (var p in c.Healing.Targets)
            {
                var head = new HealsHead(p.Name, p.Total, everything != 0 ? p.Total / everything : null, 0, p.Mp, p.PerMp,
                                         p.Overcure, p.OvercurePct);
                items.Add((head, null));
                if (!heads.Contains(p.Name)) continue;
                double most = RowMarks.Largest(p.Parts.Select(x => x.Total));
                foreach (var a in HealsSort.Order(p.Parts, k => PartKey(k, people: true)))
                {
                    var pick = HealPick(null, p.Name);
                    items.Add((head, new HealsLine(a.Name, pick, a.Name, true, false, a.Total, a.Casts, a.Avg, a.Min,
                                                   a.Max, p.Total, most, a.Mp, a.PerMp, a.Overcure, a.OvercurePct)));
                    if (!lines.Contains(a.Name + KeyGap + pick)) continue;
                    double under = RowMarks.Largest(a.Parts.Select(x => x.Total));
                    foreach (var h in HealsSort.Order(a.Parts, k => PartKey(k, people: false)))
                        items.Add((head, new HealsLine(a.Name, HealPick(h.Name, p.Name), h.Name, false, true, h.Total,
                                                       h.Casts, h.Avg, h.Min, h.Max, a.Total, under, h.Mp, h.PerMp,
                                                       h.Overcure, h.OvercurePct, true, h.Base, h.Also)));
                }
            }
        }
        else
        {
            top = RowMarks.Largest(c.Healing.Actors.Select(a => a.Total));
            foreach (var a in c.Healing.Actors)
            {
                var head = new HealsHead(a.Name, a.Total, a.Share, a.PetTotal, a.Mp, a.PerMp, a.Overcure, a.OvercurePct);
                items.Add((head, null));
                if (!heads.Contains(a.Name)) continue;
                // Out of everything under this heading, the pet's heals too:
                // the column adds up to 100. A third line's is out of the
                // line it is under.
                double all = a.Total;
                if (!ByPerson)
                {
                    double most = RowMarks.Largest(a.ActionList.Select(x => x.Total));
                    foreach (var act in HealsSort.Order(a.ActionList, HealKey))
                    {
                        items.Add((head, new HealsLine(a.Name, act.Name, act.Name, false, false, act.Total, act.Casts,
                                                       act.Avg, act.Min, act.Max, all, most, act.Mp, act.PerMp,
                                                       act.Overcure, act.OvercurePct, true, act.Base, act.Also)));
                        if (!lines.Contains(a.Name + KeyGap + act.Name)) continue;
                        double under = RowMarks.Largest(act.Targets.Select(x => x.Total));
                        foreach (var p in HealsSort.Order(act.Targets, k => PartKey(k, people: true)))
                            items.Add((head, new HealsLine(a.Name, HealPick(act.Name, p.Name), p.Name, true, true, p.Total,
                                                           p.Casts, p.Avg, p.Min, p.Max, act.Total, under, p.Mp, p.PerMp,
                                                           p.Overcure, p.OvercurePct, false, p.Base, p.Also)));
                    }
                    continue;
                }
                double mostOn = RowMarks.Largest(a.Targets.Select(x => x.Total));
                foreach (var p in HealsSort.Order(a.Targets, k => PartKey(k, people: true)))
                {
                    var pick = HealPick(null, p.Name);
                    items.Add((head, new HealsLine(a.Name, pick, p.Name, true, false, p.Total, p.Casts, p.Avg, p.Min,
                                                   p.Max, all, mostOn, p.Mp, p.PerMp, p.Overcure, p.OvercurePct)));
                    if (!lines.Contains(a.Name + KeyGap + pick)) continue;
                    double under = RowMarks.Largest(p.Parts.Select(x => x.Total));
                    foreach (var h in HealsSort.Order(p.Parts, k => PartKey(k, people: false)))
                        items.Add((head, new HealsLine(a.Name, HealPick(h.Name, p.Name), h.Name, false, true, h.Total,
                                                       h.Casts, h.Avg, h.Min, h.Max, p.Total, under, h.Mp, h.PerMp,
                                                       h.Overcure, h.OvercurePct, true, h.Base, h.Also)));
                }
            }
        }
        HasHeals = items.Count > 0;
        var on = HealOn;
        var fills = FillsOf(on);

        // By target the keys stand apart: a person healed and a character
        // may have the same name, and a row's heading is fixed when it is made.
        string mark = healed ? ((char)2).ToString() : "";
        string Key((HealsHead Head, HealsLine? Line) i) =>
            mark + (i.Line is not { } l ? i.Head.Name : i.Head.Name + KeyGap + l.Actor + KeyGap + l.Pick);

        Rows.Sync(Heals, items, Key, i => new HealRow(Key(i), i.Line?.Actor ?? i.Head.Name, i.Line?.Pick,
                                                      i.Line?.Under == true), (row, i) =>
        {
            if (i.Line is not { } l)
            {
                var h = i.Head;
                row.Name = NameOf(h.Name);
                row.Swatch = SwatchOf(h.Name);
                row.PanelSwatch = Solid(PanelColorOf(h.Name));
                row.Shade = HeadingShadeOf(h.Name);
                row.PanelShade = PanelHeadingShadeOf(h.Name);
                row.Fraction = RowMarks.Fraction(h.Total, top);
                row.Job = JobBadge(h.Name);
                row.Total = Format.Int(h.Total);
                row.Share = Percent(h.Share);
                row.Pet = h.Pet != 0 ? "incl. " + Format.Int(h.Pet) + " pet" : "";
                row.Mp = MpText(h.Mp);
                row.PerMp = PerMpText(h.PerMp);
                (row.Overcure, row.OvercurePct) = OvercureText(h.Overcure, h.OvercurePct);
                row.Open = heads.Contains(h.Name);
                row.Label = (healed ? "Heals on " : "Heals of ") + NameOf(h.Name);
                return;
            }
            // A person is drawn in their colour; a heal has no swatch.
            row.Name = l.Person ? NameOf(l.Name) : l.Name;
            row.Swatch = l.Person ? SwatchOf(l.Name) : null;
            row.PanelSwatch = l.Person ? Solid(PanelColorOf(l.Name)) : null;
            row.Casts = Format.Int(l.Casts);
            row.Total = Format.Int(l.Total);
            row.Avg = Format.Int(l.Avg);
            row.Min = Whole(l.Min);
            row.Max = Format.Int(l.Max);
            row.Share = Format.Num(l.Of != 0 ? l.Total / l.Of * 100 : 0, 1) + "%";
            row.ShareFraction = RowMarks.Fraction(l.Total, l.Most);
            row.Mp = MpText(l.Mp);
            row.PerMp = PerMpText(l.PerMp);
            // A heal's base, a dash with none; a person's only where their gear makes it another.
            row.Base = l.Base is double b ? Format.Int(b) : l.Heal ? None : "";
            row.BaseTip = l.Also;
            (row.Overcure, row.OvercurePct) = OvercureText(l.Overcure, l.OvercurePct);
            row.Open = !l.Under && lines.Contains(l.Actor + KeyGap + l.Pick);
            (row.Selected, row.Side, row.SideFill, row.PanelSideFill) = MarkOf(on, fills, l.Actor, l.Pick);
            row.Label = NameOf(l.Actor) + ", " + (HealParts(l.Pick) is (null, { } who)
                ? "heals on " + NameOf(who)
                : HealWords(l.Pick));
        });
    }

    // --------------------------------------------------------------- support

    const string SupportByBuffs = "Buffs", SupportByEnfeebles = "Enfeebles";

    /// <summary>What the Support table lists: "Buffs" (every buff, removal
    /// and ward, and under one the people it reached) or "Enfeebles" (every
    /// enfeeble, and under one the monsters it reached). The same table and
    /// the same figures either way. Saved.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupportHint), nameof(SupportHitTip), nameof(SupportMissTip),
                              nameof(SupportRateTip), nameof(SupportCastsTip), nameof(SupportReachedTip))]
    private string supportBy = SupportByBuffs;

    bool ByEnfeebles => SupportBy == SupportByEnfeebles;

    /// <summary>What the pane's heading and the column headings say, of
    /// what is listed.</summary>
    public string SupportHint => ByEnfeebles
        ? "Enfeebles: casts by character and action, and on how many monsters they took. Select an action to show which it reached. Never damage"
        : "Buffs, status removals and wards: casts by character and action, and on how many they took. Select an action to show who it reached. Never damage or healing";
    public string SupportCastsTip => ByEnfeebles
        ? "One per cast, however many monsters it reached" : "One per cast, however many people it reached";
    public string SupportHitTip => ByEnfeebles ? "The monsters it took effect on" : "The people it took effect on";
    public string SupportMissTip => ByEnfeebles
        ? "The monsters it did not take on: resisted, or no effect (already asleep, immune)"
        : "The people it had no effect on: an Erase that found nothing to remove, a Protect on someone with a stronger one";
    public string SupportRateTip => ByEnfeebles ? "Hit out of every monster it reached" : "Hit out of everyone it reached";
    public string SupportReachedTip => ByEnfeebles ? "Show or hide the monsters it reached" : "Show or hide the people it reached";

    /// <summary>Each keeps its own open lines. Nothing is counted.</summary>
    partial void OnSupportByChanged(string value)
    {
        settings.SupportBy = value;
        settings.Save();
        if (snapshot is { } c) DrawSupport(c);
    }

    /// <summary>Every buff, removal and ward (or enfeeble), under the
    /// character who cast it: casts, and of those reached, on how many it
    /// took (Zerg.Core's Support says why there is nothing more).</summary>
    public ObservableCollection<SupportRow> SupportRows { get; } = [];
    [ObservableProperty] private bool hasSupport;
    /// <summary>The casts in all, for the pane's heading.</summary>
    [ObservableProperty] private string supportNote = "";
    [ObservableProperty] private string supportEmptyText = "";

    /// <summary>The actions whose people (Buffs) or monsters (Enfeebles)
    /// are listed under them, by the caster's real name and the action
    /// (<see cref="KeyGap"/> between). Every action starts closed.</summary>
    readonly HashSet<string> openSupport = new(StringComparer.Ordinal), openEnfeebles = new(StringComparer.Ordinal);

    HashSet<string> OpenSupport => ByEnfeebles ? openEnfeebles : openSupport;

    /// <summary>Pressing an action lists who it reached under it; pressing
    /// it again puts them away.</summary>
    [RelayCommand]
    void ToggleSupport(SupportRow row)
    {
        if (row.IsHeading || row.IsUnder) return;
        if (!OpenSupport.Remove(row.Key)) OpenSupport.Add(row.Key);
        // Nothing is counted again: the last count, with more or fewer of its lines.
        if (snapshot is { } c) DrawSupport(c);
    }

    void DrawSupport(Snapshot c)
    {
        var enfeebles = ByEnfeebles;
        var totals = enfeebles ? c.Enfeebles : c.Support;
        var open = OpenSupport;
        SupportEmptyText = SessionText.Empty(Shown.Session, Viewing, what: enfeebles ? "enfeebles" : "support");
        var items = new List<(SupportCaster Caster, SupportAction? Action, SupportTarget? Target)>();
        foreach (var a in totals.Casters)
        {
            items.Add((a, null, null));
            foreach (var act in a.Actions)
            {
                items.Add((a, act, null));
                if (!open.Contains(a.Name + KeyGap + act.Name)) continue;
                foreach (var p in act.Targets) items.Add((a, act, p));
            }
        }
        HasSupport = items.Count > 0;
        SupportNote = totals.Casts > 0 ? Format.Int(totals.Casts) + " cast" + (totals.Casts == 1 ? "" : "s") : "";

        static string Key((SupportCaster Caster, SupportAction? Action, SupportTarget? Target) i) =>
            i.Action == null ? i.Caster.Name
            : i.Target == null ? i.Caster.Name + KeyGap + i.Action.Name
            : i.Caster.Name + KeyGap + i.Action.Name + KeyGap + i.Target.Name;

        Rows.Sync(SupportRows, items, Key, i => new SupportRow(Key(i), i.Caster.Name, i.Action?.Name, i.Target?.Name),
            (row, i) =>
        {
            var (a, act, p) = i;
            if (act == null)
            {
                row.Name = NameOf(a.Name);
                row.Swatch = SwatchOf(a.Name);
                row.PanelSwatch = Solid(PanelColorOf(a.Name));
                row.Job = JobBadge(a.Name);
                row.Casts = Format.Int(a.Casts);
                row.Hits = Format.Int(a.Hits);
                row.Misses = Format.Int(a.Misses);
                row.HitRate = Percent(a.HitRate);
                row.Label = (enfeebles ? "Enfeebles of " : "Support of ") + NameOf(a.Name);
                return;
            }
            if (p != null)
            {
                // A monster has no colour and no alias: asking for either
                // would give it a colour slot of the party's.
                row.Name = enfeebles ? p.Name : NameOf(p.Name);
                row.Swatch = enfeebles ? null : SwatchOf(p.Name);
                row.PanelSwatch = enfeebles ? null : Solid(PanelColorOf(p.Name));
                row.Casts = Format.Int(p.Casts);
                row.Hits = Format.Int(p.Hits);
                row.Misses = Format.Int(p.Misses);
                row.HitRate = Percent(p.HitRate);
                row.Label = NameOf(a.Name) + ", " + act.Name + " on " + row.Name + ", " + row.Casts + " cast" +
                            (p.Casts == 1 ? "" : "s") + ", " + row.Hits + " hit, " + row.Misses + " missed, " + row.HitRate;
                return;
            }
            row.Name = act.Name;
            row.Casts = Format.Int(act.Casts);
            row.Hits = Format.Int(act.Hits);
            row.Misses = Format.Int(act.Misses);
            row.HitRate = Percent(act.HitRate);
            row.Open = open.Contains(row.Key);
            row.Label = NameOf(a.Name) + ", " + act.Name + ", " + row.Casts + " cast" + (act.Casts == 1 ? "" : "s") +
                        ", " + row.Hits + " hit, " + row.Misses + " missed, " + row.HitRate;
        });
    }

    // ------------------------------------------------------------ drill-down

    /// <summary>What the drill-down is open on while the Heals table is By
    /// spell, by the character's real name and a <see cref="HealPick"/>,
    /// and the one set beside it; <see cref="personDrill"/> the same By
    /// caster, <see cref="healedDrill"/> By target. <see cref="HealOn"/> is
    /// the one on screen.</summary>
    DrillOn? healDrill, personDrill, healedDrill;

    ref DrillOn? HealOn
    {
        get
        {
            if (ByHealed) return ref healedDrill;
            if (ByPerson) return ref personDrill;
            return ref healDrill;
        }
    }

    /// <summary>The heal drill-down just opened: the window brings it into view.</summary>
    public event Action? HealDrillOpened;

    /// <summary>What the heal drill-down's card draws: the drill-down card,
    /// over the heals (Views/DrillCard).</summary>
    public DrillView HealingDrill { get; }

    /// <summary>
    /// Selecting a heal or a person opens their drill-down and lists the
    /// line under them (the people it reached, the heals that reached
    /// them); selecting it again puts both away. A line under that opens
    /// its own drill-down, one heal on one person, and nothing under it.
    /// </summary>
    [RelayCommand]
    void HealDrill(HealRow row)
    {
        if (row.Action == null) return;
        ref var on = ref HealOn;
        if (PairedByPress(ref on, row.Actor, row.Action))
        {
            Recount();
            return;
        }
        bool same = on is { } d && d.IsA(row.Actor, row.Action);
        on = same ? null : new DrillOn(row.Actor, row.Action);
        if (!row.IsUnder)
        {
            var line = row.Actor + KeyGap + row.Action;
            if (same) OpenLines.Remove(line);
            else OpenLines.Add(line);
        }
        Recount();
        if (!same)
        {
            OpenDrillPane(Zerg.Core.Layout.PaneLayouts.HealingSection, Zerg.Core.Layout.PaneLayouts.HealDrill);
            HealDrillOpened?.Invoke();
        }
    }

    [RelayCommand]
    void CloseHealDrill()
    {
        HealOn = null;
        Recount();
    }

    /// <summary>A pick's figures: <see cref="Healing.Distribution"/> over its
    /// heal, or all of the character's, on its person or on everyone.</summary>
    Distribution SpreadOf(Snapshot c, string actor, string pick)
    {
        var (heal, who) = HealParts(pick);
        return Healing.Distribution(c.Heals, actor, heal, who, HealingRule);
    }

    void DrawHealDrill(Snapshot c)
    {
        var current = HealOn;
        DescribePick(HealingDrill.Pick, current, DrillWords.Healing, HealWords);
        if (current is not { } on)
        {
            HealingDrill.Close();
            return;
        }
        if (on.Paired)
        {
            var a = SpreadOf(c, on.Actor, on.Action);
            var b = SpreadOf(c, on.ActorB!, on.ActionB!);
            var (pair, who) = Heading(on, HealWords);
            HealingDrill.ShowPair(DrillKey(on), pair,
                                  who + Dot + Versus(a.Count, b.Count) + " casts" + Dot + Versus(a.Total, b.Total) + " healed",
                                  SwatchOf(on.Actor), a, b, CompareSheet.Sides(a, b, healing: true, HealingRule), SidesOf(on), NameOf);
            return;
        }

        // What each cast healed, summed over everyone it reached: a Curaga
        // on five is one figure, as an area attack is in the damage
        // drill-down. A pet's heals are here too, under the pet's name. On
        // one person, each cast is what it healed them alone.
        var d = SpreadOf(c, on.Actor, on.Action);
        var (_, person) = HealParts(on.Action);
        var note = NameOf(on.Actor) + Dot + Format.Int(d.Count) + " cast" + (d.Count == 1 ? "" : "s") +
                   Dot + Format.Int(d.Total) + " healed";
        // Not accuracy: a heal cannot miss.
        var tiles = DrillTiles(d, new StatTile("Casts", Format.Int(d.Count)));
        var title = HealParts(on.Action) is (null, { } p) ? "Heals on " + NameOf(p) : HealWords(on.Action);
        var caption = person != null ? "Healed per cast, on " + NameOf(person) + " alone" : "Healed per cast";
        HealingDrill.Show(DrillKey(on), title, note, SwatchOf(on.Actor), d, tiles,
                          DrillCaption(caption, d), ColorOf(on.Actor), PanelColorOf(on.Actor), NameOf);
    }
}
