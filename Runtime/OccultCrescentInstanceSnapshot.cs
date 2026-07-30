namespace CrescentAtlas.Runtime;

public sealed record OccultCrescentInstanceSnapshot(
    string InstancePointer,
    float? ContentTimeLeftSeconds)
{
    public static OccultCrescentInstanceSnapshot Empty { get; } = new(string.Empty, null);
}
