using System.IO;

namespace Zerg;

/// <summary>
/// Command-line options. A player passes one without knowing it: the parse
/// Windows hands Zerg when a .zerg file is opened from Explorer (one launch
/// per file, when several are opened at once). The rest
/// exist for development. A machine where the addon writes somewhere unusual
/// is what the Settings page is for.
/// </summary>
/// <param name="EventsDir">The folder to follow for this run, over the one in
/// the settings and without changing it; null when not given.</param>
/// <param name="Open">The parses to open in the View section, a tab each,
/// as full paths in the order given; empty when none were.</param>
sealed record Options(string? EventsDir, IReadOnlyList<string> Open)
{
    public const string Usage =
        "Zerg.exe [--events-dir <dir>] [<parse>.zerg ...]\n\n" +
        "  --events-dir  where the VibeXI addon writes, for this run only\n" +
        "                (default: the folder on the Settings page)\n" +
        "  <parse>.zerg  an exported parse, opened in a View tab of its own";

    public static Options Parse(string[] args)
    {
        string? eventsDir = null;
        var parses = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i]
                : throw new ArgumentException($"{args[i]} needs a value.");

            switch (args[i])
            {
                case "--events-dir": eventsDir = Path.GetFullPath(Next()); break;
                // Whatever Windows hands over is a file, and any file is
                // taken: one that is not a parse is said so in the View
                // section, as one picked with Import would be.
                case var a when !a.StartsWith("--"): parses.Add(Path.GetFullPath(a)); break;
                default: throw new ArgumentException($"Unknown option {args[i]}.");
            }
        }
        return new Options(eventsDir, parses);
    }
}
