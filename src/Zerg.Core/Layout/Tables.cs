namespace Zerg.Core.Layout;

/// <summary>
/// The tables whose columns the player can move and hide, each as its card
/// writes it: every column in the order of its cells, the first fixed (the
/// name a row begins with) and a blank column that ends a row fixed too.
/// The keys are the ones the tables' headings sort by. A table's widths
/// stay in its card; these say only what each column is.
///
/// <para>The Overview's is <see cref="CharacterColumns.Table"/>.</para>
/// </summary>
public static class Tables
{
    /// <summary>The Actions table (<c>Views/ActionsCard</c>).</summary>
    public static ColumnTable Actions { get; } = new("actions",
    [
        new("name", "Action", Fixed: true),
        new("hits", "Hits"),
        new("misses", "Miss"),
        new("accuracy", "Acc"),
        new("total", "Total"),
        new("avg", "Avg"),
        new("min", "Min"),
        new("max", "Max"),
        new("spread", "Min · avg · max"),
        new("share", "Share"),
        new("end", "", Fixed: true),
    ]);

    /// <summary>The Filters pane's By target table (<c>Views/TallyCard</c>).</summary>
    public static ColumnTable Targets { get; } = new("targets",
    [
        new("name", "Target", Fixed: true),
        new("damage", "Damage"),
        new("share", "Share"),
        new("accuracy", "Acc"),
        new("wsAvg", "WS Avg"),
        new("wsShare", "WS %"),
        new("killTime", "Kill time"),
    ]);

    /// <summary>The Filters pane's By damage type table: the same card,
    /// whose last four columns a type never has.</summary>
    public static ColumnTable Types { get; } = new("types",
    [
        new("name", "Type", Fixed: true),
        new("damage", "Damage"),
        new("share", "Share"),
        new("accuracy", "", Fixed: true),
        new("wsAvg", "", Fixed: true),
        new("wsShare", "", Fixed: true),
        new("killTime", "", Fixed: true),
    ]);

    /// <summary>The Battle Log (<c>Views/FightsCard</c>): its first column the clock.</summary>
    public static ColumnTable Fights { get; } = new("fights",
    [
        new("when", "When", Fixed: true),
        new("target", "Target"),
        new("killTime", "Kill time"),
        new("damage", "Damage"),
        new("dps", "DPS"),
    ]);

    /// <summary>Healing by character (<c>Views/HealBarsCard</c>).</summary>
    public static ColumnTable Healers { get; } = new("healers",
    [
        new("name", "Character", Fixed: true),
        new("job", "Job"),
        new("healing", "Healing"),
        new("share", "Healing %"),
        new("hps", "HPS"),
        new("casts", "Casts"),
        new("avg", "Avg / cast"),
        new("petHealing", "Pet Healing"),
    ]);

    /// <summary>The Heals table (<c>Views/HealActionsCard</c>), the same under each of its tabs.</summary>
    public static ColumnTable Heals { get; } = new("heals",
    [
        new("name", "Heal", Fixed: true),
        new("casts", "Casts"),
        new("total", "Total"),
        new("avg", "Avg"),
        new("min", "Min"),
        new("max", "Max"),
        new("share", "Share"),
        new("end", "", Fixed: true),
    ]);

    /// <summary>The Support table (<c>Views/SupportCard</c>).</summary>
    public static ColumnTable Support { get; } = new("support",
    [
        new("name", "Character / action", Fixed: true),
        new("casts", "Casts"),
        new("hits", "Hit"),
        new("misses", "Miss"),
        new("hitRate", "Hit %"),
        new("end", "", Fixed: true),
    ]);
}
