using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Zerg.Charts;
using Zerg.Core;

namespace Zerg;

/// <summary>
/// One drill-down, as Views/DrillCard draws it: the Damage section's, over
/// one action's hits, or the Healing section's, over one heal's casts. The
/// two are one card and one of these each, told apart by their words.
///
/// <para><b>The list of hits is kept</b>, not made again on every count: a
/// count that only brought new hits puts them at the top and leaves every
/// other row where it is, so a player scrolling through it in a fight is not
/// thrown back to the top, and a hit is spelled once. Each row's distance
/// from the average is brought up to date, as the average moves with every
/// hit. Anything else (another action, a filter, names hidden or shown, a
/// hit already listed that came out otherwise) makes the list again.</para>
/// </summary>
public sealed partial class DrillView : ObservableObject
{
    public DrillView(DrillWords words, PanelInfo panel, ICommand close, ICommand swap, PairPick pick)
    {
        Words = words;
        Panel = panel;
        CloseCommand = close;
        SwapCommand = swap;
        Pick = pick;
    }

    /// <summary>How this drill-down speaks: of hits or of casts.</summary>
    public DrillWords Words { get; }

    /// <summary>The table it goes with, whose panel shows it under a row
    /// while that table floats.</summary>
    public PanelInfo Panel { get; }

    public ICommand CloseCommand { get; }
    /// <summary>Makes B the action the drill-down is opened on, and A the other.</summary>
    public ICommand SwapCommand { get; }
    /// <summary>What is set beside the action: the heading's pick button.</summary>
    public PairPick Pick { get; }

    /// <summary>A second action is set beside the first: the figures are
    /// both sides' in a table, the chart is the two on shared bins, and
    /// every hit carries its side's tag.</summary>
    [ObservableProperty] private bool paired;
    /// <summary>While paired: the figures, A against B.</summary>
    [ObservableProperty] private ActionSpread? spread;
    [ObservableProperty] private PairedHistogramModel? pairHistogram;
    /// <summary>While paired, each side's colour: A the character's own, B
    /// its next shade or the other character's (Shades.Beside).</summary>
    [ObservableProperty] private Brush fillA = Brushes.Gray;
    [ObservableProperty] private Brush fillB = Brushes.Gray;
    [ObservableProperty] private Brush panelFillA = Brushes.Gray;
    [ObservableProperty] private Brush panelFillB = Brushes.Gray;

    [ObservableProperty] private bool open;
    [ObservableProperty] private string title = "";
    [ObservableProperty] private string note = "";
    /// <summary>Whose it is, as a swatch before the note.</summary>
    [ObservableProperty] private Brush? swatch;
    [ObservableProperty] private IReadOnlyList<StatTile> tiles = [];
    [ObservableProperty] private HistogramModel? histogram;
    [ObservableProperty] private Color fill = Colors.Gray;
    /// <summary>The same, in a floating panel's colours.</summary>
    [ObservableProperty] private Color panelFill = Colors.Gray;
    [ObservableProperty] private string caption = "";

    /// <summary>Every hit, newest first.</summary>
    public ObservableCollection<HitRow> Hits { get; } = [];

    /// <summary>What the list was made for, and the hits in it, oldest first.</summary>
    string? listedFor;
    readonly List<(double T, double Amount, string Target, bool Crit, int Side)> listed = [];

    /// <summary>Nothing picked: the card folds to its heading.</summary>
    public void Close()
    {
        Open = false;
        Unpair();
        if (Hits.Count > 0 || listedFor != null)
        {
            Hits.Clear();
            listed.Clear();
            listedFor = null;
        }
    }

    /// <summary>
    /// One action's hits (or one heal's casts), drawn. <paramref name="key"/>
    /// says what they are of and how they are drawn: the same key from one
    /// count to the next is a list that may only have grown.
    /// </summary>
    public void Show(string key, string title, string note, Brush swatch, Distribution d,
                     IReadOnlyList<StatTile> tiles, string caption, Color fill, Color panelFill,
                     Func<string, string> nameOf)
    {
        Open = true;
        Unpair();
        Title = title;
        Note = note;
        Swatch = swatch;
        if (d.Count == 0)
        {
            Tiles = Kept(Tiles, [new StatTile(Words.Nothing, "")]);
            Histogram = null;
            Caption = "";
            Hits.Clear();
            listed.Clear();
            listedFor = key;
            return;
        }
        Tiles = Kept(Tiles, tiles);
        Histogram = HistogramModel.From(d);
        Fill = fill;
        PanelFill = panelFill;
        Caption = caption;
        DrawHits(key, [.. d.Events.Select(e => (e, -1))], [d.Avg], [], nameOf);
    }

    /// <summary>
    /// Two actions side by side: <paramref name="a"/>, the one the drill-down
    /// was opened on, and <paramref name="b"/>, set beside it. The figures and
    /// bins are <paramref name="spread"/>'s; the list is both sides' hits,
    /// newest first, each measured against its own side's average.
    /// </summary>
    /// <param name="fills">A's colour, B's, and the same two in a floating panel's colours.</param>
    public void ShowPair(string key, string title, string note, Brush swatch, Distribution a, Distribution b,
                         ActionSpread spread, (Color A, Color B, Color PanelA, Color PanelB) fills,
                         Func<string, string> nameOf)
    {
        Open = true;
        Paired = true;
        Title = title;
        Note = note;
        Swatch = swatch;
        Spread = spread;
        PairHistogram = PairedHistogramModel.From(spread, "Side A", "Side B", Words.Side);
        Brush[] sides = [MainViewModel.Solid(fills.A), MainViewModel.Solid(fills.B)];
        Brush[] panelSides = [MainViewModel.Solid(fills.PanelA), MainViewModel.Solid(fills.PanelB)];
        (FillA, FillB, PanelFillA, PanelFillB) = (sides[0], sides[1], panelSides[0], panelSides[1]);
        Caption = spread.Caption;
        Tiles = [];
        Histogram = null;

        // Both sides' hits on one clock, oldest first: A's before B's at the
        // same instant, so the order is the same from one count to the next.
        var both = a.Events.Select(e => (e, 0)).Concat(b.Events.Select(e => (e, 1)))
                    .OrderBy(x => x.e.T).ThenBy(x => x.Item2).ToList();
        DrawHits(key, both, [a.Avg, b.Avg], [sides, panelSides], nameOf);
    }

    void Unpair()
    {
        if (!Paired) return;
        Paired = false;
        Spread = null;
        PairHistogram = null;
    }

    static IReadOnlyList<StatTile> Kept(IReadOnlyList<StatTile> had, IReadOnlyList<StatTile> now) =>
        had.SequenceEqual(now) ? had : now;

    /// <param name="events">Every hit, oldest first, with whose it is: -1 over one action, else 0 or 1.</param>
    /// <param name="avgs">The average each is measured against: one, or one a side.</param>
    /// <param name="fills">A pair's tags: the sides' brushes, then the same in a panel's colours.</param>
    void DrawHits(string key, List<(CombatEvent e, int Side)> events, double[] avgs, Brush[][] fills,
                  Func<string, string> nameOf)
    {
        bool grown = key == listedFor && events.Count >= listed.Count;
        for (int i = 0; grown && i < listed.Count; i++)
            grown = listed[i] == Of(events[i].e, events[i].Side);
        if (!grown)
        {
            Hits.Clear();
            listed.Clear();
            listedFor = key;
        }

        // Each with how far it fell from its side's average, out of the
        // furthest any of that side's fell: the length of the small bar beside it.
        var furthest = new double[avgs.Length];
        for (int s = 0; s < avgs.Length; s++)
        {
            int side = avgs.Length == 1 ? -1 : s;
            furthest[s] = RowMarks.Furthest(events.Where(x => x.Side == side).Select(x => x.e.Dmg), avgs[s]);
        }
        static int At(int side) => Math.Max(0, side);
        foreach (var row in Hits) row.Measure(avgs[At(row.SideIndex)], furthest[At(row.SideIndex)]);
        for (int i = listed.Count; i < events.Count; i++)
        {
            var (e, side) = events[i];
            listed.Add(Of(e, side));
            var row = new HitRow(Format.Elapsed(e.T), e.Target.Length > 0 ? nameOf(e.Target) : MainViewModel.None,
                                 e.Dmg, Words.Crits && e.Crit, side,
                                 side >= 0 ? fills[0][side] : null, side >= 0 ? fills[1][side] : null);
            row.Measure(avgs[At(side)], furthest[At(side)]);
            // Newest first.
            Hits.Insert(0, row);
        }
    }

    static (double, double, string, bool, int) Of(CombatEvent e, int side) => (e.T, e.Dmg, e.Target, e.Crit, side);
}

/// <summary>What a drill-down calls what it lists, and the words on its card.</summary>
/// <param name="Unit">"hit", or "cast".</param>
/// <param name="Every">The list's heading: "Every hit".</param>
/// <param name="Amount">Its figure's heading: "Damage", "Healed".</param>
/// <param name="Nothing">The one figure while there is nothing to list.</param>
/// <param name="Pick">The folded heading's note: what to pick.</param>
/// <param name="Away">The folded heading's note while its table floats.</param>
/// <param name="Close">Close's name, to UI Automation.</param>
/// <param name="CloseTip">Close's tooltip.</param>
/// <param name="Chart">The chart's name, to UI Automation.</param>
/// <param name="ChartEmpty">What the chart says with nothing in it.</param>
/// <param name="Crits">A critical one is marked.</param>
/// <param name="Side">What is set beside what: "action", "heal".</param>
public sealed record DrillWords(string Unit, string Every, string Amount, string Nothing, string Pick, string Away,
                                string Close, string CloseTip, string Chart, string ChartEmpty, bool Crits, string Side)
{
    /// <summary>The pick button's first line while two are set side by side.</summary>
    public string Alone => "This " + Side + " alone";
    /// <summary>The pick button with nothing set beside the drill-down's own.</summary>
    public string Nobody => "Compare with…";

    public static readonly DrillWords Damage = new(
        "hit", "Every hit", "Damage", "No hits in range", "Select an action to drill down",
        "Showing in the Actions panel", "Close drill-down", "Put the action away. The pane folds to its heading",
        "Damage distribution chart", "", Crits: true, Side: "action");

    public static readonly DrillWords Healing = new(
        "cast", "Every cast", "Healed", "No casts in range", "Select a heal to drill down",
        "Showing in the Heals panel", "Close heal drill-down", "Put the heal away. The pane folds to its heading",
        "Healing distribution chart", "No casts recorded", Crits: false, Side: "heal");
}
