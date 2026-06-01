using Jot.Theming;
using Xunit;

namespace Jot.Tests;

public class ThemingTests
{
    public static IEnumerable<object[]> AllThemes() => Themes.All.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(AllThemes))]
    public void Foreground_MeetsAaContrast_AgainstEffectiveBackground(JotTheme theme)
    {
        var background = theme.EffectiveTextBackground();
        var ratio = Contrast.Ratio(theme.Foreground, background);
        Assert.True(ratio >= Contrast.AaNormal,
            $"Theme '{theme.Id}': text contrast {ratio:F2} is below AA ({Contrast.AaNormal}) on {background}.");
    }

    [Theory]
    [MemberData(nameof(AllThemes))]
    public void MutedText_IsAtLeastReadable(JotTheme theme)
    {
        // Secondary text (line numbers, status) should clear the large-text AA threshold of 3:1.
        var background = theme.EffectiveTextBackground();
        Assert.True(Contrast.Ratio(theme.Muted, background) >= 3.0,
            $"Theme '{theme.Id}': muted text contrast is below 3:1.");
        Assert.True(Contrast.Ratio(theme.LineNumber, background) >= 3.0,
            $"Theme '{theme.Id}': line-number contrast is below 3:1.");
    }

    [Fact]
    public void Get_UnknownId_FallsBackToDark()
    {
        Assert.Equal("dark", Themes.Get("nope").Id);
        Assert.Equal("dark", Themes.Get(null).Id);
    }

    [Fact]
    public void DefaultIsDark_AndAllIdsAreUnique()
    {
        Assert.Equal("dark", Themes.DefaultId);
        Assert.Equal(Themes.All.Count, Themes.All.Select(t => t.Id).Distinct().Count());
    }

    [Theory]
    [InlineData("#000000", "#ffffff", 21.0)]
    [InlineData("#ffffff", "#ffffff", 1.0)]
    public void Contrast_Ratio_KnownValues(string fg, string bg, double expected)
    {
        Assert.Equal(expected, Contrast.Ratio(fg, bg), precision: 1);
    }

    [Fact]
    public void Composite_TranslucentOverWhite_LightensTowardWhite()
    {
        // Black at ~50% (0x80 = 128/255) over white is mid-grey.
        var result = Contrast.CompositeHex("#80000000", "#ffffff");
        Assert.Equal("#7f7f7f", result, ignoreCase: true);
    }
}
