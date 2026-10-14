using System.Windows.Threading;

namespace Zerg;

/// <summary>
/// Something to do once a control has stood still: a slider reports every
/// step of a drag and a colour picker every point its marker passes, and the
/// settings file is not written for each. What is on screen has followed
/// already; this is the save, and whatever else is only worth doing once.
///
/// <para>Each <see cref="Poke"/> puts the moment off again. <see cref="Flush"/>
/// does it now if it is waiting, which is what closing does, so nothing
/// moved in the last moment before Zerg closes is lost.</para>
/// </summary>
sealed class Debouncer
{
    /// <summary>How long a control has to stand still: 400 ms.</summary>
    public static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(400);

    readonly DispatcherTimer timer;
    readonly Action done;

    /// <summary>On the thread whose dispatcher it is to run on.</summary>
    public Debouncer(Action done, TimeSpan? wait = null)
    {
        this.done = done;
        timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher.CurrentDispatcher)
        {
            Interval = wait ?? Settle,
        };
        timer.Tick += (_, _) => Flush();
    }

    /// <summary>Something moved: the moment is put off again.</summary>
    public void Poke()
    {
        timer.Stop();
        timer.Start();
    }

    /// <summary>Waiting for the moment.</summary>
    public bool Waiting => timer.IsEnabled;

    /// <summary>Done now, if it was waiting; nothing if it was not.</summary>
    public void Flush()
    {
        if (!timer.IsEnabled) return;
        timer.Stop();
        done();
    }
}
