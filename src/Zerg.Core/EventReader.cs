using System.Text.RegularExpressions;

namespace Zerg.Core;

/// <summary>
/// Reads the addon's event file one line at a time into damage rows, heals and
/// the roster.
///
/// The addon reads the game's own action packets and writes one JSON object
/// per line, so there is nothing to infer here: a line is validated, its clock
/// scaled from seconds to milliseconds, and it is filed. What the packet
/// states outright:
/// <list type="bullet">
/// <item><c>use</c>: one action is one use however many targets it reached;
/// the addon mints it from the packet's own target list.</item>
/// <item><c>actorKind</c> / <c>targetKind</c>: player, pet, mob, npc, from
/// the entity's spawn flags. The roster is a thin wrapper over these.</item>
/// <item><c>crit</c> and <c>hit</c>: real flags, so a miss, a parry, a shadow
/// and an evade are told apart and accuracy has an exact denominator.</item>
/// <item><c>owner</c>: a pet names its master.</item>
/// <item>job lines: a party member's main and sub job.</item>
/// </list>
///
/// The wire clock is whole seconds (the addon has no finer clock), with
/// <c>seq</c> ordering rows inside one second. Everything after this class is
/// in milliseconds; <c>t</c> is scaled here and never again.
/// </summary>
public sealed class EventReader
{
    /// <summary>The action name a row with none gets: an added effect riding on a swing.</summary>
    public const string AdditionalEffect = "Additional Effect";

    /// <param name="owner">The character the file belongs to, from its name.
    /// Only used to pin that character's colour; parsing ignores it.</param>
    public EventReader(string? owner = null)
    {
        Roster.Owner = string.IsNullOrEmpty(owner) ? null : owner;
    }

    public Roster Roster { get; } = new();

    /// <summary>Damage rows, in file order.</summary>
    public List<CombatEvent> Events { get; } = [];

    /// <summary>Heals, kept apart from <see cref="Events"/> so no damage count can see them.</summary>
    public List<HealEvent> Heals { get; } = [];

    /// <summary>How many records have been fed, blank and unreadable lines included.</summary>
    public int LineNo { get; private set; }

    /// <summary>How many of them were not records the contract allows
    /// (<see cref="EventRecord.Malformed"/>), or were damage rows or heals
    /// with no time: read past, and counted here so the log can say so.</summary>
    public int Unreadable { get; private set; }

    /// <summary>One line of the file. Returns the damage row it made, or null
    /// (blank, unreadable, or a line that is not a damage row).</summary>
    public CombatEvent? Feed(string? line) => Feed(EventRecord.Parse(line));

    /// <summary>
    /// One record that is already read: a line parsed off the UI thread, or a
    /// record of an imported export. Both come in through here, so a record
    /// read back from an export cannot be read differently from the line it
    /// started as. Null is a blank line.
    /// </summary>
    public CombatEvent? Feed(EventRecord? record)
    {
        LineNo++;
        return record is null ? null : Ingest(record);
    }

    CombatEvent? Ingest(EventRecord raw)
    {
        // The addon's own notices (its startup probe, and message ids it did
        // not recognise). Not events; they stay in the file for anyone
        // searching it, and what else they hold is theirs to shape.
        if (raw.Kind == "meta") return null;
        if (raw.Malformed)
        {
            Unreadable++;
            return null;
        }

        // A job line is a fact about a character, not an event: no damage, no
        // target, no use. As a row it would put a zero-damage line into every
        // count, so it goes on the roster instead.
        if (raw.Kind == "job")
        {
            var who = raw.Actor;
            if (who.Length > 0)
            {
                // It comes off a party slot, so it is also proof the name is
                // one of ours. For a member who never acts it is the only
                // proof there is; without it the healer reads as a stranger.
                Roster.Note(who, "player");
                Roster.NoteJob(who, new JobInfo(
                    Main: Or(raw.Main, JobInfo.None), MainId: raw.MainId, MainLevel: raw.MainLevel,
                    Sub: Or(raw.Sub, JobInfo.None), SubId: raw.SubId, SubLevel: raw.SubLevel));
            }
            return null;
        }

        // Everything after this is on the clock, and a row with no time
        // cannot be put on it.
        if (raw.Kind.Length == 0 || raw.Actor.Length == 0) return null;
        if (raw.T is not double t)
        {
            Unreadable++;
            return null;
        }

        // A heal is not a damage row and must not become one: every damage
        // count runs over Events, where a heal would be a miss and could start
        // the session clock. Which actions are heals, and the amount, are the
        // addon's decision; this only files the line.
        if (raw.Kind == "heal")
        {
            Roster.Note(raw.Actor, raw.ActorKind);
            Roster.Note(raw.Target, raw.TargetKind);
            Heals.Add(new HealEvent
            {
                T = t * 1000,
                Seq = raw.Seq,
                Use = raw.Use,
                Via = raw.Via,
                Actor = raw.Actor,
                ActorKind = raw.ActorKind,
                Action = raw.Action,
                ActionId = raw.ActionId,
                Target = raw.Target,
                TargetKind = raw.TargetKind,
                Hp = raw.Hp,
                Msg = raw.Msg,
                Line = LineNo,
                Owner = NullIfEmpty(raw.Owner),
                Pet = NullIfEmpty(raw.Pet),
            });
            return null;
        }

        Roster.Note(raw.Actor, raw.ActorKind);
        Roster.Note(raw.Target, raw.TargetKind);

        var e = new CombatEvent
        {
            T = t * 1000,
            Seq = raw.Seq,
            Use = raw.Use,
            Kind = raw.Kind,
            Actor = raw.Actor,
            ActorKind = raw.ActorKind,
            Action = Or(raw.Action, AdditionalEffect),
            ActionId = raw.ActionId,
            Target = raw.Target,
            TargetKind = raw.TargetKind,
            Dmg = raw.Dmg,
            Hit = raw.Hit,
            Crit = raw.Crit,
            Burst = raw.Burst,
            Msg = raw.Msg,
            Line = LineNo,
            // Only on a pet's own rows, so they stay off every other row.
            Owner = NullIfEmpty(raw.Owner),
            Pet = NullIfEmpty(raw.Pet),
        };
        Events.Add(e);
        return e;
    }

    static string? NullIfEmpty(string s) => s.Length > 0 ? s : null;

    static string Or(string s, string fallback) => s.Length > 0 ? s : fallback;

    /// <summary>
    /// Drops the rows and keeps the roster. Which names are monsters does not
    /// stop being true because the session was restarted, and the jobs must
    /// survive: the addon only writes a job line when a job changes, so a
    /// cleared map would stay empty until somebody swapped.
    /// </summary>
    public void Reset()
    {
        Events.Clear();
        Heals.Clear();
        LineNo = 0;
        Unreadable = 0;
        dropped = 0;
    }

    int dropped;

    /// <summary>Damage rows read since the last <see cref="Reset"/>, the
    /// dropped ones included.</summary>
    public int Rows => dropped + Events.Count;

    /// <summary>
    /// Lets go of the rows and heals read so far, and keeps everything else:
    /// the roster, the line count, <see cref="Rows"/>. For rows nothing can
    /// ever count (an idle session's).
    /// </summary>
    public void DropRows()
    {
        dropped += Events.Count;
        Events.Clear();
        Heals.Clear();
    }

    // ------------------------------------------------------------ file names

    // [0-9] rather than \d, which in .NET also matches other scripts' digits;
    // and "." never matches a line break of any kind (Zl and Zp are U+2028 and U+2029).
    static readonly Regex DailyName = new(
        @"^([^\n\r\p{Zl}\p{Zp}]*?)_[0-9]{4}\.[0-9]{2}\.[0-9]{2}\.jsonl$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    // What the addon writes now: one file per load, named for the local date
    // and time it was loaded (YYYYMMDDHHMMSS).
    static readonly Regex LoadName = new(
        @"^([^\n\r\p{Zl}\p{Zp}]*?)-[0-9]{14}\.jsonl$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static readonly Regex LeadingName = new(@"^([A-Za-z]+)[_\-]", RegexOptions.CultureInvariant);

    /// <summary>
    /// Whose event file this is, from its name: "Player-20260730213045.jsonl"
    /// (the addon's file, one per load) and "Player_2026.07.30.jsonl" (the
    /// file it wrote before, one per day) are Player's. Any other name is
    /// searched for a leading name. Null when there is none.
    /// </summary>
    public static string? OwnerOf(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var m = DailyName.Match(name);
        if (!m.Success) m = LoadName.Match(name);
        if (m.Success) return m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : null;
        var o = LeadingName.Match(name);
        return o.Success ? o.Groups[1].Value : null;
    }
}
