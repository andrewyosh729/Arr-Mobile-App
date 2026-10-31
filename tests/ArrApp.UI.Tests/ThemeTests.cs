using ArrApp.Core;
using ArrApp.UI.Theme;

namespace ArrApp.UI.Tests;

public class ThemeTests {
    [Fact]
    public void Each_Arr_Has_Its_Own_Accent() {
        var sonarr = AccentPalette.For(ArrType.Sonarr);
        var radarr = AccentPalette.For(ArrType.Radarr);

        Assert.NotEqual(sonarr, radarr);
        Assert.NotEqual(AccentPalette.Default, sonarr);
        Assert.NotEqual(AccentPalette.Default, radarr);
        Assert.Equal(AccentPalette.Default, AccentPalette.For(null));
    }
}
