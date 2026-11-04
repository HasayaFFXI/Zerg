namespace Zerg.Core.Layout;

/// <summary>
/// The columns of the Overview table (<c>Views/BarsCard</c>): Character,
/// Job, then seventeen figures, and the column groups the player may hide
/// from its Columns menu.
///
/// <para>The widths are what the design draws them at in a table 930 wide,
/// as shares, so the table is as drawn in its pane and in proportion in any
/// other. The heading and every row share them. (DPS has four units more
/// than the design draws, from the name: a party's rate runs to four
/// figures before the point.) Each has the least it may be: a figure column
/// its longest figure (seven figures of damage, 100.0%) or its heading,
/// whichever is wider; the name enough for eight letters.</para>
///
/// <para>When even the leasts do not fit, the table drops columns, a group
/// at a time (<c>Views/Shed</c>): Ranged, Skillchain, Pet and Deaths first
/// (<see cref="Full"/> says when); then, when what is left does not fit
/// either, Weaponskill, Accuracy, Crit % and Delay (<see cref="Less"/>), leaving
/// the name, the job and the three that say who did the most. Under those
/// the table scrolls. A group the player has hidden is left out of both,
/// so hiding one gives the others the room before they go.</para>
/// </summary>
public static class CharacterColumns
{
    /// <summary>The columns while the rows are shaded.</summary>
    public const string Shaded =
        "134*90,70*62,68*64,64*62,58*58,62*56,58*54,58*54,58*56,58*54,68*62,52*52,54*56,58*56,64*58,54*56,66*58,58*56,56*54";

    /// <summary>With "shade characters" off the share is a small bar beside
    /// the Damage % figure, which needs the room: the name gives it up.</summary>
    public const string Unshaded =
        "94*90,70*62,68*64,104*100,58*58,62*56,58*54,58*54,58*56,58*54,68*62,52*52,54*56,58*56,64*58,54*56,66*58,58*56,56*54";

    /// <summary>The groups the Columns menu hides, by the key settings.json keeps them under.</summary>
    public const string Ranged = "ranged", Weaponskill = "weaponskill", Skillchain = "skillchain", Pet = "pet";

    /// <summary>Every group that can be hidden, in the table's order.</summary>
    public static IReadOnlyList<string> Groups { get; } = [Ranged, Weaponskill, Skillchain, Pet];

    /// <summary>The Deaths column, the last.</summary>
    const int Deaths = 18;

    /// <summary>The columns a group stands over; none for a key that is not a group.</summary>
    static IEnumerable<int> Of(string group) => group switch
    {
        Ranged => [8, 9],
        Weaponskill => [10, 11, 12, 13],
        Skillchain => [14, 15],
        Pet => [16, 17],
        _ => [],
    };

    /// <summary>The columns the table needs room for, with <paramref name="hidden"/> left out.</summary>
    public static string Full(bool shaded, IEnumerable<string> hidden) =>
        Columns.Without(shaded ? Shaded : Unshaded, hidden.SelectMany(Of));

    /// <summary>The columns left once the first to go have gone, with <paramref name="hidden"/> left out too.</summary>
    public static string Less(bool shaded, IEnumerable<string> hidden) =>
        Columns.Without(shaded ? Shaded : Unshaded,
                        hidden.Concat([Ranged, Skillchain, Pet]).SelectMany(Of).Append(Deaths));
}
