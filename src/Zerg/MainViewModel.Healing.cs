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
// Healing leaves a pet's heals out, in the hero tile, the line and the bars
// alike. They are their owner's, in a column of their own, and in the heals
// table under the pet's name.
public sealed partial class MainViewModel
{
    // ----------------------------------------------------------------- tiles
    //
    // The clock is the Damage section's own (ClockText, ClockNote, Tag):
    // there is one session.

    [ObservableProperty] private string healTotalText = "0";
    [ObservableProperty] private string healTotalNote = "";
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
        // The heals that Healing counts, a pet's left out, as rows a chart
        // can add up: the cumulative lines and the band's marks are both
        // drawn from these.
        var own = Healing.AsEvents(c.Heals.Where(h => !h.IsPets));
        DrawHealTiles(c, own);
        DrawHealLine(c, own);
        DrawHealBars(c);
        DrawHeals(c);
        DrawHealDrill(c);
        DrawSupport(c);
    }

    void DrawHealTiles(Snapshot c, IReadOnlyList<CombatEvent> own)
    {
        var h = c.Healing;
        HealTotalText = Format.Int(h.Total);
        // Whoever healed anything, their pet's heals included.
        int n = h.Actors.Count(a => a.Total + a.PetTotal > 0);
        HpsNote = n > 0 ? n + " healer" + (n == 1 ? "" : "s") : None;

        var pulse = Shown.Session.StartedAt != null ? BandMarks.Pulse(own, c.Elapsed) : null;
        HasHealMarks = pulse != null;
        HealTotalMark = Kept(HealTotalMark, pulse?.Bars);
        if (pulse != null) HealTotalMarkCaption = BandMarks.Caption(pulse.Bucket);
        HpsMark = Kept(HpsMark, pulse?.Running);
        // Largest first, by their own healing: a pet's is not in the total
        // these are shares of.
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
        // A line for everyone who healed anything themselves. Every healer
        // keeps their own: a party has two or three, so there is no pack to
        // fold into an "others" line as the damage chart does.
        var names = c.Healing.Actors.Where(a => a.Total > 0).Select(a => a.Name).ToList();

        double? now = Shown.Session.StartedAt != null ? c.Elapsed : null;
        var cum = Counting.Cumulative(rows, names, 0, now);

        // Largest total last, so the leading line is drawn on top.
        var lines = cum.Series.Select(s => new Drawn(s.Name, new LineSeries(NameOf(s.Name), s.Values, ColorOf(s.Name))))
                              .OrderBy(l => l.Series.Values.Length > 0 ? l.Series.Values[^1] : 0).ToList();
        HealLine = new LineModel(cum.Times, lines.Select(l => l.Series).ToList(), cum.Live == true,
                                 Running: cum.Live == true && Shown.Session.Running);
        PanelHealLine = AppTheme.IsDark ? HealLine : HealLine with
        {
            Series = lines.Select(l => l.Series with { Color = PanelColorOf(l.Real!) }).ToList(),
        };
    }

    // -------------------------------------------------- healing by character

    public ObservableCollection<HealerRow> Healers { get; } = [];
    [ObservableProperty] private bool hasHealers;
    /// <summary>The line that closes the table: the party as a whole.</summary>
    public HealPartyRow HealParty { get; } = new();

    /// <summary>The card as it floats: one short row per character.</summary>
    public ObservableCollection<HealStripRow> HealStrip { get; } = [];

    void DrawHealBars(Snapshot c)
    {
        // Everyone who healed anything, a pet's heals counting: a character
        // whose pet did all of it has a row, with no bar to speak of and the
        // figure under Pet Healing.
        // Largest first, or as the headings order it; the strip the same.
        var actors = HealersSort.Order(c.Healing.Actors.Where(a => a.Total + a.PetTotal > 0), HealerKey);

        // The row is the bar, to the leading healer's own healing: a pet's
        // is not in it, as it is not in Healing.
        double top = RowMarks.Largest(actors.Select(a => a.Total));

        string Tip(HealerTotals a) => RowTip(a.Name, a.Avg is double avg ? Format.Int(avg) + " avg per cast" : null);

        HasHealers = actors.Count > 0;
        Rows.Sync(Healers, actors, a => a.Name, a => new HealerRow(a.Name), (row, a) =>
        {
            row.Total = a.Total;
            row.Shade = ShadeOf(a.Name);
            row.Fraction = RowMarks.Fraction(a.Total, top);
            row.IsOwner = a.Name == Roster.Owner;
            row.Tip = Tip(a);
            row.Name = NameOf(a.Name);
            row.Swatch = SwatchOf(a.Name);
            row.Job = Roster.JobLabel(a.Name) is { Length: > 0 } job ? job : None;
            row.JobTip = Roster.JobTitle(a.Name);
            row.ShowJob = !HideNames;
            row.Healing = Format.Int(a.Total);
            row.Share = Percent(a.Share);
            row.Casts = Format.Int(a.Casts);
            row.Avg = Whole(a.Avg);
            // A dash, not a zero: most characters have no pet to have healed.
            row.PetHealing = a.PetTotal != 0 ? Format.Int(a.PetTotal) : None;
        });

        // The Party line: the party's own figures, which the count already
        // has (HealTotals). The number under Job is how many rows stand
        // above it; its HPS is written by the draw beat with every row's.
        var h = c.Healing;
        HealParty.Count = Format.Int(actors.Count);
        HealParty.Healing = Format.Int(h.Total);
        HealParty.Share = h.Total > 0 ? Percent(1) : None;
        HealParty.Casts = Format.Int(h.Casts);
        HealParty.Avg = Whole(h.Avg);
        HealParty.PetHealing = h.PetTotal != 0 ? Format.Int(h.PetTotal) : None;

        // Floating, the strip: Healing, its share, and casts. Nothing in it
        // moves with the clock, as nothing in the damage strip does.
        Rows.Sync(HealStrip, actors, a => a.Name, a => new HealStripRow(a.Name), (row, a) =>
        {
            row.Name = NameOf(a.Name);
            row.Job = JobBadge(a.Name);
            row.Fill = PanelShadeOf(a.Name);
            row.Edge = Solid(PanelColorOf(a.Name));
            row.Fraction = RowMarks.Fraction(a.Total, top);
            row.Healing = Format.Int(a.Total);
            row.Share = Percent(a.Share);
            row.Casts = Format.Int(a.Casts);
            row.IsOwner = a.Name == Roster.Owner;
            row.Tip = Tip(a);
        });
    }

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
        _ => null,
    };

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
    readonly record struct HealsHead(string Name, double Total, double? Share, double Pet);

    /// <summary>A line of the Heals table under a heading: whose (the
    /// character who cast it), what it is (its pick), the name it is drawn
    /// with, whether that is a person, whether it is the third line, its
    /// figures, what its share is out of (Of), and the largest beside it
    /// (Most).</summary>
    readonly record struct HealsLine(string Actor, string Pick, string Name, bool Person, bool Under, double Total,
                                     int Casts, double Avg, double? Min, double Max, double Of, double Most);

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
            double everything = c.Healing.Total + c.Healing.PetTotal;
            top = RowMarks.Largest(c.Healing.Targets.Select(p => p.Total));
            foreach (var p in c.Healing.Targets)
            {
                var head = new HealsHead(p.Name, p.Total, everything != 0 ? p.Total / everything : null, 0);
                items.Add((head, null));
                if (!heads.Contains(p.Name)) continue;
                double most = RowMarks.Largest(p.Parts.Select(x => x.Total));
                foreach (var a in HealsSort.Order(p.Parts, k => PartKey(k, people: true)))
                {
                    var pick = HealPick(null, p.Name);
                    items.Add((head, new HealsLine(a.Name, pick, a.Name, true, false, a.Total, a.Casts, a.Avg, a.Min,
                                                   a.Max, p.Total, most)));
                    if (!lines.Contains(a.Name + KeyGap + pick)) continue;
                    double under = RowMarks.Largest(a.Parts.Select(x => x.Total));
                    foreach (var h in HealsSort.Order(a.Parts, k => PartKey(k, people: false)))
                        items.Add((head, new HealsLine(a.Name, HealPick(h.Name, p.Name), h.Name, false, true, h.Total,
                                                       h.Casts, h.Avg, h.Min, h.Max, a.Total, under)));
                }
            }
        }
        else
        {
            top = RowMarks.Largest(c.Healing.Actors.Select(a => a.Total));
            foreach (var a in c.Healing.Actors)
            {
                var head = new HealsHead(a.Name, a.Total, a.Share, a.PetTotal);
                items.Add((head, null));
                if (!heads.Contains(a.Name)) continue;
                // Out of everything under this heading, the pet's heals too:
                // they are listed here, so the column adds up to 100. A third
                // line's is out of the line it is under.
                double all = a.Total + a.PetTotal;
                if (!ByPerson)
                {
                    double most = RowMarks.Largest(a.ActionList.Select(x => x.Total));
                    foreach (var act in HealsSort.Order(a.ActionList, HealKey))
                    {
                        items.Add((head, new HealsLine(a.Name, act.Name, act.Name, false, false, act.Total, act.Casts,
                                                       act.Avg, act.Min, act.Max, all, most)));
                        if (!lines.Contains(a.Name + KeyGap + act.Name)) continue;
                        double under = RowMarks.Largest(act.Targets.Select(x => x.Total));
                        foreach (var p in HealsSort.Order(act.Targets, k => PartKey(k, people: true)))
                            items.Add((head, new HealsLine(a.Name, HealPick(act.Name, p.Name), p.Name, true, true, p.Total,
                                                           p.Casts, p.Avg, p.Min, p.Max, act.Total, under)));
                    }
                    continue;
                }
                double mostOn = RowMarks.Largest(a.Targets.Select(x => x.Total));
                foreach (var p in HealsSort.Order(a.Targets, k => PartKey(k, people: true)))
                {
                    var pick = HealPick(null, p.Name);
                    items.Add((head, new HealsLine(a.Name, pick, p.Name, true, false, p.Total, p.Casts, p.Avg, p.Min,
                                                   p.Max, all, mostOn)));
                    if (!lines.Contains(a.Name + KeyGap + pick)) continue;
                    double under = RowMarks.Largest(p.Parts.Select(x => x.Total));
                    foreach (var h in HealsSort.Order(p.Parts, k => PartKey(k, people: false)))
                        items.Add((head, new HealsLine(a.Name, HealPick(h.Name, p.Name), h.Name, false, true, h.Total,
                                                       h.Casts, h.Avg, h.Min, h.Max, p.Total, under)));
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
                row.Pet = h.Pet != 0 ? "+ " + Format.Int(h.Pet) + " pet" : "";
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
            row.Open = !l.Under && lines.Contains(l.Actor + KeyGap + l.Pick);
            (row.Selected, row.Side, row.SideFill, row.PanelSideFill) = MarkOf(on, fills, l.Actor, l.Pick);
            row.Label = NameOf(l.Actor) + ", " + (HealParts(l.Pick) is (null, { } who)
                ? "heals on " + NameOf(who)
                : HealWords(l.Pick));
        });
    }

    // --------------------------------------------------------------- support

    /// <summary>Every buff, removal and ward, under the character who cast
    /// it: casts, and of the people reached, on how many it took (Zerg.Core's
    /// Support says why there is nothing more).</summary>
    public ObservableCollection<SupportRow> SupportRows { get; } = [];
    [ObservableProperty] private bool hasSupport;
    /// <summary>The casts in all, for the pane's heading.</summary>
    [ObservableProperty] private string supportNote = "";
    [ObservableProperty] private string supportEmptyText = "";

    /// <summary>The actions whose people are listed under them, by the
    /// caster's real name and the action (<see cref="KeyGap"/> between).
    /// Every action starts closed.</summary>
    readonly HashSet<string> openSupport = new(StringComparer.Ordinal);

    /// <summary>Pressing an action lists the people it reached under it;
    /// pressing it again puts them away.</summary>
    [RelayCommand]
    void ToggleSupport(SupportRow row)
    {
        if (row.IsHeading || row.IsUnder) return;
        if (!openSupport.Remove(row.Key)) openSupport.Add(row.Key);
        // Nothing is counted again: the last count, with more or fewer of its lines.
        if (snapshot is { } c) DrawSupport(c);
    }

    void DrawSupport(Snapshot c)
    {
        SupportEmptyText = SessionText.Empty(Shown.Session, Viewing, what: "support");
        var items = new List<(SupportCaster Caster, SupportAction? Action, SupportTarget? Target)>();
        foreach (var a in c.Support.Casters)
        {
            items.Add((a, null, null));
            foreach (var act in a.Actions)
            {
                items.Add((a, act, null));
                if (!openSupport.Contains(a.Name + KeyGap + act.Name)) continue;
                foreach (var p in act.Targets) items.Add((a, act, p));
            }
        }
        HasSupport = items.Count > 0;
        SupportNote = c.Support.Casts > 0 ? Format.Int(c.Support.Casts) + " cast" + (c.Support.Casts == 1 ? "" : "s") : "";

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
                row.Label = "Support of " + NameOf(a.Name);
                return;
            }
            if (p != null)
            {
                row.Name = NameOf(p.Name);
                row.Swatch = SwatchOf(p.Name);
                row.PanelSwatch = Solid(PanelColorOf(p.Name));
                row.Casts = Format.Int(p.Casts);
                row.Hits = Format.Int(p.Hits);
                row.Misses = Format.Int(p.Misses);
                row.HitRate = Percent(p.HitRate);
                row.Label = NameOf(a.Name) + ", " + act.Name + " on " + NameOf(p.Name) + ", " + row.Casts + " cast" +
                            (p.Casts == 1 ? "" : "s") + ", " + row.Hits + " hit, " + row.Misses + " missed, " + row.HitRate;
                return;
            }
            row.Name = act.Name;
            row.Casts = Format.Int(act.Casts);
            row.Hits = Format.Int(act.Hits);
            row.Misses = Format.Int(act.Misses);
            row.HitRate = Percent(act.HitRate);
            row.Open = openSupport.Contains(row.Key);
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
    static Distribution SpreadOf(Snapshot c, string actor, string pick)
    {
        var (heal, who) = HealParts(pick);
        return Healing.Distribution(c.Heals, actor, heal, who);
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
                                  SwatchOf(on.Actor), a, b, CompareSheet.Sides(a, b, healing: true), SidesOf(on), NameOf);
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
