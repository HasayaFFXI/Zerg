using System.Globalization;

namespace Zerg.Core;

/// <summary>
/// One monster the party killed: the fight that ended in its death line.
/// </summary>
/// <param name="Target">The monster, by name.</param>
/// <param name="Id">Its id, where the file has one: a spawn point
/// (<see cref="Kills"/>).</param>
/// <param name="At">When it was killed, on the session clock: the killing
/// blow's time, the last party row at it before the death line.</param>
/// <param name="Length">How long it was fought, in milliseconds: from the
/// first party row at it to its death line. Null when no party row reached
/// it.</param>
/// <param name="Damage">What the characters who count dealt it.</param>
/// <param name="After">The line of the death that came before it on the same
/// spawn (or 0): its rows are after that line.</param>
/// <param name="Through">Its own death line: its rows are before it.</param>
public sealed record Fight(string Target, double? Id, double At, double? Length, double Damage, int After, int Through)
{
    /// <summary>Its id as XIUI's target bar shows it: "002".</summary>
    public string? Code => Id is double id ? Kills.Code(id) : null;

    /// <summary>"Tiger [002]": one monster, one life of it.</summary>
    public string Named => Kills.Named(Target, Id);

    /// <summary>Its damage over its own length; null with no length.</summary>
    public double? Dps => Length is double l && l > 0 ? Damage / (l / 1000) : null;

    /// <summary>A row of this fight: at this spawn (or this name, in a file
    /// without ids), between the death before it and its own.</summary>
    public bool Holds(CombatEvent e) =>
        e.Line > After && e.Line < Through && (Id is double id ? e.TargetId == id : e.TargetId == null && e.Target == Target);
}

/// <summary>One of the party who died.</summary>
/// <param name="Name">Who.</param>
/// <param name="At">When, on the session clock.</param>
/// <param name="Next">When they next did something of their own, on the
/// session clock; null when they have not since, or not inside the session.
/// That is all the file says: a death line names who died, and nothing says
/// when they were raised.</param>
public sealed record PartyDeath(string Name, double At, double? Next);

/// <summary>The session's kills and the party's deaths.</summary>
public sealed record KillTotals(IReadOnlyList<Fight> Fights, IReadOnlyList<PartyDeath> Deaths)
{
    public static readonly KillTotals None = new([], []);

    /// <summary>How many of this monster, by name, were killed.</summary>
    public int KillsOf(string target) => Fights.Count(f => f.Target == target);

    /// <summary>The average length of the fights with one, in milliseconds;
    /// null with none.</summary>
    public static double? AverageLength(IEnumerable<Fight> fights)
    {
        double sum = 0;
        int n = 0;
        foreach (var f in fights)
            if (f.Length is double l) { sum += l; n++; }
        return n > 0 ? sum / n : null;
    }

    /// <summary>How many times one of the party died.</summary>
    public int DeathsOf(string name) => Deaths.Count(d => d.Name == name);
}

/// <summary>
/// Kills and deaths, from the addon's death lines.
///
/// <para><b>A kill is a death line on a monster</b>, and its fight is the
/// party's rows at that monster since the last time it died. The id names a
/// spawn point, not one life: a monster that respawns comes back with the
/// same id, so the death line is what splits one fight on it from the next.
/// A file from before rows carried ids is matched by name, and two monsters
/// of one name fought at once are then one fight, ended by the first death.</para>
///
/// <para><b>A kill is in the session when its killing blow is</b>: the last
/// party row at it before the death line. The death line comes about a second
/// after that blow, so the last kill of a pull would otherwise fall after the
/// end a paused clock is snapped to. With no party row at it, the death
/// line's own time is asked.</para>
///
/// <para><b>A party death is in the session when its time is</b>, as a heal
/// is: one after the last party damage row is after the end.</para>
/// </summary>
public static class Kills
{
    /// <summary>The last three hex digits of a monster's id, uppercase, as
    /// XIUI's target bar shows them: 17195010 is 0x1066002, "002". The
    /// monster's slot in its zone, so it is unique within a zone only.</summary>
    public static string Code(double id)
    {
        var hex = ((long)id).ToString("X", CultureInfo.InvariantCulture);
        return hex.Length >= 3 ? hex[^3..] : hex.PadLeft(3, '0');
    }

    /// <summary>"Tiger [002]", or the name alone with no id.</summary>
    public static string Named(string target, double? id) => id is double d ? target + " [" + Code(d) + "]" : target;

    /// <summary>"Tiger (10)": every monster of that name, and how many were
    /// killed; the name alone when none was.</summary>
    public static string Counted(string target, int kills) =>
        kills > 0 ? target + " (" + Format.Int(kills) + ")" : target;

    /// <summary>Kills an hour over <paramref name="seconds"/> of clock; null
    /// with no clock.</summary>
    public static double? PerHour(int kills, double seconds) => seconds > 0 ? kills / (seconds / 3600) : null;

    /// <summary>
    /// The kills and the party's deaths the session covers. A row's fight is
    /// decided by every party row, whatever the viewer's switches, as the end
    /// of a session is; a fight's damage is what the characters who count
    /// dealt it, as By target's is.
    /// </summary>
    /// <param name="actors">Characters switched off, keyed by name
    /// (<see cref="FilterOptions.Actors"/>): their damage is no fight's, and
    /// their deaths are not listed.</param>
    public static KillTotals Count(EventReader reader, Session session,
                                   IReadOnlyDictionary<string, bool>? actors = null, bool skillchains = true)
    {
        if (session.StartedAt == null || reader.Deaths.Count == 0) return KillTotals.None;
        var roster = reader.Roster;
        var rows = reader.Events;
        var deaths = reader.Deaths;

        var open = new Dictionary<string, Open>(StringComparer.Ordinal);
        var lastDeath = new Dictionary<string, int>(StringComparer.Ordinal);
        var fights = new List<Fight>();
        var died = new List<DeathEvent>();
        int i = 0;
        foreach (var d in deaths)
        {
            // Every row before this death line, in file order.
            for (; i < rows.Count && rows[i].Line < d.Line; i++)
            {
                var e = rows[i];
                if (e.TargetKind != "mob" || Counting.Counted(e, roster, null, true) == null) continue;
                var key = Key(e.TargetId, e.Target);
                if (!open.TryGetValue(key, out var o))
                    open[key] = o = new Open { After = lastDeath.GetValueOrDefault(key) };
                o.First ??= e.T;
                o.Last = e.T;
                if (e.Hit && !double.IsNaN(e.Dmg) && Counting.Counted(e, roster, actors, skillchains) != null)
                    o.Damage += e.Dmg;
            }

            if (!d.IsKill)
            {
                if (d.TargetKind == "player") died.Add(d);
                continue;
            }
            // Rows of a file without ids are kept by name; its death lines may
            // still carry one.
            var k = Key(d.TargetId, d.Target);
            if (!open.ContainsKey(k) && d.TargetId != null && open.ContainsKey(Key(null, d.Target))) k = Key(null, d.Target);
            open.Remove(k, out var fight);
            int after = fight?.After ?? lastDeath.GetValueOrDefault(k);
            lastDeath[k] = d.Line;
            if (session.At(fight?.Last ?? d.T) is not double at) continue;
            double? length = fight?.First is double first ? Math.Max(0, d.T - first) : null;
            fights.Add(new Fight(d.Target, k.StartsWith('#') ? d.TargetId : null, at, length, fight?.Damage ?? 0,
                                 after, d.Line));
        }

        var party = new List<PartyDeath>();
        foreach (var d in died)
        {
            if (session.At(d.T) is not double at) continue;
            if (actors != null && actors.TryGetValue(d.Target, out var on) && !on) continue;
            party.Add(new PartyDeath(d.Target, at, NextOf(reader, d.Target, d.Line) is double t ? session.At(t) : null));
        }
        return new KillTotals(fights, party);
    }

    sealed class Open
    {
        public int After;
        public double? First, Last;
        public double Damage;
    }

    static string Key(double? id, string name) =>
        id is double d ? "#" + d.ToString(CultureInfo.InvariantCulture) : "n:" + name;

    /// <summary>The time of the first thing <paramref name="name"/> did after
    /// line <paramref name="line"/>, of their own (not a pet's): a swing, a
    /// cast, a heal, a buff, an enfeeble. Null when nothing.</summary>
    static double? NextOf(EventReader reader, string name, int line)
    {
        double? next = null;
        int nextLine = int.MaxValue;
        void Look<T>(List<T> list, Func<T, int> lineOf, Func<T, string> actorOf, Func<T, double> timeOf)
        {
            for (int j = After(list, lineOf, line); j < list.Count; j++)
            {
                var x = list[j];
                if (lineOf(x) >= nextLine) return;
                if (actorOf(x) != name) continue;
                next = timeOf(x);
                nextLine = lineOf(x);
                return;
            }
        }
        Look(reader.Events, e => e.Line, e => e.Actor, e => e.T);
        Look(reader.Heals, h => h.Line, h => h.Actor, h => h.T);
        Look(reader.Support, e => e.Line, e => e.Actor, e => e.T);
        Look(reader.Enfeebles, e => e.Line, e => e.Actor, e => e.T);
        return next;
    }

    /// <summary>The first index of a list in file order whose line is after
    /// <paramref name="line"/>.</summary>
    static int After<T>(List<T> list, Func<T, int> lineOf, int line)
    {
        int lo = 0, hi = list.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (lineOf(list[mid]) <= line) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }
}
