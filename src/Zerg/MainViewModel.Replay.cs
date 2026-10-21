using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using Zerg.Core;

namespace Zerg;

/// <summary>One of the newest event files, as the Replay menu lists it.</summary>
/// <param name="Followed">The file the session is following now.</param>
public sealed record EventFileEntry(string Path, string Owner, string When, string Length, string Size, bool Followed);

// Replay: an addon event file read whole into a View tab of its own, cropped
// to the range wanted, and exported as a parse. The tab is a parse's in every
// other way: its tracker is read only, it is counted and drawn by the code
// that draws the session, and the session goes on underneath.
public sealed partial class MainViewModel
{
    /// <summary>How many event files the Replay menu lists.</summary>
    const int RecentFiles = 8;

    /// <summary>Picks an event file off the disk, starting in the folder
    /// given. The main window supplies it.</summary>
    public Func<string, string?>? EventPicker { get; set; }

    /// <summary>The newest event files in the folder being followed, for the
    /// Replay menu: who, when, how long and how big. Read when the menu
    /// opens, from each file's ends alone.</summary>
    public IReadOnlyList<EventFileEntry> RecentEventFiles()
    {
        var list = new List<EventFileEntry>();
        foreach (var f in EventFiles.Recent(feed.Directory, RecentFiles))
        {
            var (first, last) = EventFiles.Times(f.FullName);
            string when = "", length = "";
            if (first is double a && last is double b && Format.TryLocal(a * 1000, out var from)
                && Format.TryLocal(b * 1000, out var to))
            {
                when = from.ToString("MMM d, HH':'mm", CultureInfo.InvariantCulture) + " – " +
                       to.ToString("HH':'mm", CultureInfo.InvariantCulture);
                length = Format.Elapsed((b - a + 1) * 1000);
            }
            bool followed = string.Equals(f.Name, live.File, StringComparison.OrdinalIgnoreCase);
            list.Add(new EventFileEntry(f.FullName, EventReader.OwnerOf(f.Name) ?? f.Name, when, length, Size(f.Length),
                                        followed));
        }
        return list;
    }

    static string Size(long bytes) =>
        bytes == 0 ? "empty"
        : bytes >= 1024 * 1024 ? Format.Num(bytes / (1024.0 * 1024), 1) + " MB"
        : Format.Int(Math.Max(1, Math.Ceiling(bytes / 1024.0))) + " KB";

    /// <summary>Replays an event file from the Replay menu, in a tab of its own.</summary>
    [RelayCommand]
    async Task ReplayFile(string? path)
    {
        if (path != null && await Read(() => ParseDialog.Read(path))) Section = ViewSection;
    }

    /// <summary>Picks any event file to replay.</summary>
    [RelayCommand]
    async Task BrowseReplay()
    {
        if (await Read(() => EventPicker?.Invoke(EventsFolder) is { } path ? ParseDialog.Read(path) : null))
            Section = ViewSection;
    }

    /// <summary>
    /// Reads an event file into a tab of its own. The tab opens at once and
    /// shows how far the read has got; the file is read off the UI thread,
    /// as the addon may still be writing it, and the cards fill in when it
    /// is done, the range all of it. A file with no party row in it is
    /// refused, the tab closed: there is nothing to start a clock.
    /// </summary>
    async Task<bool> ReplayNow(ParseText file, bool clears)
    {
        if (file.Path is not { } path) return false;
        ViewedParse? tab = null;
        var range = new ReplayRange(settled => Cropped(tab!, settled));
        tab = new ViewedParse(Tracker.Replaying(file.Name), file.Name, path, range, viewMode, Pick);
        if (clears) ViewError = "";
        Parses.Add(tab);
        Log.Write($"replaying {file.Name} ({Parses.Count} open)");
        HasParse = true;
        Pick(tab);

        var read = new Progress<(int Lines, int Of)>(p => range.Read(p.Lines, p.Of));
        IProgress<(int, int)> told = read;
        try
        {
            var replay = await Task.Run(() =>
            {
                var lines = EventFiles.ReadAll(path);
                return Replay.Read(lines, file.Name, n => told.Report((n, lines.Count)), range.Stopping);
            }, range.Stopping);
            if (!(replay.Damage > 0))
            {
                CloseParse(tab);
                ViewError = file.Name + ": nothing to replay. It holds " + Format.Int(replay.Lines) + " line" +
                            (replay.Lines == 1 ? "" : "s") + " and no party damage.";
                Log.Write($"not replayed {file.Name}: no party damage in {replay.Lines} lines");
                return true;
            }
            tab.Loaded(replay);
            tab.Tracker.Load(replay, null);
            Log.Write($"replayed {file.Name}: {replay.Lines} lines, {replay.Reader.Events.Count} rows");
            // The range is the whole file, and the count follows from it.
            range.Loaded(replay);
        }
        catch (OperationCanceledException)
        {
            Log.Write($"replay of {file.Name} called off");
            CloseParse(tab);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            CloseParse(tab);
            ViewError = "Could not read " + file.Name + ": " + e.Message;
            Log.Write($"not replayed {file.Name}: {e.Message}");
        }
        return true;
    }

    /// <summary>
    /// The range moved: the replay's tracker is measured over it, and counted
    /// again while it is on screen. While a handle is being dragged the
    /// counts are as frequent as a live session's at most; let go, at once.
    /// </summary>
    void Cropped(ViewedParse tab, bool settled)
    {
        if (tab.Range is not { Replay: { } replay } range) return;
        tab.Tracker.Crop(replay.Crop(range.From, range.To));
        if (settled) Log.Write($"range of {tab.File}: {Format.Clock(range.From)} to {Format.Clock(range.To)}");
        if (Viewing && ReferenceEquals(viewed, tab)) RecountSoon(now: settled);
    }
}
