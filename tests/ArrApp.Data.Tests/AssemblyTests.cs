namespace ArrApp.Data.Tests;

public class AssemblyTests {
    [Fact]
    public void Data_Assembly_References_Core() {
        var refs = typeof(ArrApp.Data.AssemblyMarker).Assembly.GetReferencedAssemblies();
        Assert.Contains(refs, a => a.Name == "ArrApp.Core");
    }
}
