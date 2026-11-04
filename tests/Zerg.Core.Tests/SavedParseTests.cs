using System.Globalization;

namespace Zerg.Core.Tests;

/// <summary>
/// A real parse, saved by Zerg: a 55-minute session of seven characters with
/// healing (Parses/MNK.zerg), in format version 2. Its figures are pinned here so a change to how a
/// parse is read, counted or written cannot move them unnoticed.
/// </summary>
public class SavedParseTests
{
    static readonly HashSet<string> Nobody = [];

    static string Text() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Parses", "MNK.zerg")).ReplaceLineEndings("\n");

    [Fact]
    public void It_reads_every_record()
    {
        var parse = ParseFile.Import(Text());

        Assert.Equal(0, parse.Skipped);
        Assert.Equal(3390, parse.Source.Events.Count);
        Assert.Equal(258, parse.Source.Heals.Count);
        Assert.Equal("Hasaya", parse.Source.Roster.Owner);
        Assert.Equal(7, parse.Source.Roster.Jobs.Count);
        Assert.Equal("Hasaya-20261009215928.jsonl", parse.File);
    }

    [Fact]
    public void It_counts_what_it_counted_when_it_was_saved()
    {
        var snap = Tracker.Of(ParseFile.Import(Text())).Count(Nobody, true);

        Assert.Equal(3_336_000, snap.Elapsed);
        Assert.Equal(183_689, snap.Totals.Total);
        // A pet's heals in Healing (RULES.md); saved, it read 43,880 without them.
        Assert.Equal(51_725, snap.Healing.Total);
        Assert.Equal(7_845, snap.Healing.PetTotal);
    }

    [Fact]
    public void Saved_again_it_is_the_same_file()
    {
        var text = Text();
        var parse = ParseFile.Import(text);
        var exported = DateTimeOffset.Parse(parse.Exported!, CultureInfo.InvariantCulture).ToUnixTimeMilliseconds();

        // But for its end: saved when a session ran to the end of its last
        // row's second, it is snapped to that row itself (RULES.md).
        Assert.Equal(1_791_604_144_000, parse.Session.PausedAt);
        Assert.Equal(text.Replace("\"pausedAt\": 1791604144000", "\"pausedAt\": 1791604143000"),
                     Tracker.Of(parse).Export(exported));
    }
}
