namespace Zerg.Core;

/// <summary>
/// One count of the session as it stands: what every card is drawn from, the
/// Damage section's and the Healing section's alike. Both are measured in the
/// one count, over the same window, so a figure in
/// one section and a figure in the other are never from two different moments.
/// </summary>
/// <param name="Events">The rows that count, credited and on the session clock.</param>
/// <param name="Totals">The party, over those rows: the characters isolated, or everyone.</param>
/// <param name="Elapsed">The session clock when this was counted, in milliseconds.</param>
/// <param name="Latched">An armed session found its zero during this count.</param>
public sealed record Snapshot(IReadOnlyList<CombatEvent> Events, Aggregate Totals, double Elapsed, bool Latched)
{
    /// <summary>Every character who acted in the session, isolated or not,
    /// largest first, each with their own figures and their share of them
    /// all: what the Overview table and the Character button
    /// list. Of what the target, type and fight isolated leave, as each of
    /// those lists is of what the others leave.</summary>
    public IReadOnlyList<ActorTotals> Characters { get; init; } = [];

    /// <summary>The characters damage is isolated to, by name, in the order
    /// they are picked from; none while everyone counts.</summary>
    public IReadOnlyList<string> IsolatedCharacters { get; init; } = [];

    /// <summary>Every character's deaths, isolated or not, as
    /// <see cref="Characters"/> lists everyone: <see cref="Kills"/> has
    /// only those of the characters isolated.</summary>
    public IReadOnlyList<PartyDeath> Died { get; init; } = [];

    /// <summary>How many times this character died, isolated or not.</summary>
    public int DiedOf(string name) => Died.Count(d => d.Name == name);

    /// <summary>The characters healing is isolated to, by name; none while
    /// everyone counts. The Healing section's own: it cuts the heals and
    /// the support, and no damage.</summary>
    public IReadOnlyList<string> IsolatedHealers { get; init; } = [];

    /// <summary>The heals that count: in the session, the party's, of the
    /// healers isolated, on the session clock, a pet's credited to its owner.</summary>
    public IReadOnlyList<HealEvent> Heals { get; init; } = [];

    /// <summary>The party's healing, over those heals.</summary>
    public HealTotals Healing { get; init; } = new();

    /// <summary>How often each buff, removal and ward was cast, over the
    /// same window and healers isolated as the heals (<see cref="Zerg.Core.Support"/>).</summary>
    public SupportTotals Support { get; init; } = SupportTotals.None;

    /// <summary>How often each enfeeble was cast and on how many of the
    /// monsters it reached it took, over the same window and healers: the
    /// same figures as <see cref="Support"/>'s, of what the party aimed at
    /// its enemies.</summary>
    public SupportTotals Enfeebles { get; init; } = SupportTotals.None;

    /// <summary>The targets damage is isolated to, by name; none while every
    /// target counts. Healing is never isolated: a heal's target is who it
    /// healed.</summary>
    public IReadOnlyList<string> Isolated { get; init; } = [];

    /// <summary>What each target took from the characters isolated, every
    /// target, isolated or not, in the order they are picked from
    /// (<see cref="Zerg.Core.Targets.Sorted"/>). Of the damage types
    /// isolated, where any are: each list is of what the other filter leaves.</summary>
    public IReadOnlyList<TargetTotal> Targets { get; init; } = [];
    /// <summary>What those add up to, rows that name no target included:
    /// the figure of "All targets".</summary>
    public double AllTargets { get; init; }
    /// <summary>Each target's Accuracy and weaponskill figures, by name
    /// (<see cref="Counting.ByTarget"/>): of the same rows as
    /// <see cref="Targets"/>, every target, isolated or not.</summary>
    public IReadOnlyDictionary<string, SplitFigures> TargetFigures { get; init; } =
        new Dictionary<string, SplitFigures>(StringComparer.Ordinal);

    Lazy<IReadOnlyList<TargetGroup>>? grouped;
    /// <summary>The damage grouped by target (<see cref="Zerg.Core.TargetGroups"/>):
    /// of the same rows as <see cref="Targets"/>, every character's. Worked
    /// out the first time it is asked for, and only then: only the Overview
    /// pane's By target tab asks.</summary>
    public IReadOnlyList<TargetGroup> TargetGroups => grouped?.Value ?? [];
    /// <summary>How <see cref="TargetGroups"/> is worked out, when asked:
    /// over rows of its own, never the list kept between counts.</summary>
    public Func<IReadOnlyList<TargetGroup>> Grouping { init => grouped = new(value); }

    /// <summary>The damage types isolated, by key, in the order they are
    /// picked from; none while every type counts.</summary>
    public IReadOnlyList<string> IsolatedTypes { get; init; } = [];
    /// <summary>What each damage type came to, every type, isolated or not,
    /// by its key, in the order they are picked from
    /// (<see cref="DamageTypes.Sorted"/>). Of the damage dealt to the
    /// targets isolated, where any are.</summary>
    public IReadOnlyList<TargetTotal> Types { get; init; } = [];
    /// <summary>What those add up to: the figure of "All types".</summary>
    public double AllTypes { get; init; }

    /// <summary>The damage of every type to every target: what
    /// <see cref="Totals"/> would total with nothing isolated.</summary>
    public double Whole { get; init; }

    /// <summary>
    /// An amount of damage per second of the session clock, or null while
    /// targets are isolated: there is no rate to give then. The clock is the
    /// session's. It ran while the party fought everything else, so damage
    /// to one target over it is no DPS anybody dealt, and the event file
    /// does not say how long a target was fought. Null too for damage over
    /// a clock that never ran (<see cref="Counting.PerSecond"/>).
    /// </summary>
    public double? Rate(double total, double seconds) =>
        Isolated.Count > 0 || IsolatedFight != null ? null : Counting.PerSecond(total, seconds);

    /// <summary>The one fight damage is isolated to, or null; beside the
    /// targets isolated, if any. Its rate is no more a DPS anybody dealt
    /// than a target's is.</summary>
    public Fight? IsolatedFight { get; init; }

    /// <summary>What damage is isolated to, by name: the isolated fight's
    /// monster, or the targets picked; none with neither.</summary>
    public IReadOnlyList<string> Scope =>
        IsolatedFight is { } f && !Isolated.Contains(f.Target) ? [.. Isolated, f.Target] : Isolated;

    /// <summary>What follows a name of <see cref="Scope"/> in a label: the
    /// one fight's code, " [00B]"; for a name, how many of it were killed,
    /// " (10)", and nothing when none was.</summary>
    public string Tag(string name) =>
        !Isolated.Contains(name) && IsolatedFight is { Code: { } code } ? " [" + code + "]"
        : Isolated.Contains(name) && Kills.KillsOf(name) is int n and > 0 ? " (" + Format.Int(n) + ")" : "";

    /// <summary>The monsters the party killed in the session, and the
    /// party's deaths (<see cref="Zerg.Core.Kills"/>). Every kill, whatever
    /// is isolated; the deaths of the characters who count.</summary>
    public KillTotals Kills { get; init; } = KillTotals.None;

    /// <summary>Every character's delay samples in the session, whatever
    /// is isolated (<see cref="Zerg.Core.Delays"/>); each character's
    /// figure is on their totals.</summary>
    public DelayTotals Delays { get; init; } = DelayTotals.None;

    /// <summary>The kills of what is isolated: the fight's and those of
    /// the targets picked, every kill with neither.</summary>
    public IReadOnlyList<Fight> Killed =>
        Isolated.Count == 0 && IsolatedFight == null ? Kills.Fights
        : Kills.Fights.Where(k => Isolated.Contains(k.Target) || k.Through == IsolatedFight?.Through).ToList();

    /// <summary>
    /// Kills an hour of the session clock, of what is isolated. Unlike a
    /// DPS this stays while targets are isolated: a kill is a moment, not an
    /// amount spread over the fight, so how many of them fell in an hour of
    /// the session is as true of one target as of all. Null with one fight
    /// isolated alone (one kill an hour says nothing) and with no clock.
    /// </summary>
    public double? KillRate(double seconds) =>
        IsolatedFight != null && Isolated.Count == 0 ? null : Zerg.Core.Kills.PerHour(Killed.Count, seconds);

    /// <summary>Every character who healed in the session, largest first,
    /// isolated or not, each with their share of them all: what the
    /// Healing by character table lists.</summary>
    public IReadOnlyList<HealerTotals> Healers { get; init; } = [];

    /// <summary>The biggest hit of any action; the first found wins a tie.</summary>
    public BestHit? Best => Totals.Best;

    /// <summary>The biggest heal: what one target got from one action, a
    /// pet's included. The earliest wins a tie.</summary>
    public BestHit? BestHeal
    {
        get
        {
            HealEvent? best = null;
            foreach (var h in Heals)
                if (best == null || h.Hp > best.Hp) best = h;
            return best == null ? null : new BestHit(best.Hp, best.Actor, best.Action);
        }
    }

    /// <summary>
    /// Who leads by healing, a pet's included as it is in the Healing
    /// column; nobody while nobody has healed anything. Of two level, the
    /// one listed first.
    /// </summary>
    public HealerTotals? TopHealer
    {
        get
        {
            HealerTotals? top = null;
            foreach (var a in Healing.Actors)
                if (a.Total > 0 && (top == null || a.Total > top.Total)) top = a;
            return top;
        }
    }
}

/// <summary>
/// The live session: the event file being followed, the rows read from it,
/// the session clock, and who has which colour. No UI and no file access; it
/// is handed lines and asked for counts.
///
/// <para><b>Start is also the reset.</b> There is no separate Reset: pressing
/// Start during a session is how the next pull gets measured. It re-arms, and
/// the next counted hit is the new zero. The rows read so far are dropped,
/// which changes nothing on screen (the new session would not cover them) but
/// keeps each count proportional to the rows since Start rather than to the
/// whole evening. The place in the file is not moved: nothing is re-read.</para>
///
/// <para><b>Over a measurement the button asks first</b> (<see cref="First"/>).
/// Start sits beside Pause, and reaching for one and pressing the other at
/// the end of a fight would drop the fight. So the first press only asks,
/// and the pair of buttons is the question's two answers: Start is Confirm
/// and the second button is Cancel. Nothing opens: the pair is pressed from
/// a panel over the game, where a dialog would take the keyboard from it.</para>
///
/// <para>What a restart keeps: the colour slots (a character changing hue
/// between pulls is worse than a stale slot) and the whole roster (monsters
/// stay monsters, and jobs are only written when they change).</para>
///
/// <para><b>A new event file starts from nothing</b>, session included. A
/// clock still running from the previous character or the previous day would
/// be measuring a session that is not this one.</para>
///
/// <para>The session is never saved. A clock restored after a restart would
/// be measuring time the player was not in a fight for.</para>
///
/// <para><b>An idle session keeps no rows.</b> It can never count them: Start
/// drops whatever was read before it. A window left open all evening keeps
/// the roster and the colour slots of what it read, and nothing else.</para>
///
/// <para><b>What the session covers is kept between counts.</b> The rows
/// only ever grow at the end, and whether one of them is in the session
/// (and where it is on its clock) does not depend on any other, so a count
/// reads only what arrived since the last one. Whatever would change that
/// answer for a row already read (the zero, a pause, the snapped end, the
/// skillchain switch, a name found to be ours or not) starts it over.</para>
///
/// <para><b>An imported parse is a tracker too</b> (<see cref="Of"/>), so it is
/// counted, coloured and drawn by the same code as the session, and nothing
/// downstream can tell the two apart. It is a finished recording: its clock
/// is paused for good, and Start, Pause, Cancel and new lines are all
/// refused. Resuming it would run its clock on today's time over a file that
/// will never grow.</para>
/// </summary>
public sealed class Tracker
{
    /// <summary>The event file being followed (name only), or null before the
    /// first. For an import, the event file the parse was recorded from, if it says.</summary>
    public string? File { get; private set; }

    /// <summary>This is an imported parse, not the session: read only.</summary>
    public bool Imported { get; private init; }

    /// <summary>An imported parse, to be counted as a session is. The owner
    /// is the exporter's, so slot 0 and the one name Hide names keeps are
    /// theirs, not the viewer's.</summary>
    public static Tracker Of(ImportedParse parse) => new()
    {
        Imported = true, File = parse.File, Reader = parse.Source, Session = parse.Session,
    };

    /// <summary>A replayed event file is an import too, read only, with
    /// one difference: its clock is the range picked over it, and is given
    /// again each time the range moves (<see cref="Crop"/>). Empty, counting
    /// nothing, until the file has been read (<see cref="Load"/>).</summary>
    public bool Replayed { get; private init; }

    /// <summary>A replay's tracker, before its file has been read.</summary>
    public static Tracker Replaying(string file) => new() { Imported = true, Replayed = true, File = file };

    /// <summary>The replay's file, read, measured over <paramref name="range"/>.</summary>
    public void Load(Replay replay, Session? range)
    {
        if (!Replayed) return;
        Reader = replay.Reader;
        Crop(range);
    }

    /// <summary>
    /// A replay measured over another range (<see cref="Replay.Crop"/>), or
    /// over nothing with none (a range no party row falls in). A new session
    /// object, so every row kept between counts is read again against it.
    /// </summary>
    public void Crop(Session? range)
    {
        if (Replayed) Session = range ?? Session.Idle();
    }
    public EventReader Reader { get; private set; } = new();
    public Session Session { get; private set; } = Session.Idle();
    public Cast Cast { get; private set; } = new();
    /// <summary>Lines read from the file, whatever they held.</summary>
    public long Lines { get; private set; }

    /// <summary>
    /// How soon after the asking press another one is no answer, in
    /// milliseconds: Windows' own double-click time, unless changed. Two
    /// presses that close together are one gesture, and a question a
    /// double-click answers protects nothing.
    /// </summary>
    public const double Settle = 500;

    /// <summary>When Start was pressed over a measurement and has not been
    /// answered yet; null otherwise.</summary>
    public double? AskedAt { get; private set; }

    /// <summary>The two buttons are Confirm and Cancel: a restart is waiting for its answer.</summary>
    public bool Asking => AskedAt != null;

    /// <summary>A different file is being followed, or the same one from its top.</summary>
    public void Follow(string file)
    {
        if (Imported) return;
        File = file;
        Reader = new EventReader(EventReader.OwnerOf(file));
        Cast = new Cast();
        Session = Session.Idle();
        Lines = 0;
        AskedAt = null;
    }

    /// <summary>No file is being followed any more: the folder they are
    /// looked for in has changed. As a tracker starts, session included,
    /// until a file is found in the new one.</summary>
    public void Unfollow()
    {
        if (Imported) return;
        File = null;
        Reader = new EventReader();
        Cast = new Cast();
        Session = Session.Idle();
        Lines = 0;
        AskedAt = null;
    }

    /// <summary>Lines of the file, read here.</summary>
    public void Feed(IReadOnlyList<string> lines) => Feed(lines.Select(EventRecord.Parse).ToList());

    /// <summary>Lines of the file, already read (<see cref="EventRecord.Parse"/>),
    /// one record or null (a blank line) to a line.</summary>
    public void Feed(IReadOnlyList<EventRecord?> records)
    {
        if (Imported) return;
        foreach (var r in records) Reader.Feed(r);
        Lines += records.Count;
        if (Session.ArmedAt == null && Session.StartedAt == null)
        {
            // Idle: the slots are given now, in the order the names were
            // met, as a count would have; the rows themselves go.
            Cast.Assign(Reader.Events, Reader.Roster);
            Reader.DropRows();
            Cast.Rewind();
            return;
        }
        // A row from before the press can be read after it: the end is
        // worked out from the rows' own times, like everything else.
        if (Session.PausedAt != null) Snap(Session);
    }

    /// <summary>Arms the session; during one, re-arms it for the next pull.
    /// The act itself, with no question: <see cref="First"/> is the button.</summary>
    public void Start(double? now = null)
    {
        if (Imported) return;
        Reader.Reset();
        Cast.Rewind();
        Session = Session.Arm(now);
        AskedAt = null;
    }

    /// <summary>
    /// The first button: Start, and over a measurement a question before it.
    /// Idle or armed there is nothing to lose, so it arms at once. Once the
    /// clock has started the first press only asks, and the next one is the
    /// answer, unless it comes within <see cref="Settle"/> of the first.
    /// The session is measured as before while the question is up.
    /// </summary>
    /// <returns>True when the session was armed; false when the press only
    /// asked, or came too soon to be an answer.</returns>
    public bool First(double? now = null)
    {
        if (Imported) return false;
        double t = now ?? Session.Now();
        if (AskedAt is double asked)
        {
            if (t - asked < Settle) return false;
        }
        else if (Session.StartedAt != null)
        {
            AskedAt = t;
            return false;
        }
        Start(now);
        return true;
    }

    /// <summary>The question is taken back, and the measurement kept: Cancel
    /// was pressed, or nobody answered.</summary>
    public void Withdraw() => AskedAt = null;

    /// <summary>The second button: Cancel while a restart is being asked
    /// about or the session is armed, otherwise Pause or Resume.</summary>
    public void Second(double? now = null)
    {
        if (Asking) Withdraw();
        else if (Session.Armed) Cancel();
        else TogglePause(now);
    }

    /// <summary>
    /// Calls off an armed session: back to counting nothing, exactly as a new
    /// event file leaves things. Arming is a statement about a pull that has
    /// not happened yet, and it can turn out wrong; without this the only way
    /// out would be to let it catch a hit and throw that session away. The
    /// rows read while armed are kept: an idle session counts nothing, so
    /// they cannot be seen, and the next Start drops them anyway.
    /// </summary>
    public void Cancel()
    {
        if (Session.Armed) Session = Session.Idle();
    }

    /// <summary>
    /// Pause stops the damage and the clock together. Freeze one without the
    /// other and the figures lie: a running clock over a frozen total reads as
    /// a wipe, a frozen clock over a running total as a record. Reading does
    /// not stop: rows that land during a pause are kept and left out by their
    /// own times. Nothing to do before the clock has started.
    ///
    /// <para><b>A paused clock is read at the last thing it measured</b>
    /// (<see cref="Snap"/>), not at the press. Pause is pressed some time
    /// after the last swing, never on it, and that wait would otherwise be
    /// in every DPS the parse is remembered by. Resume puts it back.</para>
    /// </summary>
    public void TogglePause(double? now = null)
    {
        if (Imported || Session.StartedAt == null) return;
        if (Session.Running) Snap(Session.Pause(now));
        else Session.Resume(now);
    }

    /// <summary>
    /// Ends a paused session at its last party damage row. Whose row is not asked, nor whether it was a
    /// skillchain: the end belongs to the session, as its zero does, so the
    /// screen, the exported file and a compared run all divide by one clock
    /// whatever filters each is seen through.
    /// </summary>
    Session Snap(Session s)
    {
        s.EndedAt = null;   // or a row behind the old end would not be seen
        return s.Snap(Counting.LastCounted(Reader.Events, s, new FilterOptions { Roster = Reader.Roster }));
    }

    /// <summary>
    /// There is a finished measurement to save: the clock has started and is
    /// stopped. A running clock is a moving target, out of date before the
    /// file is written; a paused one has a fixed denominator, which is what
    /// makes a parse worth handing to someone. An import is always paused,
    /// so it can be exported again.
    /// </summary>
    public bool CanExport => Session.StartedAt != null && Session.PausedAt != null;

    /// <summary>
    /// The paused parse as the text of an export, or null while there is
    /// none to save. Only the rows the session can ever count go in: after
    /// the zero and outside every pause. An import cannot be resumed, so
    /// nothing else could be seen under any filter its reader sets, and the
    /// file is the parse, not whatever happened to have been read. The
    /// filters are not in it: what is isolated, skillchains and hidden names are
    /// the viewer's, and the reader's own apply.
    ///
    /// <para>The file ends where the parse does: at its last party damage
    /// row, not at the press (<see cref="Snap"/>). The session on screen
    /// already does.</para>
    /// </summary>
    public string? Export(double? now = null)
    {
        if (!CanExport) return null;
        var ended = Snap(Session.Clone());
        // A kill goes with its killing blow, as it is counted: its death line
        // comes about a second later, often after the end.
        var kills = Kills.Count(Reader, ended).Fights.Select(f => f.Through).ToHashSet();
        return ParseFile.Export(Reader, ended, t => ended.At(t) != null, File, now,
                                d => d.IsKill ? kills.Contains(d.Line) : ended.At(d.T) != null);
    }

    /// <summary>
    /// Counts the session as it stands at <paramref name="now"/>.
    ///
    /// <para>An armed session is given its zero here, first, if a row has
    /// qualified: the same pass must draw the row that started the clock. A
    /// filter change comes through here too, on purpose. Switching skillchains
    /// back on can make a row already read the first that counts, and it is
    /// then the right zero. The zero is set once and never moved.</para>
    /// </summary>
    /// <param name="characters">Characters to isolate, by name: only damage
    /// they dealt is counted, and only their deaths. Empty: everyone. It moves
    /// no clock, no rate and no healing figure.</param>
    /// <param name="targets">Targets to isolate, by name: only damage dealt
    /// to these is counted. Null or empty: every target. It moves no clock
    /// (the zero and the end are asked without it) and no healing figure.</param>
    /// <param name="types">Damage types to isolate (<see cref="DamageTypes"/>):
    /// only damage of these is counted. It moves no clock or healing figure
    /// either, and unlike a target takes no rate away.</param>
    /// <param name="fight">One fight to isolate (a line of the Battle Log):
    /// only damage of its rows is counted. As a target is, and with it.</param>
    /// <param name="healers">Characters to isolate healing to, by name: only
    /// their heals and support are counted. The Healing section's own; it
    /// moves no damage figure. Null or empty: everyone.</param>
    public Snapshot Count(IReadOnlySet<string>? characters, bool skillchains, double? now = null,
                          IReadOnlySet<string>? targets = null, IReadOnlySet<string>? types = null,
                          Fight? fight = null, IReadOnlySet<string>? healers = null)
    {
        var all = Reader.Events;
        var roster = Reader.Roster;
        Cast.Assign(all, roster);

        bool latched = false;
        if (Session.Armed &&
            Counting.FirstCounted(all, Session, new FilterOptions { Roster = roster, Skillchains = skillchains })
                is double zero)
        {
            Session.Start(zero);
            latched = true;
        }

        // The one call that knows the time of day: it drops what the session
        // does not cover and puts the rest on its clock.
        var scoped = Scoped(skillchains);

        // What each character dealt, each target took and each type came
        // to, each before its own filter and after the others': a list goes
        // on naming everything it is picked from, of what the others leave.
        double whole = 0;
        foreach (var e in scoped)
            if (e.Hit && !double.IsNaN(e.Dmg)) whole += e.Dmg;
        var who = Targets.Only(characters);
        var only = Targets.Only(targets);
        var onlyTypes = Targets.Only(types);
        var theirs = who != null ? Counting.Filter(scoped, new FilterOptions { Characters = who }) : scoped;
        var ofTypes = onlyTypes != null ? Counting.Filter(theirs, new FilterOptions { Types = onlyTypes }) : theirs;
        var byTarget = Targets.Totals(ofTypes);
        var byType = DamageTypes.Totals(only != null || fight != null
            ? Counting.Filter(theirs, new FilterOptions { Targets = only, Fight = fight }) : theirs);
        // No session here: these rows are on its clock already. Always a new
        // list: the one kept between counts grows, and a snapshot must not.
        var everyone = Counting.Filter(scoped, new FilterOptions { Targets = only, Types = onlyTypes, Fight = fight });
        var shown = who != null ? Counting.Filter(everyone, new FilterOptions { Characters = who }) : everyone;
        // Every character's rows of the types isolated, for the damage
        // grouped by target, which is worked out later if at all: a list of
        // its own, as the kept one grows. With no target or fight isolated
        // that is everyone's.
        var typed = only == null && fight == null ? everyone
            : Counting.Filter(scoped, new FilterOptions { Types = onlyTypes });

        // The session clock is what every DPS is divided by, read once so the
        // party's figure and each character's cannot differ by a moment.
        var elapsed = Session.Elapsed(now);
        var totals = Counting.Aggregate(shown, elapsed / 1000);
        var byCharacter = who != null ? Counting.Aggregate(everyone, elapsed / 1000) : totals;

        // Healing, over the same window, of the healers isolated. Heals are
        // kept apart from the damage rows, so nothing above can see one, and
        // none of them can start the clock: only a counted hit does that, and
        // a cure cast before the first swing falls before the zero.
        var healWho = Targets.Only(healers);
        var healScoped = HealScoped();
        var healShown = Healing.Filter(healScoped, new FilterOptions { Characters = healWho });
        var healing = Healing.Totals(healShown);
        var byHealer = healWho != null ? Healing.Totals(healScoped) : healing;
        // Support likewise: kept apart from the damage rows, so a Protect
        // before the pull neither starts the clock nor is counted, since it
        // falls before the zero.
        var support = Zerg.Core.Support.Totals(
            Zerg.Core.Support.Filter(SupportScoped(supportKept), new FilterOptions { Characters = healWho }));
        // And enfeebles: a Sleepga before the pull is as far outside the
        // session as that Protect.
        var enfeebles = Zerg.Core.Support.Totals(
            Zerg.Core.Support.Filter(SupportScoped(enfeeblesKept), new FilterOptions { Characters = healWho }));

        var (kills, everyonesKills) = KillsNow(who, skillchains);

        // Delay: each character's own, of the targets, fight and types
        // isolated, whoever else is isolated.
        var delays = DelaysNow();
        foreach (var a in byCharacter.Actors) a.Delay = delays.Mean(a.Name, only, onlyTypes, fight);
        if (who != null)
            foreach (var a in totals.Actors) a.Delay = delays.Mean(a.Name, only, onlyTypes, fight);

        return new Snapshot(shown, totals, elapsed, latched)
        {
            Characters = byCharacter.Actors,
            IsolatedCharacters = who != null ? Targets.Sorted(who) : [],
            IsolatedHealers = healWho != null ? Targets.Sorted(healWho) : [],
            Heals = healShown, Healing = healing, Healers = byHealer.Actors, Support = support, Enfeebles = enfeebles,
            Isolated = only != null ? Targets.Sorted(only) : [],
            Targets = Targets.Pickable(byTarget.Keys).Select(n => new TargetTotal(n, byTarget[n])).ToList(),
            AllTargets = byTarget.Values.Sum(),
            TargetFigures = Counting.ByTarget(ofTypes),
            Grouping = () => Zerg.Core.TargetGroups.Of(typed, who, delays, onlyTypes),
            Delays = delays,
            IsolatedTypes = onlyTypes != null ? DamageTypes.Sorted(onlyTypes) : [],
            Types = DamageTypes.Sorted(byType.Keys).Select(k => new TargetTotal(k, byType[k])).ToList(),
            AllTypes = byType.Values.Sum(),
            Whole = whole,
            // The fight as this count has it: its damage follows who counts.
            IsolatedFight = fight is null ? null : kills.Fights.FirstOrDefault(f => f.Through == fight.Through) ?? fight,
            Kills = kills,
            Died = everyonesKills.Deaths,
        };
    }

    // ------------------------------------------------------- kills, kept

    (Scope Scope, int Rows, int Deaths, int Heals, int Support, int Enfeebles, string Who)? killsAt;
    KillTotals killsKept = KillTotals.None, everyoneKept = KillTotals.None;

    /// <summary>The kills and deaths (<see cref="Kills.Count"/>) of the
    /// characters isolated, and of everyone, worked out again only when
    /// something they depend on has moved: new lines, the session, a name's
    /// kind, the characters isolated or the skillchain switch.</summary>
    (KillTotals Theirs, KillTotals Everyone) KillsNow(IReadOnlySet<string>? characters, bool skillchains)
    {
        var r = Reader;
        var at = (Now(skillchains), r.Events.Count, r.Deaths.Count, r.Heals.Count, r.Support.Count, r.Enfeebles.Count,
                  characters is null ? "" : string.Join('\n', characters.Order(StringComparer.Ordinal)));
        if (killsAt != at)
        {
            killsAt = at;
            everyoneKept = Kills.Count(r, Session, null, skillchains);
            killsKept = characters is null ? everyoneKept : Kills.Count(r, Session, characters, skillchains);
        }
        return (killsKept, everyoneKept);
    }

    // ------------------------------------------------------ delay, kept

    (Scope Scope, int Rows, int Deaths, int Heals, int Support, int Enfeebles)? delaysAt;
    DelayTotals delaysKept = DelayTotals.None;

    /// <summary>The delay samples (<see cref="Delays.Count"/>), worked out
    /// again only when something they depend on has moved: new lines, the
    /// session or a name's kind. No switch of the viewer's moves them.</summary>
    DelayTotals DelaysNow()
    {
        var r = Reader;
        var at = (Now(true), r.Events.Count, r.Deaths.Count, r.Heals.Count, r.Support.Count, r.Enfeebles.Count);
        if (delaysAt != at)
        {
            delaysAt = at;
            delaysKept = Delays.Count(r, Session);
        }
        return delaysKept;
    }

    // ------------------------------------------------- kept between counts

    /// <summary>Everything a row's place in the session depends on but the
    /// row: while this stays the same, a row read once is read for good.</summary>
    readonly record struct Scope(EventReader Reader, Session Session, double? Started, int Spans, double? Paused,
                                 double? Ended, int Roster, bool Skillchains);

    Scope Now(bool skillchains) => new(Reader, Session, Session.StartedAt, Session.Spans.Count, Session.PausedAt,
                                       Session.EndedAt, Reader.Roster.Version, skillchains);

    Scope? rowsScope, healsScope;
    readonly List<CombatEvent> rowsKept = [];
    readonly List<HealEvent> healsKept = [];
    int rowsRead, healsRead;

    /// <summary>Support rows or enfeebles kept between counts: which of the
    /// reader's lists, what of it has been read, and under what scope.</summary>
    sealed class Kept(Func<EventReader, List<CombatEvent>> of)
    {
        public readonly Func<EventReader, List<CombatEvent>> Of = of;
        public Scope? Scope;
        public readonly List<CombatEvent> Rows = [];
        public int Read;
    }

    readonly Kept supportKept = new(r => r.Support);
    readonly Kept enfeeblesKept = new(r => r.Enfeebles);

    /// <summary>The rows the session covers, credited and on its clock, with
    /// skillchains as asked: <see cref="Counting.Filter"/> with the session,
    /// over only what is new since the last count.</summary>
    List<CombatEvent> Scoped(bool skillchains)
    {
        var scope = Now(skillchains);
        var all = Reader.Events;
        if (rowsScope != scope || rowsRead > all.Count)
        {
            rowsScope = scope;
            rowsKept.Clear();
            rowsRead = 0;
        }
        if (rowsRead < all.Count)
        {
            var fresh = rowsRead == 0 ? all : all.GetRange(rowsRead, all.Count - rowsRead);
            rowsKept.AddRange(Counting.Filter(fresh,
                new FilterOptions { Session = Session, Roster = Reader.Roster, Skillchains = skillchains }));
            rowsRead = all.Count;
        }
        return rowsKept;
    }

    /// <summary>The same for the heals (<see cref="Healing.Filter"/>).</summary>
    List<HealEvent> HealScoped()
    {
        var scope = Now(true);
        var all = Reader.Heals;
        if (healsScope != scope || healsRead > all.Count)
        {
            healsScope = scope;
            healsKept.Clear();
            healsRead = 0;
        }
        if (healsRead < all.Count)
        {
            var fresh = healsRead == 0 ? all : all.GetRange(healsRead, all.Count - healsRead);
            healsKept.AddRange(Healing.Filter(fresh, new FilterOptions { Session = Session, Roster = Reader.Roster }));
            healsRead = all.Count;
        }
        return healsKept;
    }

    /// <summary>The same for the support rows or the enfeebles
    /// (<see cref="Zerg.Core.Support.Filter"/>).</summary>
    List<CombatEvent> SupportScoped(Kept kept)
    {
        var scope = Now(true);
        var all = kept.Of(Reader);
        if (kept.Scope != scope || kept.Read > all.Count)
        {
            kept.Scope = scope;
            kept.Rows.Clear();
            kept.Read = 0;
        }
        if (kept.Read < all.Count)
        {
            var fresh = kept.Read == 0 ? all : all.GetRange(kept.Read, all.Count - kept.Read);
            kept.Rows.AddRange(Zerg.Core.Support.Filter(fresh, new FilterOptions { Session = Session, Roster = Reader.Roster }));
            kept.Read = all.Count;
        }
        return kept.Rows;
    }
}
