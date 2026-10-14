using System.Globalization;

namespace Zerg.Core;

/// <summary>
/// How Zerg spells its numbers and times. US-style grouping (1,234,567)
/// whatever the machine's culture, so a figure reads the same on every
/// player's screen and in every screenshot.
///
/// <para><b>One rounding rule</b>: a figure is rounded on its value, halves
/// away from zero, to the places it is printed with. A figure that rounds to
/// zero is printed without a sign. Not a number is a dash, as an empty cell
/// is.</para>
/// </summary>
public static class Format
{
    static readonly CultureInfo Us = CultureInfo.InvariantCulture;

    /// <summary>Rounded and grouped: 1,234,568.</summary>
    public static string Int(double n) => Num(n, 0);

    /// <summary>Grouped, with exactly <paramref name="dp"/> decimals: 1,234.6.</summary>
    public static string Num(double n, int dp = 1)
    {
        if (double.IsNaN(n)) return "—";
        if (double.IsInfinity(n)) return n > 0 ? "∞" : "-∞";
        var r = Round(n, dp);
        if (r == 0) r = 0;   // no "-0"
        return r.ToString("N" + dp.ToString(Us), Us);
    }

    /// <summary>
    /// Halves away from zero, on the value. <see cref="Math.Round(double, int, MidpointRounding)"/>
    /// takes no more than 15 places; past them a double holds no fraction
    /// worth rounding.
    /// </summary>
    public static double Round(double n, int dp = 0) =>
        Math.Round(n, Math.Clamp(dp, 0, 15), MidpointRounding.AwayFromZero);

    /// <summary>1.2M, 45.6K, or the whole number under 10,000.</summary>
    public static string Compact(double n)
    {
        var a = Math.Abs(n);
        if (a >= 1e6) return Num(n / 1e6, 1) + "M";
        if (a >= 1e4) return Num(n / 1e3, 1) + "K";
        return Int(n);
    }

    /// <summary>
    /// Unix milliseconds as a local date and time, if it is one a
    /// <see cref="DateTimeOffset"/> can hold: the one way in from a time of
    /// the world, so a time read from a damaged file cannot throw.
    /// </summary>
    public static bool TryLocal(double ms, out DateTimeOffset local)
    {
        local = default;
        if (!double.IsFinite(ms)) return false;
        var t = Math.Truncate(ms);
        if (t < DateTimeOffset.MinValue.ToUnixTimeMilliseconds() || t > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())
            return false;
        try
        {
            local = DateTimeOffset.FromUnixTimeMilliseconds((long)t).ToLocalTime();
            return true;
        }
        catch (ArgumentOutOfRangeException) { return false; }   // the offset pushed it past the end
    }

    /// <summary>A time of day, local: 21:07:45. Only for when something happened
    /// in the world; a measurement is drawn with <see cref="Elapsed"/>.</summary>
    public static string Clock(double ms) =>
        TryLocal(ms, out var d) ? d.ToString("HH':'mm':'ss", Us) : "—";

    /// <summary>A local date: 2026-07-30.</summary>
    public static string Day(double ms) =>
        TryLocal(ms, out var d) ? d.ToString("yyyy'-'MM'-'dd", Us) : "—";

    /// <summary>
    /// Milliseconds since the zero → the label Zerg reads time in: <c>m:ss</c>
    /// under an hour, <c>h:mm:ss</c> over. The leading field is unpadded, so
    /// <c>3:07</c> cannot be misread as a time of day.
    /// </summary>
    public static string Elapsed(double ms)
    {
        if (!double.IsFinite(ms)) return "—";
        var (h, m, s) = Parts(ms);
        var text = h > 0
            ? string.Create(Us, $"{h}:{m:00}:{s:00}")
            : string.Create(Us, $"{m}:{s:00}");
        return ms <= -1000 ? "-" + text : text;
    }

    /// <summary>
    /// The same clock at a fixed width: <c>MM:SS</c>, and <c>H:MM:SS</c> past
    /// the hour. For a readout that is watched: a figure that changes width at
    /// 0:59 → 1:00 shifts every digit beside it.
    /// </summary>
    public static string Stopwatch(double ms)
    {
        if (!double.IsFinite(ms)) return "—";
        var (h, m, s) = Parts(ms);
        return h > 0 ? string.Create(Us, $"{h}:{m:00}:{s:00}") : string.Create(Us, $"{m:00}:{s:00}");
    }

    /// <summary>Whole hours, minutes and seconds in |<paramref name="ms"/>|.</summary>
    static (long H, long M, long S) Parts(double ms)
    {
        var sec = (long)Math.Min(Math.Floor(Math.Abs(ms) / 1000), long.MaxValue / 2);
        return (sec / 3600, sec % 3600 / 60, sec % 60);
    }

    /// <summary>Seconds → "1h 5m", "4m 07s" or "42s".</summary>
    public static string Duration(double sec)
    {
        if (!double.IsFinite(sec)) return "—";
        var (h, m, s) = Parts(Math.Max(0, Round(sec)) * 1000);
        if (h > 0) return string.Create(Us, $"{h}h {m}m");
        if (m > 0) return string.Create(Us, $"{m}m {s:00}s");
        return string.Create(Us, $"{s}s");
    }
}
