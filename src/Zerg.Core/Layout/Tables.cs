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
        new("mp", "MP Spent"),
        new("perMp", "HP/MP"),
        new("overcure", "Overcure"),
        new("overcurePct", "Overcure %"),
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
        new("mp", "MP Spent"),
        new("perMp", "HP/MP"),
        new("base", "Base"),
        new("overcure", "Overcure"),
        new("overcurePct", "Overcure %"),
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

    // ------------------------------------------------------- floating panels

    /// <summary>
    /// The Overview as it floats: the strip (<c>Views/BarsCard</c>), every
    /// column the table has, each where the table writes it. The job stands
    /// beside the name in a strip, so its column is fixed, nothing wide and
    /// never listed. As installed the strip shows what it showed before it
    /// had columns to choose: Damage, its share, the four split rates and
    /// the deaths. A panel's columns are its own (settings.json's
    /// <c>panelColumns</c>).
    /// </summary>
    public static ColumnTable Strip { get; } = new("bars",
        [.. CharacterColumns.Table.All.Select(c => c.Key == "job" ? c with { Name = "", Fixed = true } : c)],
        new Dictionary<string, string>
        {
            [CharacterColumns.Ranged] = "Ranged",
            [CharacterColumns.Weaponskill] = "Weaponskill",
            [CharacterColumns.Skillchain] = "Skillchain",
            [CharacterColumns.Pet] = "Pet",
        },
        ["dps", CharacterColumns.Weaponskill, CharacterColumns.Skillchain, CharacterColumns.Pet]);

    /// <summary>Healing by character as it floats: the strip
    /// (<c>Views/HealBarsCard</c>), every column the table has, the job
    /// fixed beside the name as in the damage strip. As installed:
    /// Healing, its share, Casts, MP Spent, HP/MP and Overcure %.</summary>
    public static ColumnTable HealStrip { get; } = new("healers",
        [.. Healers.All.Select(c => c.Key == "job" ? c with { Name = "", Fixed = true } : c)],
        hidden: ["hps", "avg", "overcure", "petHealing"]);

    /// <summary>A drill-down's list of hits (<c>Views/DrillCard</c>), as it
    /// floats: the time first and fixed, then the action and its target,
    /// the amount, the mark of how far it fell from the average, and that
    /// distance in figures. A blank column ends the row.</summary>
    public static ColumnTable Hits { get; } = HitList("hits", "Damage");

    /// <summary>The healing drill-down's list of casts.</summary>
    public static ColumnTable Casts { get; } = HitList("casts", "Healing");

    static ColumnTable HitList(string key, string amount) => new(key,
    [
        new("time", "Time", Fixed: true),
        new("action", "Action"),
        new("amount", amount),
        new("mark", "Mark"),
        new("vsAvg", "vs avg"),
        new("end", "", Fixed: true),
    ]);

    /// <summary>Compare's live panel (<c>Views/CompareLive</c>): each
    /// character's rate so far and its change from the other run's.</summary>
    public static ColumnTable Live { get; } = new("live",
    [
        new("name", "Character", Fixed: true),
        new("rate", "Rate"),
        new("change", "Change"),
    ]);

    // ------------------------------------------------------------- Compare

    /// <summary>Compare's Overview: the same columns as the Damage section's
    /// (<see cref="CharacterColumns.Table"/>), its own key, and fewer shown
    /// as installed: Damage %, Ranged, Skillchain, Pet and all but the
    /// weaponskills' average are in the Columns menu.</summary>
    public static ColumnTable CompareBars { get; } = new("cbars", CharacterColumns.Table.All,
        new Dictionary<string, string>
        {
            [CharacterColumns.Ranged] = "Ranged",
            [CharacterColumns.Weaponskill] = "Weaponskill",
            [CharacterColumns.Skillchain] = "Skillchain",
            [CharacterColumns.Pet] = "Pet",
        },
        ["share", "rangedAccuracy", "rangedCrit", "wsDamage", "wsShare", "wsAccuracy", "scDamage", "scShare",
         "petDamage", "petAccuracy"]);

    /// <summary>The columns of Compare's Overview that go first when it is
    /// narrow, in the order they go.</summary>
    public static IReadOnlyList<string> CompareShed { get; } = ["crit", "delay", "wsAvg", "accuracy", "deaths", "job"];

    /// <summary>Compare's Healing by character.</summary>
    public static ColumnTable CompareHealers { get; } = new("chbars",
    [
        new("name", "Character", Fixed: true),
        new("job", "Job"),
        new("healing", "Healing"),
        new("share", "Healing %"),
        new("hps", "HPS"),
        new("casts", "Casts"),
        new("avg", "Avg / cast"),
        new("mp", "MP Spent"),
        new("perMp", "HP/MP"),
        new("overcure", "Overcure"),
        new("overcurePct", "Overcure %"),
        new("deaths", "Deaths"),
    ]);

    /// <summary>Compare's Actions.</summary>
    public static ColumnTable CompareActions { get; } = new("cactions",
    [
        new("name", "Action", Fixed: true),
        new("hits", "Hits"),
        new("accuracy", "Acc"),
        new("total", "Total"),
        new("avg", "Avg"),
        new("max", "Max"),
        new("share", "Share"),
        new("end", "", Fixed: true),
    ]);

    /// <summary>Compare's Heals.</summary>
    public static ColumnTable CompareHeals { get; } = new("cheals",
    [
        new("name", "Heal", Fixed: true),
        new("casts", "Casts"),
        new("total", "Total"),
        new("avg", "Avg"),
        new("max", "Max"),
        new("share", "Share"),
        new("mp", "MP Spent"),
        new("perMp", "HP/MP"),
        new("base", "Base"),
        new("overcure", "Overcure"),
        new("overcurePct", "Overcure %"),
        new("end", "", Fixed: true),
    ]);

    /// <summary>Compare's Battle Log: a line per kind of monster.</summary>
    public static ColumnTable CompareFights { get; } = new("cfights",
    [
        new("name", "Monster", Fixed: true),
        new("kills", "Kills"),
        new("dps", "DPS"),
        new("killTime", "Kill time"),
    ]);

    /// <summary>Compare's Filters, By target.</summary>
    public static ColumnTable CompareTargets { get; } = new("cfilterTargets",
    [
        new("name", "Target", Fixed: true),
        new("damage", "Damage"),
        new("share", "Share"),
    ]);

    /// <summary>Compare's Filters, By damage type.</summary>
    public static ColumnTable CompareTypes { get; } = new("cfilterTypes",
    [
        new("name", "Type", Fixed: true),
        new("damage", "Damage"),
        new("share", "Share"),
    ]);
}
