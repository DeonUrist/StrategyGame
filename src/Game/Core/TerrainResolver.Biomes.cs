namespace StrategyGame.Core;

public static partial class TerrainResolver
{
    public static BaseBiome PickBaseBiome(
        MoistureLevel moisture,
        TemperatureBand temperature,
        int grasslandShrublandBias = 0,
        int desertBadlandsBias = 0,
        int coniferBroadleafForestBias = 0,
        int variantRoll = 0)
    {
        var roll = Math.Clamp(variantRoll, 0, 99);
        return temperature switch
        {
            TemperatureBand.Arctic => BaseBiome.IceSheet,
            TemperatureBand.Subarctic => moisture == MoistureLevel.Wet ? BaseBiome.Taiga : BaseBiome.Tundra,
            TemperatureBand.Temperate => moisture switch
            {
                MoistureLevel.Dry => PickVariant(BaseBiome.Grassland, BaseBiome.Shrubland, grasslandShrublandBias, roll),
                MoistureLevel.Normal => PickVariant(BaseBiome.ConiferForest, BaseBiome.BroadleafForest, coniferBroadleafForestBias, roll),
                MoistureLevel.Wet => BaseBiome.Swamp,
                _ => BaseBiome.Grassland
            },
            TemperatureBand.Tropical => moisture switch
            {
                MoistureLevel.Dry => PickVariant(BaseBiome.Desert, BaseBiome.Badlands, desertBadlandsBias, roll),
                MoistureLevel.Normal => BaseBiome.Prairie,
                MoistureLevel.Wet => BaseBiome.Jungle,
                _ => BaseBiome.Prairie
            },
            _ => BaseBiome.Grassland
        };
    }

    public static string ResolveRegionBiome(BaseBiome biome)
    {
        return biome switch
        {
            BaseBiome.IceSheet => "Ice Sheet",
            BaseBiome.ConiferForest => "Conifer Forest",
            BaseBiome.BroadleafForest => "Broadleaf Forest",
            _ => biome.ToString()
        };
    }

    private static BaseBiome PickVariant(BaseBiome first, BaseBiome second, int bias, int roll)
    {
        return roll < Math.Clamp(bias, 0, 100) ? second : first;
    }
}
