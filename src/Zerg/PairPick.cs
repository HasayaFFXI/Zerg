using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Zerg;

/// <summary>One action (or heal) a side of a pair can be set to, in a pick
/// button's menu (Views/PairPicker).</summary>
/// <param name="Name">The action, as its row names it.</param>
/// <param name="Note">How many times it was used, quieter, at the line's right.</param>
/// <param name="Ticked">It is the side's action now.</param>
/// <param name="Pick">Sets the side to it.</param>
public sealed record PairOption(string Name, string Note, bool Ticked, Action Pick);

/// <summary>
/// The actions of one character (or one job) a side can be set to. The
/// first group of a menu is the one the pair is about and stands in the menu
/// itself; every other has a line of its own that opens onto its actions.
/// </summary>
/// <param name="Who">The character as drawn, or "" for lines that belong to nobody.</param>
public sealed record PairGroup(string Who, Brush? Swatch, IReadOnlyList<PairOption> Options);

/// <summary>
/// What a pick button for one side of a pair shows, and what its menu is
/// made of. The menu's lines are asked for when it opens, not on every
/// count: a session goes on counting under it, and eighteen characters'
/// actions are a few hundred lines nobody asked for.
/// </summary>
/// <param name="name">What a screen reader calls the button.</param>
/// <param name="groups">The lines, worked out when the menu opens.</param>
public sealed partial class PairPick(string name, Func<IReadOnlyList<PairGroup>> groups) : ObservableObject
{
    public string Name { get; } = name;

    /// <summary>The side's action as it stands: what the button reads.</summary>
    [ObservableProperty] private string label = "";
    [ObservableProperty] private string? tip;
    /// <summary>The menu would have something to choose.</summary>
    [ObservableProperty] private bool canPick = true;

    public IReadOnlyList<PairGroup> Groups() => groups();
}
