using System.Text.Json;

namespace Zerg.Core.Tests;

/// <summary>
/// A file a person edits (settings.json is read this way): one value of the
/// wrong shape costs that value, never the rest.
/// </summary>
public class LenientJsonTests
{
    sealed class Sample
    {
        public string Theme { get; set; } = "System";
        public int LowAccuracy { get; set; } = 90;
        public int PanelOpacity { get; set; } = 85;
        public bool Skillchains { get; set; } = true;
        public List<string> Excluded { get; set; } = [];
        public Dictionary<string, int> PanelOpacities { get; set; } = [];
    }

    static readonly JsonSerializerOptions Options =
        LenientJson.Options(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    static (Sample? Read, List<string> Dropped) Read(string json)
    {
        var dropped = new List<string>();
        return (LenientJson.Read<Sample>(json, Options, (key, _) => dropped.Add(key)), dropped);
    }

    [Fact]
    public void A_whole_number_may_be_written_with_a_fraction_or_as_text()
    {
        var (s, dropped) = Read("""{"panelOpacity": 85.5, "lowAccuracy": "75", "panelOpacities": {"line": 40.4}}""");
        Assert.Equal(86, s!.PanelOpacity);
        Assert.Equal(75, s.LowAccuracy);
        Assert.Equal(40, s.PanelOpacities["line"]);
        Assert.Empty(dropped);
    }

    [Fact]
    public void A_value_of_the_wrong_shape_costs_that_value_only()
    {
        var (s, dropped) = Read("""
            {"theme": "Dark", "lowAccuracy": "lots", "skillchains": null, "excluded": ["Tank"], "panelOpacity": [1]}
            """);
        Assert.Equal("Dark", s!.Theme);
        Assert.Equal(["Tank"], s.Excluded);
        Assert.Equal(90, s.LowAccuracy);
        Assert.True(s.Skillchains);
        Assert.Equal(85, s.PanelOpacity);
        Assert.Equal(["lowAccuracy", "panelOpacity", "skillchains"], dropped.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_null_list_is_read_as_null_for_the_reader_to_mend()
    {
        // JSON's null is a list's null; Settings.Normal makes it empty.
        var (s, dropped) = Read("""{"excluded": null}""");
        Assert.Null(s!.Excluded);
        Assert.Empty(dropped);
    }

    [Fact]
    public void Text_that_is_not_an_object_is_nothing()
    {
        Assert.Null(Read("not json").Read);
        Assert.Null(Read("[1, 2]").Read);
        Assert.Null(Read("").Read);
    }

    [Fact]
    public void A_number_too_large_for_its_property_is_dropped()
    {
        var (s, dropped) = Read("""{"lowAccuracy": 1e40, "theme": "Light"}""");
        Assert.Equal(90, s!.LowAccuracy);
        Assert.Equal("Light", s.Theme);
        Assert.Equal(["lowAccuracy"], dropped);
    }
}
