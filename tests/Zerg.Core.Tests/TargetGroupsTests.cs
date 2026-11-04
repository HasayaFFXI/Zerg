namespace Zerg.Core.Tests;

/// <summary>The damage grouped by target: the Overview pane's By target tab.</summary>
public class TargetGroupsTests
{
    static readonly HashSet<string> Nobody = [];

    static HashSet<string> Only(params string[] names) => new(names, StringComparer.Ordinal);

    static Tracker Session(params string[] lines)
    {
        var t = new Tracker();
        t.Follow("Hasaya_2026.07.30.jsonl");
        t.Start(0);
        t.Feed(lines);
        return t;
    }

    static Tracker Fight() => Session(
        Lines.Hit(10, "Hasaya", 100, target: "Kirin"),
        Lines.Hit(12, "Hasaya", 0, target: "Kirin", hit: false),
        Lines.Hit(14, "Tank", 40, target: "Kirin"),
        Lines.Hit(16, "Hasaya", 900, kind: "ws", action: "Savage Blade", target: "Kirin"),
        Lines.Hit(20, "Hasaya", 300, target: "Genbu"),
        Lines.Hit(22, "Tank", 600, kind: "ws", action: "Savage Blade", target: "Genbu"),
        Lines.Hit(24, "Tank", 0, target: "Byakko", hit: false));

    [Fact]
    public void Each_target_lists_every_character_with_their_share_of_it()
    {
        var c = Fight().Count(Nobody, true, 30_000);
        Assert.Equal(["Kirin", "Genbu", "Byakko"], c.TargetGroups.Select(g => g.Name));

        var kirin = c.TargetGroups[0];
        Assert.Equal(1040, kirin.Total);
        Assert.Equal(["Hasaya", "Tank"], kirin.Characters.Select(a => a.Name));
        Assert.Equal(1000.0 / 1040, kirin.Characters[0].Share);
        Assert.Equal(40.0 / 1040, kirin.Characters[1].Share);

        // Nothing landed on Byakko: listed with its try, at nothing.
        var byakko = c.TargetGroups[2];
        Assert.Equal(0, byakko.Total);
        Assert.Equal(0, byakko.Party.AutoAcc);
    }

    [Fact]
    public void A_target_and_its_characters_read_as_they_would_with_it_alone_isolated()
    {
        var t = Fight();
        var c = t.Count(Nobody, true, 30_000);
        foreach (var g in c.TargetGroups)
        {
            var alone = t.Count(Nobody, true, 30_000, Only(g.Name));
            Assert.Equal(alone.Totals.Total, g.Total);
            Assert.Equal(alone.Totals.Party, g.Party);
            Assert.Equal(alone.Totals.Actors.Select(a => (a.Name, a.Total, a.AutoAcc, a.WsAvg)),
                         g.Characters.Select(a => (a.Name, a.Total, a.AutoAcc, a.WsAvg)));
        }
    }

    [Fact]
    public void Isolated_characters_cut_the_targets_figures_and_not_the_list_of_characters()
    {
        var c = Fight().Count(Only("Tank"), true, 30_000);
        var kirin = c.TargetGroups.Single(g => g.Name == "Kirin");
        Assert.Equal(40, kirin.Total);
        Assert.Null(kirin.Party.WsAvg);
        Assert.Equal(["Hasaya", "Tank"], kirin.Characters.Select(a => a.Name));
    }

    [Fact]
    public void Every_target_is_listed_whatever_target_is_isolated()
    {
        // As By target lists them: what is isolated is picked from here too.
        var c = Fight().Count(Nobody, true, 30_000, Only("Genbu"));
        Assert.Equal(["Kirin", "Genbu", "Byakko"], c.TargetGroups.Select(g => g.Name));
        Assert.Equal(1040, c.TargetGroups[0].Total);
    }

    [Fact]
    public void Isolated_types_cut_every_target()
    {
        var c = Fight().Count(Nobody, true, 30_000, types: Only("ws"));
        Assert.Equal(900, c.TargetGroups.Single(g => g.Name == "Kirin").Total);
        Assert.Equal(600, c.TargetGroups.Single(g => g.Name == "Genbu").Total);
    }

    [Fact]
    public void Rows_with_no_target_are_not_grouped()
    {
        var c = Session(Lines.Hit(10, "Hasaya", 100, target: ""), Lines.Hit(12, "Hasaya", 50, target: "Kirin"))
            .Count(Nobody, true, 30_000);
        Assert.Equal(["Kirin"], c.TargetGroups.Select(g => g.Name));
    }
}
