using Zerg.Core.Layout;

namespace Zerg.Core.Tests;

public class ColumnTableTests
{
    // Name, A, a group G of two (G1, G2), B, and a blank column that ends the row.
    static readonly ColumnTable T = new("t",
    [
        new("name", "Name", Fixed: true),
        new("a", "A"),
        new("g1", "One", "g"),
        new("g2", "Two", "g"),
        new("b", "B"),
        new("end", "", Fixed: true),
    ], new Dictionary<string, string> { ["g"] = "Group" });

    static int[] Visual(ColumnLayout l) => [.. Enumerable.Range(0, l.Count).Select(l.At)];

    [Fact]
    public void As_installed_the_table_is_as_written()
    {
        Assert.Equal(["a", "g", "b"], T.Installed);
        Assert.Equal([0, 1, 2, 3, 4, 5], Visual(T.Default));
        Assert.True(T.Default.IsDefault);
        Assert.Empty(T.Default.Hidden);
        Assert.Equal(1, T.Lead);
    }

    [Fact]
    public void A_group_moves_as_one_and_the_fixed_columns_keep_their_places()
    {
        var order = T.Move(null, "g", 0);
        Assert.Equal(["g", "a", "b"], order);
        var l = T.Lay(order, null);
        Assert.Equal([0, 2, 3, 1, 4, 5], Visual(l));
        Assert.Equal(1, l.SlotOf(2));
        Assert.Equal(3, l.SlotOf(1));
        Assert.Equal(["g", "a", "b"], l.Order);
        Assert.False(l.IsDefault);
    }

    [Fact]
    public void A_move_is_to_a_place_among_the_others()
    {
        Assert.Equal(["g", "b", "a"], T.Move(null, "a", 2));
        Assert.Equal(["g", "b", "a"], T.Move(null, "a", 99));
        Assert.Equal(["a", "g", "b"], T.Move(null, "a", 0));
        Assert.Equal(["b", "a", "g"], T.Move(null, "b", -3));
        // A column inside a group, a fixed column or nothing: no move.
        Assert.Equal(["a", "g", "b"], T.Move(null, "g1", 0));
        Assert.Equal(["a", "g", "b"], T.Move(null, "name", 2));
        Assert.Equal(["a", "g", "b"], T.Move(null, "zzz", 2));
    }

    [Fact]
    public void Repair_lets_go_of_what_the_table_has_not_got_and_puts_back_what_is_missing()
    {
        Assert.Equal(["b", "a", "g"], T.Repair(["b", "zzz", "a", "b", "g1", "name", "g"]));
        // G left out: after A, the unit before it as installed.
        Assert.Equal(["b", "a", "g"], T.Repair(["b", "a"]));
        // A left out: first, as nothing is before it.
        Assert.Equal(["a", "b", "g"], T.Repair(["b", "g"]));
        Assert.Equal(T.Installed, T.Repair([]));
        Assert.Equal(T.Installed, T.Repair(null));
    }

    [Fact]
    public void Hidden_names_columns_or_a_whole_group()
    {
        var l = T.Lay(null, ["g", "zzz", "name", "end"]);
        Assert.True(l.Shown(0));
        Assert.True(l.Shown(1));
        Assert.False(l.Shown(2));
        Assert.False(l.Shown(3));
        Assert.True(l.Shown(5));
        Assert.Equal(["g1", "g2"], l.Hidden);
        Assert.False(l.Shows("g"));

        var one = T.Lay(null, ["g2"]);
        Assert.True(one.Shows("g"));
        Assert.False(one.Shows("g2"));
        Assert.Equal("20,30,40,0,60,8", one.Needs("20,30,40,50,60,8"));
    }

    [Fact]
    public void After_the_lead_is_the_first_shown_column_after_the_fixed_ones()
    {
        Assert.Equal("a", T.Default.AfterLead);
        Assert.Equal("g1", T.Lay(["g", "a", "b"], null).AfterLead);
        Assert.Equal("b", T.Lay(null, ["a", "g"]).AfterLead);
        Assert.Equal("end", T.Lay(null, ["a", "g", "b"]).AfterLead);
    }

    [Fact]
    public void A_fixed_column_among_the_movable_ones_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new ColumnTable("x",
            [new("a", "A"), new("f", "F", Fixed: true), new("b", "B")]));
    }

    [Fact]
    public void The_tables_have_their_names_fixed_first()
    {
        foreach (var t in new[] { CharacterColumns.Table, Tables.Actions, Tables.Targets, Tables.Types,
                                  Tables.Fights, Tables.Healers, Tables.Heals, Tables.Support })
        {
            Assert.True(t.All[0].Fixed, t.Key);
            Assert.Equal(1, t.Lead);
        }
        Assert.Equal(["damage", "share"], Tables.Types.Installed);
    }
}
