using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zerg.Core;

namespace Zerg;

/// <summary>What a line of a Compare table is.</summary>
public enum CompareRowKind
{
    /// <summary>A character, an action, a target, a heal: a line of figures.</summary>
    Line,
    /// <summary>A character's heading over their actions, or a target's
    /// over its characters: folds what is under it.</summary>
    Head,
    /// <summary>A pair the player made: one action of A against another of B.</summary>
    Cross,
    /// <summary>The whole party, under the lines.</summary>
    Party,
}

/// <summary>
/// One line of a Compare table: its name, the job (or "DRK → SAM"), the
/// tag of a line in one run only, its shade and A's tick, and its cells by
/// column key. Kept across counts and updated in place (<see cref="Rows.Sync"/>):
/// a count gives it new cells, and only what changed is drawn again.
/// </summary>
public sealed partial class CompareRow(string key, CompareRowKind kind) : Row(key)
{
    static readonly IReadOnlyDictionary<string, Cell> NoCells = new Dictionary<string, Cell>();

    public CompareRowKind Kind { get; } = kind;
    public bool IsHead => Kind == CompareRowKind.Head;
    public bool IsCross => Kind == CompareRowKind.Cross;
    public bool IsLine => Kind == CompareRowKind.Line;

    [ObservableProperty] private string name = "";
    [ObservableProperty] private string job = "";
    /// <summary>The character changed main job between the runs: a dot beside the job.</summary>
    [ObservableProperty] private bool jobChanged;
    /// <summary>"A only", "B only", or "".</summary>
    [ObservableProperty] private string tag = "";
    /// <summary>The tag is A's: drawn in A's colour.</summary>
    [ObservableProperty] private bool tagA;
    [ObservableProperty] private Brush? swatch;
    /// <summary>B's leading figure over B's leader: the row's shade.</summary>
    [ObservableProperty] private double fraction;
    /// <summary>A's leading figure over B's leader: A's tick.</summary>
    [ObservableProperty] private double tickA;
    [ObservableProperty] private IReadOnlyDictionary<string, Cell> cells = NoCells;
    /// <summary>A heading's lines are shown.</summary>
    [ObservableProperty] private bool open;
    /// <summary>A line under a heading.</summary>
    [ObservableProperty] private bool nested;
    /// <summary>A line of a list to isolate from that is isolated, or one that is not while others are.</summary>
    [ObservableProperty] private bool isolated;
    [ObservableProperty] private bool dimmed;
    /// <summary>A pair's two ends: "Hasaya · Tachi: Gekko".</summary>
    [ObservableProperty] private string nameA = "";
    [ObservableProperty] private string nameB = "";

    /// <summary>The key the line stands for in its table: a character's,
    /// an action's (with <see cref="Owner"/>), a target's.</summary>
    public string Subject { get; init; } = key;
    /// <summary>The character an action line or a heading is of.</summary>
    public string Owner { get; init; } = "";
    public CrossPair? Pair { get; init; }

    public Action<CompareRow>? Pressed { get; init; }
    public Action<CompareRow>? Cleared { get; init; }

    [RelayCommand]
    void Press() => Pressed?.Invoke(this);

    [RelayCommand]
    void Clear() => Cleared?.Invoke(this);

    public Cell this[string column] => Cells.GetValueOrDefault(column) ?? Cell.Blank;

    public override string ToString() => IsCross ? NameA + " against " + NameB : Name;
}

/// <summary>
/// One line of Deltas: a character (A's total, B's, the change), one of
/// their actions under them (B's average and its change, the change in
/// total, B's uses and their change), or the headings over those actions.
/// </summary>
public sealed partial class DeltaRow(string key) : Row(key)
{
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string job = "";
    [ObservableProperty] private string tag = "";
    [ObservableProperty] private bool tagA;
    [ObservableProperty] private bool jobChanged;
    [ObservableProperty] private bool nested;
    [ObservableProperty] private bool open;
    [ObservableProperty] private bool canOpen;
    /// <summary>The action the drill-down is open on.</summary>
    [ObservableProperty] private bool isolated;
    [ObservableProperty] private double delta;
    [ObservableProperty] private double scale;
    /// <summary>The bar is grey: healing, or a character in one run only.</summary>
    [ObservableProperty] private bool grey;
    [ObservableProperty] private Brush? swatch;
    /// <summary>A character's total in A; "" under a character.</summary>
    [ObservableProperty] private string previous = "";
    /// <summary>A character's total in B; an action's average (A's, in A's
    /// ink, for an action of A only).</summary>
    [ObservableProperty] private string figure = "";
    [ObservableProperty] private bool figureIsA;
    /// <summary>The average's change over A's, and its tone.</summary>
    [ObservableProperty] private string figureChange = "";
    [ObservableProperty] private ChangeTone figureTone;
    /// <summary>The change in total, and for a character the same over A as a percentage.</summary>
    [ObservableProperty] private string change = "";
    [ObservableProperty] private string changePercent = "";
    [ObservableProperty] private ChangeTone changeTone;
    /// <summary>An action's uses in B, and how many more or fewer than A's.</summary>
    [ObservableProperty] private string uses = "";
    [ObservableProperty] private string usesChange = "";

    /// <summary>The headings over a character's actions, not a line.</summary>
    public bool IsCaption { get; init; }
    public string Owner { get; init; } = "";
    /// <summary>The action, for a line under a character.</summary>
    public string Subject { get; init; } = "";
    public Action<DeltaRow>? Pressed { get; init; }

    [RelayCommand]
    void Press() => Pressed?.Invoke(this);

    public override string ToString() => Name;
}

/// <summary>One line of the live panel: a character's rate so far, and its change.</summary>
public sealed partial class LiveRow(string key) : Row(key)
{
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string job = "";
    [ObservableProperty] private string tag = "";
    [ObservableProperty] private bool tagA;
    [ObservableProperty] private Brush? swatch;
    [ObservableProperty] private double fraction;
    [ObservableProperty] private double tickA;
    [ObservableProperty] private Cell rate = Cell.Blank;

    public override string ToString() => Name;
}

/// <summary>One figure of the band, or of the drill-down's figures.</summary>
public sealed record BandItem(string Key, string Label, Cell Cell)
{
    /// <summary>UI Automation names a list's record by this.</summary>
    public override string ToString() => $"{Label} {Cell.Spoken}";
}

/// <summary>A line of the cumulative chart's ends: whose, and its total.</summary>
public sealed record RunEnd(Brush Swatch, string Name, string Total)
{
    public override string ToString() => $"{Name} {Total}";
}
