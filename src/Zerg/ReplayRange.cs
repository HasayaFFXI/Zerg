using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zerg.Core;

namespace Zerg;

/// <summary>
/// The range a replay is measured over, and everything the range band says
/// about it: the two handles' times and how long it is, and, while the
/// file is still being read, how far it has got. It holds no clock: each
/// move is handed to the view model (<c>moved</c>), which crops the
/// replay's tracker and counts it again.
/// </summary>
public sealed partial class ReplayRange : ObservableObject
{
    readonly Action<bool> moved;
    readonly CancellationTokenSource stop = new();

    /// <param name="moved">The range moved: true once it has settled (a
    /// handle let go, a time typed), false while a handle is dragged.</param>
    internal ReplayRange(Action<bool> moved) => this.moved = moved;

    /// <summary>The file, once read; null while it is being read.</summary>
    public Replay? Replay { get; private set; }

    /// <summary>Asked while the file is read; set when the read is called off.</summary>
    internal CancellationToken Stopping => stop.Token;

    [ObservableProperty] private bool reading = true;
    /// <summary>How much of the file has been read, 0 to 1.</summary>
    [ObservableProperty] private double progress;
    [ObservableProperty] private string readText = "Reading the file";

    /// <summary>The handles, wall-clock milliseconds, on whole seconds.</summary>
    public double From { get; private set; }
    public double To { get; private set; }

    [ObservableProperty] private string fromText = "";
    [ObservableProperty] private string toText = "";
    [ObservableProperty] private string lengthText = "";

    /// <summary>How far the read has got.</summary>
    internal void Read(int lines, int of)
    {
        Progress = of > 0 ? (double)lines / of : 0;
        ReadText = "Replaying " + Format.Int(lines) + " of " + Format.Int(of) + " lines";
    }

    /// <summary>The file has been read: the range is all of it.</summary>
    internal void Loaded(Replay replay)
    {
        Replay = replay;
        OnPropertyChanged(nameof(Replay));
        Reading = false;
        Set(replay.First, replay.Last, settled: true);
    }

    /// <summary>
    /// Moves the handles: on whole seconds, inside the file, and at least a
    /// second apart, whichever way round they were given.
    /// </summary>
    public void Set(double from, double to, bool settled)
    {
        if (Replay is not { } r) return;
        if (to < from) (from, to) = (to, from);
        double first = Math.Floor(r.First / Session.Second) * Session.Second;
        double last = Math.Ceiling(r.Last / Session.Second) * Session.Second;
        from = Math.Clamp(Math.Floor(from / Session.Second) * Session.Second, first, last - Session.Second);
        to = Math.Clamp(Math.Ceiling(to / Session.Second) * Session.Second, from + Session.Second, last);
        bool same = from == From && to == To;
        if (same && !settled) return;
        From = from;
        To = to;
        OnPropertyChanged(nameof(From));
        OnPropertyChanged(nameof(To));
        FromText = Format.Clock(from);
        ToText = Format.Clock(to);
        LengthText = Format.Elapsed(to - from);
        moved(settled);
    }

    /// <summary>Every line of the file.</summary>
    [RelayCommand]
    void Whole()
    {
        if (Replay is { } r) Set(r.First, r.Last, settled: true);
    }

    /// <summary>A handle's time typed in its box. False, and nothing moved,
    /// when it names no time in the file; the box then shows the handle again.</summary>
    public bool Type(bool start, string text)
    {
        if (Replay?.TimeOf(text) is not double t)
        {
            OnPropertyChanged(start ? nameof(FromText) : nameof(ToText));
            return false;
        }
        if (start) Set(t, Math.Max(To, t + Session.Second), settled: true);
        else Set(Math.Min(From, t - Session.Second), t, settled: true);
        return true;
    }

    /// <summary>Calls the read off; the tab then closes.</summary>
    [RelayCommand]
    internal void Stop() => stop.Cancel();
}
