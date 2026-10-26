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

    // ----------------------------------------------------------------- heals

    public ObservableCollection<HealRow> Heals { get; } = [];
    [ObservableProperty] private bool hasHeals;
    /// <summary>The characters whose heals are showing under their heading,
    /// by real name. Everyone starts as a heading alone, as in the actions
    /// table.</summary>
    readonly HashSet<string> openHeals = new(StringComparer.Ordinal);

    /// <summary>Selecting a character shows their heals under the heading;
    /// selecting them again puts the heals away.</summary>
    [RelayCommand]
    void ToggleHeals(HealRow row)
    {
        if (!row.IsHeading) return;
        if (!openHeals.Remove(row.Actor)) openHeals.Add(row.Actor);
        // Nothing is counted again: the last count, with more or fewer of its lines.
        if (snapshot is { } c) DrawHeals(c);
    }

    void DrawHeals(Snapshot c)
    {
        // A heading is shaded to the healer's own healing out of the
        // leading healer's, as in the table above; a heal's share is drawn
        // to its total out of the largest of that character's heals (Most).
        var items = new List<(HealerTotals Actor, HealAction? Action, double Most)>();
        double top = RowMarks.Largest(c.Healing.Actors.Select(a => a.Total));
        foreach (var a in c.Healing.Actors)
        {
            items.Add((a, null, top));
            if (!openHeals.Contains(a.Name)) continue;
            double most = RowMarks.Largest(a.ActionList.Select(x => x.Total));
            foreach (var act in HealsSort.Order(a.ActionList, HealKey)) items.Add((a, act, most));
        }
        HasHeals = items.Count > 0;
        var fills = FillsOf(healDrill);

        static string Key((HealerTotals Actor, HealAction? Action, double Most) i) =>
            i.Action == null ? i.Actor.Name : i.Actor.Name + KeyGap + i.Action.Name;

        Rows.Sync(Heals, items, Key, i => new HealRow(Key(i), i.Actor.Name, i.Action?.Name), (row, i) =>
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
                row.Share = Percent(a.Share);
                row.Pet = a.PetTotal != 0 ? "+ " + Format.Int(a.PetTotal) + " pet" : "";
                row.Open = openHeals.Contains(a.Name);
                row.Label = "Heals of " + NameOf(a.Name);
                return;
            }
            // Out of everything under this heading, the pet's heals too:
            // they are listed here, so the column adds up to 100.
            double all = a.Total + a.PetTotal;
            row.Name = act.Name;
            row.Casts = Format.Int(act.Casts);
            row.Total = Format.Int(act.Total);
            row.Avg = Format.Int(act.Avg);
            row.Min = Whole(act.Min);
            row.Max = Format.Int(act.Max);
            row.Share = Format.Num(all != 0 ? act.Total / all * 100 : 0, 1) + "%";
            row.ShareFraction = RowMarks.Fraction(act.Total, most);
            (row.Selected, row.Side, row.SideFill, row.PanelSideFill) = MarkOf(healDrill, fills, a.Name, act.Name);
            row.Label = NameOf(a.Name) + ", " + act.Name;
        });
    }

    // ------------------------------------------------------------ drill-down

    /// <summary>The heal the drill-down is open on, by the character's real
    /// name, and the one set beside it.</summary>
    DrillOn? healDrill;

    /// <summary>The heal drill-down just opened: the window brings it into view.</summary>
    public event Action? HealDrillOpened;

    /// <summary>What the heal drill-down's card draws: the drill-down card,
    /// over the heals (Views/DrillCard).</summary>
    public DrillView HealingDrill { get; }

    /// <summary>Selecting a heal opens its drill-down; selecting it again closes it.</summary>
    [RelayCommand]
    void HealDrill(HealRow row)
    {
        if (row.Action == null) return;
        if (PairedByPress(ref healDrill, row.Actor, row.Action))
        {
            Recount();
            return;
        }
        bool same = healDrill is { } d && d.IsA(row.Actor, row.Action);
        healDrill = same ? null : new DrillOn(row.Actor, row.Action);
        Recount();
        if (healDrill != null)
        {
            OpenDrillPane(Zerg.Core.Layout.PaneLayouts.HealingSection, Zerg.Core.Layout.PaneLayouts.HealDrill);
            HealDrillOpened?.Invoke();
        }
    }

    [RelayCommand]
    void CloseHealDrill()
    {
        healDrill = null;
        Recount();
    }

    void DrawHealDrill(Snapshot c)
    {
        DescribePick(HealingDrill.Pick, healDrill, DrillWords.Healing);
        if (healDrill is not { } on)
        {
            HealingDrill.Close();
            return;
        }
        if (on.Paired)
        {
            var events = Healing.AsEvents(c.Heals);
            var a = Counting.Distribution(events, on.Actor, on.Action);
            var b = Counting.Distribution(events, on.ActorB!, on.ActionB!);
            var (title, who) = Heading(on);
            HealingDrill.ShowPair(DrillKey(on), title,
                                  who + Dot + Versus(a.Count, b.Count) + " casts" + Dot + Versus(a.Total, b.Total) + " healed",
                                  SwatchOf(on.Actor), a, b, CompareSheet.Sides(a, b, healing: true), SidesOf(on), NameOf);
            return;
        }

        // What each cast healed, summed over everyone it reached: a Curaga
        // on five is one figure, as an area attack is in the damage
        // drill-down. A pet's heals are here too, under the pet's name.
        var d = Counting.Distribution(Healing.AsEvents(c.Heals), on.Actor, on.Action);
        var note = NameOf(on.Actor) + Dot + Format.Int(d.Count) + " cast" + (d.Count == 1 ? "" : "s") +
                   Dot + Format.Int(d.Total) + " healed";
        // Not accuracy: a heal cannot miss.
        var tiles = DrillTiles(d, new StatTile("Casts", Format.Int(d.Count)));
        HealingDrill.Show(DrillKey(on), on.Action, note, SwatchOf(on.Actor), d, tiles,
                          DrillCaption("Healed per cast", d), ColorOf(on.Actor), PanelColorOf(on.Actor), NameOf);
    }
}
