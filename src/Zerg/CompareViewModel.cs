using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zerg.Core;
using Zerg.Core.Layout;

namespace Zerg;

/// <summary>Where a side's run comes from.</summary>
public enum RunSource { None, File, Tab, Live }

/// <summary>A line of a side's picker: a source to take, with what little
/// it says of itself (a length, "replay", a date, an extension).</summary>
public sealed class SourceOption(string label, string note, bool enabled, bool chosen, string? mark, Action use)
{
    public string Label { get; } = label;
    public string Note { get; } = note;
    public bool Enabled { get; } = enabled;
    public bool Chosen { get; } = chosen;
    /// <summary>"A" or "B" where the other side has it already.</summary>
    public string? Mark { get; } = mark;
    public IRelayCommand Command { get; } = new RelayCommand(use, () => enabled);
    public override string ToString() => Label;
}

/// <summary>
/// One side of a comparison: A, the baseline, or B, the run set against it.
/// Each is a name the player gives it, a source (a saved parse, a View tab,
/// a replay's range, or the live session) and a scope (the whole party, or
/// one character). The name stays with the side when its source changes;
/// nothing of a side is saved.
/// </summary>
public sealed partial class RunSide : ObservableObject
{
    public const string DefaultA = "Previous Run", DefaultB = "This Run";

    readonly CompareViewModel owner;

    public RunSide(CompareViewModel owner, bool isA)
    {
        this.owner = owner;
        IsA = isA;
        name = isA ? DefaultA : DefaultB;
    }

    public CompareViewModel Owner => owner;
    public bool IsA { get; }
    public string Letter => IsA ? "A" : "B";

    /// <summary>What the player calls the run, wherever a run is meant.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Called))]
    private string name;

    partial void OnNameChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        owner.Renamed();
    }

    /// <summary>The name as drawn: never blank.</summary>
    public string Called => string.IsNullOrWhiteSpace(Name) ? (IsA ? DefaultA : DefaultB) : Name.Trim();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(HasRun))]
    private ImportedParse? run;

    public bool IsEmpty => Run == null;
    public bool HasRun => Run != null;

    /// <summary>The file the run came from, as the chip shows it.</summary>
    [ObservableProperty] private string file = "";
    [ObservableProperty] private string length = "";
    [ObservableProperty] private bool isLive;
    [ObservableProperty] private string error = "";
    /// <summary>A file is being held over the chip.</summary>
    [ObservableProperty] private bool isOver;
    /// <summary>The picker is open.</summary>
    [ObservableProperty] private bool picking;
    /// <summary>The chip's name is being edited in place.</summary>
    [ObservableProperty] private bool renaming;
    /// <summary>One character of the run as the whole of it; null for the party.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Whole), nameof(One), nameof(ScopeText))]
    private string? scope;

    /// <summary>The one character the side is scoped to, as the chip shows it; "" for the party.</summary>
    public string ScopeText => Scope == null ? "" : owner.NameOf(Scope);

    /// <summary>Scopes the side to one character (the picker's list of them).</summary>
    [RelayCommand]
    void ScopeTo(string who) => Scope = who;

    public bool Whole { get => Scope == null; set { if (value) Scope = null; } }
    public bool One
    {
        get => Scope != null;
        set { if (value && Scope == null) Scope = owner.FirstOf(this); }
    }

    partial void OnScopeChanged(string? value) => owner.Rescoped(this);

    internal RunSource Source { get; set; }
    internal ViewedParse? Tab { get; set; }
    internal string? Path { get; set; }

    /// <summary>The lines of the picker, made as it opens.</summary>
    public ObservableCollection<SourceOption> Options { get; } = [];
    /// <summary>The run's characters, for One character.</summary>
    public ObservableCollection<string> Characters { get; } = [];

    [RelayCommand]
    void Pick()
    {
        owner.FillPicker(this);
        Picking = true;
    }

    [RelayCommand]
    void Rename() => Renaming = true;

    [RelayCommand]
    void Done()
    {
        Renaming = false;
        if (string.IsNullOrWhiteSpace(Name)) Name = IsA ? DefaultA : DefaultB;
    }

    /// <summary>A file dropped on the chip.</summary>
    public void Take(string path) => owner.UseFile(this, path);

    internal void Show(ImportedParse parse, string file)
    {
        Run = parse;
        File = file;
        Length = Format.Stopwatch(parse.Session.Elapsed(parse.Session.PausedAt), tenths: true);
        Error = "";
        Characters.Clear();
        foreach (var n in parse.Source.Roster.Kinds.Where(k => k.Value == "player").Select(k => k.Key).Order(StringComparer.Ordinal))
            Characters.Add(n);
    }

    internal void Empty()
    {
        Run = null;
        File = Length = Error = "";
        IsLive = false;
        Source = RunSource.None;
        Tab = null;
        Path = null;
        Characters.Clear();
        Scope = null;
    }

    /// <summary>Everything the side holds but its name, to hand to the other.</summary>
    internal (ImportedParse? Run, string File, string Length, bool Live, RunSource Source, ViewedParse? Tab, string? Path, string? Scope, string[] Who) Held =>
        (Run, File, Length, IsLive, Source, Tab, Path, Scope, [.. Characters]);

    internal void Hold((ImportedParse? Run, string File, string Length, bool Live, RunSource Source, ViewedParse? Tab, string? Path, string? Scope, string[] Who) h)
    {
        (Run, File, Length, IsLive, Source, Tab, Path) = (h.Run, h.File, h.Length, h.Live, h.Source, h.Tab, h.Path);
        Characters.Clear();
        foreach (var n in h.Who) Characters.Add(n);
        Scope = h.Scope;
        OnPropertyChanged(nameof(ScopeText));
        OnPropertyChanged(nameof(Whole));
        OnPropertyChanged(nameof(One));
    }

    public override string ToString() => Called;
}

/// <summary>
/// The Compare section: two runs side by side, every figure of the current
/// run (B) against A's whole run. Everything printed comes from a
/// <see cref="CompareSheet"/>; this holds the sides and the player's
/// choices, and hands the sheet's lines to the rows the cards draw.
/// </summary>
public sealed partial class CompareViewModel : ObservableObject
{
    public const string Damage = "Damage", Healing = "Healing";
    public const string ByCharacters = "Characters", ByTargets = "Targets", ByJobs = "Jobs";
    public const string PaceBoth = "Both", PaceGap = "Gap";
    public const string HealsBySpell = "Spell", HealsByCaster = "Caster", HealsByTarget = "Target";
    public const string FiltersByTargets = "Targets", FiltersByTypes = "Types";

    readonly Settings settings;
    readonly MainViewModel host;
    readonly DispatcherTimer beat;
    CompareSheet? sheet, jobs;
    IReadOnlyList<CompareGroup>? targetGroups;
    int version;
    bool stale = true;
    /// <summary>A's measurement and B's, with what they were measured under.</summary>
    (ImportedParse? Run, string Key, Measurement M) lastA, lastB;

    public RunSide A { get; }
    public RunSide B { get; }

    /// <summary>Picks a parse off the disk. The main window supplies it.</summary>
    public Func<ParseText?>? Picker { get; set; }

    /// <summary>Compare's own isolation, from the command bar while the
    /// section is on screen: it cuts both runs, by name, and never touches
    /// the live sections' filters or clock.</summary>
    public PickFilter CharacterFilter { get; }
    public PickFilter TargetFilter { get; } = PickFilter.OfTargets(paired: true);
    public PickFilter TypeFilter { get; } = PickFilter.OfTypes(paired: true);

    public TableColumns BarsColumns { get; }
    public TableColumns HealersColumns { get; }
    public TableColumns ActionsColumns { get; }
    public TableColumns HealsColumns { get; }
    public TableColumns FightsColumns { get; }
    public TableColumns TargetColumns { get; }
    public TableColumns TypeColumns { get; }
    public ColumnSort BarsSort { get; }
    public ColumnSort HealersSort { get; }
    public ColumnSort ActionsSort { get; }
    public ColumnSort HealsSort { get; }
    public ColumnSort FightsSort { get; }
    public ColumnSort TargetSort { get; }
    public ColumnSort TypeSort { get; }

    public CompareViewModel(Settings settings, MainViewModel host)
    {
        (this.settings, this.host) = (settings, host);
        A = new RunSide(this, isA: true);
        B = new RunSide(this, isA: false);
        CharacterFilter = PickFilter.OfCharacters(n => sheet?.Draw(n) ?? n);
        mode = settings.CompareMode == Healing ? Healing : Damage;
        overviewBy = settings.CompareOverviewBy is ByTargets or ByJobs ? settings.CompareOverviewBy : ByCharacters;
        showA = settings.CompareShowA;
        paceMode = settings.ComparePace == PaceGap ? PaceGap : PaceBoth;

        BarsColumns = new TableColumns(Tables.CompareBars, settings);
        HealersColumns = new TableColumns(Tables.CompareHealers, settings);
        ActionsColumns = new TableColumns(Tables.CompareActions, settings);
        HealsColumns = new TableColumns(Tables.CompareHeals, settings);
        FightsColumns = new TableColumns(Tables.CompareFights, settings);
        TargetColumns = new TableColumns(Tables.CompareTargets, settings);
        TypeColumns = new TableColumns(Tables.CompareTypes, settings);
        BarsSort = new ColumnSort("cbars", settings);
        HealersSort = new ColumnSort("chbars", settings);
        ActionsSort = new ColumnSort("cactions", settings);
        HealsSort = new ColumnSort("cheals", settings);
        FightsSort = new ColumnSort("cfights", settings);
        TargetSort = new ColumnSort("cfilterTargets", settings);
        TypeSort = new ColumnSort("cfilterTypes", settings);
        foreach (var sort in new[] { BarsSort, HealersSort, ActionsSort, HealsSort, FightsSort, TargetSort, TypeSort })
            sort.Changed += Show;

        foreach (var (filter, word) in new[] { (CharacterFilter, "characters"), (TargetFilter, "targets"), (TypeFilter, "types") })
            filter.Changed += () =>
            {
                Log.Write($"compare {word} " + (filter.IsOn ? filter.Names : "all"));
                Redraw();
            };

        // A live side is counted again once a second.
        beat = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        beat.Tick += (_, _) => Beat();
    }

    // -------------------------------------------------------------- switches

    /// <summary>The section is on screen. Off it nothing is drawn, but a
    /// live side goes on being counted while its panel floats.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPanes), nameof(ShowsEmpty))]
    private bool active;

    partial void OnActiveChanged(bool value)
    {
        if (value) FillEmpty();
        if (value && stale) Redraw();
    }

    /// <summary>The panes are on screen: the section is, and B holds a run
    /// (with A empty, B's own figures and no changes).</summary>
    public bool ShowsPanes => Active && B.HasRun;

    /// <summary>B is empty: in the panes' place, B's name and the ways to fill it.</summary>
    public bool ShowsEmpty => Active && !B.HasRun;

    /// <summary>The empty B's lines: the live session, and the newest View tab.</summary>
    public ObservableCollection<SourceOption> EmptyOptions { get; } = [];

    void FillEmpty()
    {
        EmptyOptions.Clear();
        if (CanUseCurrent) EmptyOptions.Add(new SourceOption("Live session", host.ClockText, true, false, null, () => UseLive(B)));
        if (host.Parses.LastOrDefault() is { } tab && !(A.Source == RunSource.Tab && A.Tab == tab))
            EmptyOptions.Add(new SourceOption(tab.Name, tab.IsReplay ? "replay"
                                                  : Format.Stopwatch(tab.Tracker.Session.Elapsed(tab.Tracker.Session.PausedAt), true),
                                              true, false, null, () => UseTab(B, tab)));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDamage), nameof(IsHealing), nameof(BarsTitle), nameof(ActionsTitle),
                              nameof(PaceTitle), nameof(RateWord), nameof(UsesWord), nameof(ActionWord))]
    private string mode;

    public bool IsHealing => Mode == Healing;
    public bool IsDamage => !IsHealing;
    public string BarsTitle => IsHealing ? "Healing by character" : "Overview";
    public string ActionsTitle => IsHealing ? "Heals" : "Actions";
    public string PaceTitle => IsHealing ? "Cumulative healing" : "Cumulative damage";
    public string RateWord => IsHealing ? "HPS" : "DPS";
    public string UsesWord => IsHealing ? "Casts" : "Uses";
    public string ActionWord => IsHealing ? "Heal" : "Action";

    partial void OnModeChanged(string value)
    {
        settings.CompareMode = value;
        settings.Save();
        CloseDrill();
        Show();
    }

    /// <summary>Overview's tab: By character, By target or By job.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OverviewByCharacters), nameof(OverviewByTargets), nameof(OverviewByJobs))]
    private string overviewBy;

    public bool OverviewByCharacters { get => OverviewBy == ByCharacters; set { if (value) OverviewBy = ByCharacters; } }
    public bool OverviewByTargets { get => OverviewBy == ByTargets; set { if (value) OverviewBy = ByTargets; } }
    public bool OverviewByJobs { get => OverviewBy == ByJobs; set { if (value) OverviewBy = ByJobs; } }

    partial void OnOverviewByChanged(string value)
    {
        settings.CompareOverviewBy = value;
        settings.Save();
        if (value == ByJobs && jobs == null) Redraw();
        else Show();
    }

    /// <summary>Cells carry A's figure on a line under B's.</summary>
    [ObservableProperty] private bool showA;

    partial void OnShowAChanged(bool value)
    {
        settings.CompareShowA = value;
        settings.Save();
    }

    public string ShowAWords => "Show " + A.Called + "'s figures";

    /// <summary>The cumulative chart: both lines, or the gap between them.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PaceBothOn), nameof(PaceGapOn))]
    private string paceMode;

    public bool PaceBothOn { get => PaceMode == PaceBoth; set { if (value) PaceMode = PaceBoth; } }
    public bool PaceGapOn { get => PaceMode == PaceGap; set { if (value) PaceMode = PaceGap; } }

    partial void OnPaceModeChanged(string value)
    {
        settings.ComparePace = value;
        settings.Save();
        if (sheet != null) DrawPace(sheet);
    }

    /// <summary>The Heals pane's tab. Not saved.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HealsBySpellOn), nameof(HealsByCasterOn), nameof(HealsByTargetOn))]
    private string healsBy = HealsByCaster;

    public bool HealsBySpellOn { get => HealsBy == HealsBySpell; set { if (value) HealsBy = HealsBySpell; } }
    public bool HealsByCasterOn { get => HealsBy == HealsByCaster; set { if (value) HealsBy = HealsByCaster; } }
    public bool HealsByTargetOn { get => HealsBy == HealsByTarget; set { if (value) HealsBy = HealsByTarget; } }

    partial void OnHealsByChanged(string value) => Show();

    /// <summary>The Filters pane's tab. Not saved.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FiltersByTargetsOn), nameof(FiltersByTypesOn))]
    private string filtersBy = FiltersByTargets;

    public bool FiltersByTargetsOn { get => FiltersBy == FiltersByTargets; set { if (value) FiltersBy = FiltersByTargets; } }
    public bool FiltersByTypesOn { get => FiltersBy == FiltersByTypes; set { if (value) FiltersBy = FiltersByTypes; } }

    /// <summary>The live sections' own setting, not a second one.</summary>
    public bool Skillchains => host.Skillchains;

    /// <summary>Include Skillchains, Hide names or the theme changed.</summary>
    internal void SharedChanged()
    {
        OnPropertyChanged(nameof(Skillchains));
        Redraw();
    }

    public bool ShadeCharacters => host.ShadeCharacters;

    internal void ShadingChanged() => OnPropertyChanged(nameof(ShadeCharacters));

    public SplitNode Layout => host.CompareLayout;

    internal void LayoutChanged() => OnPropertyChanged(nameof(Layout));

    /// <summary>A run's colour changed: the chart is handed it again.</summary>
    internal void Recolour()
    {
        if (!Active) stale = true;
        else if (sheet is { } s) DrawPace(s);
    }

    // ----------------------------------------------------------------- sides

    [ObservableProperty] private bool hasAny;

    /// <summary>Both sides hold a run.</summary>
    [ObservableProperty] private bool ready;

    /// <summary>There is a session with a clock to take.</summary>
    public bool CanUseCurrent { get; private set; }

    internal void SessionChanged(bool started)
    {
        if (CanUseCurrent == started) return;
        CanUseCurrent = started;
        OnPropertyChanged(nameof(CanUseCurrent));
        if (Active) FillEmpty();
    }

    /// <summary>A side was renamed: every place a name is drawn follows.</summary>
    internal void Renamed()
    {
        OnPropertyChanged(nameof(ShowAWords));
        OnPropertyChanged(nameof(Status));
        if (sheet is { } s && Active)
        {
            DrawPace(s);
            DrawDrill(s);
            DrawLive(s);
        }
        NamesChanged?.Invoke();
    }

    /// <summary>Raised when a side's name changes, for whatever words a view makes of them.</summary>
    public event Action? NamesChanged;

    internal void Rescoped(RunSide side)
    {
        Log.Write($"compare {side.Letter} scope " + (side.Scope ?? "party"));
        Redraw();
    }

    /// <summary>The first character a side may be scoped to.</summary>
    internal string? FirstOf(RunSide side) => side.Characters.FirstOrDefault();

    /// <summary>The picker's lines: the live session, the View tabs, the
    /// parses opened lately, and the ways to another.</summary>
    internal void FillPicker(RunSide side)
    {
        var other = side.IsA ? B : A;
        side.Options.Clear();
        string? Mark(RunSource s, ViewedParse? tab = null, string? path = null) =>
            other.Source == s && (s == RunSource.Live || other.Tab == tab && tab != null || path != null && other.Path == path)
                ? other.Letter : null;

        var liveMark = Mark(RunSource.Live);
        side.Options.Add(new SourceOption("Live session", CanUseCurrent ? "LIVE " + host.ClockText : "",
                                          CanUseCurrent && liveMark == null, side.Source == RunSource.Live, liveMark,
                                          () => UseLive(side)));
        foreach (var tab in host.Parses)
        {
            var mark = Mark(RunSource.Tab, tab);
            side.Options.Add(new SourceOption(tab.Name, tab.IsReplay ? "replay" : Format.Stopwatch(tab.Tracker.Session.Elapsed(tab.Tracker.Session.PausedAt), true),
                                              mark == null, side.Tab == tab, mark, () => UseTab(side, tab)));
        }
        foreach (var path in settings.CompareRecent)
        {
            if (host.Parses.Any(t => t.IsFile(path))) continue;
            var mark = Mark(RunSource.File, path: path);
            string when = "";
            try { when = File.GetLastWriteTime(path).ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
            side.Options.Add(new SourceOption(System.IO.Path.GetFileName(path), when, mark == null && File.Exists(path),
                                              side.Path == path, mark, () => UseFile(side, path)));
        }
        side.Options.Add(new SourceOption("Open a parse…", ".zerg", true, false, null, () => OpenInto(side)));
        side.Options.Add(new SourceOption("Replay an event file…", ".jsonl", host.EventPicker != null, false, null,
                                          () => ReplayInto(side)));
    }

    void Picked(RunSide side) => side.Picking = false;

    internal void UseLive(RunSide side)
    {
        Picked(side);
        if (host.Current() is not { } text) return;
        Read(side, text, RunSource.Live, null, null);
    }

    internal void UseTab(RunSide side, ViewedParse tab)
    {
        Picked(side);
        var now = Session.Now();
        if (CompareSheet.Snapshot(tab.Tracker.Reader, tab.Tracker.Session, tab.File, now) is not { } text)
        {
            side.Error = "Not read yet";
            return;
        }
        Read(side, new ParseText(tab.Name, Text: text), RunSource.Tab, tab, tab.Path);
    }

    internal void UseFile(RunSide side, string path)
    {
        Picked(side);
        if (ParseDialog.IsEventFile(path))
        {
            ReplayPath(side, path);
            return;
        }
        Read(side, ParseDialog.Read(path), RunSource.File, null, path);
    }

    void OpenInto(RunSide side)
    {
        Picked(side);
        if (Picker?.Invoke() is not { } file) return;
        Read(side, file, RunSource.File, null, file.Path);
    }

    void ReplayInto(RunSide side)
    {
        Picked(side);
        if (host.EventPicker?.Invoke(host.EventsFolder) is { } path) ReplayPath(side, path);
    }

    /// <summary>An event file, replayed in a View tab of its own; the side
    /// takes it once it has been read.</summary>
    async void ReplayPath(RunSide side, string path)
    {
        if (await host.ReplayQuietly(path) is { } tab) UseTab(side, tab);
    }

    /// <summary>A run into a side, read off the UI thread; whatever goes
    /// wrong is the side's to say, and leaves what it held.</summary>
    async void Read(RunSide side, ParseText file, RunSource source, ViewedParse? tab, string? path)
    {
        try
        {
            var parse = await Task.Run(() => ParseFile.Import(file.Read()));
            side.Show(parse, source == RunSource.Live ? "Live session" : ParseDialog.Title(file.Name));
            (side.Source, side.Tab, side.Path, side.IsLive) = (source, tab, path, source == RunSource.Live);
            side.Scope = null;
            if (source == RunSource.File && path != null) Remember(path);
            Log.Write($"compare {side.Letter} ← {file.Name}");
            Forget();
        }
        catch (ParseImportException e)
        {
            side.Error = e.Message;
            Log.Write($"not compared {file.Name}: {e.Reason}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            side.Error = "Could not open: " + e.Message;
        }
        Live();
        Redraw();
    }

    void Remember(string path)
    {
        settings.CompareRecent = [path, .. settings.CompareRecent.Where(p => !string.Equals(p, path, StringComparison.OrdinalIgnoreCase)).Take(4)];
        settings.Save();
    }

    [RelayCommand]
    void ClearSide(RunSide side)
    {
        side.Empty();
        Forget();
        Live();
        Redraw();
    }

    /// <summary>The sides change places, names with their runs.</summary>
    [RelayCommand]
    void Swap()
    {
        var (na, nb) = (A.Name, B.Name);
        var held = A.Held;
        A.Hold(B.Held);
        B.Hold(held);
        (A.Name, B.Name) = (nb, na);
        // A match is of A's names to B's: with the runs swapped, it is the other way.
        matches = matches.ToDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal);
        for (int i = 0; i < pairs.Count; i++) pairs[i] = pairs[i].Swapped;
        for (int i = 0; i < healPairs.Count; i++) healPairs[i] = healPairs[i].Swapped;
        if (drill is { } d) drill = d.Swapped;
        Log.Write("compare swapped");
        lastA = lastB = default;
        Redraw();
    }

    /// <summary>The rows are about to mean other things: matches and pairs go.</summary>
    void Forget()
    {
        matches.Clear();
        pairs.Clear();
        healPairs.Clear();
        drill = null;
        lastA = lastB = default;
        TargetFilter.Reset();
        TypeFilter.Reset();
        CharacterFilter.Reset();
    }

    // ----------------------------------------------------------- live sides

    /// <summary>A live side is counted again each second; the panel shows while one is.</summary>
    void Live()
    {
        bool live = A.IsLive || B.IsLive;
        if (live && !beat.IsEnabled) beat.Start();
        if (!live && beat.IsEnabled) beat.Stop();
        IsLive = live;
        var panel = host.Panels[PanelSet.CompareLive];
        if (live && !panel.IsOut) panel.PopOutCommand.Execute(null);
        if (!live && panel.IsOut) panel.DockCommand.Execute(null);
    }

    [ObservableProperty] private bool isLive;

    bool beating;

    async void Beat()
    {
        if (beating) return;
        if (host.Current() is not { } text) return;
        beating = true;
        try
        {
            var parse = await Task.Run(() => ParseFile.Import(text.Read()));
            foreach (var side in new[] { A, B })
                if (side.IsLive)
                {
                    side.Run = parse;
                    side.Length = Format.Stopwatch(parse.Session.Elapsed(parse.Session.PausedAt), tenths: true);
                }
            if (A.IsLive) lastA = default;
            if (B.IsLive) lastB = default;
            Redraw();
        }
        catch (Exception e) when (e is ParseImportException or IOException)
        {
            Log.Write("compare live: " + e.Message);
        }
        finally
        {
            beating = false;
        }
    }

    // ----------------------------------------------------- matches and pairs

    /// <summary>A's characters matched by hand with B's: A's name → B's.</summary>
    Dictionary<string, string> matches = new(StringComparer.Ordinal);
    readonly List<CrossPair> pairs = [], healPairs = [];
    CrossPair? drill;

    /// <summary>The other run's characters a line may be matched with.</summary>
    public IReadOnlyList<string> MatchesFor(CompareRow row) =>
        sheet == null || row.Kind != CompareRowKind.Line ? []
        : row.Tag == "A only" ? sheet.OnlyB
        : row.Tag == "B only" ? sheet.OnlyA
        : [];

    /// <summary>Matches a line in one run only with another in the other.</summary>
    public void Match(CompareRow row, string with)
    {
        if (sheet == null) return;
        // A line in A only is keyed by A's name; one in B only by B's.
        string a = row.Tag == "A only" ? sheet.A.Original.GetValueOrDefault(row.Key, row.Key) : with;
        string b = row.Tag == "A only" ? with : row.Key;
        matches[a] = b;
        Log.Write($"compare match {a} → {b}");
        lastA = default;
        Redraw();
    }

    public bool IsMatched(CompareRow row) => matches.ContainsValue(row.Key);

    public void Unmatch(CompareRow row)
    {
        foreach (var k in matches.Where(p => p.Value == row.Key).Select(p => p.Key).ToList()) matches.Remove(k);
        lastA = default;
        Redraw();
    }

    /// <summary>A pair the player made, pinned atop Actions.</summary>
    public void AddPair(CrossPair p)
    {
        var list = IsHealing ? healPairs : pairs;
        if (p.KeyA == p.KeyB && p.ActionA == p.ActionB) return;
        if (!list.Contains(p)) list.Insert(0, p);
        Show();
    }

    void RemovePair(CrossPair p)
    {
        pairs.Remove(p);
        healPairs.Remove(p);
        Show();
    }

    // --------------------------------------------------------------- drawing

    [ObservableProperty] private IReadOnlyList<BandItem> band = [];

    /// <summary>The status line's words while the section is on screen.</summary>
    public string Status =>
        (A.HasRun ? A.Called + " · " + A.File : "") + (A.HasRun && B.HasRun ? "   " : "") + (B.HasRun ? B.Called + " · " + B.File : "");

    /// <summary>What every count of the two runs is measured under.</summary>
    CompareOptions Options(bool byJob) => new()
    {
        ByJob = byJob,
        Skillchains = host.Skillchains,
        HideNames = host.HideNames,
        Targets = TargetFilter.Only,
        Types = TypeFilter.Only,
        Characters = CharacterFilter.Only,
        Matches = matches.Count > 0 ? new Dictionary<string, string>(matches, StringComparer.Ordinal) : null,
        ScopeA = A.Scope,
        ScopeB = B.Scope,
    };

    static string KeyOf(CompareOptions o) =>
        string.Join("|", o.ByJob, o.Skillchains, string.Join(",", o.Targets ?? new HashSet<string>()),
                    string.Join(",", o.Types ?? new HashSet<string>()), string.Join(",", o.Characters ?? new HashSet<string>()),
                    string.Join(",", (o.Matches ?? new Dictionary<string, string>()).Select(p => p.Key + ">" + p.Value)),
                    o.ScopeA, o.ScopeB);

    /// <summary>Measures both runs again (off the UI thread) and draws them.</summary>
    internal async void Redraw()
    {
        HasAny = A.HasRun || B.HasRun;
        Ready = A.HasRun && B.HasRun;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(ShowsPanes));
        OnPropertyChanged(nameof(ShowsEmpty));
        if (Active && !B.HasRun) FillEmpty();
        if (!Active && !IsLive)
        {
            stale = true;
            return;
        }
        stale = false;
        if (!HasAny)
        {
            sheet = jobs = null;
            Show();
            return;
        }
        int asked = ++version;
        var o = Options(false);
        var (ra, rb) = (A.Run, B.Run);
        bool wantJobs = OverviewBy == ByJobs;
        var (la, lb) = (lastA, lastB);
        string key = KeyOf(o);
        var made = await Task.Run(() =>
        {
            var ma = la.Run == ra && la.Key == key && la.M != null ? la.M : CompareSheet.MeasureA(ra, o);
            var mb = lb.Run == rb && lb.Key == key && lb.M != null ? lb.M : CompareSheet.MeasureB(rb, o);
            var s = CompareSheet.Of(ma, mb, o);
            var j = wantJobs ? CompareSheet.Of(ra, rb, o with { ByJob = true }) : null;
            return (s, j, ma, mb);
        });
        if (asked != version) return;
        (lastA, lastB) = ((ra, key, made.ma), (rb, key, made.mb));
        sheet = made.s;
        jobs = made.j;
        targetGroups = null;
        Show();
    }

    /// <summary>Draws everything from the runs as last measured.</summary>
    void Show()
    {
        if (sheet is not { } s)
        {
            Band = [];
            Bars.Clear(); Healers.Clear(); ActionRows.Clear(); HealRows.Clear(); TallyRows.Clear();
            TargetRows.Clear(); TypeRows.Clear(); DeltaRows.Clear(); LiveRows.Clear();
            Pace = null; Gap = null;
            return;
        }
        Band = (IsHealing ? s.HealBand : s.DamageBand).Select(b => new BandItem(b.Key, b.Label, b.Cell)).ToList();
        TargetFilter.Draw(s.TargetList, s.A.Targets.Values.Sum(), s.B.Targets.Values.Sum());
        TypeFilter.Draw(s.TypeList, s.A.Kinds.Values.Sum(), s.B.Kinds.Values.Sum());
        CharacterFilter.Draw(s.Characters.Select(c => new PairTotal(c.Key, c["damage"].ValueA ?? 0, c["damage"].ValueB ?? 0)).ToList(),
                             s.A.Total, s.B.Total);
        if (drill is { } d && !Has(s, d)) drill = null;
        DrawOverview(s);
        DrawActions(s);
        DrawDeltas(s);
        DrawPace(s);
        DrawTally(s);
        DrawFilters(s);
        DrawDrill(s);
        DrawLive(s);
    }

    /// <summary>Either end of a drill-down is still something one of the runs did.</summary>
    bool Has(CompareSheet s, CrossPair p) =>
        s.ActionsIn(false, p.KeyA, IsHealing).Any(x => x.Action == p.ActionA)
        || s.ActionsIn(true, p.KeyB, IsHealing).Any(x => x.Action == p.ActionB);
}
