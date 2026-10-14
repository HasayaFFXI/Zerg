using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace Zerg.Native;

/// <summary>
/// One Zerg per signed-in user. Two would follow the same event file, write
/// the same settings over each other and lay the same panels over the game
/// twice, so a second launch does one thing: it tells the first to come
/// forward, handing on the parses it was asked to open if any, and ends.
///
/// <para>Explorer opens several selected files by starting one launch per
/// file, all at once. So the first listens as soon as it has made its claim,
/// before it has a window: a launch that finds it then is acted on as soon
/// as the window is up, in the order it came.</para>
///
/// <para>A named mutex says whether there is a first; a named pipe carries
/// the message to it. Both are named per Windows session, so two people
/// signed in to one machine each have their own.</para>
/// </summary>
static class SingleInstance
{
    const string Name = "VibeXI.Zerg";

    /// <summary>Held for as long as the process lives; that is what makes it the first.</summary>
    static Mutex? mutex;

    static string PipeName => Name + "." + Process.GetCurrentProcess().SessionId;

    /// <summary>
    /// True if this is the only Zerg running, and from then on it is the one
    /// later launches find. Call before anything is read or written: a second
    /// launch must leave the log and the settings to the first.
    /// </summary>
    public static bool Claim()
    {
        mutex = new Mutex(initiallyOwned: true, @"Local\" + Name, out bool first);
        return first;
    }

    /// <summary>
    /// From a second launch: asks the first to come forward, and to open
    /// <paramref name="parses"/>, if any. Windows lets a program put
    /// its window in front only when the program in front agrees, and for a
    /// moment after being started that is this one, so it passes the right
    /// on before it asks.
    /// </summary>
    public static void Signal(IReadOnlyList<string> parses)
    {
        AllowSetForegroundWindow(ASFW_ANY);
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            // The first may itself be only just starting, its pipe not yet
            // open, or busy with the launches ahead of this one.
            pipe.Connect(10000);
            // The message is the paths in UTF-8, a line each (no file name
            // can hold a line break), and nothing for no parse: the
            // connection alone is the call to come forward.
            var paths = Encoding.UTF8.GetBytes(string.Join('\n', parses));
            pipe.Write(paths, 0, paths.Length);
        }
        // Nothing listening: the first is on its way out, or stuck. There is
        // nobody to tell and nowhere this launch may write that it happened.
        catch (Exception e) when (e is IOException or TimeoutException or UnauthorizedAccessException) { }
    }

    /// <summary>
    /// In the first: listens for later launches for as long as the process
    /// lives. <paramref name="cameAgain"/> is called on a background thread,
    /// with the parses that launch was asked to open, if any.
    /// </summary>
    public static void Listen(Action<IReadOnlyList<string>> cameAgain)
    {
        var thread = new Thread(() =>
        {
            int failed = 0;
            while (true)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                                                               PipeOptions.CurrentUserOnly);
                    pipe.WaitForConnection();
                    failed = 0;
                    using var message = new MemoryStream();
                    pipe.CopyTo(message);
                    var paths = Encoding.UTF8.GetString(message.ToArray());
                    cameAgain(paths.Split('\n', StringSplitOptions.RemoveEmptyEntries));
                }
                catch (IOException e)
                {
                    // A launch that went away mid-word: the next one is
                    // listened for. One failure after another is the pipe
                    // itself (its name busy), and is not tried at full speed
                    // for good: a little later each time, and then not at all.
                    if (++failed >= GiveUpAfter)
                    {
                        Log.Write("single instance: can't listen for a second launch (" + e.Message + ")");
                        return;
                    }
                    Thread.Sleep(Math.Min(failed, 10) * 200);
                }
                catch (UnauthorizedAccessException e)
                {
                    // The name is taken by something that is not Zerg. A second
                    // launch will find nothing to bring forward, and that is all.
                    Log.Write("single instance: can't listen for a second launch (" + e.Message + ")");
                    return;
                }
            }
        })
        { IsBackground = true, Name = "second launch" };
        thread.Start();
    }

    /// <summary>Failures in a row after which a second launch is no longer listened for.</summary>
    const int GiveUpAfter = 50;

    const uint ASFW_ANY = 0xFFFFFFFF;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AllowSetForegroundWindow(uint processId);
}
