namespace Zerg.Core;

/// <summary>
/// A stretch of the session clock picked on a Timeline, in milliseconds from
/// the session's zero, its ends included. The Damage and Healing sections
/// each have one of their own, or none: then the whole session counts.
///
/// <para><b>Measured as a replay's range is</b> (<see cref="Replay.Crop"/>):
/// its clock runs from the first party row in it to the last
/// (<see cref="Measure"/>), so the quiet either side of a drag is no time
/// anybody fought, and no rate is divided by it. Rows outside the range
/// are not counted; the rows in it keep their times on the session clock,
/// so a time in a table is the time on the Timeline above it.</para>
/// </summary>
public sealed record ClockRange(double From, double To)
{
    /// <summary>The range between two points dragged, whichever way round.</summary>
    public static ClockRange Of(double a, double b) => a <= b ? new(a, b) : new(b, a);

    public double Length => To - From;

    /// <summary>Is a time on the session clock inside it, ends included?</summary>
    public bool Holds(double t) => t >= From && t <= To;

    /// <summary>
    /// What <paramref name="picked"/> measures over the rows the session
    /// counts (<paramref name="rows"/>, on its clock): from the first row at
    /// or after its start to the last at or before its end. Null when no row
    /// falls in it: there is nothing to start its clock, and nothing counts.
    /// </summary>
    public static ClockRange? Measure(ClockRange picked, IReadOnlyList<CombatEvent> rows)
    {
        double first = double.PositiveInfinity, last = double.NegativeInfinity;
        foreach (var e in rows)
        {
            if (!picked.Holds(e.T)) continue;
            if (e.T < first) first = e.T;
            if (e.T > last) last = e.T;
        }
        return double.IsFinite(first) ? new ClockRange(first, last) : null;
    }

    /// <summary>The rows inside <paramref name="measured"/>; none with none.</summary>
    public static List<CombatEvent> Within(IReadOnlyList<CombatEvent> rows, ClockRange? measured) =>
        measured is null ? [] : rows.Where(e => measured.Holds(e.T)).ToList();

    /// <summary>The heals inside <paramref name="measured"/>; none with none.</summary>
    public static List<HealEvent> Within(IReadOnlyList<HealEvent> heals, ClockRange? measured) =>
        measured is null ? [] : heals.Where(h => measured.Holds(h.T)).ToList();

    /// <summary>"00:08.0 – 00:14.2": a range as its heading and the band print it.</summary>
    public string Text => Format.Elapsed(From, tenths: true) + " – " + Format.Elapsed(To, tenths: true);
}
