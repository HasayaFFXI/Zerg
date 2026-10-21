using System.Globalization;

namespace Zerg.Core;

/// <summary>
/// An addon event file read whole, to be cropped to the stretch wanted and
/// exported as a parse.
///
/// <para><b>Replayed through the live door.</b> Every line goes through
/// <see cref="EventReader.Feed(string?)"/>, as a polled line does, into one
/// reader; nothing here reads a record another way. What a replay adds is a
/// clock chosen after the fact: <see cref="Crop"/> makes the session a
/// Start and a Pause would have made around the range, and that session is
/// counted, drawn and exported by the code that counts the live one.</para>
///
/// <para><b>Asked without the viewer's switches.</b> The bars and a crop's
/// zero and end are all of every party row: excluded characters and
/// skillchains included, as a paused session's end is
/// (<see cref="Counting.LastCounted"/>). A range is picked over rows already
/// read, and a zero that moved with each chip would re-date the parse.</para>
///
/// <para><b>One range, one clock</b>, from its zero to its end, quiet
/// stretches included, as a session left running through them has it.</para>
/// </summary>
public sealed class Replay
{
    /// <summary>The rows and heals, and the roster, of the whole file.</summary>
    public EventReader Reader { get; }
    /// <summary>The file's name, without its folder.</summary>
    public string? File { get; }
    /// <summary>Lines read, blank and unreadable ones included.</summary>
    public int Lines => Reader.LineNo;
    /// <summary>The first row or heal's time, wall-clock milliseconds; 0 with none.</summary>
    public double First { get; }
    /// <summary>The end of the second the last row or heal landed in.</summary>
    public double Last { get; }
    public double Span => Last - First;
    /// <summary>The party's damage over the whole file. None, and there is
    /// nothing to replay.</summary>
    public double Damage { get; }

    /// <summary>The party's rows' times, in order: what a crop's zero is looked up in.</summary>
    readonly double[] times;
    /// <summary>The damage each of those landed, 0 for one that did not.</summary>
    readonly double[] dealt;

    Replay(EventReader reader, string? file)
    {
        Reader = reader;
        File = file;
        var roster = reader.Roster;

        // Credited, as a count credits them: a pet's row is its owner's.
        var party = new List<CombatEvent>();
        foreach (var e in reader.Events)
            if (Counting.Counted(e, roster, null, skillchains: true) is { } row) party.Add(row);
        // The file's order is the addon's, which is the clock's within a
        // second; stable, so rows of one second keep it.
        party = [.. party.OrderBy(e => e.T)];
        times = [.. party.Select(e => e.T)];
        dealt = [.. party.Select(e => e.Hit && double.IsFinite(e.Dmg) ? e.Dmg : 0)];
        Damage = dealt.Sum();

        double first = double.PositiveInfinity, last = double.NegativeInfinity;
        foreach (var e in reader.Events) (first, last) = (Math.Min(first, e.T), Math.Max(last, e.T));
        foreach (var h in reader.Heals) (first, last) = (Math.Min(first, h.T), Math.Max(last, h.T));
        (First, Last) = double.IsFinite(first) ? (first, last + Session.Second) : (0, 0);
    }

    /// <summary>
    /// Lines of an event file → its replay. <paramref name="read"/> is told
    /// how many have been read now and then, and <paramref name="cancel"/> is
    /// asked as often.
    /// </summary>
    public static Replay Read(IReadOnlyList<string> lines, string? file, Action<int>? read = null,
                              CancellationToken cancel = default)
    {
        var reader = new EventReader(EventReader.OwnerOf(file));
        for (int i = 0; i < lines.Count; i++)
        {
            reader.Feed(lines[i]);
            if ((i + 1) % 2048 == 0)
            {
                cancel.ThrowIfCancellationRequested();
                read?.Invoke(i + 1);
            }
        }
        read?.Invoke(lines.Count);
        return new Replay(reader, file);
    }

    /// <summary>
    /// The session a range of the file is measured over, or null when no
    /// party row falls in it: there is nothing to start its clock.
    ///
    /// <para>As Start and Pause would have made it: armed at
    /// <paramref name="from"/>, its zero the first party row from there on,
    /// paused at <paramref name="to"/> and snapped back to the last party row
    /// before it (<see cref="Session.Snap"/>). Both are whole seconds, the
    /// wire clock's: <paramref name="from"/> is floored, as arming is, and
    /// <paramref name="to"/> raised.</para>
    /// </summary>
    public Session? Crop(double from, double to)
    {
        from = Math.Floor(from / Session.Second) * Session.Second;
        to = Math.Ceiling(to / Session.Second) * Session.Second;
        // The first row at or after the arming: a search lands on any of
        // several rows of one second, so it steps back to the first.
        int at = Array.BinarySearch(times, from);
        if (at < 0) at = ~at;
        while (at > 0 && times[at - 1] >= from) at--;
        if (at >= times.Length || times[at] >= to) return null;
        double zero = times[at];

        var s = new Session { ArmedAt = from, StartedAt = zero, PausedAt = to };
        return s.Snap(Counting.LastCounted(Reader.Events, s, new FilterOptions { Roster = Reader.Roster }));
    }

    /// <summary>
    /// The party's damage in <paramref name="count"/> equal stretches of the
    /// whole file, first to last: the bars a range is picked over.
    /// </summary>
    public double[] Bins(int count)
    {
        var bins = new double[Math.Max(0, count)];
        if (count <= 0 || !(Span > 0)) return bins;
        for (int i = 0; i < times.Length; i++)
        {
            int at = (int)Math.Floor((times[i] - First) / Span * count);
            bins[Math.Clamp(at, 0, count - 1)] += dealt[i];
        }
        return bins;
    }

    /// <summary>
    /// A time of day typed as <c>H:mm:ss</c> or <c>H:mm</c> → the moment in
    /// the file it names, local time, or null when it names none. A file can
    /// run past midnight, so each day it touches is tried in turn.
    /// </summary>
    public double? TimeOf(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || Span <= 0) return null;
        if (!TimeSpan.TryParseExact(text.Trim(), [@"h\:mm\:ss", @"hh\:mm\:ss", @"h\:mm", @"hh\:mm"],
                                    CultureInfo.InvariantCulture, out var time) || time >= TimeSpan.FromDays(1))
            return null;
        if (!Format.TryLocal(First, out var start) || !Format.TryLocal(Last, out var end)) return null;
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
        {
            var local = day + time;
            double ms = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeMilliseconds();
            if (ms >= First && ms <= Last) return ms;
        }
        return null;
    }
}
