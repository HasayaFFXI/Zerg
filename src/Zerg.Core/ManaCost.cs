namespace Zerg.Core;

/// <summary>
/// What a heal costs in MP, by what its line already says: <c>via</c> and
/// <c>actionId</c>. The addon writes no cost, and none is needed: a spell's
/// cost is its list cost, the same for everyone who casts it.
///
/// <para>The costs are the server's own: spells from LandSandBoat's
/// <c>sql/spell_list.sql</c> (its <c>mpCost</c>), Blood Pacts from
/// <c>scripts/globals/job_utils/summoner.lua</c> (<c>baseMPCostMap</c>).
/// The ids are the addon's healing ids (<c>vx_enums.lua</c>,
/// <c>E.HealingSpells</c> and <c>E.PetHealing</c>): a spell's is the
/// spell's, a pet's the server's ability id.</para>
///
/// <para><b>The list cost, not what was spent.</b> Conserve MP, Manafont
/// and Apogee change what a cast cost, and the packet says none of it. A
/// heal not here has no cost: a job ability (Chakra, Reward), a waltz (TP),
/// a wyvern's breath, and Restoral, which the server's tables do not have.
/// Nor has a spell a pet cast: an automaton pays for its Cure from its own
/// MP (<see cref="Of(HealEvent)"/>).
/// RULES.md, "MP Spent and HP/MP".</para>
/// </summary>
public static class ManaCost
{
    static readonly Dictionary<int, double> Spells = new()
    {
        [1] = 8,     // Cure
        [2] = 24,    // Cure II
        [3] = 46,    // Cure III
        [4] = 88,    // Cure IV
        [5] = 135,   // Cure V
        [6] = 227,   // Cure VI
        [7] = 60,    // Curaga
        [8] = 120,   // Curaga II
        [9] = 180,   // Curaga III
        [10] = 260,  // Curaga IV
        [11] = 380,  // Curaga V
        [549] = 8,   // Pollen
        [578] = 37,  // Wild Carrot
        [581] = 55,  // Healing Breeze
        [593] = 72,  // Magic Fruit
        [645] = 40,  // Exuviation
        [658] = 106, // Plenilune Embrace
        [690] = 145, // White Wind
    };

    /// <summary>Blood Pacts, which cost the summoner's MP.</summary>
    static readonly Dictionary<int, double> Pacts = new()
    {
        [512] = 6,   // Healing Ruby
        [517] = 124, // Healing Ruby II
        [594] = 119, // Whispering Wind
    };

    /// <summary>One cast's MP, or null for a heal that costs none or isn't known.</summary>
    public static double? Of(string via, double actionId)
    {
        if (double.IsNaN(actionId) || actionId != Math.Floor(actionId) || actionId is < 0 or > int.MaxValue) return null;
        var table = via switch
        {
            "magic" => Spells,
            "pet" => Pacts,
            _ => null,
        };
        return table != null && table.TryGetValue((int)actionId, out var mp) ? mp : null;
    }

    /// <summary>The heal's cost to the character it is credited to, by its
    /// line. A spell a pet cast (an automaton's Cure, a jug pet's) is paid
    /// from the pet's own MP, not the player's: none. A Blood Pact is the
    /// one pet heal the player pays for.</summary>
    public static double? Of(HealEvent h) => h.IsPets && h.Via == "magic" ? null : Of(h.Via, h.ActionId);

    /// <summary>HP healed per MP spent, or null with no MP spent.</summary>
    public static double? PerMp(double hp, double mp) => mp > 0 ? hp / mp : null;
}
