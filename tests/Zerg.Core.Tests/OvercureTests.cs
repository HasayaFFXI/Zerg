using Zerg.Core;

namespace Zerg.Core.Tests;

/// <summary>Overcure (RULES.md, "Overcure"): each caster's base for a spell
/// learned from the amounts that recur, day/weather and Divine Seal amounts
/// counted whole, a cast below every full amount charged the rest of the
/// plain base.</summary>
public class OvercureTests
{
    static double next = 1;

    static HealEvent Cure(string target, double hp, string actor = "Federal", string action = "Cure III",
                          double? use = null, string via = "magic") =>
        new()
        {
            T = next * 1000, Use = use ?? next++, Via = via, Actor = actor, ActorKind = "player", Action = action,
            ActionId = 3, Target = target, TargetKind = "player", Hp = hp,
        };

    /// <summary>Federal's Cure III lands on 232 on Gillette and Acidace,
    /// three times each, and whatever <paramref name="more"/> adds.</summary>
    static List<HealEvent> Session(params IEnumerable<HealEvent> more) =>
    [
        Cure("Gillette", 232), Cure("Gillette", 232), Cure("Gillette", 232),
        Cure("Acidace", 232), Cure("Acidace", 232), Cure("Acidace", 232),
        .. more,
    ];

    [Fact]
    public void The_amount_that_recurs_is_the_base_and_a_cast_below_it_was_cut_short()
    {
        var short_ = Cure("Acidace", 100);
        var heals = Session(short_);
        var fit = Overcure.Fit(heals);
        Assert.Equal(232, fit.Base("Federal", "Cure III"));
        Assert.Equal(132, fit.Of(short_));
        Assert.Equal(0, fit.Of(heals[0]));
    }

    [Fact]
    public void A_cure_on_full_health_is_charged_the_whole_base()
    {
        var none = Cure("Acidace", 0);
        Assert.Equal(232, Overcure.Fit(Session(none)).Of(none));
    }

    [Theory]
    [InlineData(174)] // Darkness: 0.75
    [InlineData(255)] // Auroras or Lightsday: 1.1
    [InlineData(150)] // Darkness on Darksday: 0.65
    [InlineData(313)] // Stellar Glare on Lightsday: 1.35
    public void A_day_or_weather_amount_healed_whole(double hp)
    {
        var dw = Cure("Svelty", hp);
        Assert.Equal(0, Overcure.Fit(Session(dw)).Of(dw));
    }

    [Fact]
    public void A_capped_cast_is_charged_against_the_plain_base_not_a_day_or_weather_amount()
    {
        // 200 is under 255 (1.1) and under 232: the rest of 232, not of 255.
        var short_ = Cure("Svelty", 200);
        Assert.Equal(32, Overcure.Fit(Session(short_)).Of(short_));
    }

    [Fact]
    public void A_cast_above_the_base_is_never_charged()
    {
        var more = Cure("Svelty", 240);
        Assert.Equal(0, Overcure.Fit(Session(more)).Of(more));
    }

    [Fact]
    public void Divine_seal_doubles_a_whole_cast_and_is_not_the_base()
    {
        // A sealed Curaga: twice the base on one, cut short on the other.
        var whole = Cure("Gillette", 464, action: "Curaga II", use: 900);
        // 400 is no sealed amount (300 would be: 2 × 150, Darkness on Darksday).
        var cut = Cure("Acidace", 400, action: "Curaga II", use: 900);
        var heals = new List<HealEvent>
        {
            Cure("Gillette", 232, action: "Curaga II", use: 901), Cure("Acidace", 232, action: "Curaga II", use: 901),
            Cure("Gillette", 232, action: "Curaga II", use: 902), Cure("Acidace", 232, action: "Curaga II", use: 902),
            whole, cut,
        };
        var fit = Overcure.Fit(heals);
        Assert.Equal(232, fit.Base("Federal", "Curaga II"));
        Assert.Equal(0, fit.Of(whole));
        Assert.Equal(64, fit.Of(cut));
    }

    [Fact]
    public void A_spell_with_no_base_has_no_overcure_and_is_left_out_of_the_share()
    {
        var once = Cure("Gillette", 500, action: "Cure V");
        var heals = Session(once, Cure("Acidace", 100));
        var fit = Overcure.Fit(heals);
        Assert.Null(fit.Base("Federal", "Cure V"));
        Assert.Null(fit.Of(once));

        var t = Healing.Totals(heals, fit);
        var cureV = t.Actors.Single().ActionList.Single(a => a.Name == "Cure V");
        Assert.Null(cureV.OvercurePct);
        Assert.Null(cureV.Base);
        // 132 over 6 × 232 + 100 healed and 132 more: Cure V's 500 is not in it.
        Assert.Equal(132, t.Overcure);
        Assert.Equal(132 / (6 * 232 + 100 + 132.0), t.OvercurePct!.Value, 9);
    }

    [Fact]
    public void A_targets_own_gear_makes_their_base_another()
    {
        // Svelty gets 243 (232 × 1.047) when whole: their gear, not a day or
        // weather. Too few of their casts for 243 to be a base of its own
        // (under 15 %: the rest were cut short), so it is read as gear.
        var cut = Cure("Svelty", 200);
        var heals = Session([Cure("Svelty", 243), Cure("Svelty", 243), Cure("Svelty", 243), cut,
                             .. Enumerable.Range(100, 18).Select(hp => Cure("Svelty", hp))]);
        var fit = Overcure.Fit(heals);
        Assert.Equal(232, fit.Base("Federal", "Cure III"));
        Assert.Equal(243, fit.Base("Federal", "Cure III", "Svelty"));
        Assert.Equal(43, fit.Of(cut));

        var heal = Healing.Totals(heals, fit).Actors.Single().ActionList.Single();
        Assert.Equal(243, heal.Targets.Single(p => p.Name == "Svelty").Base);
        // Everyone else's is the heal's own, and is not said again.
        Assert.Null(heal.Targets.Single(p => p.Name == "Gillette").Base);
    }

    [Fact]
    public void A_second_base_is_told_on_the_pointer()
    {
        var heals = Session(Cure("Gillette", 196), Cure("Gillette", 196), Cure("Acidace", 196), Cure("Acidace", 196));
        var fit = Overcure.Fit(heals);
        Assert.Equal(232, fit.Base("Federal", "Cure III"));
        Assert.Equal("Also 196", Overcure.Also(fit.Others("Federal", "Cure III")));
        Assert.Null(Overcure.Also([]));
    }

    [Fact]
    public void Heals_that_day_and_weather_do_not_move_have_only_their_base()
    {
        // A waltz lands on 174 three times: 0.75 of 232 is no day/weather
        // amount for it, so it is a cut-short cast.
        var cut = Cure("Svelty", 174, actor: "Esquire", action: "Curing Waltz III", via: "ability");
        var heals = new List<HealEvent>
        {
            Cure("Gillette", 232, actor: "Esquire", action: "Curing Waltz III", via: "ability"),
            Cure("Gillette", 232, actor: "Esquire", action: "Curing Waltz III", via: "ability"),
            Cure("Acidace", 232, actor: "Esquire", action: "Curing Waltz III", via: "ability"),
            Cure("Acidace", 232, actor: "Esquire", action: "Curing Waltz III", via: "ability"),
            cut,
        };
        Assert.Equal(58, Overcure.Fit(heals).Of(cut));
    }

    [Fact]
    public void Renamed_heals_keep_their_own_casters_charge()
    {
        // Federal and Esquire under one name (a job): each charged against
        // their own base, 232 and 300; one base for the two is none.
        var cut = Cure("Svelty", 250, actor: "Esquire");
        var heals = Session(Cure("Gillette", 300, actor: "Esquire"), Cure("Gillette", 300, actor: "Esquire"),
                            Cure("Acidace", 300, actor: "Esquire"), Cure("Acidace", 300, actor: "Esquire"),
                            Cure("Svelty", 200), cut);
        var fit = Overcure.Fit(heals);
        var renamed = heals.Select(h => h with { Actor = "WHM" }).ToList();
        var job = fit.Renamed(heals, renamed);
        Assert.Equal(32, job.Of(renamed[^2]));
        Assert.Equal(50, job.Of(renamed[^1]));
        Assert.Null(job.Of(cut));
        Assert.Null(job.Base("WHM", "Cure III"));

        // One character renamed: their own base under the new name.
        var one = heals.Where(h => h.Actor == "Federal").ToList();
        var matched = fit.Renamed(one, one.Select(h => h with { Actor = "Fed" }).ToList());
        Assert.Equal(232, matched.Base("Fed", "Cure III"));
    }

    [Fact]
    public void The_partys_healing_by_spell_counts_a_cast_and_its_mp_once()
    {
        var heals = new List<HealEvent>
        {
            Cure("Gillette", 232, action: "Curaga II", use: 800), Cure("Acidace", 232, action: "Curaga II", use: 800),
            Cure("Gillette", 232, actor: "Esquire", action: "Curaga II", use: 801),
        };
        var spell = Healing.Totals(heals).Spells.Single();
        Assert.Equal(("Curaga II", 2, 696.0), (spell.Name, spell.Casts, spell.Total));
        Assert.Equal(2 * ManaCost.Of(heals[0])!.Value, spell.Mp);
    }

    [Fact]
    public void Totals_carry_overcure_through_every_line()
    {
        var heals = Session(Cure("Acidace", 100), Cure("Gillette", 200));
        var t = Healing.Totals(heals, Overcure.Fit(heals));
        var federal = t.Actors.Single();
        var heal = federal.ActionList.Single();
        Assert.Equal(164, heal.Overcure);
        Assert.Equal(164, federal.Overcure);
        Assert.Equal(132, heal.Targets.Single(p => p.Name == "Acidace").Overcure);
        Assert.Equal(32, t.Targets.Single(p => p.Name == "Gillette").Overcure);
        // By caster: Gillette under Federal, Cure III under her, with its base.
        var onGillette = federal.Targets.Single(p => p.Name == "Gillette").Parts.Single();
        Assert.Equal(32, onGillette.Overcure);
        Assert.Equal(232, onGillette.Base);
        // Without bases, nothing.
        Assert.Null(Healing.Totals(heals).OvercurePct);
    }
}
