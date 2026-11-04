namespace Zerg.Core;

/// <summary>
/// The VibeXI addon this Zerg is written against, and whether an event file
/// was written by an older one.
///
/// <para><b>An independent number.</b> It moves when the addon changes what
/// it writes in a way Zerg relies on, not with Zerg's own version; that the
/// two were both 2.0.0 once is a coincidence.</para>
///
/// <para><b>Read from the file's first line</b>, the addon's
/// <c>kind:"meta"</c> line (<see cref="EventReader.Addon"/>). A newer addon
/// is taken as it is. An older one, a file whose first line is not the meta
/// line, and a version that does not read as numbers are all behind: the
/// counts may then be wrong or missing, and the player is told so.</para>
/// </summary>
public static class AddonVersion
{
    /// <summary>The addon version Zerg expects.</summary>
    public const string Expected = "2.0.0";

    /// <summary>What the player is shown while the file is from an older addon.</summary>
    public const string Warning =
        "VibeXI addon version is behind expected version: " + Expected + ", data may be inaccurate or missing";

    /// <summary>
    /// The version an event file states is older than <see cref="Expected"/>,
    /// or there is none, or it is not a version: one to three whole numbers
    /// separated by dots ("2", "2.0", "2.0.1"), parts left out read as 0.
    /// </summary>
    public static bool IsBehind(string? version)
    {
        if (Parse(version) is not { } found) return true;
        var expected = Parse(Expected)!;
        for (int i = 0; i < expected.Length; i++)
            if (found[i] != expected[i]) return found[i] < expected[i];
        return false;
    }

    /// <summary>Major, minor and patch; null for anything else.</summary>
    static int[]? Parse(string? version)
    {
        if (string.IsNullOrEmpty(version)) return null;
        var parts = version.Split('.');
        if (parts.Length > 3) return null;
        var n = new int[3];
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            // Digits only, [0-9] (not char.IsDigit, which takes other scripts').
            if (p.Length == 0 || p.Length > 9 || p.Any(c => c is < '0' or > '9')) return null;
            n[i] = int.Parse(p, System.Globalization.CultureInfo.InvariantCulture);
        }
        return n;
    }
}
