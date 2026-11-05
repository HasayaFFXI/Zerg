namespace Zerg.Core.Layout;

/// <summary>
/// The columns of the Overview table (<c>Views/BarsCard</c>): Character,
/// Job, then seventeen figures, four groups of them under a name.
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
/// at a time (<c>Views/Shed</c>): Ranged, Skillchain, Pet and Deaths first;
/// then, when what is left (<see cref="Less"/>) does not fit either,
/// Weaponskill, Accuracy, Crit % and Delay, leaving the name, the job and
/// the three that say who did the most. Under those the table scrolls. A
/// column the player has hidden needs no room in either
/// (<see cref="ColumnLayout.Needs"/>), so hiding one gives the others the
/// room before they go. Where the player has moved them does not change
/// what goes.</para>
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

    /// <summary>The groups' keys, as settings.json keeps them.</summary>
    public const string Ranged = "ranged", Weaponskill = "weaponskill", Skillchain = "skillchain", Pet = "pet";

    /// <summary>The columns, as BarsCard writes them, and what the player may do with them.</summary>
    public static ColumnTable Table { get; } = new("bars",
    [
        new("name", "Character", Fixed: true),
        new("job", "Job"),
        new("damage", "Damage"),
        new("share", "Damage %"),
        new("dps", "DPS"),
        new("accuracy", "Accuracy"),
        new("crit", "Crit %"),
        new("delay", "Delay"),
        new("rangedAccuracy", "Acc", Ranged),
        new("rangedCrit", "Crit %", Ranged),
        new("wsDamage", "Damage", Weaponskill),
        new("wsAvg", "Avg", Weaponskill),
        new("wsShare", "%", Weaponskill),
        new("wsAccuracy", "Acc", Weaponskill),
        new("scDamage", "Damage", Skillchain),
        new("scShare", "%", Skillchain),
        new("petDamage", "Damage", Pet),
        new("petAccuracy", "Acc", Pet),
        new("deaths", "Deaths"),
    ], new Dictionary<string, string>
    {
        [Ranged] = "Ranged",
        [Weaponskill] = "Weaponskill",
        [Skillchain] = "Skillchain",
        [Pet] = "Pet",
    });

    /// <summary>The columns that go first: Ranged, Skillchain, Pet and Deaths.</summary>
    static readonly int[] First = [8, 9, 14, 15, 16, 17, 18];

    /// <summary>The columns left once the first to go have gone: those made 0 wide.</summary>
    public static string Less(bool shaded) => Columns.Zeroed(shaded ? Shaded : Unshaded, First);

    /// <summary><see cref="Less"/>, for XAML to name.</summary>
    public static string LessShaded { get; } = Less(true);
    public static string LessUnshaded { get; } = Less(false);
}
