using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zerg.Charts;
using Zerg.Core;

namespace Zerg;

// The Timelines' ranges: a stretch dragged across the Damage section's
// Timeline, or the Healing section's, and every figure of that section
// counted over it alone (RULES.md, "A range on the Timeline"). Each section
// has its own, and each parse its own (kept in Looking with the filters).
// Never saved; a range belongs to its session's clock, so a new session (a
// Start, a new event file, a replay's range moved) has none.
public sealed partial class MainViewModel
{
    /// <summary>The Damage section's range, as dragged; null for the whole session.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDamageRange), nameof(DamageRangeText))]
    private ClockRange? damageRange;

    /// <summary>The Healing section's range, likewise.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHealingRange), nameof(HealingRangeText))]
    private ClockRange? healingRange;

    public bool HasDamageRange => DamageRange != null;
    public bool HasHealingRange => HealingRange != null;
    /// <summary>"0:08.0 – 0:14.2", in the Timeline's heading and under the band's clock.</summary>
    public string DamageRangeText => DamageRange?.Text ?? "";
    public string HealingRangeText => HealingRange?.Text ?? "";

    /// <summary>The session the ranges are on the clock of.</summary>
    Session? rangedOf;

    /// <summary>A range dragged across the Damage section's Timeline:
    /// counted as the drag goes, as a replay's range is (a count at most
    /// every <see cref="CountGap"/>), and at once when it is let go.</summary>
    [RelayCommand]
    void PickDamage(RangePick? pick)
    {
        if (pick == null || (!pick.Settled && pick.Range == DamageRange)) return;
        DamageRange = pick.Range;
        if (pick.Settled) Log.Write($"damage range {pick.Range.Text}");
        RecountSoon(now: pick.Settled);
    }

    /// <summary>Reset range, or a click outside it: the Damage section counts the whole session again.</summary>
    [RelayCommand]
    void WholeDamage()
    {
        if (DamageRange == null) return;
        DamageRange = null;
        Log.Write("damage range cleared");
        Recount();
    }

    /// <summary>A range dragged across the Healing section's Timeline, likewise.</summary>
    [RelayCommand]
    void PickHeal(RangePick? pick)
    {
        if (pick == null || (!pick.Settled && pick.Range == HealingRange)) return;
        HealingRange = pick.Range;
        if (pick.Settled) Log.Write($"healing range {pick.Range.Text}");
        RecountSoon(now: pick.Settled);
    }

    /// <summary>Reset range, for the Healing section.</summary>
    [RelayCommand]
    void WholeHeal()
    {
        if (HealingRange == null) return;
        HealingRange = null;
        Log.Write("healing range cleared");
        Recount();
    }

    /// <summary>The ranges go when the session they were picked on does:
    /// another session's clock is another clock.</summary>
    void KeepRanges()
    {
        if (ReferenceEquals(rangedOf, Shown.Session)) return;
        rangedOf = Shown.Session;
        DamageRange = HealingRange = null;
    }

    // ----------------------------------------------------- the band's clock

    /// <summary>The healing band's clock, which is the damage band's but
    /// for a range picked in one section and not the other.</summary>
    [ObservableProperty] private string healClockText = "00:00";
    [ObservableProperty] private string healClockTenths = ".0";
    [ObservableProperty] private string healClockNote = "—";
    /// <summary>What each band's clock is called: "Elapsed", or "Range" over one.</summary>
    [ObservableProperty] private string clockLabel = "Elapsed";
    [ObservableProperty] private string healClockLabel = "Elapsed";

    /// <summary>Under a total counted over a range.</summary>
    const string InRange = "in the range picked";

    /// <summary>A band's clock: the session's, or over a range its length,
    /// called "Range", with the range dragged under it.</summary>
    static (string Label, string Text, string Tenths, string Note) BandClock(ClockRange? pick, double ms, Session s, double now) =>
        pick is null
            ? ("Elapsed", SessionText.Stopwatch(s, now), SessionText.Tenths(s, now), SessionText.ClockNote(s))
            : ("Range", Format.Stopwatch(ms), Format.Tenths(ms), pick.Text);

    /// <summary>Kills an hour of the Healing section's range, as the beat writes it.</summary>
    string HealKillRate(double seconds) =>
        killedIn ?? (snapshot?.HealKillRate(seconds) is double r ? Format.Num(r, 1) + " kills per hour" : None);
}
