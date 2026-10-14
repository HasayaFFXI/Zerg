namespace Zerg.Core.Tests;

/// <summary>
/// Zerg's number formatting: US-style grouping, and one rounding rule, halves
/// away from zero on the value (so 1.005, which is a hair under as a double,
/// is 1.00 to two places). Nothing prints as "-0", and not a number is a dash.
/// </summary>
public class FormatTests
{
    static readonly double[] Nums =
    [
        0, -0.0, 0.4, 0.5, 0.49999999999999994, 1.5, 2.5, -2.5, -0.5, -0.4, 1.25, 0.05, 0.15, 2.675, 1.005,
        999.95, 9999.4, 9999.5, 9999.95, 10000, 12345.678, 99999.95, 999949.9, 999950, 999999.95, 1e6,
        1234567.89, -1234567.89, -15000, 123456789012, 1e15, 9007199254740993, 1e21, 1.5e21, 1e-7, 4.35,
        0.035, double.NaN, double.PositiveInfinity, double.NegativeInfinity, -0.04, 0.125, 0.375, -1.25, 5e-324,
    ];

    static readonly string[] Int =
    [
        "0", "0", "0", "1", "0", "2", "3", "-3", "-1", "0", "1", "0", "0", "3", "1", "1,000", "9,999", "10,000",
        "10,000", "10,000", "12,346", "100,000", "999,950", "999,950", "1,000,000", "1,000,000", "1,234,568",
        "-1,234,568", "-15,000", "123,456,789,012", "1,000,000,000,000,000", "9,007,199,254,740,992",
        "1,000,000,000,000,000,000,000", "1,500,000,000,000,000,000,000", "0", "4", "0", "—", "∞", "-∞", "0",
        "0", "0", "-1", "0",
    ];

    /// <summary>To no places, as <see cref="Int"/>: one rule.</summary>
    static readonly string[] D0 = Int;

    static readonly string[] D1 =
    [
        "0.0", "0.0", "0.4", "0.5", "0.5", "1.5", "2.5", "-2.5", "-0.5", "-0.4", "1.3", "0.1", "0.2", "2.7", "1.0",
        "1,000.0", "9,999.4", "9,999.5", "10,000.0", "10,000.0", "12,345.7", "100,000.0", "999,949.9", "999,950.0",
        "1,000,000.0", "1,000,000.0", "1,234,567.9", "-1,234,567.9", "-15,000.0", "123,456,789,012.0",
        "1,000,000,000,000,000.0", "9,007,199,254,740,992.0", "1,000,000,000,000,000,000,000.0",
        "1,500,000,000,000,000,000,000.0", "0.0", "4.4", "0.0", "—", "∞", "-∞", "0.0", "0.1", "0.4", "-1.3", "0.0",
    ];

    static readonly string[] D2 =
    [
        "0.00", "0.00", "0.40", "0.50", "0.50", "1.50", "2.50", "-2.50", "-0.50", "-0.40", "1.25", "0.05", "0.15",
        "2.68", "1.00", "999.95", "9,999.40", "9,999.50", "9,999.95", "10,000.00", "12,345.68", "99,999.95",
        "999,949.90", "999,950.00", "999,999.95", "1,000,000.00", "1,234,567.89", "-1,234,567.89", "-15,000.00",
        "123,456,789,012.00", "1,000,000,000,000,000.00", "9,007,199,254,740,992.00",
        "1,000,000,000,000,000,000,000.00", "1,500,000,000,000,000,000,000.00", "0.00", "4.35", "0.04", "—", "∞",
        "-∞", "-0.04", "0.13", "0.38", "-1.25", "0.00",
    ];

    static readonly string[] D3 =
    [
        "0.000", "0.000", "0.400", "0.500", "0.500", "1.500", "2.500", "-2.500", "-0.500", "-0.400", "1.250",
        "0.050", "0.150", "2.675", "1.005", "999.950", "9,999.400", "9,999.500", "9,999.950", "10,000.000",
        "12,345.678", "99,999.950", "999,949.900", "999,950.000", "999,999.950", "1,000,000.000", "1,234,567.890",
        "-1,234,567.890", "-15,000.000", "123,456,789,012.000", "1,000,000,000,000,000.000",
        "9,007,199,254,740,992.000", "1,000,000,000,000,000,000,000.000", "1,500,000,000,000,000,000,000.000",
        "0.000", "4.350", "0.035", "—", "∞", "-∞", "-0.040", "0.125", "0.375", "-1.250", "0.000",
    ];

    [Fact]
    public void Whole_numbers_round_halves_away_from_zero_then_group() =>
        Assert.Equal(Int, Nums.Select(Format.Int));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Decimals_round_the_value(int dp) =>
        Assert.Equal(dp switch { 0 => D0, 1 => D1, 2 => D2, _ => D3 }, Nums.Select(n => Format.Num(n, dp)));

    [Fact]
    public void The_largest_double_prints_every_digit() =>
        Assert.Equal(
            "179,769,313,486,231,570,814,527,423,731,704,356,798,070,567,525,844,996,598,917,476,803,157,260,780,028," +
            "538,760,589,558,632,766,878,171,540,458,953,514,382,464,234,321,326,889,464,182,768,467,546,703,537,516," +
            "986,049,910,576,551,282,076,245,490,090,389,328,944,075,868,508,455,133,942,304,583,236,903,222,948,165," +
            "808,559,332,123,348,274,797,826,204,144,723,168,738,177,180,919,299,881,250,404,026,184,124,858,368",
            Format.Int(double.MaxValue));

    [Fact]
    public void Compact_switches_at_ten_thousand_and_a_million()
    {
        Assert.Equal("9,999", Format.Compact(9999.4));
        Assert.Equal("10,000", Format.Compact(9999.5));     // under ten thousand, though it rounds to it
        Assert.Equal("10.0K", Format.Compact(10000));
        Assert.Equal("1,000.0K", Format.Compact(999999.95));
        Assert.Equal("1.0M", Format.Compact(1e6));
        Assert.Equal("-15.0K", Format.Compact(-15000));
    }

    [Fact]
    public void Clocks()
    {
        Assert.Equal("0:00", Format.Elapsed(999));
        Assert.Equal("3:07", Format.Elapsed(187_000));
        Assert.Equal("1:00:00", Format.Elapsed(3_600_000));
        Assert.Equal("-0:05", Format.Elapsed(-5_500));
        Assert.Equal("00:59", Format.Stopwatch(59_999));
        Assert.Equal("01:00", Format.Stopwatch(60_000));
        Assert.Equal("1:02:03", Format.Stopwatch(3_723_000));
        Assert.Equal("42s", Format.Duration(42.4));
        Assert.Equal("4m 07s", Format.Duration(247));
        Assert.Equal("1h 5m", Format.Duration(3_930));
        // Under a second either side of zero is no time, and has no sign.
        Assert.Equal("0:00", Format.Elapsed(-500));
        Assert.Equal("—", Format.Elapsed(double.NaN));
        Assert.Equal("—", Format.Stopwatch(double.PositiveInfinity));
        Assert.Equal("—", Format.Duration(double.NaN));
    }

    [Fact]
    public void A_time_of_day_a_clock_cannot_show_is_a_dash()
    {
        // Past what DateTimeOffset holds: a damaged file, not a crash.
        Assert.Equal("—", Format.Clock(3e14));
        Assert.Equal("—", Format.Clock(8e15));
        Assert.Equal("—", Format.Clock(double.NaN));
        Assert.Equal("—", Format.Day(-1e15));
        Assert.False(Format.TryLocal(3e14, out _));
        Assert.True(Format.TryLocal(1_791_600_808_000, out _));
    }
}
