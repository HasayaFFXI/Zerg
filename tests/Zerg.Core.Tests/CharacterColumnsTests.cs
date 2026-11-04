using Zerg.Core.Layout;

namespace Zerg.Core.Tests;

public class CharacterColumnsTests
{
    [Fact]
    public void Without_drops_the_columns_at_the_places_named()
    {
        Assert.Equal("a,c", Columns.Without("a, b, c, d", [1, 3]));
        Assert.Equal("a,b", Columns.Without("a,b", [7]));
        Assert.Equal("", Columns.Without(null, [0]));
    }

    [Fact]
    public void With_nothing_hidden_the_table_needs_every_column()
    {
        Assert.Equal(CharacterColumns.Shaded, CharacterColumns.Full(true, []));
        Assert.Equal(CharacterColumns.Unshaded, CharacterColumns.Full(false, []));
        Assert.Equal(19, Columns.Parse(CharacterColumns.Shaded).Length);
        Assert.Equal(19, Columns.Parse(CharacterColumns.Unshaded).Length);
    }

    [Fact]
    public void The_first_step_keeps_the_name_the_six_figures_and_the_weaponskill_four()
    {
        Assert.Equal("134*90,70*62,68*64,64*62,58*58,62*56,58*54,58*54,68*62,52*52,54*56,58*56",
                     CharacterColumns.Less(true, []));
        Assert.Equal("94*90,70*62,68*64,104*100,58*58,62*56,58*54,58*54,68*62,52*52,54*56,58*56",
                     CharacterColumns.Less(false, []));
    }

    [Fact]
    public void A_hidden_group_needs_no_room_in_either_step()
    {
        var full = Columns.Least(CharacterColumns.Full(true, []));
        Assert.Equal(full - 56 - 54, Columns.Least(CharacterColumns.Full(true, [CharacterColumns.Ranged])));
        Assert.Equal(full - 62 - 52 - 56 - 56,
                     Columns.Least(CharacterColumns.Full(true, [CharacterColumns.Weaponskill])));

        // The weaponskill four are in the second step; the others have gone already.
        var less = Columns.Least(CharacterColumns.Less(true, []));
        Assert.Equal(less - 62 - 52 - 56 - 56,
                     Columns.Least(CharacterColumns.Less(true, [CharacterColumns.Weaponskill])));
        Assert.Equal(less, Columns.Least(CharacterColumns.Less(true, [CharacterColumns.Pet, CharacterColumns.Skillchain])));
    }

    [Fact]
    public void Every_group_hidden_leaves_the_eight_and_deaths()
    {
        Assert.Equal("134*90,70*62,68*64,64*62,58*58,62*56,58*54,58*54,56*54",
                     CharacterColumns.Full(true, CharacterColumns.Groups));
    }

    [Fact]
    public void A_key_that_is_not_a_group_hides_nothing()
    {
        Assert.Equal(CharacterColumns.Shaded, CharacterColumns.Full(true, ["deaths", "", "Ranged"]));
    }
}
