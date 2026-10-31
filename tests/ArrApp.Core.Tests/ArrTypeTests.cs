using ArrApp.Core;

namespace ArrApp.Core.Tests;

public class ArrTypeTests {
    [Fact]
    public void Defines_Sonarr_And_Radarr() {
        Assert.Equal(["Sonarr", "Radarr"], Enum.GetNames<ArrType>());
    }
}
