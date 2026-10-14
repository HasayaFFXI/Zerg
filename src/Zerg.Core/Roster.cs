using System.Globalization;

namespace Zerg.Core;

/// <summary>
/// Who is who: every name seen, what kind of entity it is, and each party
/// member's jobs.
///
/// The addon reads each entity's spawn flags and states its kind outright
/// (player, pet, mob, npc, or other for one it could not resolve), so this is
/// a lookup, not a guess.
///
/// The maps keep the order names were first seen, which is the order an
/// export writes them in.
/// </summary>
public sealed class Roster
{
    /// <summary>Kinds whose damage counts as the party's. Everything else is the enemy.</summary>
    public static bool IsOurs(string? kind) => kind is "player" or "pet";

    /// <summary>The character the event file belongs to, from its name.</summary>
    public string? Owner { get; set; }

    /// <summary>name → player, pet, mob, npc or other.</summary>
    public OrderedDictionary<string, string> Kinds { get; } = new(StringComparer.Ordinal);

    /// <summary>name → that character's jobs, as last reported.</summary>
    public OrderedDictionary<string, JobInfo> Jobs { get; } = new(StringComparer.Ordinal);

    /// <summary>Goes up whenever <see cref="Kinds"/> changes, so whatever was
    /// worked out from who is a monster knows when to work it out again.</summary>
    public int Version { get; private set; }

    /// <summary>
    /// First real answer wins. A target the addon could not resolve arrives as
    /// <c>other</c> (named "Unknown"), and one of those never overwrites a
    /// name already classified from a good lookup; a spawn classification
    /// cannot change, so a later <c>other</c> is a failed lookup, not news.
    /// </summary>
    public void Note(string? name, string? kind)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(kind)) return;
        Kinds.TryGetValue(name, out var had);
        if (kind == "other" && !string.IsNullOrEmpty(had)) return;
        if ((string.IsNullOrEmpty(had) || had == "other") && had != kind)
        {
            Kinds[name] = kind;
            Version++;
        }
    }

    /// <summary>
    /// Last write wins, unlike <see cref="Note"/>. The addon writes a job line
    /// only when the job changed, so a second one for the same character is a
    /// real change (they swapped jobs), and the newest answer is the true one.
    /// </summary>
    public void NoteJob(string? name, JobInfo? job)
    {
        if (string.IsNullOrEmpty(name) || job is null) return;
        Jobs[name] = job;
    }

    public JobInfo? JobOf(string name) => Jobs.GetValueOrDefault(name);

    /// <summary>
    /// "WAR/NIN", "WAR" with no sub-job, or "" if no job was ever reported:
    /// the honest answer for a trust, a pet, or anyone the party table never
    /// listed. Callers draw "" as a dash rather than inventing a job.
    /// </summary>
    public string JobLabel(string name)
    {
        var j = JobOf(name);
        if (j is not { HasMain: true }) return "";
        return j.HasSub ? j.Main + "/" + j.Sub : j.Main;
    }

    /// <summary>The same pair with levels, for a tooltip: "WAR75 / NIN37".</summary>
    public string JobTitle(string name)
    {
        var j = JobOf(name);
        if (j is not { HasMain: true }) return "";
        var s = j.Main + Level(j.MainLevel);
        if (j.HasSub) s += " / " + j.Sub + Level(j.SubLevel);
        return s;
    }

    static string Level(double level) =>
        level != 0 && double.IsFinite(level) ? level.ToString(CultureInfo.InvariantCulture) : "";

    /// <summary>
    /// Anything not positively ours counts as a monster, so an entity the addon
    /// could not resolve is left out of the party's totals rather than quietly
    /// added to them. Under-counting a stranger beats crediting one.
    /// </summary>
    public bool IsMob(string name) => !IsOurs(Kinds.GetValueOrDefault(name));
}
