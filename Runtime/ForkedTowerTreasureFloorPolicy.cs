namespace CrescentAtlas.Runtime;

public static class ForkedTowerTreasureFloorPolicy
{
    private static readonly IReadOnlyDictionary<uint, IReadOnlySet<uint>> TreasureRowsByMap =
        new Dictionary<uint, IReadOnlySet<uint>>
        {
            [1178] = new HashSet<uint> { 1983, 1996 },
            [1179] = new HashSet<uint> { 1992, 2001 },
            [1181] = new HashSet<uint> { 1984, 1997 },
            [1182] = new HashSet<uint> { 1993, 2002 },
            [1185] = new HashSet<uint> { 1985, 1998 },
            [1187] = new HashSet<uint> { 1991, 2000 },
            [1188] = new HashSet<uint> { 1994, 2003 },
            [1189] = new HashSet<uint> { 1995, 2004 },
            [1190] = new HashSet<uint> { 1989, 1990, 1999 },
        };

    public static bool IsCandidateForMap(uint mapId, uint treasureRowId)
        => TreasureRowsByMap.TryGetValue(mapId, out var rows)
           && rows.Contains(treasureRowId);
}
