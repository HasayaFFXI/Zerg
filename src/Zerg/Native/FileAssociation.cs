using System.Runtime.InteropServices;
using Microsoft.Win32;
using Zerg.Core;

namespace Zerg.Native;

/// <summary>
/// .zerg files opened from Explorer, in Zerg, with Zerg's icon. Written for
/// the signed-in user alone (under HKCU\Software\Classes), so it asks for no
/// administrator, and only when the player asks for it on the Settings page:
/// a build run from somewhere else never takes it over by starting.
///
/// <para>Two keys: <c>.zerg</c> names the program id <c>Zerg.Parse</c> (and
/// lists it among the programs that can open one), and <c>Zerg.Parse</c>
/// says how: the exe's own icon, and the exe given the file. Explorer is told
/// afterwards, so the icons change at once.</para>
///
/// <para>One thing outranks it and cannot be written by a program: a
/// player's own "Always" from Open with, which Windows keeps under
/// <c>FileExts\.zerg\UserChoice</c>. <see cref="ChosenElsewhere"/> reads
/// it, so the page can say so.</para>
/// </summary>
static class FileAssociation
{
    public const string ProgId = "Zerg.Parse";
    const string Classes = @"Software\Classes\";
    static string Ext => ParseFile.Extension;

    /// <summary>The exe that is running, as Windows should start it.</summary>
    public static string ThisExe => Environment.ProcessPath ?? "";

    /// <summary>The exe .zerg files are set to open with, or null when they
    /// are not set to open in any Zerg.</summary>
    public static string? Target()
    {
        using var ext = Registry.CurrentUser.OpenSubKey(Classes + Ext);
        if (ext?.GetValue(null) as string != ProgId) return null;
        using var command = Registry.CurrentUser.OpenSubKey(Classes + ProgId + @"\shell\open\command");
        return command?.GetValue(null) is string line ? ExeOf(line) : null;
    }

    /// <summary>The program a player chose with Open with and Always, if it is
    /// not Zerg; null when there is no such choice or it is Zerg.</summary>
    public static string? ChosenElsewhere()
    {
        using var choice = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + Ext + @"\UserChoice");
        return choice?.GetValue("ProgId") is string id && id != ProgId &&
               !id.Equals(@"Applications\Zerg.exe", StringComparison.OrdinalIgnoreCase)
            ? id : null;
    }

    /// <summary>.zerg files open in <paramref name="exe"/>, with its icon.</summary>
    public static void Register(string exe)
    {
        var cu = Registry.CurrentUser;
        using (var ext = cu.CreateSubKey(Classes + Ext))
        {
            ext.SetValue(null, ProgId);
            using var others = ext.CreateSubKey("OpenWithProgids");
            others.SetValue(ProgId, "");
        }
        using (var id = cu.CreateSubKey(Classes + ProgId))
            id.SetValue(null, "Zerg parse");
        using (var icon = cu.CreateSubKey(Classes + ProgId + @"\DefaultIcon"))
            icon.SetValue(null, $"\"{exe}\",0");
        using (var command = cu.CreateSubKey(Classes + ProgId + @"\shell\open\command"))
            command.SetValue(null, $"\"{exe}\" \"%1\"");
        Changed();
    }

    /// <summary>Takes back what <see cref="Register"/> wrote, and nothing of
    /// another program's: the extension's default only while it names Zerg.</summary>
    public static void Unregister()
    {
        var cu = Registry.CurrentUser;
        using (var ext = cu.OpenSubKey(Classes + Ext, writable: true))
        {
            if (ext != null)
            {
                using (var others = ext.OpenSubKey("OpenWithProgids", writable: true))
                    others?.DeleteValue(ProgId, throwOnMissingValue: false);
                if (ext.GetValue(null) as string == ProgId) ext.DeleteValue("", throwOnMissingValue: false);
            }
        }
        // Empty now unless something else wrote there: then it stays.
        bool empty;
        using (var ext = cu.OpenSubKey(Classes + Ext))
            empty = ext is { ValueCount: 0 } && ext.GetSubKeyNames().All(IsEmpty);
        if (empty) cu.DeleteSubKeyTree(Classes + Ext, throwOnMissingSubKey: false);
        cu.DeleteSubKeyTree(Classes + ProgId, throwOnMissingSubKey: false);
        Changed();

        static bool IsEmpty(string sub)
        {
            using var key = Registry.CurrentUser.OpenSubKey(Classes + Ext + @"\" + sub);
            return key is null || key.ValueCount == 0 && key.SubKeyCount == 0;
        }
    }

    /// <summary>The exe in a command line: quoted, or up to the first space.</summary>
    static string ExeOf(string line)
    {
        line = line.Trim();
        if (line.StartsWith('"'))
        {
            int end = line.IndexOf('"', 1);
            return end > 0 ? line[1..end] : line[1..];
        }
        int space = line.IndexOf(' ');
        return space > 0 ? line[..space] : line;
    }

    /// <summary>Tells Explorer the associations changed: icons and Open are
    /// read again without a sign-out.</summary>
    static void Changed() => SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, 0, 0);

    const int SHCNE_ASSOCCHANGED = 0x08000000;
    const uint SHCNF_IDLIST = 0;

    [DllImport("shell32.dll")]
    static extern void SHChangeNotify(int eventId, uint flags, nint item1, nint item2);
}
