namespace Zerg.Core.Tests;

/// <summary>The Overview's tick boxes over the character and target filters.
/// Written By character: the outer filter is the characters', the inner the targets'.</summary>
public class OverviewTicksTests
{
    static HashSet<string> S(params string[] keys) => new(keys, StringComparer.Ordinal);

    static void Is((HashSet<string> Outer, HashSet<string> Inner) got, string[] outer, string[] inner)
    {
        Assert.Equal(outer.Order(), got.Outer.Order());
        Assert.Equal(inner.Order(), got.Inner.Order());
    }

    [Fact]
    public void Nothing_isolated_counts_no_pair()
    {
        Assert.False(OverviewTicks.Counts(S(), S(), "Hasaya", "Kirin"));
        Assert.True(OverviewTicks.Counts(S("Hasaya"), S(), "Hasaya", "Kirin"));
        Assert.True(OverviewTicks.Counts(S(), S("Kirin"), "Rook", "Kirin"));
        Assert.False(OverviewTicks.Counts(S("Hasaya"), S("Kirin"), "Hasaya", "Genbu"));
    }

    [Fact]
    public void A_heading_is_all_while_every_row_under_it_counts()
    {
        Func<IReadOnlyCollection<string>> rows = () => S("Kirin", "Genbu");
        Assert.Equal(TickState.All, OverviewTicks.Heading(S("Hasaya"), S(), "Hasaya", rows));
        Assert.Equal(TickState.None, OverviewTicks.Heading(S("Rook"), S(), "Hasaya", rows));
        Assert.Equal(TickState.Some, OverviewTicks.Heading(S("Hasaya"), S("Kirin"), "Hasaya", rows));
        Assert.Equal(TickState.All, OverviewTicks.Heading(S("Hasaya"), S("Kirin", "Genbu"), "Hasaya", rows));
        Assert.Equal(TickState.None, OverviewTicks.Heading(S("Rook"), S("Kirin"), "Hasaya", rows));
        // A target isolated and nobody: everyone who hit it is counted there.
        Assert.Equal(TickState.Some, OverviewTicks.Heading(S(), S("Kirin"), "Rook", rows));
        Assert.Equal(TickState.None, OverviewTicks.Heading(S(), S("Byakko"), "Rook", rows));
        Assert.Equal(TickState.None, OverviewTicks.Heading(S(), S(), "Hasaya", rows));
    }

    [Fact]
    public void A_heading_adds_to_the_characters_and_lets_go_the_targets()
    {
        Is(OverviewTicks.PressHeading(S(), S(), "Hasaya", TickState.None), ["Hasaya"], []);
        Is(OverviewTicks.PressHeading(S("Rook"), S(), "Hasaya", TickState.None), ["Hasaya", "Rook"], []);
        Is(OverviewTicks.PressHeading(S("Hasaya", "Rook"), S(), "Hasaya", TickState.All), ["Rook"], []);
        // Part counted: all of it, and nothing else.
        Is(OverviewTicks.PressHeading(S("Hasaya", "Rook"), S("Kirin"), "Hasaya", TickState.Some), ["Hasaya"], []);
        Is(OverviewTicks.PressHeading(S(), S("Kirin"), "Hasaya", TickState.Some), ["Hasaya"], []);
    }

    [Fact]
    public void A_heading_all_counted_beside_targets_is_let_go()
    {
        Is(OverviewTicks.PressHeading(S("Hasaya", "Rook"), S("Kirin"), "Hasaya", TickState.All), ["Rook"], ["Kirin"]);
        // Alone: the targets go with it, or everyone at them would count.
        Is(OverviewTicks.PressHeading(S("Hasaya"), S("Kirin", "Genbu"), "Hasaya", TickState.All), [], []);
    }

    [Fact]
    public void A_pair_alone_is_both_filters()
    {
        Is(OverviewTicks.PressPair(S(), S(), "Hasaya", "Kirin", [], []), ["Hasaya"], ["Kirin"]);
        // Sharing nothing with what is isolated: it starts again.
        Is(OverviewTicks.PressPair(S("Rook"), S("Genbu"), "Hasaya", "Kirin", [], []), ["Hasaya"], ["Kirin"]);
        Is(OverviewTicks.PressPair(S(), S("Genbu"), "Hasaya", "Kirin", [], []), ["Hasaya"], ["Kirin"]);
    }

    [Fact]
    public void A_pair_sharing_a_side_widens_the_block()
    {
        Is(OverviewTicks.PressPair(S("Hasaya"), S("Kirin"), "Hasaya", "Genbu", [], []), ["Hasaya"], ["Genbu", "Kirin"]);
        Is(OverviewTicks.PressPair(S("Hasaya"), S("Kirin"), "Rook", "Kirin", [], []), ["Hasaya", "Rook"], ["Kirin"]);
    }

    [Fact]
    public void Letting_a_pair_go_takes_as_little_with_it_as_it_can()
    {
        // One target of several.
        Is(OverviewTicks.PressPair(S("Hasaya"), S("Genbu", "Kirin"), "Hasaya", "Kirin", [], []), ["Hasaya"], ["Genbu"]);
        // One character of several at one target.
        Is(OverviewTicks.PressPair(S("Hasaya", "Rook"), S("Kirin"), "Hasaya", "Kirin", [], []), ["Rook"], ["Kirin"]);
        // The last pair: nothing is isolated.
        Is(OverviewTicks.PressPair(S("Hasaya"), S("Kirin"), "Hasaya", "Kirin", [], []), [], []);
        // Several characters, every target: that character goes.
        Is(OverviewTicks.PressPair(S("Hasaya", "Rook"), S(), "Hasaya", "Kirin", [], []), ["Rook"], []);
    }

    [Fact]
    public void Unticking_a_row_of_a_ticked_heading_leaves_the_rest()
    {
        Is(OverviewTicks.PressPair(S("Hasaya"), S(), "Hasaya", "Kirin", ["Kirin", "Genbu", "Byakko"], []),
           ["Hasaya"], ["Byakko", "Genbu"]);
        // Its only row: nothing left.
        Is(OverviewTicks.PressPair(S("Hasaya"), S(), "Hasaya", "Kirin", ["Kirin"], []), [], []);
        // A target isolated alone (By target, its heading ticked): everyone else at it.
        Is(OverviewTicks.PressPair(S(), S("Kirin"), "Hasaya", "Kirin", [], ["Hasaya", "Rook"]), ["Rook"], ["Kirin"]);
        Is(OverviewTicks.PressPair(S(), S("Kirin"), "Hasaya", "Kirin", [], ["Hasaya"]), [], []);
    }
}
