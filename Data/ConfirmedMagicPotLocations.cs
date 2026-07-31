namespace CrescentAtlas.Data;

public static class ConfirmedMagicPotLocations
{
    public static IReadOnlyDictionary<uint, Vector3> NorthHorn { get; } =
        new Dictionary<uint, Vector3>
        {
            [2072] = new(233.0f, 7.729229f, -470.0f),
            [2073] = new(-505.2822f, 53.14409f, 244.041f),
        };
}
