namespace Zerg.Core.Tests;

public class OrderingTests
{
    // ------------------------------------------------------------- presses

    [Fact]
    public void A_figure_goes_largest_first_then_smallest_first_then_back_to_the_table_s_own()
    {
        var one = TableOrder.Press(null, "damage", words: false);
        Assert.Equal(new TableOrder("damage", true), one);
        var two = TableOrder.Press(one, "damage", words: false);
        Assert.Equal(new TableOrder("damage", false), two);
        Assert.Null(TableOrder.Press(two, "damage", words: false));
    }

    [Fact]
    public void Words_go_A_to_Z_first()
    {
        var one = TableOrder.Press(null, "name", words: true);
        Assert.Equal(new TableOrder("name", false), one);
        var two = TableOrder.Press(one, "name", words: true);
        Assert.Equal(new TableOrder("name", true), two);
        Assert.Null(TableOrder.Press(two, "name", words: true));
    }

    [Fact]
    public void Another_column_starts_at_its_own_first()
    {
        var byDamage = new TableOrder("damage", false);
        Assert.Equal(new TableOrder("dps", true), TableOrder.Press(byDamage, "dps", words: false));
        Assert.Equal(new TableOrder("name", false), TableOrder.Press(byDamage, "name", words: true));
    }

    // ------------------------------------------------------------ the file

    [Fact]
    public void An_order_is_written_as_its_column_and_a_way_and_read_back()
    {
        Assert.Equal("wsAvg desc", new TableOrder("wsAvg", true).ToString());
        Assert.Equal("name asc", new TableOrder("name", false).ToString());
        Assert.Equal(new TableOrder("wsAvg", true), TableOrder.Parse("wsAvg desc"));
        Assert.Equal(new TableOrder("name", false), TableOrder.Parse("name ASC"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("damage")]
    [InlineData("damage up")]
    [InlineData("damage desc more")]
    public void Anything_else_is_the_table_s_own_order(string? text) => Assert.Null(TableOrder.Parse(text));

    // ------------------------------------------------------------ ordering

    sealed record R(string Name, double? Figure, string? Job = null);

    static readonly SortKey<R>? Unknown = null;

    static SortKey<R>? Keys(string column) => column switch
    {
        "figure" => SortKey<R>.Of(r => r.Figure),
        "name" => SortKey<R>.Of(r => r.Name),
        "job" => SortKey<R>.Of(r => r.Job),
        _ => Unknown,
    };

    static string Names(IEnumerable<R> rows) => string.Join(" ", rows.Select(r => r.Name));

    // In the table's own order: the largest first.
    static readonly R[] Table =
    [
        new("Ayame", 300, "SAM"),
        new("Bo", null, "WHM"),
        new("cid", 200),
        new("Dee", 200, "BLM"),
        new("Eve", double.NaN, "SAM"),
        new("Fay", 100, "BRD"),
    ];

    [Fact]
    public void No_order_and_a_column_the_table_does_not_have_leave_the_table_s_own()
    {
        Assert.Equal("Ayame Bo cid Dee Eve Fay", Names(Ordering.Apply(Table, null, Keys)));
        Assert.Equal("Ayame Bo cid Dee Eve Fay", Names(Ordering.Apply(Table, new TableOrder("gone", true), Keys)));
    }

    [Fact]
    public void Equal_figures_keep_their_order_and_a_dash_goes_last_either_way()
    {
        Assert.Equal("Ayame cid Dee Fay Bo Eve", Names(Ordering.Apply(Table, new TableOrder("figure", true), Keys)));
        Assert.Equal("Fay cid Dee Ayame Bo Eve", Names(Ordering.Apply(Table, new TableOrder("figure", false), Keys)));
    }

    [Fact]
    public void Words_go_without_regard_to_case_and_none_go_last()
    {
        Assert.Equal("Ayame Bo cid Dee Eve Fay", Names(Ordering.Apply(Table, new TableOrder("name", false), Keys)));
        Assert.Equal("Fay Eve Dee cid Bo Ayame", Names(Ordering.Apply(Table, new TableOrder("name", true), Keys)));
        // Two samurai stay in the table's order, and cid, with no job, is last.
        Assert.Equal("Dee Fay Ayame Eve Bo cid", Names(Ordering.Apply(Table, new TableOrder("job", false), Keys)));
        Assert.Equal("Bo Ayame Eve Fay Dee cid", Names(Ordering.Apply(Table, new TableOrder("job", true), Keys)));
    }
}
