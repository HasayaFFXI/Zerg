using Zerg.Core;

namespace Zerg.Core.Tests;

/// <summary>MP Spent and HP/MP (RULES.md, "MP Spent and HP/MP"): a heal's
/// list cost by its via and id, charged once a cast, and HP/MP over the
/// heals that cost MP alone.</summary>
public class ManaCostTests
{
    static HealEvent Heal(double use, string actor, string action, double id, string target, double hp,
                          string via = "magic", string? owner = null) =>
        new()
        {
            T = use * 1000, Use = use, Via = via, Actor = actor, ActorKind = owner != null ? "pet" : "player",
            Action = action, ActionId = id, Target = target, TargetKind = "player", Hp = hp, Owner = owner,
        };

    [Theory]
    [InlineData("magic", 4, 88.0)]   // Cure IV
    [InlineData("magic", 8, 120.0)]  // Curaga II
    [InlineData("magic", 578, 37.0)] // Wild Carrot
    [InlineData("pet", 512, 6.0)]    // Healing Ruby
    [InlineData("pet", 517, 124.0)]  // Healing Ruby II
    public void A_heal_costs_its_list_cost(string via, double id, double mp) =>
        Assert.Equal(mp, ManaCost.Of(via, id));

    [Theory]
    [InlineData("ability", 550)] // Chakra, by its ability id
    [InlineData("ability", 702)] // Curing Waltz: TP
    [InlineData("pet", 640)]     // Healing Breath
    [InlineData("magic", 711)]   // Restoral: not in the server's tables
    [InlineData("magic", 9999)]
    [InlineData("magic", 4.5)]
    [InlineData("pet", 4)]       // a spell's id on a pet's heal is not the spell
    public void A_heal_with_no_known_cost_has_none(string via, double id) =>
        Assert.Null(ManaCost.Of(via, id));

    [Fact]
    public void A_curaga_is_charged_once_however_many_it_reached()
    {
        var t = Healing.Totals(
        [
            Heal(1, "Mage", "Curaga II", 8, "A", 200),
            Heal(1, "Mage", "Curaga II", 8, "B", 200),
            Heal(1, "Mage", "Curaga II", 8, "C", 200),
            Heal(2, "Mage", "Cure IV", 4, "A", 440),
        ]);
        var mage = t.Actors.Single();
        Assert.Equal(208, mage.Mp);
        Assert.Equal(1040.0 / 208, mage.PerMp);
        var curaga = mage.Actions["Curaga II"];
        Assert.Equal(120, curaga.Mp);
        Assert.Equal(5, curaga.PerMp);
        Assert.Equal(208, t.Mp);
        Assert.Equal(1040.0 / 208, t.PerMp);
    }

    [Fact]
    public void On_a_person_a_casts_cost_is_shared_among_everyone_it_reached()
    {
        var t = Healing.Totals(
        [
            Heal(1, "Mage", "Curaga II", 8, "A", 300),
            Heal(1, "Mage", "Curaga II", 8, "B", 0), // at full health: wasted
            Heal(1, "Mage", "Curaga II", 8, "C", 60),
        ]);
        var curaga = t.Actors.Single().Actions["Curaga II"];
        var b = curaga.Targets.Single(p => p.Name == "B");
        Assert.Equal(40, b.Mp);
        Assert.Equal(0, b.PerMp);
        Assert.Equal(7.5, curaga.Targets.Single(p => p.Name == "A").PerMp);
        // The people add up to the cast.
        Assert.Equal(120, curaga.Targets.Sum(p => p.Mp));
        // The same from the other end: everything that reached B, by whom, with what.
        var healedB = t.Targets.Single(p => p.Name == "B");
        Assert.Equal(40, healedB.Mp);
        Assert.Equal(40, healedB.Parts.Single().Parts.Single().Mp);
        Assert.Equal(40, t.Actors.Single().Targets.Single(p => p.Name == "B").Mp);
    }

    [Fact]
    public void A_free_heal_moves_neither_side()
    {
        var t = Healing.Totals(
        [
            Heal(1, "Monk", "Chakra", 550, "Monk", 600, via: "ability"),
            Heal(2, "Monk", "Cure", 1, "Monk", 40),
        ]);
        var monk = t.Actors.Single();
        Assert.Equal(640, monk.Total);
        Assert.Equal(8, monk.Mp);
        Assert.Equal(5, monk.PerMp);
        var chakra = monk.Actions["Chakra"];
        Assert.Equal(0, chakra.Mp);
        Assert.Null(chakra.PerMp);
    }

    [Fact]
    public void A_healer_whose_heals_cost_nothing_has_no_hp_per_mp()
    {
        var t = Healing.Totals([Heal(1, "Drake", "Healing Breath", 640, "Lancer", 120, via: "pet", owner: "Lancer")]);
        Assert.Equal(0, t.Mp);
        Assert.Null(t.PerMp);
        Assert.All(t.Actors, a => Assert.Null(a.PerMp));
    }

    [Fact]
    public void A_blood_pact_is_charged_to_its_summoner()
    {
        var heals = Healing.Filter(
        [
            Heal(1, "Carbuncle", "Healing Ruby II", 517, "A", 310, via: "pet", owner: "Caller"),
            Heal(1, "Carbuncle", "Healing Ruby II", 517, "B", 310, via: "pet", owner: "Caller"),
        ]);
        var caller = Healing.Totals(heals).Actors.Single();
        Assert.Equal("Caller", caller.Name);
        Assert.Equal(124, caller.Mp);
        Assert.Equal(620.0 / 124, caller.PerMp);
    }

    [Fact]
    public void A_spell_a_pet_cast_costs_its_owner_nothing()
    {
        // An automaton's Cure IV comes out of the automaton's MP, not the puppetmaster's.
        var heals = Healing.Filter(
        [
            Heal(1, "Robo", "Cure IV", 4, "Tank", 450, owner: "Puppeteer"),
            Heal(2, "Robo", "Cure IV", 4, "Tank", 450, owner: "Puppeteer"),
        ]);
        var t = Healing.Totals(heals);
        var owner = t.Actors.Single();
        Assert.Equal("Puppeteer", owner.Name);
        Assert.Equal(900, owner.Total);
        Assert.Equal(0, owner.Mp);
        Assert.Null(owner.PerMp);
        Assert.Null(owner.ActionList.Single().PerMp);
        Assert.Null(owner.Targets.Single().PerMp);
        Assert.Null(t.PerMp);
    }

    [Fact]
    public void A_pets_spell_moves_neither_side_of_its_owners_own_cures()
    {
        var heals = Healing.Filter(
        [
            Heal(1, "Robo", "Cure IV", 4, "Tank", 450, owner: "Puppeteer"),
            Heal(2, "Puppeteer", "Cure", 1, "Tank", 40),
        ]);
        var owner = Healing.Totals(heals).Actors.Single();
        Assert.Equal(8, owner.Mp);
        Assert.Equal(5, owner.PerMp);
    }

    [Fact]
    public void A_second_result_on_one_person_is_not_charged_again()
    {
        var t = Healing.Totals(
        [
            Heal(1, "Mage", "Curaga", 7, "A", 100),
            Heal(1, "Mage", "Curaga", 7, "A", 20),
            Heal(1, "Mage", "Curaga", 7, "B", 80),
        ]);
        var curaga = t.Actors.Single().Actions["Curaga"];
        Assert.Equal(60, curaga.Mp);
        Assert.Equal(30, curaga.Targets.Single(p => p.Name == "A").Mp);
        Assert.Equal(4, curaga.Targets.Single(p => p.Name == "A").PerMp);
    }

    [Fact]
    public void A_heal_with_no_use_is_a_cast_of_its_own()
    {
        var a = Heal(1, "Mage", "Cure II", 2, "A", 90) with { Use = null };
        var b = Heal(2, "Mage", "Cure II", 2, "A", 90) with { Use = null };
        Assert.Equal(48, Healing.Totals([a, b]).Mp);
    }
}
