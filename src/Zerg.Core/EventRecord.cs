using System.Text.Json;

namespace Zerg.Core;

/// <summary>
/// One record of the event file, read and typed but not yet filed: what
/// <see cref="EventReader"/> turns into a damage row, a heal, a job or
/// nothing. Reading is pure, so it can be done off the UI thread
/// (<see cref="EventTail.Poll"/>), and filing, which has the roster and the
/// line count to keep, is left to the reader.
///
/// <para><b>A field is read as the JSON type the contract gives it, and as
/// nothing else.</b> The addon writes JSON numbers, booleans and strings; a
/// field of another type is corruption, and the record is
/// <see cref="Malformed"/> rather than coerced into damage. A field that is
/// missing, or <c>null</c>, is the type's empty value: 0, false, "".</para>
/// </summary>
public sealed class EventRecord
{
    /// <summary>A nested record is no part of the contract; this is room to
    /// skip one an addon might add, and no more.</summary>
    static readonly JsonDocumentOptions LineOptions = new() { MaxDepth = 8 };

    /// <summary>
    /// The latest a record's time may be, in seconds: the last second a
    /// <see cref="DateTimeOffset"/> can hold, so whatever is drawn from it can
    /// be shown as a time of day. Before 1970 is no time either.
    /// </summary>
    public const double LatestTime = 253_402_300_799;

    public string Kind { get; private set; } = "";
    /// <summary>Seconds, as the wire has it; null when the record has none.</summary>
    public double? T { get; private set; }
    public double Seq { get; private set; }
    public double? Use { get; private set; }
    public string Via { get; private set; } = "";
    public string Actor { get; private set; } = "";
    public string ActorKind { get; private set; } = "";
    public string Action { get; private set; } = "";
    public double ActionId { get; private set; }
    public string Target { get; private set; } = "";
    public string TargetKind { get; private set; } = "";
    public double Dmg { get; private set; }
    public double Hp { get; private set; }
    public bool Hit { get; private set; }
    public bool Crit { get; private set; }
    public bool Burst { get; private set; }
    public double Msg { get; private set; }
    public string Owner { get; private set; } = "";
    public string Pet { get; private set; } = "";
    public string Main { get; private set; } = "";
    public double MainId { get; private set; }
    public double MainLevel { get; private set; }
    public string Sub { get; private set; } = "";
    public double SubId { get; private set; }
    public double SubLevel { get; private set; }

    /// <summary>
    /// Not a record the contract allows: not JSON, not an object, or a known
    /// field of the wrong type (a string where a number goes, a number that
    /// is not finite, a time outside <see cref="LatestTime"/>). What it would
    /// have said is not to be trusted, so none of it is.
    /// </summary>
    public bool Malformed { get; private set; }

    /// <summary>A line that is not a record at all.</summary>
    public static readonly EventRecord Unreadable = new() { Malformed = true };

    /// <summary>
    /// One line of the event file → its record; null for a blank line, and
    /// <see cref="Unreadable"/> for one that is not a JSON object.
    /// </summary>
    public static EventRecord? Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        try
        {
            using var doc = JsonDocument.Parse(line, LineOptions);
            return Read(doc.RootElement);
        }
        catch (JsonException) { return Unreadable; }
        catch (ArgumentException) { return Unreadable; }   // a lone surrogate
    }

    /// <summary>A parsed value → its record; <see cref="Unreadable"/> for
    /// anything but an object. Keys it does not know are passed over.</summary>
    public static EventRecord Read(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object) return Unreadable;
        var r = new EventRecord();
        bool bad = false;
        foreach (var p in e.EnumerateObject())
        {
            var v = p.Value;
            switch (p.Name)
            {
                case "kind": r.Kind = Str(v, ref bad); break;
                case "t": r.T = Opt(v, ref bad); break;
                case "seq": r.Seq = Num(v, ref bad); break;
                case "use": r.Use = Opt(v, ref bad); break;
                case "via": r.Via = Str(v, ref bad); break;
                case "actor": r.Actor = Str(v, ref bad); break;
                case "actorKind": r.ActorKind = Str(v, ref bad); break;
                case "action": r.Action = Str(v, ref bad); break;
                case "actionId": r.ActionId = Num(v, ref bad); break;
                case "target": r.Target = Str(v, ref bad); break;
                case "targetKind": r.TargetKind = Str(v, ref bad); break;
                case "dmg": r.Dmg = Num(v, ref bad); break;
                case "hp": r.Hp = Num(v, ref bad); break;
                case "hit": r.Hit = Bool(v, ref bad); break;
                case "crit": r.Crit = Bool(v, ref bad); break;
                case "burst": r.Burst = Bool(v, ref bad); break;
                case "msg": r.Msg = Num(v, ref bad); break;
                case "owner": r.Owner = Str(v, ref bad); break;
                case "pet": r.Pet = Str(v, ref bad); break;
                case "main": r.Main = Str(v, ref bad); break;
                case "mainId": r.MainId = Num(v, ref bad); break;
                case "mainLvl": r.MainLevel = Num(v, ref bad); break;
                case "sub": r.Sub = Str(v, ref bad); break;
                case "subId": r.SubId = Num(v, ref bad); break;
                case "subLvl": r.SubLevel = Num(v, ref bad); break;
            }
        }
        if (r.T is double t && t is < 0 or > LatestTime) bad = true;
        r.Malformed = bad;
        return r;
    }

    static string Str(JsonElement v, ref bool bad)
    {
        if (v.ValueKind == JsonValueKind.String) return v.GetString() ?? "";
        if (v.ValueKind != JsonValueKind.Null) bad = true;
        return "";
    }

    static double? Opt(JsonElement v, ref bool bad)
    {
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) && double.IsFinite(d)) return d;
        if (v.ValueKind != JsonValueKind.Null) bad = true;
        return null;
    }

    static double Num(JsonElement v, ref bool bad) => Opt(v, ref bad) ?? 0;

    static bool Bool(JsonElement v, ref bool bad)
    {
        switch (v.ValueKind)
        {
            case JsonValueKind.True: return true;
            case JsonValueKind.False: case JsonValueKind.Null: return false;
            default: bad = true; return false;
        }
    }
}
