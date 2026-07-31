namespace CrescentAtlas.Data;

public static class ConfirmedCarrotObjects
{
    // Fortune Carrot EventObj Base/Data ID. This is distinct from the
    // Knowledge Crystal EventObj (2007457).
    public const uint FortuneCarrotDataId = 2010139;

    public static bool IsKnownDataId(uint dataId)
        => dataId == FortuneCarrotDataId;
}
