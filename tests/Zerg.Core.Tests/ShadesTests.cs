namespace Zerg.Core.Tests;

/// <summary>The colour of side B of a pair, beside side A's.</summary>
public class ShadesTests
{
    static readonly (byte, byte, byte) Sam = (0xFC, 0x6E, 0x03), War = (0xE6, 0x00, 0x00), White = (255, 255, 255);

    [Fact]
    public void Another_character_keeps_their_own_colour()
    {
        Assert.Equal(War, Shades.Beside(Sam, War, lightTheme: false));
    }

    [Fact]
    public void One_character_against_themself_is_their_next_shade()
    {
        Assert.Equal(Shades.Step(Sam, 1, lightTheme: false), Shades.Beside(Sam, Sam, lightTheme: false));
        Assert.Equal(Shades.Step(Sam, 1, lightTheme: true), Shades.Beside(Sam, Sam, lightTheme: true));
    }

    [Fact]
    public void A_shade_that_cannot_move_is_passed_over()
    {
        // White has no lighter step on a dark page: the step toward the page is taken.
        var b = Shades.Beside(White, White, lightTheme: false);
        Assert.Equal(Shades.Step(White, 2, lightTheme: false), b);
        Assert.NotEqual(White, b);
    }

    [Fact]
    public void Two_colours_too_close_to_tell_apart_are_shaded_apart()
    {
        (byte, byte, byte) near = (0xF8, 0x70, 0x08);
        Assert.Equal(Shades.Step(Sam, 1, lightTheme: false), Shades.Beside(Sam, near, lightTheme: false));
    }
}
