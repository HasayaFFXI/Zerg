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
    public DrillView(DrillWords words, PanelInfo panel, ICommand close)
    {
        Words = words;
        Panel = panel;
        CloseCommand = close;
    }

    /// <summary>How this drill-down speaks: of hits or of casts.</summary>
    public DrillWords Words { get; }

    /// <summary>The table it goes with, whose panel shows it under a row
    /// while that table floats.</summary>
    public PanelInfo Panel { get; }

    public ICommand CloseCommand { get; }

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
    readonly List<(double T, double Amount, string Target, bool Crit)> listed = [];

    /// <summary>Nothing picked: the card folds to its heading.</summary>
    public void Close()
    {
        Open = false;
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
        DrawHits(key, d, nameOf);
    }

    static IReadOnlyList<StatTile> Kept(IReadOnlyList<StatTile> had, IReadOnlyList<StatTile> now) =>
        had.SequenceEqual(now) ? had : now;

    void DrawHits(string key, Distribution d, Func<string, string> nameOf)
    {
        var events = d.Events;
        bool grown = key == listedFor && events.Count >= listed.Count;
        for (int i = 0; grown && i < listed.Count; i++)
            grown = listed[i] == Of(events[i]);
        if (!grown)
        {
            Hits.Clear();
            listed.Clear();
            listedFor = key;
        }

        // Each with how far it fell from the average, out of the furthest
        // any of them fell: the length of the small bar beside it.
        double furthest = RowMarks.Furthest(events.Select(e => e.Dmg), d.Avg);
        foreach (var row in Hits) row.Measure(d.Avg, furthest);
        for (int i = listed.Count; i < events.Count; i++)
        {
            var e = events[i];
            listed.Add(Of(e));
            var row = new HitRow(Format.Elapsed(e.T), e.Target.Length > 0 ? nameOf(e.Target) : MainViewModel.None,
                                 e.Dmg, Words.Crits && e.Crit);
            row.Measure(d.Avg, furthest);
            // Newest first.
            Hits.Insert(0, row);
        }
    }

    static (double, double, string, bool) Of(CombatEvent e) => (e.T, e.Dmg, e.Target, e.Crit);
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
public sealed record DrillWords(string Unit, string Every, string Amount, string Nothing, string Pick, string Away,
                                string Close, string CloseTip, string Chart, string ChartEmpty, bool Crits)
{
    public static readonly DrillWords Damage = new(
        "hit", "Every hit", "Damage", "No hits in range", "Select an action to drill down",
        "Showing in the Actions panel", "Close drill-down", "Put the action away. The pane folds to its heading",
        "Damage distribution chart", "", Crits: true);

    public static readonly DrillWords Healing = new(
        "cast", "Every cast", "Healed", "No casts in range", "Select a heal to drill down",
        "Showing in the Heals panel", "Close heal drill-down", "Put the heal away. The pane folds to its heading",
        "Healing distribution chart", "No casts recorded", Crits: false);
}
