namespace StrategyGame.Core;

public static partial class TerrainResolver
{
    public static ResolvedTerrain Resolve(GameState state, HexTile tile)
    {
        if (tile.Elevation.IsWaterLike())
        {
            return ResolveWater(state, tile);
        }

        if (tile.RegionId is { } regionId && state.Regions.TryGetValue(regionId, out var region))
        {
            return ResolveLand(region, tile);
        }

        return Resolve(tile);
    }

    public static ResolvedTerrain Resolve(HexTile tile)
    {
        // This overload is used by tests and water/resource helpers when region
        // context is not available. Land without a region falls back to Plain.
        if (tile.Elevation.IsWaterLike())
        {
            return ResolveWater(tile);
        }

        var name = ResolveRegionBiome(BaseBiome.Grassland);
        var movementCost = MovementCost(name, tile.Elevation, tile.FeatureIds);
        var defense = DefenseModifier(tile.Elevation, tile.FeatureIds);
        return new ResolvedTerrain(name, movementCost, true, defense);
    }

    public static ResolvedTerrain ResolveLand(RegionState region, HexTile tile)
    {
        var finalName = region.FinalBiomeName;
        var movementCost = MovementCost(finalName, tile.Elevation, tile.FeatureIds);
        var defense = DefenseModifier(tile.Elevation, tile.FeatureIds);
        return new ResolvedTerrain(finalName, movementCost, true, defense);
    }
}
