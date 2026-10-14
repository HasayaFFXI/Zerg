using CommunityToolkit.Mvvm.ComponentModel;
using Zerg.Core;

namespace Zerg;

/// <summary>
/// One parse open in the View section: a tab in the title bar, its own
/// read-only <see cref="Zerg.Core.Tracker"/>, what its band says about it,
/// and which side of it the tab shows. Several can be open at once; the
/// <see cref="MainViewModel"/> holds them in the order they were opened
/// and says which one is picked.
/// </summary>
public sealed partial class ViewedParse : ObservableObject
{
    readonly Action<ViewedParse> pick;

    internal ViewedParse(Tracker tracker, string file, string? path, RunSummary info, string? recorded, string mode,
                         Action<ViewedParse> pick)
    {
        Tracker = tracker;
        File = file;
        Path = path;
        Name = ParseDialog.Title(file);
        Tip = recorded ?? file;
        (Started, Length, Party, Skipped) = (info.Started, info.Length, info.Party, info.Skipped);
        this.mode = mode;
        this.pick = pick;
    }

    /// <summary>The parse, counted as the session is.</summary>
    public Tracker Tracker { get; }
    /// <summary>The file it was opened from, by name.</summary>
    public string File { get; }
    /// <summary>The whole path of that file, where there was one: how a
    /// file opened a second time is known to be open already.</summary>
    public string? Path { get; }
    /// <summary>What the parse is called: its file's name, without the extension.</summary>
    public string Name { get; }
    /// <summary>The event file the parse was recorded from, where it says.</summary>
    public string Tip { get; }
    public string Started { get; }
    public string Length { get; }
    public string Party { get; }
    public string Skipped { get; }

    /// <summary>Which side of the parse the tab shows: "Damage" or
    /// "Healing". Each tab keeps its own.</summary>
    [ObservableProperty] private string mode;

    /// <summary>
    /// This tab is the one on screen. The tab's radio button is bound to it
    /// both ways, because UI Automation checks a radio button without a
    /// click: checked, it asks for the tab; unchecked, it changes nothing,
    /// and the view model says which tab is picked (<see cref="Mark"/>).
    /// </summary>
    public bool IsPicked
    {
        get => picked;
        set
        {
            if (value && !picked) pick(this);
            // A radio button left unchecked by the binding's answer is put
            // back to what is true.
            OnPropertyChanged();
        }
    }
    bool picked;

    /// <summary>The view model says whether this tab is the one on screen.</summary>
    internal void Mark(bool on)
    {
        if (picked == on) return;
        picked = on;
        OnPropertyChanged(nameof(IsPicked));
    }

    /// <summary>What UI Automation calls the tab's item in the list of tabs.</summary>
    public override string ToString() => Name;

    /// <summary>The same file as <paramref name="path"/>, by its whole path.</summary>
    internal bool IsFile(string? path) =>
        Path is { } mine && path is { } other &&
        string.Equals(System.IO.Path.GetFullPath(mine), System.IO.Path.GetFullPath(other), StringComparison.OrdinalIgnoreCase);
}
