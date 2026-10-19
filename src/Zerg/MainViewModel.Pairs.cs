using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using Zerg.Core;

namespace Zerg;

// Two actions side by side in a live section's drill-down: the one it was
// opened on (A) and one set beside it (B), the same character's or anyone
// else's. Picked with Ctrl and a press on a second row, or from the pick
// button on the drill-down's heading. RULES.md has the rules.
public sealed partial class MainViewModel
{
    /// <summary>
    /// What a drill-down is open on, by real names: one action of one
    /// character, and, while two are set side by side, the other (B).
    /// </summary>
    sealed record DrillOn(string Actor, string Action, string? ActorB = null, string? ActionB = null)
    {
        public bool Paired => ActorB != null && ActionB != null;
        /// <summary>The same drill-down with nothing beside it.</summary>
        public DrillOn Alone => this with { ActorB = null, ActionB = null };
        public bool IsA(string actor, string action) => Actor == actor && Action == action;
        public bool IsB(string actor, string action) => ActorB == actor && ActionB == action;
        /// <summary>B the one it is open on, A the one beside it.</summary>
        public DrillOn Swapped => Paired ? new(ActorB!, ActionB!, Actor, Action) : this;
    }

    static bool IsChain(string action) => action.StartsWith("Skillchain:", StringComparison.Ordinal);

    /// <summary>A drill-down with its skillchains taken out: closed if it is
    /// open on one, put back to one action if a skillchain was beside it.</summary>
    static DrillOn? WithoutChains(DrillOn? on) =>
        on == null || IsChain(on.Action) ? null : on.ActionB is { } b && IsChain(b) ? on.Alone : on;

    /// <summary>
    /// A drill-down kept to what is still there: closed if its own action
    /// is not in <paramref name="has"/>, put back to one action if the one
    /// beside it is not.
    /// </summary>
    static DrillOn? Still(DrillOn? on, Func<string, string, bool> has) =>
        on == null || !has(on.Actor, on.Action) ? null
        : on.Paired && !has(on.ActorB!, on.ActionB!) ? on.Alone : on;

    /// <summary>
    /// The two sides' colours, and the same in a floating panel's: A is the
    /// character's own, B the other character's own or, for one character
    /// against themself (or two whose colours nearly match), A's next shade
    /// that can be told from it (Shades.Beside).
    /// </summary>
    (Color A, Color B, Color PanelA, Color PanelB) SidesOf(DrillOn on)
    {
        Color a = ColorOf(on.Actor), panelA = PanelColorOf(on.Actor);
        return (a, Beside(a, ColorOf(on.ActorB!), !AppTheme.IsDark), panelA,
                Beside(panelA, PanelColorOf(on.ActorB!), light: false));
    }

    static Color Beside(Color a, Color b, bool light)
    {
        var (r, g, bl) = Shades.Beside((a.R, a.G, a.B), (b.R, b.G, b.B), light);
        return Color.FromRgb(r, g, bl);
    }

    /// <summary>How a table's row of <paramref name="actor"/>'s <paramref name="action"/>
    /// is marked: open in the drill-down, and which side of a pair it is.</summary>
    static (bool Selected, string Side, Brush? Fill, Brush? PanelFill) MarkOf(DrillOn? on, Brush[]? fills, string actor,
                                                                             string action)
    {
        if (on == null) return (false, "", null, null);
        if (on.IsA(actor, action)) return (true, fills != null ? "A" : "", fills?[0], fills?[2]);
        if (fills != null && on.IsB(actor, action)) return (false, "B", fills[1], fills[3]);
        return (false, "", null, null);
    }

    /// <summary>The sides' brushes for the rows, or null over one action.</summary>
    Brush[]? FillsOf(DrillOn? on)
    {
        if (on is not { Paired: true }) return null;
        var (a, b, pa, pb) = SidesOf(on);
        return [Solid(a), Solid(b), Solid(pa), Solid(pb)];
    }

    /// <summary>A side as a heading names it: the action, after the
    /// character when it is not the drill-down's own.</summary>
    string SideName(DrillOn on) =>
        on.ActorB == on.Actor ? on.ActionB! : NameOf(on.ActorB!) + Dot + on.ActionB;

    /// <summary>The heading of a pair: what each side is, and whose.</summary>
    (string Title, string Who) Heading(DrillOn on) =>
        (on.Action + " vs " + SideName(on),
         on.ActorB == on.Actor ? NameOf(on.Actor) : NameOf(on.Actor) + " vs " + NameOf(on.ActorB!));

    static string Versus(double a, double b) => Format.Int(a) + " vs " + Format.Int(b);

    /// <summary>
    /// The lines of a drill-down's pick button: first, the character's own
    /// actions (and, while two are side by side, the way back to one); then a
    /// line for each other character, largest first, opening onto theirs.
    /// </summary>
    /// <param name="actors">Every character with what they did, as the table lists them, and how many times.</param>
    IReadOnlyList<PairGroup> PairGroups(DrillOn? on, DrillWords words,
                                        IEnumerable<(string Actor, IEnumerable<(string Action, double Uses)> Actions)> actors,
                                        Action<DrillOn?> set)
    {
        if (on == null) return [];
        string Uses(double n) => Format.Int(n) + " " + words.Unit + (n == 1 ? "" : "s");
        PairOption Option(string actor, string action, double uses) =>
            new(action, Uses(uses), on.IsB(actor, action), () => set(on with { ActorB = actor, ActionB = action }));

        var groups = new List<PairGroup>();
        var mine = new List<PairOption>();
        if (on.Paired) mine.Add(new PairOption(words.Alone, "", false, () => set(on.Alone)));
        var list = actors.ToList();
        foreach (var (actor, actions) in list.Where(x => x.Actor == on.Actor))
            mine.AddRange(actions.Where(x => x.Action != on.Action).Select(x => Option(actor, x.Action, x.Uses)));
        groups.Add(new PairGroup(NameOf(on.Actor), SwatchOf(on.Actor), mine));
        foreach (var (actor, actions) in list.Where(x => x.Actor != on.Actor))
        {
            var options = actions.Select(x => Option(actor, x.Action, x.Uses)).ToList();
            if (options.Count > 0) groups.Add(new PairGroup(NameOf(actor), SwatchOf(actor), options));
        }
        return groups;
    }

    /// <summary>What a drill-down's pick button reads, for the pair it is open on.</summary>
    void DescribePick(PairPick pick, DrillOn? on, DrillWords words)
    {
        pick.Label = on is { Paired: true } ? SideName(on) : words.Nobody;
        pick.Tip = "Set another " + words.Side + " beside this one, this character’s or anyone’s. " +
                   "Or hold Ctrl and select its row in the table";
    }

    // --------------------------------------------------------------- damage

    PairPick MakeDamagePick() => new("Compare with another action", () =>
        snapshot is not { } c ? []
        : PairGroups(drill, DrillWords.Damage,
                     c.Totals.Actors.Select(a => (a.Name, a.ActionList.Select(x => (x.Name, (double)x.Hits)))),
                     SetDrill));

    void SetDrill(DrillOn? on)
    {
        drill = on;
        Recount();
    }

    [RelayCommand]
    void SwapDrill() => SetDrill(drill?.Swapped);

    /// <summary>Ctrl with a press: a second action set beside the one open,
    /// or the one beside it taken away again. False when there is no pair to
    /// make: nothing open, or the press is on the open action itself.</summary>
    static bool PairedByPress(ref DrillOn? on, string actor, string action)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || on is not { } open || open.IsA(actor, action)) return false;
        on = open.IsB(actor, action) ? open.Alone : open with { ActorB = actor, ActionB = action };
        return true;
    }

    // -------------------------------------------------------------- healing

    PairPick MakeHealPick() => new("Compare with another heal", () =>
        snapshot is not { } c ? []
        : PairGroups(healDrill, DrillWords.Healing,
                     c.Healing.Actors.Select(a => (a.Name, a.ActionList.Select(x => (x.Name, (double)x.Casts)))),
                     SetHealDrill));

    void SetHealDrill(DrillOn? on)
    {
        healDrill = on;
        Recount();
    }

    [RelayCommand]
    void SwapHealDrill() => SetHealDrill(healDrill?.Swapped);
}
