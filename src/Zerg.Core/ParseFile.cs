using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Zerg.Core;

/// <summary>Why a file could not be imported.</summary>
public enum ImportError
{
    /// <summary>Not an exported parse at all.</summary>
    NotAParse,
    /// <summary>One of the addon's own event files, opened by mistake.</summary>
    EventFile,
    /// <summary>Written by a newer Zerg, in a shape this one does not know.</summary>
    TooNew,
    /// <summary>No complete, paused session, so no clock to measure it on.</summary>
    NoSession,
    /// <summary>No event list, or no heal list.</summary>
    NoEvents,
    /// <summary>Larger than any parse Zerg would write.</summary>
    TooLarge,
}

/// <summary>An import that failed, with a message written for the player.</summary>
public sealed class ParseImportException(ImportError reason, string message) : Exception(message)
{
    public ImportError Reason { get; } = reason;
}

/// <summary>A parse read back from a file.</summary>
/// <param name="Source">An ordinary reader, so nothing downstream can tell an
/// import from a live file.</param>
/// <param name="Session">The parse's clock, always paused.</param>
/// <param name="Skipped">Event records that did not read as damage rows.</param>
/// <param name="File">The event file the parse was read from, if recorded.</param>
/// <param name="Exported">When it was exported (ISO 8601), if recorded.</param>
public sealed record ImportedParse(EventReader Source, Session Session, int Skipped, string? File, string? Exported);

/// <summary>
/// A paused parse, saved for another copy of Zerg to open.
///
/// <para>The file is the addon's own records plus the two things a reader
/// cannot recover from them: the session clock, and the roster (jobs are
/// written once, on change, so most of them come before the Start press and
/// are not in the event list). Events, heals and job lines are
/// stored in the wire format (seconds, <c>mainLvl</c>) and read back through
/// <see cref="EventReader.Feed(EventRecord)"/>, the same path a live line
/// takes, so an import is not a second source that could disagree with the
/// first; it is the first source, replayed.</para>
///
/// <para>Never saved as <c>.jsonl</c>: Zerg follows the newest <c>*.jsonl</c> in
/// the events folder, and an export saved there must not be mistaken for
/// the addon's live file.</para>
/// </summary>
public static class ParseFile
{
    public const string FormatName = "vibexi-parse";
    /// <summary>Bump when the document's shape changes; an older Zerg then
    /// refuses the file with a message rather than misreading it.</summary>
    public const int Version = 1;

    /// <summary>What an export is saved as. A file is told by what is in it,
    /// not by its name.</summary>
    public const string Extension = ".zerg";

    /// <summary>
    /// The largest file Zerg will open as a parse, in bytes. An evening of
    /// alliance combat is a few tens of megabytes; a file past this is not
    /// one Zerg wrote, and reading it would take the window with it.
    /// </summary>
    public const long MaxSize = 200L * 1024 * 1024;

    static readonly JsonDocumentOptions ReadOptions = new() { MaxDepth = 16 };

    // Names as they are, "<b>" and "é" included, not as \u escapes, so a
    // person reading the file reads the names.
    static readonly JsonWriterOptions HeadOptions = new()
    {
        Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    static readonly JsonWriterOptions RowOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// The name an export is offered under: whose parse, and when the pull
    /// began, in local time: "Player_parse_2026.07.30_2130.zerg".
    /// </summary>
    public static string SuggestedName(string? owner, double startedAt)
    {
        var who = new string((owner ?? "").Where(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-').ToArray());
        var when = Format.TryLocal(startedAt, out var d)
            ? "_" + d.ToString("yyyy'.'MM'.'dd'_'HHmm", CultureInfo.InvariantCulture) : "";
        return (who.Length > 0 ? who + "_" : "") + "parse" + when + Extension;
    }

    /// <summary>
    /// Whether an export may be saved under this name. Never as
    /// <c>.jsonl</c>: Zerg follows the newest file of that type in the
    /// events folder, and a parse saved there would be taken for the live
    /// event file.
    /// </summary>
    public static bool CanSaveAs(string path) => !path.TrimEnd().EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------- writing

    /// <summary>
    /// A reader and its session → the text of an export. <paramref name="keep"/>
    /// picks which rows and heals go in, by their wall-clock time (default: all);
    /// <paramref name="file"/> is the event file they came from, for the record;
    /// <paramref name="now"/> is the export time in Unix milliseconds.
    ///
    /// <para>The head is indented for reading; events and heals are one per
    /// line, the way the addon's own file has them, so a long parse stays
    /// something a person can scroll and a diff can line up. The text is
    /// made here and then, so a poll landing while a save dialog is open
    /// cannot change what gets written.</para>
    /// </summary>
    public static string Export(EventReader reader, Session session, Func<double, bool>? keep = null,
                                string? file = null, double? now = null)
    {
        keep ??= _ => true;
        var roster = reader.Roster;
        // Where the clock is read is where the file says it was paused: a
        // snapped session (Session.Snap) is written as one paused there, in
        // the shape every reader already knows. A pause after that point is
        // inside the one the file ends on, and is not written.
        var paused = session.EndedAt ?? session.PausedAt;

        var buffer = new ArrayBufferWriter<byte>(64 * 1024);
        using (var w = new Utf8JsonWriter(buffer, HeadOptions))
        {
            w.WriteStartObject();
            w.WriteString("format", FormatName);
            w.WriteNumber("version", Version);
            w.WriteString("exported", IsoTime(now ?? Session.Now()));
            if (string.IsNullOrEmpty(file)) w.WriteNull("file");
            else w.WriteString("file", file);
            if (roster.Owner is null) w.WriteNull("owner");
            else w.WriteString("owner", roster.Owner);

            w.WriteStartObject("session");
            Number(w, "armedAt", session.ArmedAt);
            Number(w, "startedAt", session.StartedAt);
            w.WriteStartArray("spans");
            foreach (var s in session.Spans)
            {
                if (paused is double end && s.From >= end) continue;
                w.WriteStartObject();
                Number(w, "from", s.From);
                Number(w, "to", s.To);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            Number(w, "pausedAt", paused);
            w.WriteEndObject();

            w.WriteStartObject("kinds");
            foreach (var (name, kind) in roster.Kinds) w.WriteString(name, kind);
            w.WriteEndObject();

            w.WriteStartArray("jobs");
            foreach (var (name, job) in roster.Jobs) WriteJob(w, name, job);
            w.WriteEndArray();
            // The object is left open: the two lists follow, a record a line.
            w.Flush();
        }

        var sb = new StringBuilder(Encoding.UTF8.GetString(buffer.WrittenSpan), buffer.WrittenCount + 1024);
        // Support rows are damage rows on the wire, and go back among them
        // in the order they were read: a reader files them apart again.
        List(sb, "events", reader.Events.Concat(reader.Support).Where(e => keep(e.T)).OrderBy(e => e.Line), WriteRow);
        List(sb, "heals", reader.Heals.Where(h => keep(h.T)), WriteHeal);
        sb.Append("\n}\n");
        return sb.ToString();
    }

    static void List<T>(StringBuilder sb, string name, IEnumerable<T> rows, Action<Utf8JsonWriter, T> write)
    {
        sb.Append(",\n  \"").Append(name).Append("\": [");
        var line = new ArrayBufferWriter<byte>(512);
        using var w = new Utf8JsonWriter(line, RowOptions);
        bool first = true;
        foreach (var row in rows)
        {
            line.ResetWrittenCount();
            w.Reset(line);
            write(w, row);
            w.Flush();
            sb.Append(first ? "\n    " : ",\n    ").Append(Encoding.UTF8.GetString(line.WrittenSpan));
            first = false;
        }
        if (!first) sb.Append("\n  ");
        sb.Append(']');
    }

    /// <summary>A number, or null for none. Nothing Zerg holds is not
    /// finite (<see cref="EventRecord"/> refuses it), and JSON has no
    /// spelling for one if it were.</summary>
    static void Number(Utf8JsonWriter w, string name, double? v)
    {
        if (v is double d && double.IsFinite(d)) w.WriteNumber(name, d);
        else w.WriteNull(name);
    }

    /// <summary>A row → the record the addon wrote. The inverse of reading it.</summary>
    static void WriteRow(Utf8JsonWriter w, CombatEvent e)
    {
        w.WriteStartObject();
        Number(w, "t", e.T / 1000);
        Number(w, "seq", e.Seq);
        Number(w, "use", e.Use);
        w.WriteString("kind", e.Kind);
        w.WriteString("actor", e.Actor);
        w.WriteString("actorKind", e.ActorKind);
        w.WriteString("action", e.Action);
        Number(w, "actionId", e.ActionId);
        w.WriteString("target", e.Target);
        w.WriteString("targetKind", e.TargetKind);
        Number(w, "dmg", e.Dmg);
        w.WriteBoolean("hit", e.Hit);
        w.WriteBoolean("crit", e.Crit);
        w.WriteBoolean("burst", e.Burst);
        Number(w, "msg", e.Msg);
        // As the addon writes them: only a pet's own rows carry these.
        if (!string.IsNullOrEmpty(e.Owner)) w.WriteString("owner", e.Owner);
        if (!string.IsNullOrEmpty(e.Pet)) w.WriteString("pet", e.Pet);
        w.WriteEndObject();
    }

    /// <summary>A heal → the heal line the addon wrote.</summary>
    static void WriteHeal(Utf8JsonWriter w, HealEvent h)
    {
        w.WriteStartObject();
        w.WriteString("kind", "heal");
        Number(w, "t", h.T / 1000);
        Number(w, "seq", h.Seq);
        Number(w, "use", h.Use);
        w.WriteString("via", h.Via);
        w.WriteString("actor", h.Actor);
        w.WriteString("actorKind", h.ActorKind);
        w.WriteString("action", h.Action);
        Number(w, "actionId", h.ActionId);
        w.WriteString("target", h.Target);
        w.WriteString("targetKind", h.TargetKind);
        Number(w, "hp", h.Hp);
        Number(w, "msg", h.Msg);
        if (!string.IsNullOrEmpty(h.Owner)) w.WriteString("owner", h.Owner);
        if (!string.IsNullOrEmpty(h.Pet)) w.WriteString("pet", h.Pet);
        w.WriteEndObject();
    }

    /// <summary>A roster job → the job line it came from; the sub-job trio is
    /// left out when there is no sub-job, as the addon writes it.</summary>
    static void WriteJob(Utf8JsonWriter w, string name, JobInfo j)
    {
        w.WriteStartObject();
        w.WriteString("kind", "job");
        w.WriteString("actor", name);
        w.WriteString("main", j.Main);
        Number(w, "mainId", j.MainId);
        Number(w, "mainLvl", j.MainLevel);
        if (j.HasSub)
        {
            w.WriteString("sub", j.Sub);
            Number(w, "subId", j.SubId);
            Number(w, "subLvl", j.SubLevel);
        }
        w.WriteEndObject();
    }

    /// <summary>Unix milliseconds → "2026-10-02T21:07:45.123Z".</summary>
    static string IsoTime(double ms)
    {
        ms = Math.Clamp(Math.Truncate(ms), 0, DateTimeOffset.MaxValue.ToUnixTimeMilliseconds());
        return DateTimeOffset.FromUnixTimeMilliseconds((long)ms).UtcDateTime
                             .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'", CultureInfo.InvariantCulture);
    }

    // ------------------------------------------------------------- reading

    /// <summary>
    /// A session read back from a file: shape-checked, and required to be
    /// paused. Export is only offered while paused, so an unpaused session is not
    /// one Zerg wrote, and it would be meaningless if it were: its clock would
    /// count on from the moment of import over a file with nothing new in it.
    ///
    /// <para>Every time is one a clock can show, and the pauses are whole,
    /// apart and inside the session: <see cref="Session.PausedBefore"/> reads
    /// them that way, and a file whose pauses overlap or lie outside the
    /// session is not one Zerg wrote.</para>
    /// </summary>
    static Session? ReadSession(JsonElement s)
    {
        if (s.ValueKind != JsonValueKind.Object) return null;
        if (Time(s, "startedAt") is not double started || Time(s, "pausedAt") is not double paused
            || paused < started) return null;
        var spans = new List<PauseSpan>();
        if (s.TryGetProperty("spans", out var list) && list.ValueKind != JsonValueKind.Null)
        {
            if (list.ValueKind != JsonValueKind.Array) return null;
            foreach (var item in list.EnumerateArray())
            {
                if (Time(item, "from") is not double from || Time(item, "to") is not double to || to < from)
                    return null;
                spans.Add(new PauseSpan(from, to));
            }
        }
        spans.Sort((a, b) => a.From.CompareTo(b.From));
        double last = started;
        foreach (var span in spans)
        {
            if (span.From < last || span.To > paused) return null;
            last = span.To;
        }
        return new Session
        {
            ArmedAt = Time(s, "armedAt") ?? started,
            StartedAt = started,
            Spans = spans,
            PausedAt = paused,
        };
    }

    /// <summary>A time a clock can show, in Unix milliseconds, or null.</summary>
    static double? Time(JsonElement o, string name) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var v)
        && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)
        && d >= 0 && d <= EventRecord.LatestTime * 1000 ? d : null;

    /// <summary>A record of the addon's: an object with a kind.</summary>
    static bool IsRecord(JsonElement e) => KindOf(e).Length > 0;

    /// <summary>The first line is a record on its own: an addon event file.</summary>
    static bool LooksLikeEventFile(string text)
    {
        var nl = text.IndexOf('\n');
        return EventRecord.Parse(nl < 0 ? text : text[..nl]) is { Kind.Length: > 0 };
    }

    static string? Text(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s
            ? s : null;

    static string KindOf(JsonElement e) =>
        e.ValueKind == JsonValueKind.Object ? Text(e, "kind") ?? "" : "";

    const string NotParse = "That file is not a Zerg parse export.";
    const string EventFileMessage = "That is an addon event file (.jsonl), not an exported parse. " +
                                    "Open a file made with Export.";

    /// <summary>The refusal for a file too large to be a parse, before it is read.</summary>
    public static ParseImportException TooLarge() =>
        new(ImportError.TooLarge, "That file is too large to be a Zerg parse.");

    /// <summary>
    /// Text → the parse it holds, or a <see cref="ParseImportException"/> whose
    /// message is written for the player. Detection is by content, whatever the
    /// file is called.
    /// </summary>
    public static ImportedParse Import(string text)
    {
        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(text, ReadOptions);
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        {
            throw LooksLikeEventFile(text)
                ? new ParseImportException(ImportError.EventFile, EventFileMessage)
                : new ParseImportException(ImportError.NotAParse, NotParse);
        }
        using var _ = parsed;
        var doc = parsed.RootElement;

        if (doc.ValueKind != JsonValueKind.Object || Text(doc, "format") != FormatName)
            throw IsRecord(doc)
                ? new ParseImportException(ImportError.EventFile, EventFileMessage)
                : new ParseImportException(ImportError.NotAParse, NotParse);
        if (!doc.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number)
            throw new ParseImportException(ImportError.NotAParse, NotParse);
        if (version.GetDouble() > Version)
            throw new ParseImportException(ImportError.TooNew,
                "That parse was exported by a newer version of Zerg. Update this copy to open it.");
        var session = (doc.TryGetProperty("session", out var s) ? ReadSession(s) : null)
            ?? throw new ParseImportException(ImportError.NoSession,
                "That parse has no complete session, so there is no clock to measure it on.");
        if (!doc.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
            throw new ParseImportException(ImportError.NoEvents, "That parse has no event list.");
        if (!doc.TryGetProperty("heals", out var heals) || heals.ValueKind != JsonValueKind.Array)
            throw new ParseImportException(ImportError.NoEvents, "That parse has no heal list.");

        var r = new EventReader(Text(doc, "owner"));
        // Classifications first, so the events' own kinds meet the same
        // first-answer-wins rule they met when they were live.
        if (doc.TryGetProperty("kinds", out var kinds) && kinds.ValueKind == JsonValueKind.Object)
            foreach (var k in kinds.EnumerateObject())
                if (k.Value.ValueKind == JsonValueKind.String) r.Roster.Note(k.Name, k.Value.GetString());
        if (doc.TryGetProperty("jobs", out var jobs) && jobs.ValueKind == JsonValueKind.Array)
            foreach (var j in jobs.EnumerateArray())
                if (KindOf(j) == "job") r.Feed(EventRecord.Read(j));

        int skipped = 0;
        foreach (var ev in events.EnumerateArray())
        {
            // A job record here would be filed, not counted; nothing Export
            // writes puts one in this list, so it is refused, not obeyed. A
            // support row is no damage row and is filed, not skipped.
            int support = r.Support.Count;
            if (KindOf(ev) == "job" || (r.Feed(EventRecord.Read(ev)) == null && r.Support.Count == support))
                skipped++;
        }

        foreach (var h in heals.EnumerateArray())
            if (KindOf(h) == "heal") r.Feed(EventRecord.Read(h));

        return new ImportedParse(r, session, skipped, Text(doc, "file"), Text(doc, "exported"));
    }
}
