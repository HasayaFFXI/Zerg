using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zerg.Core;

namespace Zerg;

/// <summary>Where a window was: its restored rectangle in physical pixels, as
/// GetWindowPlacement reports it, and whether it was maximized over that.</summary>
public sealed record Placement(int X, int Y, int Width, int Height, bool Maximized);

/// <summary>
/// Everything Zerg remembers between runs, in
/// %LOCALAPPDATA%\VibeXI\zerg\settings.json. Best effort both ways: a missing
/// or unreadable file means defaults, and a failed save is logged and dropped.
///
/// <para><b>The file is the player's</b>, and may have been edited by hand or
/// written by another version. A value of the wrong shape costs that one
/// setting, which goes back to its default (<see cref="LenientJson"/>); a
/// <c>null</c> where a list or a word goes is the empty list or the default
/// word (<see cref="Normal"/>). Nothing in it can stop Zerg starting.</para>
/// </summary>
public sealed class Settings
{
    /// <summary>"Dark", as installed; "Light", or "System" (follow Windows).</summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>The section on screen: "Damage", "Healing", "View" or "Compare".</summary>
    public string Section { get; set; } = "Damage";

    /// <summary>Which side of its parse the View section shows: "Damage" or
    /// "Healing". The parse itself is not kept, as Compare's runs are not.</summary>
    public string ViewMode { get; set; } = "Damage";

    /// <summary>What a Compare row is: "Character" or "Job".</summary>
    public string CompareBy { get; set; } = "Character";

    /// <summary>Which side of the two runs Compare shows: "Damage" or "Healing".
    /// The runs themselves are not kept: a parse is a megabyte or two.</summary>
    public string CompareMode { get; set; } = "Damage";

    /// <summary>Characters switched off, by name. Kept across event files on
    /// purpose: a character never wanted in the count stays out of it.</summary>
    public List<string> Excluded { get; set; } = [];

    /// <summary>Whether skillchain damage is counted, credited to whoever closed the chain.</summary>
    public bool Skillchains { get; set; } = true;

    /// <summary>Draw every character but the file's owner as their job. Kept
    /// between runs because a stream stays a stream: having to switch it back
    /// on each time is the one way to forget once and show the party's names.</summary>
    public bool HideNames { get; set; }

    /// <summary>The cumulative chart folds everyone under 5% into one line.</summary>
    public bool GroupSmallLines { get; set; } = true;

    /// <summary>How the Heals table lists the heals: "Spell", as installed
    /// (each character, their heals, the people each reached), "Caster"
    /// (each character, the people they healed, the heals on each) or
    /// "Target" (each person healed, who healed them, with what).</summary>
    public string HealsBy { get; set; } = "Spell";

    /// <summary>The characters' line is opened onto every chip, on as many
    /// lines as they take. As installed it is not: one line of as many chips
    /// as fit, and a chip that says how many do not ("+5"), which opens it.
    /// A file that has the key keeps what it says.</summary>
    public bool CharactersOpen { get; set; }

    /// <summary>A character's row is shaded in their colour to the length of
    /// their share: in the two per-character tables, on their heading in the
    /// Actions and Heals tables. On as installed. Off, the row is plain and a
    /// small bar stands beside the share's figure. A file from before the
    /// key existed has none and gets the installed look.</summary>
    public bool ShadeCharacters { get; set; } = true;

    /// <summary>The rows under a character (an action, a heal) are shaded
    /// to their share, without a colour. Off as installed: the share is a
    /// small bar beside its figure.</summary>
    public bool ShadeActions { get; set; }

    /// <summary>The low accuracy mark's threshold, in percent: Accuracy, WS
    /// Acc, Pet Acc and By target's Acc under it are marked in red, more
    /// strongly the lower they fall. 50 to 100; 90 as installed.</summary>
    public int LowAccuracy { get; set; } = Core.LowMark.Installed;

    /// <summary>The colour of Compare's run A, the baseline, as it is
    /// written ("#3987E5"). Null, as installed: the colour the theme in use
    /// has for it (blue). Once set, the one colour serves both themes. It
    /// belongs to the slot: Swap exchanges the parses, not the colours. A
    /// file from before the key existed has none and gets the installed
    /// colour; so does a value that is not a colour.</summary>
    public string? RunA { get; set; }

    /// <summary>The same for run B, the run compared with it (orange as installed).</summary>
    public string? RunB { get; set; }

    /// <summary>What a floating panel's backdrop is drawn at until its own
    /// slider is moved, 15 to 100 percent. Set on the Settings page.</summary>
    public int PanelOpacity { get; set; } = Core.PanelOpacities.Initial;

    /// <summary>The panels whose own slider has been moved, by panel key.
    /// Their value outranks the default.</summary>
    public Dictionary<string, int> PanelOpacities { get; set; } = [];

    /// <summary>The draw frequency: how many times a second everything the
    /// clock moves (the elapsed time, every DPS and HPS, a cumulative chart's
    /// live edge) is redrawn, 1 to 60. Set on the Settings page.</summary>
    public int DrawFrequency { get; set; } = Core.DrawRate.Initial;

    /// <summary>The panels that were floating when Zerg was last closed, by
    /// panel key. They come back at the next start, each where it was.</summary>
    public List<string> OpenPanels { get; set; } = [];

    /// <summary>The keys that switch the floating panels between click-through
    /// and clickable, from any application. Chosen on the Settings page: a chord
    /// another program already holds cannot be had, and this is the way round it.</summary>
    public string ClickThroughKey { get; set; } = Core.KeyChord.Default;

    /// <summary>The folder the addon's event files are looked for in, chosen
    /// on the Settings page. Null is the addon's own
    /// (<see cref="AppInfo.DefaultEventsDir"/>), so a default that moves in
    /// a later build moves for everyone who never chose.</summary>
    public string? EventsDir { get; set; }

    /// <summary>Window placement by name: "main", and "panel:" plus its key
    /// for each floating panel.</summary>
    public Dictionary<string, Placement> Windows { get; set; } = [];

    /// <summary>
    /// How each section's panes are arranged, where that is not as
    /// installed: an object with a tree of splits per section ("Damage",
    /// "Healing", "Compare"), each split a share of its room and each pane
    /// by its key, with which are folded. Zerg.Core/Layout/SplitTree.Json
    /// has the form and reads it (PaneLayouts.Read, PaneLayouts.Write).
    ///
    /// <para>Null, as installed, and then the key is not written at all: a
    /// file from before arrangements were saved has none, and gets the
    /// installed ones. A section it does not mention is as installed.</para>
    ///
    /// <para>Kept as whatever JSON it is, and made sense of by the view
    /// model. Typed any more exactly, one wrong value under it (a file
    /// edited by hand, or cut short) would make the whole settings file
    /// unreadable, and every other setting would be lost with it.</para>
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Layouts { get; set; }

    /// <summary>The arrangement is locked: no rule can be dragged and no
    /// pane moved, and the headings' grips are not drawn. Remembered,
    /// because an arrangement locked for being settled should stay locked.
    /// Not as installed.</summary>
    public bool LayoutLocked { get; set; }

    /// <summary>
    /// How each table is listed where a player pressed a column heading, by
    /// the table's key ("bars", "actions", "healers", "heals", "targets",
    /// "types", "hits", "casts"): the column and the way, "damage desc"
    /// (<see cref="TableOrder"/>). A table not here is in its own order. A
    /// value that does not read, or names a column the table does not have,
    /// costs that table its order alone.
    /// </summary>
    public Dictionary<string, string> Sorts { get; set; } = [];

    /// <summary>Keys this build doesn't know, written by a newer one, kept
    /// as they were so going back a version and forward again loses nothing.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; set; }

    /// <summary><see cref="Theme"/> as the choice it names; anything else is
    /// dark, as installed. Not saved: computed.</summary>
    [JsonIgnore]
    internal ThemeChoice ThemeChoice => Theme switch
    {
        "Light" => ThemeChoice.Light,
        "System" => ThemeChoice.System,
        _ => ThemeChoice.Dark,
    };

    // ---------------------------------------------------------------- file

    static string FilePath => Path.Combine(AppInfo.DataDir, "settings.json");

    static readonly JsonSerializerOptions Json = LenientJson.Options(new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // The file is for a person to read: "Ctrl+Alt+Z" is written as
        // that, not with its plus signs escaped.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    public static Settings Load()
    {
        string text;
        try
        {
            text = File.ReadAllText(FilePath);
        }
        catch (FileNotFoundException) { return new Settings(); }
        catch (DirectoryNotFoundException) { return new Settings(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"settings: can't read {FilePath} ({e.Message}); using defaults");
            return new Settings();
        }

        bool lost = false;
        var settings = LenientJson.Read<Settings>(text, Json, (key, why) =>
        {
            Log.Write($"settings: {key} could not be read ({why}); it is back to its default");
            lost = true;
        });
        if (settings is null)
        {
            Log.Write($"settings: {FilePath} is not a settings file; using defaults");
            lost = true;
        }
        // Kept for a bug report: the next save overwrites the original.
        if (lost)
        {
            try { File.Copy(FilePath, FilePath + ".bad", overwrite: true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return (settings ?? new Settings()).Normal();
    }

    /// <summary>
    /// Every list and word there, whatever the file said: JSON's <c>null</c>
    /// is read into a list or a string as null, and nothing downstream
    /// expects one. Null entries inside a list go too.
    /// </summary>
    internal Settings Normal()
    {
        var installed = new Settings();
        Theme ??= installed.Theme;
        Section ??= installed.Section;
        ViewMode ??= installed.ViewMode;
        HealsBy ??= installed.HealsBy;
        CompareBy ??= installed.CompareBy;
        CompareMode ??= installed.CompareMode;
        ClickThroughKey ??= installed.ClickThroughKey;
        Excluded = Excluded?.Where(n => n is not null).ToList() ?? [];
        OpenPanels = OpenPanels?.Where(n => n is not null).ToList() ?? [];
        PanelOpacities ??= [];
        Sorts = Sorts?.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value) ?? [];
        Windows = Windows?.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value) ?? [];
        return this;
    }

    /// <summary>Written to a temporary file and moved into place, so a crash or
    /// a full disk mid-write never leaves half a settings file.</summary>
    public void Save()
    {
        var tmp = FilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(AppInfo.DataDir);
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"settings: can't save {FilePath} ({e.Message})");
        }
    }
}
