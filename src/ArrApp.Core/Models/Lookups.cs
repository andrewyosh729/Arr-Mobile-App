namespace ArrApp.Core.Models;

public sealed class RootFolder {
    public int Id { get; set; }
    public string Path { get; set; } = "";
    public bool Accessible { get; set; }
    public long FreeSpace { get; set; }
}

public sealed class Tag {
    public int Id { get; set; }
    public string Label { get; set; } = "";
}
