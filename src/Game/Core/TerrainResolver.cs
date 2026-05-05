namespace StrategyGame.Core;

public static partial class TerrainResolver
{
    // Water colors are constants because water bypasses the land biome color
    // table. Coast water is intentionally brighter for readability.
    private const string OceanColor = "#2f80c9";
    private const string CoastColor = "#62b7e8";

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

        var name = ResolveRegionBiome(BaseBiome.Plain, TemperatureBand.Temperate, tile.Vegetation);
        var color = ColorFor(name);
        var movementCost = MovementCost(tile.Elevation, tile.Vegetation, tile.FeatureIds);
        var defense = DefenseModifier(tile.Elevation, tile.Vegetation, tile.FeatureIds);
        return new ResolvedTerrain(name, color, movementCost, true, defense);
    }

    public static ResolvedTerrain ResolveLand(RegionState region, HexTile tile)
    {
        // Region retention and temperature stay broad regional properties, while
        // tile moisture and vegetation may differ after elevation drying.
        var baseBiome = ResolveTileBaseBiome(region, tile);
        var localBiomeName = ResolveRegionBiome(baseBiome, region.Temperature, tile.Vegetation);
        var finalName = FormatFinalBiomeName(region.Temperature, localBiomeName);
        var color = ColorFor(finalName);
        var movementCost = MovementCost(tile.Elevation, tile.Vegetation, tile.FeatureIds);
        var defense = DefenseModifier(tile.Elevation, tile.Vegetation, tile.FeatureIds);
        return new ResolvedTerrain(finalName, color, movementCost, true, defense);
    }
}
