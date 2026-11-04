namespace Zerg.Core.Tests;

/// <summary>
/// Which addon wrote the file, from its first line, and whether that addon is
/// older than the one Zerg expects (<see cref="AddonVersion"/>).
/// </summary>
public class AddonVersionTests
{
    static string Meta(string version) =>
        "{\"kind\":\"meta\",\"t\":1793801768900,\"version\":\"" + version + "\"}";

    [Theory]
    [InlineData(AddonVersion.Expected)]
    [InlineData("2.0.1")]
    [InlineData("2.1.0")]
    [InlineData("3.0.0")]
    [InlineData("2.0")]
    [InlineData("2")]
    [InlineData("10.0.0")]
    public void The_expected_addon_and_newer_ones_are_not_behind(string version) =>
        Assert.False(AddonVersion.IsBehind(version));

    [Theory]
    [InlineData("1.9.9")]
    [InlineData("1.3.0")]
    [InlineData("0.3.0")]
    [InlineData("1")]
    public void An_older_addon_is_behind(string version) =>
        Assert.True(AddonVersion.IsBehind(version));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2.0.0-beta")]
    [InlineData("v2.0.0")]
    [InlineData("2.0.0.0")]
    [InlineData("2..0")]
    [InlineData(" 2.0.0")]
    [InlineData("two")]
    [InlineData("99999999999.0.0")]
    public void A_missing_or_unreadable_version_is_behind(string? version) =>
        Assert.True(AddonVersion.IsBehind(version));

    [Fact]
    public void The_warning_names_the_expected_version() =>
        Assert.Equal("VibeXI addon version is behind expected version: " + AddonVersion.Expected +
                     ", data may be inaccurate or missing", AddonVersion.Warning);

    [Fact]
    public void The_version_is_the_first_lines()
    {
        var r = new EventReader();
        Assert.False(r.Opened);
        Assert.False(r.AddonBehind);   // nothing read yet: nothing to say

        r.Feed("");   // a blank line is no record
        Assert.False(r.Opened);
        r.Feed(Meta("2.0.0"));
        Assert.True(r.Opened);
        Assert.Equal("2.0.0", r.Addon);
        Assert.False(r.AddonBehind);

        // A later meta line is not the file's first: it changes nothing.
        r.Feed(Meta("1.0.0"));
        Assert.Equal("2.0.0", r.Addon);
        Assert.False(r.AddonBehind);
    }

    [Fact]
    public void An_older_addons_file_is_behind()
    {
        var r = new EventReader();
        r.Feed(Meta("1.9.0"));
        Assert.Equal("1.9.0", r.Addon);
        Assert.True(r.AddonBehind);
    }

    [Fact]
    public void A_newer_addons_file_is_not_behind()
    {
        var r = new EventReader();
        r.Feed(Meta("2.0.1"));
        Assert.False(r.AddonBehind);
    }

    [Theory]
    // An addon from before the meta line: its first line is a record.
    [InlineData("{\"t\":10000,\"seq\":1,\"use\":1,\"kind\":\"melee\",\"actor\":\"Hasaya\",\"actorKind\":\"player\",\"dmg\":5,\"hit\":true}")]
    [InlineData("{\"kind\":\"job\",\"t\":10000,\"actor\":\"Hasaya\",\"main\":\"SAM\"}")]
    // A meta line with no version, or with the debug build's own key.
    [InlineData("{\"kind\":\"meta\",\"t\":1793801768900}")]
    [InlineData("{\"kind\":\"meta\",\"v\":\"0.3.0\",\"t\":1793759692}")]
    // The old format's meta line: its time in seconds makes it unreadable.
    [InlineData("{\"kind\":\"meta\",\"t\":1793759411,\"version\":\"1.3.0\"}")]
    // A version that is not a string makes the line unreadable.
    [InlineData("{\"kind\":\"meta\",\"t\":1793801768900,\"version\":2}")]
    [InlineData("not json")]
    public void A_file_whose_first_line_states_no_version_is_behind(string first)
    {
        var r = new EventReader();
        r.Feed(first);
        r.Feed(Meta("2.0.0"));   // too late: not the first line
        Assert.True(r.AddonBehind);
    }

    [Fact]
    public void A_restarted_session_keeps_the_files_version()
    {
        var t = new Tracker();
        t.Follow("Hasaya-20261104091608.jsonl");
        t.Feed([Meta("1.9.0")]);
        t.Start(0);   // drops the rows; the file is not read again
        Assert.True(t.Reader.AddonBehind);
        Assert.Equal("1.9.0", t.Reader.Addon);

        // A new file is a new addon load, read from its own first line.
        t.Follow("Hasaya-20261104101608.jsonl");
        Assert.False(t.Reader.Opened);
        t.Feed([Meta("2.0.0")]);
        Assert.False(t.Reader.AddonBehind);
    }

    [Fact]
    public void A_replay_reads_the_version_of_its_file()
    {
        var behind = Replay.Read([Meta("1.9.0")], "Hasaya-20261104091608.jsonl");
        Assert.True(behind.Reader.AddonBehind);
        var current = Replay.Read([Meta("2.0.0")], "Hasaya-20261104091608.jsonl");
        Assert.False(current.Reader.AddonBehind);
    }
}
