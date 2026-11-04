using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zerg.Core;

namespace Zerg;

// The Damage section's Battle Log: every kill of the session, newest
// first, with the party's deaths between them; a kill pressed isolates
// that one fight.
public sealed partial class MainViewModel
{
    /// <summary>The kills and deaths, newest first: <see cref="FightRow"/>s
    /// and <see cref="DeathRow"/>s, each drawn by its own template.</summary>
    public ObservableCollection<Row> Fights { get; } = [];
    [ObservableProperty] private bool hasFights;
    /// <summary>What the pane says after its title: what a press does, or
    /// which fight is isolated.</summary>
    [ObservableProperty] private string fightsNote = FightsIdle;
    /// <summary>The line under the list: the band's kills and deaths, and
    /// the kill time, damage and DPS over every kill listed.</summary>
    [ObservableProperty] private string fightsCount = "";
    [ObservableProperty] private string fightsKillTime = None;
    [ObservableProperty] private string fightsDamage = None;
    [ObservableProperty] private string fightsDps = None;
    [ObservableProperty] private string? fightsTip;
    /// <summary>A fight is isolated: the pane's Clear is shown.</summary>
    [ObservableProperty] private bool fightIsolated;

    const string FightsIdle = "Every kill, newest first. Select one to isolate that fight";

    /// <summary>The fight isolated, or null. One at a time: pressing another
    /// isolates it instead. Beside the targets isolated, if any (a row of
    /// either counts). Not saved: like a target, it belongs to its pull.</summary>
    Fight? fight;

    static string FightKey(Fight f) => "k" + f.Through.ToString(System.Globalization.CultureInfo.InvariantCulture);

    void DrawFights(Snapshot c)
    {
        var kills = c.Kills;
        // Newest first; a death and a kill at the same moment, the death
        // after it, as it is read top down from the latest.
        var lines = kills.Fights.Select(f => (At: f.At, Fight: (Fight?)f, Death: (PartyDeath?)null))
            .Concat(kills.Deaths.Select(d => (At: d.At, Fight: (Fight?)null, Death: (PartyDeath?)d)))
            .OrderByDescending(x => x.At).ThenBy(x => x.Death != null)
            .ToList();
        double longest = RowMarks.Largest(kills.Fights.Select(f => f.Length ?? 0));
        var isolated = new HashSet<int>(c.Killed.Select(f => f.Through));
        bool any = c.Scope.Count > 0;

        Rows.Sync(Fights, lines,
            x => x.Fight is { } f ? FightKey(f) : "d" + x.Death!.Name + "@" + x.At.ToString(System.Globalization.CultureInfo.InvariantCulture),
            x => x.Fight != null ? new FightRow(FightKey(x.Fight), PickFight) : new DeathRow(""),
            (row, x) =>
            {
                if (row is FightRow r && x.Fight is { } f)
                {
                    r.When = Format.Elapsed(f.At);
                    r.Name = f.Target;
                    r.Code = "[" + f.Code + "]";
                    r.KillTime = f.Length is double l ? Format.Elapsed(l, tenths: true) : None;
                    r.Damage = Format.Int(f.Damage);
                    r.Dps = f.Dps is double d ? Format.Num(d, 1) : None;
                    r.Fraction = RowMarks.Fraction(f.Length ?? 0, longest);
                    r.Isolated = any && isolated.Contains(f.Through);
                    r.Dimmed = any && !r.Isolated;
                    r.Tip = r.Isolated && fight?.Through == f.Through
                        ? "Isolated: only this fight is counted. Press to let it go"
                        : r.Isolated ? f.Named + ", killed at " + r.When + ": isolated with every " + f.Target
                        : fight != null ? "Isolate this fight instead: " + f.Named + ", killed at " + r.When
                        : r.Dimmed ? "Isolate this fight as well: " + f.Named + ", killed at " + r.When
                        : "Count only the damage of this fight: " + f.Named + ", killed at " + r.When;
                }
                else if (row is DeathRow d && x.Death is { } death)
                {
                    d.When = Format.Elapsed(death.At);
                    d.Name = NameOf(death.Name) + " died";
                    d.Tip = NameOf(death.Name) + " died at " + d.When + (death.Next is double next
                        ? ". Acted again at " + Format.Elapsed(next)
                        : ". Has not acted since");
                }
            });
        HasFights = Fights.Count > 0;

        // Under the list: the kills listed together.
        int n = kills.Fights.Count;
        FightsCount = Format.Int(n) + (n == 1 ? " kill" : " kills") + Dot +
                      Format.Int(kills.Deaths.Count) + (kills.Deaths.Count == 1 ? " death" : " deaths");
        var timed = kills.Fights.Where(f => f.Length is > 0).ToList();
        FightsKillTime = KillTotals.AverageLength(kills.Fights) is double avg ? Format.Elapsed(avg, tenths: true) : None;
        FightsDamage = n > 0 ? Format.Int(kills.Fights.Sum(f => f.Damage)) : None;
        FightsDps = timed.Count > 0
            ? Format.Num(timed.Sum(f => f.Damage) / (timed.Sum(f => f.Length!.Value) / 1000), 1)
            : None;
        FightsTip = timed.Count > 0
            ? "Average kill time " + FightsKillTime + ", fastest " + Format.Elapsed(timed.Min(f => f.Length!.Value), tenths: true) +
              ", slowest " + Format.Elapsed(timed.Max(f => f.Length!.Value), tenths: true) +
              ". DPS is the damage over the kill times added up"
            : null;

        FightIsolated = fight != null;
        FightsNote = c.IsolatedFight is { } one ? one.Named + " isolated. Select it again to let it go" : FightsIdle;
    }

    /// <summary>A kill pressed: its fight isolated, or let go if it was.</summary>
    void PickFight(string key)
    {
        if (snapshot?.Kills.Fights.FirstOrDefault(f => FightKey(f) == key) is not { } f) return;
        fight = fight?.Through == f.Through ? null : f;
        Log.Write("fight " + (fight is { } on ? on.Named + " through line " + on.Through : "let go"));
        Recount();
    }

    /// <summary>The fight isolated let go.</summary>
    [RelayCommand]
    void ClearFight()
    {
        if (fight is null) return;
        fight = null;
        Log.Write("fight let go");
        Recount();
    }
}
