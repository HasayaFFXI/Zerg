using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zerg.Charts;
using Zerg.Core;
using Zerg.Core.Layout;

namespace Zerg;

/// <summary>Compare's tables, Deltas, the chart, the drill-down and the live panel.</summary>
public sealed partial class CompareViewModel
{
    const string Sep = "\n";

    public ObservableCollection<CompareRow> Bars { get; } = [];
    public ObservableCollection<CompareRow> Healers { get; } = [];
    public CompareRow Party { get; } = new("", CompareRowKind.Party) { Name = "Party" };
    public CompareRow HealParty { get; } = new("", CompareRowKind.Party) { Name = "Party" };
    public ObservableCollection<CompareRow> ActionRows { get; } = [];
    public ObservableCollection<CompareRow> HealRows { get; } = [];
    public ObservableCollection<CompareRow> TallyRows { get; } = [];
    public CompareRow AllMonsters { get; } = new("", CompareRowKind.Party) { Name = "All monsters" };
    public ObservableCollection<CompareRow> TargetRows { get; } = [];
    public ObservableCollection<CompareRow> TypeRows { get; } = [];
    public ObservableCollection<DeltaRow> DeltaRows { get; } = [];
    public ObservableCollection<LiveRow> LiveRows { get; } = [];

    readonly HashSet<string> openTargets = [], openActions = [], openDeltas = [];

    // ----------------------------------------------------------- colours

    static Color ColourOf(Identity? id, bool dark) =>
        id is null ? AppTheme.Series(0, dark) : AppTheme.Job(id.ColorJob, dark) ?? AppTheme.Series(id.Slot, dark);

    void Paint(CompareRow row, Identity? id)
    {
        row.Swatch = MainViewModel.Solid(ColourOf(id, AppTheme.IsDark));
    }

    static void Tagged(CompareRow row, Core.RunSide side)
    {
        row.Tag = side switch { Core.RunSide.AOnly => "A only", Core.RunSide.BOnly => "B only", _ => "" };
        row.TagA = side == Core.RunSide.AOnly;
    }

    /// <summary>A table's lines in the order its heading asks: by B's figure,
    /// or (a heading's Δ) by the change. Lines in one run only stay last.</summary>
    static List<CompareLine> Ordered(IEnumerable<CompareLine> lines, ColumnSort sort)
    {
        var list = lines.ToList();
        if (sort.By is null) return list;
        SortKey<CompareLine>? KeyOf(string column)
        {
            if (column == "name") return SortKey<CompareLine>.Of(l => (string?)l.Label);
            if (column.EndsWith(".delta", StringComparison.Ordinal))
            {
                var c = column[..^6];
                return SortKey<CompareLine>.Of(l => l[c].Delta);
            }
            return SortKey<CompareLine>.Of(l => l[column].ValueB);
        }
        var both = sort.Order(list.Where(l => l.Side == Core.RunSide.Both), KeyOf);
        var rest = sort.Order(list.Where(l => l.Side != Core.RunSide.Both), KeyOf);
        return [.. both, .. rest];
    }

    // ----------------------------------------------------------- Overview

    void DrawOverview(CompareSheet s)
    {
        Fill(Party, s.Party, null);
        Fill(HealParty, s.HealParty, null);
        HealParty.Job = Party.Job = "";
        if (IsHealing)
        {
            Sync(Healers, Ordered(s.Healers, HealersSort), s);
            return;
        }
        if (OverviewBy == ByTargets)
        {
            targetGroups ??= s.TargetGroups();
            var rows = new List<(string Key, CompareLine Line, bool Head, string Of)>();
            foreach (var g in targetGroups)
            {
                rows.Add(("t:" + g.Head.Key, g.Head, true, g.Head.Key));
                if (openTargets.Contains(g.Head.Key))
                    foreach (var l in Ordered(g.Lines, BarsSort)) rows.Add(("t:" + g.Head.Key + Sep + l.Key, l, false, g.Head.Key));
            }
            Zerg.Rows.Sync(Bars, rows, r => r.Key,
                r => new CompareRow(r.Key, r.Head ? CompareRowKind.Head : CompareRowKind.Line)
                {
                    Subject = r.Head ? r.Of : r.Line.Key, Owner = r.Of, Pressed = PressBar,
                },
                (row, r) =>
                {
                    Fill(row, r.Line, r.Head ? null : s.Ids.GetValueOrDefault(r.Line.Key));
                    row.Nested = !r.Head;
                    row.Open = r.Head && openTargets.Contains(r.Of);
                });
            return;
        }
        var source = OverviewBy == ByJobs && jobs is { } j ? j : s;
        Sync(Bars, Ordered(source.Characters, BarsSort), source);
    }

    void Sync(ObservableCollection<CompareRow> rows, List<CompareLine> lines, CompareSheet s) =>
        Zerg.Rows.Sync(rows, lines, l => l.Key, l => new CompareRow(l.Key, CompareRowKind.Line) { Pressed = PressBar },
                       (row, l) => Fill(row, l, s.Ids.GetValueOrDefault(l.Key)));

    void Fill(CompareRow row, CompareLine l, Identity? id)
    {
        row.Name = l.Label;
        row.Job = l.Job;
        row.JobChanged = l.JobChanged;
        row.Fraction = l.Fraction;
        row.TickA = l.TickA;
        row.Cells = l.Cells;
        Tagged(row, l.Side);
        if (id != null) Paint(row, id);
    }

    void PressBar(CompareRow row)
    {
        if (row.IsHead)
        {
            if (!openTargets.Remove(row.Subject)) openTargets.Add(row.Subject);
            if (sheet != null) DrawOverview(sheet);
        }
    }

    /// <summary>Overview's "Show in Actions": the character's actions, opened.</summary>
    public void ShowInActions(CompareRow row)
    {
        openActions.Add(row.Key);
        if (sheet != null) DrawActions(sheet);
    }

    /// <summary>Overview's "Isolate": the character alone, in both runs.</summary>
    public void Isolate(CompareRow row) => CharacterFilter.Set([row.Key]);

    // ------------------------------------------------------------ Actions

    void DrawActions(CompareSheet s)
    {
        bool heal = IsHealing;
        var list = heal ? healPairs : pairs;
        var rows = new List<(string Key, CompareRowKind Kind, CompareLine Line, string Owner, string Item, CrossPair? Pair)>();
        foreach (var p in list)
            rows.Add(("x:" + p.Key, CompareRowKind.Cross, heal ? s.CrossHeal(p) : s.Cross(p), "", "", p));

        var target = heal ? HealRows : ActionRows;
        if (heal && HealsBy != HealsByCaster)
        {
            var lines = Ordered(HealsBy == HealsBySpell ? s.HealSpells : s.HealTargets, HealsSort);
            foreach (var l in lines) rows.Add(("s:" + l.Key, CompareRowKind.Line, l, "", l.Key, null));
        }
        else
        {
            foreach (var g in heal ? s.Heals : s.Actions)
            {
                rows.Add(("h:" + g.Head.Key, CompareRowKind.Head, g.Head, g.Head.Key, g.Head.Key, null));
                if (!openActions.Contains(g.Head.Key)) continue;
                foreach (var l in Ordered(g.Lines, heal ? HealsSort : ActionsSort))
                    rows.Add((g.Head.Key + Sep + l.Key, CompareRowKind.Line, l, g.Head.Key, l.Key, null));
            }
        }
        Zerg.Rows.Sync(target, rows, r => r.Key,
            r => new CompareRow(r.Key, r.Kind)
            {
                Owner = r.Owner, Subject = r.Item, Pair = r.Pair, Pressed = PressAction,
                Cleared = x => { if (x.Pair is { } p) RemovePair(p); },
            },
            (row, r) =>
            {
                Fill(row, r.Line, r.Kind == CompareRowKind.Head ? s.Ids.GetValueOrDefault(r.Owner) : null);
                row.Nested = r.Kind == CompareRowKind.Line && r.Owner.Length > 0;
                row.Open = r.Kind == CompareRowKind.Head && openActions.Contains(r.Owner);
                if (r.Kind == CompareRowKind.Line && r.Owner.Length > 0 && s.Ids.GetValueOrDefault(r.Owner) is { } id)
                    Paint(row, id);
                if (r.Pair is { } p)
                {
                    row.NameA = s.Draw(p.KeyA) + " · " + p.ActionA;
                    row.NameB = s.Draw(p.KeyB) + " · " + p.ActionB;
                }
            });
    }

    void PressAction(CompareRow row)
    {
        if (row.IsHead)
        {
            if (!openActions.Remove(row.Owner)) openActions.Add(row.Owner);
            if (sheet != null) DrawActions(sheet);
            return;
        }
        if (row.Pair is { } p) OpenDrill(p);
        else if (row.Owner.Length > 0) OpenDrill(new CrossPair(row.Owner, row.Subject, row.Owner, row.Subject));
    }

    /// <summary>"Compare with…" on an action line: the lines to pick B's end from,
    /// for each run, each character, each action.</summary>
    public IReadOnlyList<(bool RunB, string Who, IReadOnlyList<string> Actions)> PairChoices()
    {
        if (sheet is not { } s) return [];
        var list = new List<(bool, string, IReadOnlyList<string>)>();
        foreach (var runB in new[] { false, true })
            foreach (var who in s.CharactersIn(runB, IsHealing))
                list.Add((runB, who, s.ActionsIn(runB, who, IsHealing).Select(x => x.Action).ToList()));
        return list;
    }

    /// <summary>Pins A's end (the line's own action in A) against the end picked.</summary>
    public void CompareWith(CompareRow row, bool runB, string who, string action)
    {
        var p = runB ? new CrossPair(row.Owner, row.Subject, who, action) : new CrossPair(who, action, row.Owner, row.Subject);
        AddPair(p);
        OpenDrill(p);
    }

    public string NameOf(string key) => sheet?.Draw(key) ?? key;
    public string RunName(bool runB) => runB ? B.Called : A.Called;

    // ------------------------------------------------------------- Deltas

    [ObservableProperty] private Cell deltaParty = Cell.Blank;
    /// <summary>The party's bar: its change, drawn to its own length.</summary>
    [ObservableProperty] private double deltaPartyBar;
    [ObservableProperty] private double deltaPartyScale = 1;

    void DrawDeltas(CompareSheet s)
    {
        var d = s.Deltas(IsHealing);
        DeltaParty = d.Party;
        DeltaPartyBar = d.Party.Delta ?? 0;
        DeltaPartyScale = Math.Max(1, Math.Abs(DeltaPartyBar));
        var rows = new List<(string Key, DeltaLine? Line, bool Nested, string Owner)>();
        foreach (var l in d.Lines)
        {
            rows.Add((l.Key, l, false, l.Key));
            if (!openDeltas.Contains(l.Key) || l.Actions.Count == 0) continue;
            rows.Add((l.Key + Sep, null, true, l.Key));
            foreach (var x in l.Actions) rows.Add((l.Key + Sep + x.Key, x, true, l.Key));
        }
        Zerg.Rows.Sync(DeltaRows, rows, r => r.Key,
            r => new DeltaRow(r.Key) { Owner = r.Owner, Subject = r.Nested ? r.Line?.Key ?? "" : "", IsCaption = r.Line == null,
                                       Pressed = PressDelta },
            (row, r) =>
            {
                row.Nested = r.Nested;
                if (r.Line is not { } l) return;
                bool one = l.Side != Core.RunSide.Both;
                row.Name = r.Nested ? l.Label : s.Draw(l.Key);
                row.Tag = l.Side switch { Core.RunSide.AOnly => "A only", Core.RunSide.BOnly => "B only", _ => "" };
                row.TagA = l.Side == Core.RunSide.AOnly;
                row.JobChanged = l.JobChanged;
                (row.Delta, row.Scale) = (l.Delta, d.Scale);
                if (r.Nested)
                {
                    row.Grey = d.Healing;
                    row.Previous = "";
                    row.FigureIsA = l.Side == Core.RunSide.AOnly;
                    row.Figure = row.FigureIsA ? l.Average.A : l.Average.B;
                    row.FigureChange = one ? "" : l.Average.Change.Main;
                    row.FigureTone = l.Average.Change.Tone;
                    (row.Change, row.ChangePercent, row.ChangeTone) = (l.Total.Change.Main, "", l.Total.Change.Tone);
                    (row.Uses, row.UsesChange) = (l.Uses.B, l.Uses.Change.Main);
                }
                else
                {
                    row.Job = (d.Healing ? s.Healers : s.Characters).FirstOrDefault(c => c.Key == l.Key)?.Job ?? "";
                    row.CanOpen = l.Actions.Count > 0;
                    row.Open = openDeltas.Contains(l.Key) && row.CanOpen;
                    row.Grey = d.Healing || one;
                    (row.Previous, row.Figure, row.FigureIsA, row.FigureChange) = (l.Total.A, l.Total.B, false, "");
                    // The plain difference first, its percentage after it.
                    (row.Change, row.ChangePercent, row.ChangeTone) = one
                        ? (CompareSheet.Dash, "", ChangeTone.None)
                        : (l.Total.Change.Sub, l.Total.Change.Main, l.Total.Change.Tone);
                    if (s.Ids.GetValueOrDefault(l.Key) is { } id) row.Swatch = MainViewModel.Solid(ColourOf(id, AppTheme.IsDark));
                }
            });
        MarkDeltas();
    }

    /// <summary>The action the drill-down is open on, where Deltas lists it.</summary>
    void MarkDeltas()
    {
        foreach (var row in DeltaRows)
            row.Isolated = !row.IsCaption && row.Subject.Length > 0 && drill is { } d
                           && d == new CrossPair(row.Owner, row.Subject, row.Owner, row.Subject);
    }

    /// <summary>A character: their actions open or fold. An action: the
    /// drill-down opens on it, beside Deltas; pressed again, it closes.</summary>
    void PressDelta(DeltaRow row)
    {
        if (row.IsCaption) return;
        if (row.Subject.Length > 0)
        {
            if (row.Isolated) CloseDrill();
            else OpenDrill(new CrossPair(row.Owner, row.Subject, row.Owner, row.Subject), fromDeltas: true);
            return;
        }
        if (!row.CanOpen) return;
        if (!openDeltas.Remove(row.Owner)) openDeltas.Add(row.Owner);
        if (sheet != null) DrawDeltas(sheet);
    }

    // -------------------------------------------------------------- chart

    [ObservableProperty] private LineModel? pace;
    [ObservableProperty] private GapModel? gap;

    void DrawPace(CompareSheet s)
    {
        var p = IsHealing ? s.HealPace : s.DamagePace;
        Color ca = AppTheme.Run(a: true), cb = AppTheme.Run(a: false);
        var deaths = p.DeathsA.Select(d => new LineDeath(s.Draw(d.Name) + " · " + A.Called, d.At, null, 0))
                      .Concat(p.DeathsB.Select(d => new LineDeath(s.Draw(d.Name) + " · " + B.Called, d.At, null, 1))).ToList();
        var kills = p.KillsA.Concat(p.KillsB).Select(k => new LineKill(k.Name, k.At, null)).ToList();
        var series = new List<LineSeries>();
        if (s.HasA) series.Add(new LineSeries(A.Called, p.A, ca));
        if (s.HasB) series.Add(new LineSeries(B.Called, p.B, cb));
        Pace = new LineModel(p.Times, series, Deaths: deaths, Kills: kills);
        Gap = s.HasA && s.HasB ? new GapModel(p.Times, p.Gap, B.Called + " ahead", A.Called + " ahead") : null;
    }

    // ---------------------------------------------------------- Battle Log

    void DrawTally(CompareSheet s)
    {
        var picked = TargetFilter.Picked.ToHashSet();
        Zerg.Rows.Sync(TallyRows, Ordered(s.Tally.Select(TallyAsLine), FightsSort), l => l.Key,
            l => new CompareRow(l.Key, CompareRowKind.Line) { Pressed = PressTally },
            (row, l) =>
            {
                Fill(row, l, null);
                row.Isolated = picked.Contains(l.Key);
                row.Dimmed = picked.Count > 0 && !row.Isolated;
            });
        AllMonsters.Cells = TallyAsLine(s.AllMonsters).Cells;
    }

    CompareLine TallyAsLine(TallyLine t) =>
        new(t.Name, t.Name, Core.RunSide.Both, "", "", "", 0, 0, 0, new Dictionary<string, Cell>(StringComparer.Ordinal)
        {
            ["kills"] = t.Kills, ["dps"] = IsHealing ? t.Hps : t.Dps, ["killTime"] = t.KillTime,
        });

    /// <summary>A kind of monster pressed: both runs cut to it; pressed again, let go.</summary>
    void PressTally(CompareRow row)
    {
        if (TargetFilter.Picked.Count == 1 && TargetFilter.Picked.Contains(row.Key)) TargetFilter.Reset();
        else TargetFilter.Set([row.Key]);
    }

    // ------------------------------------------------------------ Filters

    void DrawFilters(CompareSheet s)
    {
        void Draw(ObservableCollection<CompareRow> rows, IReadOnlyList<CompareLine> lines, ColumnSort sort, PickFilter filter)
        {
            var picked = filter.Picked.ToHashSet();
            Zerg.Rows.Sync(rows, Ordered(lines, sort), l => l.Key,
                l => new CompareRow(l.Key, CompareRowKind.Line) { Pressed = r => filter.Toggle(r.Key) },
                (row, l) =>
                {
                    Fill(row, l, null);
                    row.Isolated = picked.Contains(l.Key);
                    row.Dimmed = picked.Count > 0 && !row.Isolated;
                });
        }
        Draw(TargetRows, s.TargetLines.Where(l => l.Key.Length > 0).ToList(), TargetSort, TargetFilter);
        Draw(TypeRows, s.TypeLines, TypeSort, TypeFilter);
    }

    // ---------------------------------------------------------- drill-down

    [ObservableProperty] private bool drillOpen;
    [ObservableProperty] private IReadOnlyList<BandItem> drillFigures = [];
    [ObservableProperty] private PairedHistogramModel? drillHistogram;
    [ObservableProperty] private string drillTitle = "";
    public PairPick DrillWhoA { get; private set; } = null!;
    public PairPick DrillWhatA { get; private set; } = null!;
    public PairPick DrillWhoB { get; private set; } = null!;
    public PairPick DrillWhatB { get; private set; } = null!;

    /// <summary>What the drill-down opened on, for Reset.</summary>
    CrossPair? drillFrom;
    /// <summary>The layout as the drill-down left it, and the panes it folded.</summary>
    SplitNode? foldedFor;
    string[] folded = [];

    void MakePicks()
    {
        DrillWhoA = new PairPick("Character in " + A.Called, () => WhoGroups(runB: false));
        DrillWhatA = new PairPick("Action in " + A.Called, () => WhatGroups(runB: false));
        DrillWhoB = new PairPick("Character in " + B.Called, () => WhoGroups(runB: true));
        DrillWhatB = new PairPick("Action in " + B.Called, () => WhatGroups(runB: true));
        OnPropertyChanged(nameof(DrillWhoA));
        OnPropertyChanged(nameof(DrillWhatA));
        OnPropertyChanged(nameof(DrillWhoB));
        OnPropertyChanged(nameof(DrillWhatB));
    }

    IReadOnlyList<PairGroup> WhoGroups(bool runB)
    {
        if (sheet is not { } s || drill is not { } d) return [];
        var on = runB ? d.KeyB : d.KeyA;
        var options = s.CharactersIn(runB, IsHealing).Select(who => new PairOption(s.Draw(who), "", who == on, () =>
        {
            // Another character: their biggest action, or the same one if they have it.
            var acts = s.ActionsIn(runB, who, IsHealing);
            var same = runB ? d.ActionB : d.ActionA;
            var act = acts.Any(x => x.Action == same) ? same : acts.FirstOrDefault().Action ?? same;
            SetDrill(runB ? d with { KeyB = who, ActionB = act } : d with { KeyA = who, ActionA = act });
        })).ToList();
        return [new PairGroup(RunName(runB), null, options)];
    }

    IReadOnlyList<PairGroup> WhatGroups(bool runB)
    {
        if (sheet is not { } s || drill is not { } d) return [];
        var who = runB ? d.KeyB : d.KeyA;
        var on = runB ? d.ActionB : d.ActionA;
        var options = s.ActionsIn(runB, who, IsHealing).Select(x => new PairOption(x.Action, Format.Int(x.Count), x.Action == on,
            () => SetDrill(runB ? d with { ActionB = x.Action } : d with { ActionA = x.Action }))).ToList();
        return [new PairGroup(s.Draw(who), null, options)];
    }

    void SetDrill(CrossPair p)
    {
        drill = p;
        // An end set to anything but the pressed action's is a pair, pinned atop Actions.
        if (drillFrom is { } from && p != from && !(p.KeyA == p.KeyB && p.ActionA == p.ActionB)) AddPair(p);
        if (sheet != null) DrawDrill(sheet);
    }

    void OpenDrill(CrossPair p, bool fromDeltas = false)
    {
        bool was = drill != null;
        drill = drillFrom = p;
        if (DrillWhoA == null) MakePicks();
        if (!was) FoldForDrill(true, fromDeltas);
        if (sheet != null) DrawDrill(sheet);
    }

    [RelayCommand]
    void CloseDrill()
    {
        if (drill == null) return;
        drill = drillFrom = null;
        FoldForDrill(false);
        DrillOpen = false;
        MarkDeltas();
    }

    /// <summary>B back to A's own action: the same action, the same (matched) character.</summary>
    [RelayCommand]
    void ResetDrill()
    {
        if (drill is not { } d) return;
        drill = drillFrom = new CrossPair(d.KeyA, d.ActionA, d.KeyA, d.ActionA);
        if (sheet != null) DrawDrill(sheet);
    }

    [RelayCommand]
    void SwapDrill()
    {
        if (drill is { } d) SetDrill(d.Swapped);
    }

    /// <summary>Opening a drill-down folds Deltas and the chart and unfolds
    /// the drill-down; closing it gives them back, unless the player has
    /// changed the arrangement in the meantime. Opened from Deltas, it folds
    /// every pane stacked with it but Deltas instead, so Deltas stays beside
    /// it and the distribution has the rest of the column.</summary>
    void FoldForDrill(bool open, bool fromDeltas = false)
    {
        var tree = host.CompareLayout;
        if (open)
        {
            var next = SplitTree.Fold(tree, PaneLayouts.CompareDrill, false);
            if (fromDeltas)
                folded = [.. SplitTree.Stacked(next, PaneLayouts.CompareDrill)
                             .Where(k => k != PaneLayouts.CompareDeltas && SplitTree.Leaf(next, k) is { Folded: false })];
            else
            {
                string[] keys = [PaneLayouts.CompareDeltas, PaneLayouts.ComparePace];
                folded = keys.All(k => SplitTree.Leaf(next, k) is { Folded: false }) ? keys : [];
            }
            foreach (var k in folded) next = SplitTree.Fold(next, k, true);
            host.Rearrange(PaneLayouts.CompareSection, next, "drill-down opened");
            foldedFor = folded.Length > 0 ? host.CompareLayout : null;
            return;
        }
        var back = SplitTree.Fold(tree, PaneLayouts.CompareDrill, true);
        if (foldedFor != null && tree == foldedFor)
            foreach (var k in folded) back = SplitTree.Fold(back, k, false);
        foldedFor = null;
        folded = [];
        host.Rearrange(PaneLayouts.CompareSection, back, "drill-down closed");
    }

    void DrawDrill(CompareSheet s)
    {
        if (drill is not { } d)
        {
            DrillOpen = false;
            return;
        }
        if (DrillWhoA == null) MakePicks();
        var m = s.Drill(d, IsHealing);
        DrillFigures = m.Figures.Select(f => new BandItem(f.Key, f.Label, f.Cell)).ToList();
        DrillHistogram = PairedHistogramModel.From(m.Spread, A.Called, B.Called, "side");
        DrillTitle = d.KeyA == d.KeyB && d.ActionA == d.ActionB ? s.Draw(d.KeyB) + " · " + d.ActionB : "";
        DrillWhoA!.Label = s.Draw(d.KeyA);
        DrillWhatA!.Label = d.ActionA;
        DrillWhoB!.Label = s.Draw(d.KeyB);
        DrillWhatB!.Label = d.ActionB;
        DrillOpen = true;
        MarkDeltas();
    }

    // ---------------------------------------------------------- live panel

    [ObservableProperty] private string liveClock = "";
    [ObservableProperty] private Cell liveParty = Cell.Blank;

    /// <summary>The live run's side, for the panel's header: the other's name.</summary>
    public string LiveAgainst => (B.IsLive ? A : B).Called;

    void DrawLive(CompareSheet s)
    {
        OnPropertyChanged(nameof(LiveAgainst));
        if (!IsLive)
        {
            LiveRows.Clear();
            return;
        }
        var live = B.IsLive ? B : A;
        LiveClock = live.Length;
        // The panel reads B against A whichever side is live.
        LiveParty = s.DamageBand.Single(c => c.Key == "dps").Cell;
        Zerg.Rows.Sync(LiveRows, s.Characters.ToList(), l => l.Key, l => new LiveRow(l.Key), (row, l) =>
        {
            row.Name = l.Label;
            row.Job = l.Job;
            row.Tag = l.Side switch { Core.RunSide.AOnly => "A only", Core.RunSide.BOnly => "B only", _ => "" };
            row.TagA = l.Side == Core.RunSide.AOnly;
            row.Rate = l["dps"];
            row.Swatch = MainViewModel.Solid(ColourOf(s.Ids.GetValueOrDefault(l.Key), dark: true));
            double leader = s.Characters.Select(x => x["dps"].ValueB ?? 0).DefaultIfEmpty(0).Max();
            row.Fraction = leader > 0 ? Math.Clamp((l["dps"].ValueB ?? 0) / leader, 0, 1) : 0;
            row.TickA = leader > 0 ? Math.Clamp((l["dps"].ValueA ?? 0) / leader, 0, 1) : 0;
        });
    }
}

/// <summary>The Gap view's lines: B − A on the one clock, and what a side ahead is called.</summary>
public sealed record GapModel(double[] Times, double[] Gap, string AheadB, string AheadA);
