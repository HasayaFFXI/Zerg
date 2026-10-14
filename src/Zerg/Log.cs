using System.IO;

namespace Zerg;

/// <summary>
/// %LOCALAPPDATA%\VibeXI\zerg\logs\zerg.log. There is no console to print to,
/// so this is where startup decisions and crashes go -- the first thing to ask
/// a player for in a bug report. Best effort: logging never throws.
/// </summary>
static class Log
{
    static readonly object Gate = new();
    public static string FilePath => Path.Combine(AppInfo.LogDir, "zerg.log");
    const long MaxBytes = 1024 * 1024;

    /// <summary>How many lines are written between two looks at the file's size.</summary>
    const int CheckEvery = 200;
    static int sinceCheck;

    /// <summary>Starts the log, and a fresh one if it has passed 1 MB.</summary>
    public static void Start()
    {
        try
        {
            Directory.CreateDirectory(AppInfo.LogDir);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        lock (Gate) Rotate();
        Write($"---- {AppInfo.Name} {AppInfo.Version} starting, pid {Environment.ProcessId}");
    }

    public static void Write(string message)
    {
        lock (Gate)
        {
            // Looked at as it is written to, not only at a start, so a player
            // who never closes Zerg doesn't grow a file without end.
            if (++sinceCheck >= CheckEvery) Rotate();
            try { File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}"); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Past 1 MB the log starts again, keeping one old copy.</summary>
    static void Rotate()
    {
        sinceCheck = 0;
        try
        {
            var f = new FileInfo(FilePath);
            if (f.Exists && f.Length > MaxBytes) File.Move(FilePath, FilePath + ".old", overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
