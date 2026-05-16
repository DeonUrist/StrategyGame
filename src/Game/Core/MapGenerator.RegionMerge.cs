namespace StrategyGame.Core;

public static partial class MapGenerator
{
    internal static void MergeMatchingConnectedRegions(GameState state)
    {
        RebuildRegionTileCoords(state);

        var parentByRegionId = state.Regions.Keys.ToDictionary(id => id, id => id);

        foreach (var tile in state.Map.Tiles.Where(t => t.RegionId is not null))
        {
            var regionId = tile.RegionId!.Value;
            if (!state.Regions.TryGetValue(regionId, out var region))
            {
                continue;
            }

            foreach (var neighbor in state.Map.Neighbors(tile.Coord))
            {
                if (neighbor.RegionId is not { } neighborRegionId || neighborRegionId == regionId)
                {
                    continue;
                }

                if (!state.Regions.TryGetValue(neighborRegionId, out var neighborRegion))
                {
                    continue;
                }

                if (RegionsCanMerge(region, neighborRegion))
                {
                    Union(parentByRegionId, regionId, neighborRegionId);
                }
            }
        }

        var survivorByRegionId = parentByRegionId.Keys
            .GroupBy(id => Find(parentByRegionId, id))
            .SelectMany(group =>
            {
                var survivor = group.Min();
                return group.Select(id => new KeyValuePair<int, int>(id, survivor));
            })
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        foreach (var tile in state.Map.Tiles.Where(t => t.RegionId is not null))
        {
            if (survivorByRegionId.TryGetValue(tile.RegionId!.Value, out var survivorId))
            {
                tile.RegionId = survivorId;
                tile.Moisture = state.Regions[survivorId].Moisture;
            }
        }

        foreach (var regionId in state.Regions.Keys.ToList())
        {
            if (survivorByRegionId.TryGetValue(regionId, out var survivorId) && survivorId != regionId)
            {
                state.Regions.Remove(regionId);
            }
        }

        RebuildRegionTileCoords(state);
    }

    private static bool RegionsCanMerge(RegionState first, RegionState second)
    {
        return first.BaseBiome == second.BaseBiome
            && first.FinalBiomeName == second.FinalBiomeName
            && first.Temperature == second.Temperature;
    }

    private static void RebuildRegionTileCoords(GameState state)
    {
        foreach (var region in state.Regions.Values)
        {
            region.TileCoords.Clear();
        }

        foreach (var tile in state.Map.Tiles.OrderBy(t => t.Coord.R).ThenBy(t => ColumnOf(t.Coord)))
        {
            if (tile.RegionId is { } regionId && state.Regions.TryGetValue(regionId, out var region))
            {
                region.TileCoords.Add(tile.Coord);
            }
        }
    }

    private static void Union(Dictionary<int, int> parentByRegionId, int first, int second)
    {
        var firstRoot = Find(parentByRegionId, first);
        var secondRoot = Find(parentByRegionId, second);
        if (firstRoot == secondRoot)
        {
            return;
        }

        var survivor = Math.Min(firstRoot, secondRoot);
        var merged = Math.Max(firstRoot, secondRoot);
        parentByRegionId[merged] = survivor;
    }

    private static int Find(Dictionary<int, int> parentByRegionId, int regionId)
    {
        var parent = parentByRegionId[regionId];
        if (parent == regionId)
        {
            return regionId;
        }

        var root = Find(parentByRegionId, parent);
        parentByRegionId[regionId] = root;
        return root;
    }
}
