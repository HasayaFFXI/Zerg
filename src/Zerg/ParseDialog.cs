using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;

namespace Zerg;

/// <summary>
/// A parse to be opened: what it is called, and its text, or the file it
/// is in. A file is read by whoever opens it (<see cref="Read"/>), off the
/// UI thread, so the window does not stop while a large one is.
/// </summary>
public sealed record ParseText(string Name, string? Path = null, string? Text = null)
{
    /// <summary>The text. A file larger than any parse Zerg writes is
    /// refused unread (<see cref="Zerg.Core.ParseFile.MaxSize"/>).</summary>
    /// <exception cref="Zerg.Core.ParseImportException">Too large to be a parse.</exception>
    /// <exception cref="IOException">The file could not be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The file could not be read.</exception>
    public string Read()
    {
        if (Text is not null) return Text;
        if (new FileInfo(Path!).Length > Zerg.Core.ParseFile.MaxSize) throw Zerg.Core.ParseFile.TooLarge();
        return File.ReadAllText(Path!);
    }
}

/// <summary>
/// Picking an exported parse off the disk, and where to save one. One Open
/// dialog for everything that opens a parse, and one remembered folder for
/// opening and saving alike.
///
/// <para>Zerg saves parses as <c>.zerg</c>. The extension is not trusted:
/// what a file is, is decided by reading it.</para>
/// </summary>
static class ParseDialog
{
    /// <summary>Windows remembers a dialog's last folder under this.</summary>
    static readonly Guid Place = new("5f0c1f3e-7a59-4e0b-9d0a-2c41b7c3a6d2");

    /// <summary>The file the player picked, to be read; null if they cancelled.</summary>
    public static ParseText? Open(Window owner)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open an exported parse",
            Filter = "Zerg parses (*.zerg)|*.zerg|All files (*.*)|*.*",
            CheckFileExists = true,
            ClientGuid = Place,
        };
        return dialog.ShowDialog(owner) == true ? Read(dialog.FileName) : null;
    }

    /// <summary>Where the player chose to save a parse, offered under
    /// <paramref name="name"/>; null if they cancelled. In the folder parses
    /// were last opened from or saved to, so Open starts where exports went.</summary>
    public static string? Save(Window owner, string name)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export this parse",
            FileName = name,
            Filter = "Zerg parse (*" + Zerg.Core.ParseFile.Extension + ")|*" + Zerg.Core.ParseFile.Extension,
            DefaultExt = Zerg.Core.ParseFile.Extension,
            AddExtension = true,
            OverwritePrompt = true,
            ClientGuid = Place,
        };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    /// <summary>An addon event file the player picked to replay, starting in
    /// <paramref name="folder"/>, the one being followed; null if they cancelled.</summary>
    public static string? OpenEvents(Window owner, string folder)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Replay an event file",
            Filter = "Addon event files (*.jsonl)|*.jsonl|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (Directory.Exists(folder)) dialog.InitialDirectory = folder;
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    /// <summary>A file, to be read.</summary>
    public static ParseText Read(string path) => new(Path.GetFileName(path), path);

    /// <summary>Whether a file is to be replayed rather than opened: the
    /// addon's own files are <c>.jsonl</c>, and a parse never is
    /// (<see cref="Zerg.Core.ParseFile.CanSaveAs"/>).</summary>
    public static bool IsEventFile(string? path) => path?.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>A parse's name without the extension it was saved under; an
    /// event file's without its own.</summary>
    public static string Title(string fileName) => Extension.Replace(fileName, "");

    static readonly Regex Extension = new(@"\.(zerg|jsonl)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
