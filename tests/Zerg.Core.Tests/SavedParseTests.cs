using System.Globalization;

namespace Zerg.Core.Tests;

/// <summary>
/// A real parse, saved by Zerg: a 55-minute session of seven characters with
/// healing (Parses/MNK.zerg). Its figures are pinned here so a change to how a
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
        Assert.Equal(43_880, snap.Healing.Total);
    }

    [Fact]
    public void Saved_again_it_is_the_same_file()
    {
        var text = Text();
        var parse = ParseFile.Import(text);
        var exported = DateTimeOffset.Parse(parse.Exported!, CultureInfo.InvariantCulture).ToUnixTimeMilliseconds();

        Assert.Equal(text, Tracker.Of(parse).Export(exported));
    }
}
