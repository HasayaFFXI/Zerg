using Zerg.Core.Layout;

namespace Zerg.Core.Tests;

public class CharacterColumnsTests
{
    static ColumnTable T => CharacterColumns.Table;

    [Fact]
    public void Zeroed_makes_the_columns_at_the_places_named_take_no_room()
    {
        Assert.Equal("a,0,c,0", Columns.Zeroed("a, b, c, d", [1, 3]));
        Assert.Equal("a,b", Columns.Zeroed("a,b", [7]));
        Assert.Equal("", Columns.Zeroed(null, [0]));
        Assert.Equal(52, Columns.Least("20,0,32"));
    }

    [Fact]
    public void The_table_has_a_column_for_every_width()
    {
        Assert.Equal(19, Columns.Parse(CharacterColumns.Shaded).Length);
        Assert.Equal(19, Columns.Parse(CharacterColumns.Unshaded).Length);
        Assert.Equal(19, T.All.Count);
        Assert.Equal(19, Columns.Parse(CharacterColumns.Less(true)).Length);
    }

    [Fact]
    public void The_first_step_keeps_the_name_the_six_figures_and_the_weaponskill_four()
    {
        Assert.Equal("134*90,70*62,68*64,64*62,58*58,62*56,58*54,58*54,0,0,68*62,52*52,54*56,58*56,0,0,0,0,0",
                     CharacterColumns.Less(true));
        Assert.Equal("94*90,70*62,68*64,104*100,58*58,62*56,58*54,58*54,0,0,68*62,52*52,54*56,58*56,0,0,0,0,0",
                     CharacterColumns.Less(false));
    }

    [Fact]
    public void A_hidden_group_needs_no_room_in_either_step()
    {
        var full = Columns.Least(CharacterColumns.Shaded);
        var ranged = T.Lay(null, ["ranged"]);
        Assert.Equal(full - 56 - 54, Columns.Least(ranged.Needs(CharacterColumns.Shaded)));
        var ws = T.Lay(null, ["weaponskill"]);
        Assert.Equal(full - 62 - 52 - 56 - 56, Columns.Least(ws.Needs(CharacterColumns.Shaded)));

        // The weaponskill four are in the second step; the others have gone already.
        var less = Columns.Least(CharacterColumns.Less(true));
        Assert.Equal(less - 62 - 52 - 56 - 56, Columns.Least(ws.Needs(CharacterColumns.Less(true))));
        Assert.Equal(less, Columns.Least(T.Lay(null, ["pet", "skillchain"]).Needs(CharacterColumns.Less(true))));
    }

    [Fact]
    public void A_single_column_hidden_needs_no_room()
    {
        var full = Columns.Least(CharacterColumns.Shaded);
        Assert.Equal(full - 54, Columns.Least(T.Lay(null, ["delay"]).Needs(CharacterColumns.Shaded)));
        Assert.Equal(full - 52, Columns.Least(T.Lay(null, ["wsAvg"]).Needs(CharacterColumns.Shaded)));
    }

    [Fact]
    public void The_groups_are_four_units_each_its_columns_together()
    {
        Assert.Equal(["job", "damage", "share", "dps", "accuracy", "crit", "delay",
                      "ranged", "weaponskill", "skillchain", "pet", "deaths"], T.Installed);
        Assert.Equal([10, 11, 12, 13], T.Unit("weaponskill")!.Columns);
        Assert.True(T.Unit("weaponskill")!.IsGroup);
        Assert.Equal("Weaponskill", T.Unit("weaponskill")!.Name);
        Assert.False(T.Unit("crit")!.IsGroup);
        Assert.Equal("weaponskill", T.UnitOf(12)!.Key);
        Assert.Null(T.UnitOf(0));
    }
}
