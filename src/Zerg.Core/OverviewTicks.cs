namespace Zerg.Core;

/// <summary>How much of a heading of the Overview counts: none of what is
/// under it, some, or all of it.</summary>
public enum TickState { None, Some, All }

/// <summary>
/// The Overview pane's tick boxes, over the two filters they set: the
/// characters isolated and the targets isolated. There is no third filter.
/// A pair, a character at a target, counts when something is isolated and
/// neither filter leaves it out, as <see cref="Counting.Counted"/> asks it.
///
/// <para>The tab says which filter is which. By character, a heading is a
/// character (the <i>outer</i> filter is the characters') and the rows under
/// it are targets (the <i>inner</i> filter is the targets'); By target, the
/// other way round. The rules are the same either way.</para>
///
/// <para>Two filters cut together are always a block: every character
/// isolated at every target isolated. A tick is kept to that: one that
/// shares a character or a target with the block widens it, one that
/// shares neither starts again, and the boxes say what is counted, so a
/// pair that came with a widened block is ticked too.</para>
/// </summary>
public static class OverviewTicks
{
    /// <summary>The pair <paramref name="o"/> at <paramref name="i"/> counts.</summary>
    public static bool Counts(IReadOnlyCollection<string> outer, IReadOnlyCollection<string> inner, string o, string i) =>
        (outer.Count > 0 || inner.Count > 0)
        && (outer.Count == 0 || outer.Contains(o))
        && (inner.Count == 0 || inner.Contains(i));

    /// <summary>
    /// A heading's box: all while every row under it counts (isolated with
    /// nothing under the headings isolated, everything it did counts,
    /// whatever it does next); some while any of them do; none otherwise.
    /// </summary>
    /// <param name="under">The rows under it, as they are listed; asked
    /// only while something is isolated under the headings and it could
    /// count, since working them out is a count of its own.</param>
    public static TickState Heading(IReadOnlyCollection<string> outer, IReadOnlyCollection<string> inner, string o,
                                    Func<IReadOnlyCollection<string>> under)
    {
        if (inner.Count == 0) return outer.Contains(o) ? TickState.All : TickState.None;
        if (outer.Count > 0 && !outer.Contains(o)) return TickState.None;
        var rows = under();
        int counted = rows.Count(inner.Contains);
        return counted == 0 ? TickState.None : counted == rows.Count ? TickState.All : TickState.Some;
    }

    /// <summary>
    /// A heading's box pressed, reading <paramref name="now"/>. All of it
    /// counted: let it go, and with it whatever is isolated under the
    /// headings if nothing else is left isolated beside it (or all of what
    /// was isolated there would count). Otherwise it becomes all of it:
    /// added to the outer filter, as a character's row once was. Anything
    /// isolated under the headings is let go, since all of this one cannot
    /// be counted while it is, and with it the others that were counted for it.
    /// </summary>
    public static (HashSet<string> Outer, HashSet<string> Inner) PressHeading(
        IReadOnlyCollection<string> outer, IReadOnlyCollection<string> inner, string o, TickState now)
    {
        var (os, ins) = (Set(outer), Set(inner));
        if (now == TickState.All)
        {
            os.Remove(o);
            if (os.Count == 0) ins.Clear();
        }
        else
        {
            if (ins.Count > 0) (os, ins) = (Set([]), Set([]));
            os.Add(o);
        }
        return (os, ins);
    }

    /// <summary>
    /// A pair's box pressed: <paramref name="o"/> at <paramref name="i"/>.
    ///
    /// <para><b>Not counted:</b> it joins the block when it shares a side
    /// with it (its heading isolated beside targets isolated: one more
    /// target; its target isolated beside characters isolated: one more
    /// character). Otherwise it is all that is counted.</para>
    ///
    /// <para><b>Counted:</b> it goes, taking as little else with it as the
    /// block allows: one inner from several; one outer from several. All of
    /// a heading isolated alone, it is every other pair under that heading
    /// (<paramref name="besideI"/>), so that unticking one row of a ticked
    /// heading leaves the rest ticked; one inner isolated with every outer,
    /// every other outer that has a pair at it (<paramref name="besideO"/>).
    /// With nothing else left, nothing is isolated.</para>
    /// </summary>
    /// <param name="besideI">The other rows under <paramref name="o"/>'s heading.</param>
    /// <param name="besideO">The other headings with a row of <paramref name="i"/>.</param>
    public static (HashSet<string> Outer, HashSet<string> Inner) PressPair(
        IReadOnlyCollection<string> outer, IReadOnlyCollection<string> inner, string o, string i,
        IEnumerable<string> besideI, IEnumerable<string> besideO)
    {
        var (os, ins) = (Set(outer), Set(inner));
        if (!Counts(outer, inner, o, i))
        {
            if (os.Contains(o) && ins.Count > 0) ins.Add(i);
            else if (ins.Contains(i) && os.Count > 0) os.Add(o);
            else (os, ins) = (Set([o]), Set([i]));
            return (os, ins);
        }
        if (ins.Count == 0 && os.Count == 1) ins = Set(besideI.Where(x => x != i));
        else if (os.Count == 0 && ins.Count == 1) os = Set(besideO.Where(x => x != o));
        else if (ins.Count > 1) ins.Remove(i);
        else if (os.Count > 1) os.Remove(o);
        else ins.Clear();
        // A side that had something and has nothing left would count
        // everything there, and one filled from nothing with nothing in it
        // would count all that it was meant to take away: either way the
        // block is empty.
        bool emptied = outer.Count > 0 && os.Count == 0 || inner.Count > 0 && ins.Count == 0;
        bool unfilled = outer.Count == 0 && inner.Count == 1 && os.Count == 0
                        || inner.Count == 0 && outer.Count == 1 && ins.Count == 0;
        if (emptied || unfilled) (os, ins) = (Set([]), Set([]));
        return (os, ins);
    }

    static HashSet<string> Set(IEnumerable<string> keys) => new(keys, StringComparer.Ordinal);
}
