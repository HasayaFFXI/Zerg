using Zerg.Core.Layout;

namespace Zerg.Core.Tests;

/// <summary>The tables a floating panel chooses its columns from.</summary>
public class PanelColumnsTests
{
    static string[] Shown(ColumnLayout l) =>
        [.. Enumerable.Range(0, l.Count).Select(l.At).Where(c => l.Shown(c) && !l.Table.All[c].Fixed)
            .Select(c => l.Table.All[c].Key)];

    [Fact]
    public void The_strips_have_every_column_of_their_tables_in_the_same_places()
    {
        Assert.Equal(CharacterColumns.Table.All.Select(c => c.Key), Tables.Strip.All.Select(c => c.Key));
        Assert.Equal(Tables.Healers.All.Select(c => c.Key), Tables.HealStrip.All.Select(c => c.Key));
        // The groups move as one, as in the table.
        Assert.Equal(CharacterColumns.Table.Installed.Where(k => k != "job"), Tables.Strip.Installed);
    }

    [Fact]
    public void A_strip_shows_as_installed_what_it_showed_before_it_had_columns_to_choose()
    {
        Assert.Equal(["damage", "share", "accuracy", "crit", "delay", "rangedAccuracy", "rangedCrit", "deaths"],
                     Shown(Tables.Strip.Default));
        Assert.Equal(["healing", "share", "casts", "mp", "perMp", "overcurePct"], Shown(Tables.HealStrip.Default));
        Assert.True(Tables.Strip.Default.IsDefault);
    }

    [Fact]
    public void A_strips_job_stands_beside_the_name_and_is_never_listed()
    {
        Assert.Equal(2, Tables.Strip.Lead);
        Assert.Equal(2, Tables.HealStrip.Lead);
        Assert.Null(Tables.Strip.Unit("job"));
        // Hidden by name, it is shown all the same: a fixed column always is.
        Assert.True(Tables.Strip.Lay(null, ["job"]).Shown(Tables.Strip.PlaceOf("job")));
    }

    [Fact]
    public void Every_column_can_be_added_to_a_strip()
    {
        var all = Tables.Strip.Lay(null, []);
        Assert.Equal(17, Shown(all).Length);
        Assert.True(all.Shows("dps") && all.Shows("wsDamage") && all.Shows("petAccuracy"));
    }

    [Fact]
    public void A_strip_too_narrow_drops_the_columns_at_the_end_first()
    {
        // 1*110,0,72,52,... : Character, Damage and % need 234; with every
        // column as installed 572.
        const string widths = "1*110,0,72,52,56,56,56,58,62,66,72,66,50,62,72,50,74,68,40";
        var l = Tables.Strip.Default;
        Assert.Empty(l.Overflow(widths, 572));
        Assert.Equal([Tables.Strip.PlaceOf("deaths")], l.Overflow(widths, 571));
        var narrow = l.Overflow(widths, 250).Select(c => Tables.Strip.All[c].Key).ToHashSet();
        Assert.True(narrow.SetEquals(["accuracy", "crit", "delay", "rangedAccuracy", "rangedCrit", "deaths"]));
        // Moved to the end, the share goes first.
        var moved = Tables.Strip.Lay(Tables.Strip.Move(l.Order, "share", 99), l.Hidden);
        Assert.Equal([Tables.Strip.PlaceOf("share")], moved.Overflow(widths, 571));
        // A room not known yet drops nothing; nor do widths of another table.
        Assert.Empty(l.Overflow(widths, 0));
        Assert.Empty(l.Overflow("1*110,72", 10));
    }

    [Fact]
    public void A_hit_list_keeps_its_time_first_and_moves_the_rest()
    {
        Assert.Equal(["action", "amount", "mark", "vsAvg"], Tables.Hits.Installed);
        Assert.Equal("Healing", Tables.Casts.Unit("amount")!.Name);
        var moved = Tables.Hits.Lay(Tables.Hits.Move(Tables.Hits.Installed, "vsAvg", 0), ["mark"]);
        Assert.Equal(0, moved.At(0));
        Assert.Equal(Tables.Hits.PlaceOf("vsAvg"), moved.At(1));
        Assert.False(moved.Shows("mark"));
    }
}
