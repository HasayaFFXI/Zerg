using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zerg.Core;

namespace Zerg;

// Export, and the View section: a paused parse saved to a file another copy
// of Zerg can open, and one opened here to look through, its damage or its
// healing.
//
// Viewing takes nothing away. The session being followed is left whole, and
// goes on being fed by every poll while the parse is on screen: a clock that
// was running keeps running, since it is wall time, and the Damage and
// Healing sections return to it with everything the addon wrote meanwhile
// already read. Only what is counted and drawn changes hands, and only while
// the View section is the one on screen.
public sealed partial class MainViewModel
{
    /// <summary>How long the note about the last Export stays up.</summary>
    static readonly TimeSpan NoteFor = TimeSpan.FromSeconds(8);

    /// <summary>The parses open in the View section, a tab each, in the
    /// order they were opened. Kept while another section is on screen, so
    /// coming back finds them still open.</summary>
    public ObservableCollection<ViewedParse> Parses { get; } = [];

    /// <summary>The parse whose tab was picked last, or null with none open:
    /// the one on screen while the View section is.</summary>
    ViewedParse? viewed;
    DispatcherTimer? noteTimer;

    /// <summary>The View section is on screen with a parse open in it.</summary>
    bool Viewing => IsView && viewed != null;

    /// <summary>
    /// What is counted and drawn: the parse picked in the View section while
    /// that is on screen, otherwise the session. Everything that puts a
    /// figure, a name or a colour on screen reads this, so a saved parse is
    /// drawn by the code that draws the session, panels included, and the
    /// two cannot differ in how they count.
    /// </summary>
    Tracker Shown => Viewing ? viewed!.Tracker : live;

    // ------------------------------------------------------ the View section

    /// <summary>A parse is open in the View section, whichever section is on screen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDamage), nameof(IsHealing), nameof(ShowsParse), nameof(FillsPage), nameof(HideNamesTip))]
    private bool hasParse;

    /// <summary>The parse whose tab is picked: what the View section's band
    /// says. Null with none open.</summary>
    public ViewedParse? Viewed => viewed;

    /// <summary>The side last picked, which a new tab opens on.</summary>
    string viewMode;

    /// <summary>Which side of the picked parse the View section shows:
    /// "Damage" or "Healing". Each tab keeps its own; the last one picked
    /// is remembered for the next tab, and the next start.</summary>
    public string ViewMode
    {
        get => viewed?.Mode ?? viewMode;
        set
        {
            if (value == ViewMode) return;
            viewMode = value;
            if (viewed != null) viewed.Mode = value;
            settings.ViewMode = value;
            settings.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDamage));
            OnPropertyChanged(nameof(IsHealing));
            // The characters listed follow the side on screen, as they
            // follow the section.
            if (Viewing) Recount();
        }
    }

    /// <summary>Why the last file would not open. What was open stays open.</summary>
    [ObservableProperty] private string viewError = "";
    /// <summary>A file is being held over the section's card.</summary>
    [ObservableProperty] private bool viewOver;

    /// <summary>Picks a parse off the disk, and picks where to save one
    /// (given the name to offer). The main window supplies both.</summary>
    public Func<ParseText?>? Picker { get; set; }
    public Func<string, string?>? Saver { get; set; }

    /// <summary>Opens an exported parse in a tab of its own, and shows it.</summary>
    [RelayCommand]
    Task OpenParse() => Read(() => Picker?.Invoke());

    /// <summary>
    /// Import, on the command bar: the way into the View section. It opens a
    /// parse in a tab of its own and shows it. A parse already open is gone
    /// back to by its tab.
    /// </summary>
    [RelayCommand]
    async Task Import()
    {
        if (await Read(() => Picker?.Invoke())) Section = ViewSection;
    }

    /// <summary>A file dropped on the section's card.</summary>
    public void Take(string path) => _ = Read(() => ParseDialog.Read(path));

    /// <summary>A parse Windows handed Zerg: a .zerg file opened from
    /// Explorer, at a start or in the Zerg already running; several files
    /// opened together come one call each. It opens in a tab of its own,
    /// and View is shown whatever was on screen; a file that would not open
    /// is said so there, and stays said while the files opened with it
    /// open after it.</summary>
    public void Open(string path) => _ = OpenAndShow(path);

    async Task OpenAndShow(string path)
    {
        await Read(() => ParseDialog.Read(path), clears: false);
        Section = ViewSection;
    }

    /// <summary>The open asked for last, which the next waits for. A parse
    /// is read off the UI thread, and several asked for together (files
    /// opened from Explorer at once) would otherwise get their tabs in the
    /// order they finished reading, not the order they were asked for, and
    /// a file asked for twice could be read twice.</summary>
    Task opening = Task.CompletedTask;

    /// <summary>False when no file was chosen. A file that would not open
    /// is still an answer: the section's card says why, until a parse the
    /// player chose here opens (<paramref name="clears"/>). A file already
    /// open is not opened twice: its tab is picked. Each waits for the one
    /// asked for before it.</summary>
    async Task<bool> Read(Func<ParseText?> source, bool clears = true)
    {
        var before = opening;
        var done = new TaskCompletionSource();
        opening = done.Task;
        try
        {
            await before;
            return await ReadNow(source, clears);
        }
        finally
        {
            done.SetResult();
        }
    }

    async Task<bool> ReadNow(Func<ParseText?> source, bool clears)
    {
        if (source() is not { } file) return false;
        if (Parses.FirstOrDefault(p => p.IsFile(file.Path)) is { } open)
        {
            if (clears) ViewError = "";
            Pick(open);
            return true;
        }
        // The addon's own file is replayed, not imported. It may be the one
        // being written, which only a shared read can open.
        if (ParseDialog.IsEventFile(file.Path)) return await ReplayNow(file, clears);
        try
        {
            var parse = await Task.Run(() => ParseFile.Import(file.Read()));
            var tab = new ViewedParse(Tracker.Of(parse), file.Name, file.Path, CompareSheet.Describe(parse), parse.File,
                                      viewMode, Pick);
            if (clears) ViewError = "";
            Parses.Add(tab);
            Log.Write($"viewing {file.Name} ({Parses.Count} open)");
            HasParse = true;
            Pick(tab);
        }
        // An event file under another name: told by its content, and replayed.
        catch (ParseImportException e) when (e.Reason == ImportError.EventFile && file.Path != null)
        {
            return await ReplayNow(file, clears);
        }
        // Named, since several may be opened at once.
        catch (ParseImportException e)
        {
            ViewError = file.Name + ": " + e.Message;
            Log.Write($"not opened {file.Name}: {e.Message}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ViewError = "Could not open " + file.Name + ": " + e.Message;
            Log.Write($"not opened {file.Name}: {e.Message}");
        }
        return true;
    }

    /// <summary>
    /// Puts a parse on screen: its tab is picked and the View section shown.
    /// </summary>
    void Pick(ViewedParse tab)
    {
        bool moved = !ReferenceEquals(viewed, tab);
        viewed = tab;
        if (moved) Picked();
        // Into the section is a count; within it, the section does not change.
        if (!IsView) Section = ViewSection;
        else if (moved) Recount();
    }

    /// <summary>Whatever follows the picked parse is told it changed.</summary>
    void Picked()
    {
        OnPropertyChanged(nameof(Viewed));
        OnPropertyChanged(nameof(ViewMode));
        OnPropertyChanged(nameof(IsDamage));
        OnPropertyChanged(nameof(IsHealing));
        OnPropertyChanged(nameof(ShowsParse));
        OnPropertyChanged(nameof(FillsPage));
        OnPropertyChanged(nameof(HideNamesTip));
        MarkTabs();
    }

    /// <summary>Which tab is lit: the picked parse's, while View is on screen.</summary>
    void MarkTabs()
    {
        foreach (var p in Parses) p.Mark(IsView && ReferenceEquals(p, viewed));
    }

    /// <summary>
    /// A tab moved along the bar: dragged a step, or moved by a key. The
    /// last word of a move (<paramref name="done"/>) moves nothing and is
    /// only logged; the order is kept for the run, as the tabs are.
    /// </summary>
    public void MoveParse(int from, int to, bool done)
    {
        if (from < 0 || to < 0 || from >= Parses.Count || to >= Parses.Count || from == to) return;
        if (done)
        {
            Log.Write($"moved the tab of {Parses[to].File} from {from + 1} to {to + 1}");
            return;
        }
        Parses.Move(from, to);
    }

    /// <summary>
    /// Closes a parse's tab: the mark on the tab, or the band's Close, which
    /// closes the one on screen. The tab after it is picked in its place, or
    /// the one before; with none left the View section is empty. The
    /// session was never touched.
    /// </summary>
    [RelayCommand]
    void CloseParse(ViewedParse? tab)
    {
        tab ??= viewed;
        if (tab == null) return;
        int at = Parses.IndexOf(tab);
        if (at < 0) return;
        Parses.RemoveAt(at);
        Log.Write($"closed {tab.File} ({Parses.Count} open)");
        // A replay still being read stops being read.
        tab.Range?.Stop();
        if (!ReferenceEquals(tab, viewed))
        {
            looking.Remove(tab.Tracker);
            return;
        }

        bool shown = Viewing;
        viewed = Parses.Count > 0 ? Parses[Math.Min(at, Parses.Count - 1)] : null;
        ViewError = "";
        HasParse = Parses.Count > 0;
        Picked();
        // The panels were showing it too: on to the next, or back to the session.
        if (shown) Recount();
        // What it was being looked into with, kept as it went.
        looking.Remove(tab.Tracker);
    }

    // ---------------------------------------------------------------- export

    /// <summary>The parse on screen is paused, so it can be saved.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private bool canExport;

    [ObservableProperty] private string exportTip = "";

    /// <summary>What the last Export did. Clears itself.</summary>
    [ObservableProperty] private string parseNote = "";
    /// <summary>The note is about something that went wrong.</summary>
    [ObservableProperty] private bool parseNoteBad;

    void Note(string text, bool bad = false)
    {
        noteTimer ??= new DispatcherTimer(NoteFor, DispatcherPriority.Background, (_, _) => Note(""),
                                          Dispatcher.CurrentDispatcher);
        noteTimer.Stop();
        ParseNote = text;
        ParseNoteBad = bad;
        if (text.Length > 0) noteTimer.Start();
    }

    /// <summary>Export follows the clock of whatever is on screen.</summary>
    void DescribeParse()
    {
        var s = Shown.Session;
        CanExport = Shown.CanExport;
        ExportTip = Shown.Replayed
            ? CanExport ? "Save the range picked to a parse file another copy of Zerg can open with Import or in Compare"
              : viewed?.Range is { Reading: true } ? "Still reading the event file"
              : "Nothing to export — no party damage falls in the range picked"
            : CanExport ? "Save this parse to a file another copy of Zerg can open with Import or in Compare"
            : s.StartedAt != null ? "Pause first — a parse is exported once its clock has stopped"
            : "Nothing to export yet — Start, then Pause, to export a pull";
    }

    /// <summary>
    /// Saves the paused parse on screen. The text is made first, before the
    /// dialog opens: a snapshot, so a poll, or a button on a floating panel,
    /// cannot change what is written while the dialog is up.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    void Export()
    {
        var shown = Shown;
        if (shown.Export() is not { } text || shown.Session.StartedAt is not double began) return;
        try
        {
            if (Saver?.Invoke(ParseFile.SuggestedName(shown.Reader.Roster.Owner, began)) is not { } path) return;
            if (!ParseFile.CanSaveAs(path))
            {
                Note("Not exported: .jsonl is the addon's own file type, and the newest one in its folder " +
                     "is taken for the live events. Save the parse as " + ParseFile.Extension + ".", bad: true);
                return;
            }
            File.WriteAllText(path, text);
            Log.Write($"exported {path}");
            Note("Exported " + Path.GetFileName(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Note("Export failed: " + e.Message, bad: true);
        }
    }
}
