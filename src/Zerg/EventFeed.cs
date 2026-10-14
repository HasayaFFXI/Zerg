using System.IO;
using System.Windows.Threading;
using Zerg.Core;

namespace Zerg;

/// <summary>
/// Follows the event folder. A 250 ms poll on the dispatcher is the source of
/// truth; a FileSystemWatcher only brings the next poll forward when Windows
/// reports a change, since Windows reports appends to a file another process
/// holds open late or not at all. Files are read off the UI thread, one poll at
/// a time, and every event is raised on the dispatcher.
/// </summary>
public sealed class EventFeed : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);

    EventTail tail;
    readonly Dispatcher dispatcher;
    readonly DispatcherTimer timer;
    readonly object gate = new();
    FileSystemWatcher? watcher;
    bool polling, pollAgain, earlyQueued, disposed;

    /// <summary>After every successful poll, idle ones included.</summary>
    public event Action<TailUpdate>? Updated;

    /// <summary>When the file couldn't be read. The next poll retries the same bytes.</summary>
    public event Action<Exception>? Failed;

    public string Directory => tail.Directory;

    public EventFeed(string directory)
    {
        tail = new EventTail(directory);
        dispatcher = Dispatcher.CurrentDispatcher;
        timer = new DispatcherTimer(Interval, DispatcherPriority.Normal, (_, _) => Poll(), dispatcher);
        timer.Stop();
    }

    public void Start()
    {
        timer.Start();
        Poll();
    }

    /// <summary>
    /// Follows another folder from now on, starting with nothing read: the
    /// next update is the newest file there from its top (or no file). A
    /// poll of the old folder still being read is dropped when it lands.
    /// </summary>
    public void Watch(string directory)
    {
        tail = new EventTail(directory);
        DropWatcher();
        Poll();
    }

    async void Poll()
    {
        if (disposed) return;
        if (polling)
        {
            // A change reported mid-read may be past where that read stopped.
            pollAgain = true;
            return;
        }
        polling = true;
        // What this poll reads, which Watch may replace before it is back.
        var followed = tail;
        TailUpdate? update = null;
        try
        {
            EnsureWatcher();
            update = await Task.Run(followed.Poll);
        }
        catch (Exception e)
        {
            // Whatever went wrong reading, the next poll tries the same bytes
            // again; a fault that is not the disk's is logged as well, so it
            // is not taken for one.
            if (e is not (IOException or UnauthorizedAccessException)) Log.Write("poll: " + e);
            if (!disposed && followed == tail) Failed?.Invoke(e);
        }
        finally
        {
            polling = false;
        }
        // Outside the reading's own try: a fault in what is done with the
        // lines is not "can't read the event file". Nor is it let out of an
        // async void, where it would come back as a crash on every poll.
        if (update is not null && !disposed && followed == tail)
        {
            try
            {
                Updated?.Invoke(update);
            }
            catch (Exception e)
            {
                Log.Write("update: " + e);
            }
        }
        if (pollAgain)
        {
            pollAgain = false;
            Poll();
        }
    }

    /// <summary>The folder may not exist until the addon first runs, and a
    /// watcher can't watch what isn't there: retried on every poll until it can.</summary>
    void EnsureWatcher()
    {
        if (watcher is not null || !System.IO.Directory.Exists(tail.Directory)) return;
        try
        {
            var w = new FileSystemWatcher(tail.Directory, "*.jsonl")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            w.Changed += OnChange;
            w.Created += OnChange;
            w.Renamed += OnChange;
            w.Deleted += OnChange;
            // The folder went away, or the change buffer overflowed: drop it
            // and let a later poll make a new one. Only this one: a watcher
            // that Watch has replaced since must not take the new one with it.
            w.Error += (_, e) => dispatcher.BeginInvoke(() =>
            {
                Log.Write("watcher: " + e.GetException().Message);
                if (watcher == w) DropWatcher();
                else w.Dispose();
            });
            w.EnableRaisingEvents = true;
            watcher = w;
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
        {
            Log.Write("watcher: " + e.Message);
        }
    }

    /// <summary>On a pool thread. A burst of changes queues one early poll.</summary>
    void OnChange(object sender, FileSystemEventArgs e)
    {
        lock (gate)
        {
            if (earlyQueued) return;
            earlyQueued = true;
        }
        dispatcher.BeginInvoke(() =>
        {
            lock (gate) earlyQueued = false;
            Poll();
        });
    }

    void DropWatcher()
    {
        watcher?.Dispose();
        watcher = null;
    }

    public void Dispose()
    {
        disposed = true;
        timer.Stop();
        DropWatcher();
    }
}
