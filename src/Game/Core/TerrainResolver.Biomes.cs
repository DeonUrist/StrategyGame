namespace StrategyGame.Core;

public static partial class TerrainResolver
{
    public static BaseBiome PickBaseBiome(MoistureLevel moisture, WaterRetention retention)
    {
        // First pass: moisture and water retention decide the broad base biome.
        // Temperature and vegetation are applied later.
        return (moisture, retention) switch
        {
            (MoistureLevel.Dry, WaterRetention.Draining) => BaseBiome.Desert,
            (MoistureLevel.Dry, WaterRetention.Normal) => BaseBiome.Wasteland,
            (MoistureLevel.Dry, WaterRetention.Holding) => BaseBiome.Badlands,
            (MoistureLevel.Normal, WaterRetention.Draining) => BaseBiome.Dryland,
            (MoistureLevel.Normal, WaterRetention.Normal) => BaseBiome.Plain,
            (MoistureLevel.Normal, WaterRetention.Holding) => BaseBiome.Floodplain,
            (MoistureLevel.Wet, WaterRetention.Draining) => BaseBiome.Barrens,
            (MoistureLevel.Wet, WaterRetention.Normal) => BaseBiome.Wetland,
            (MoistureLevel.Wet, WaterRetention.Holding) => BaseBiome.Swamp,
            _ => BaseBiome.Plain
        };
    }

    public static BaseBiome PickBaseBiome(MoistureLevel moisture, WaterRetention retention, TemperatureBand temperature)
    {
        // Temperature is normally a flavor layer, but true desert is a hot dry
        // biome. Cold dry/draining regions use the dryland row instead so the
        // same moisture/retention slot becomes grassland, shrubland, or tundra.
        var baseBiome = PickBaseBiome(moisture, retention);
        if (baseBiome == BaseBiome.Desert && temperature is TemperatureBand.Temperate or TemperatureBand.Subarctic)
        {
            return BaseBiome.Dryland;
        }

        return baseBiome;
    }

    public static Vegetation ClampVegetation(BaseBiome biome, TemperatureBand temperature, Vegetation requested)
    {
        // The final table has invalid cells: for example desert cannot be lush,
        // and freezing terrain cannot keep vegetation. Clamp before resolving so
        // callers can request a high value safely.
        var max = MaxVegetation(biome, temperature);
        return (int)requested > (int)max ? max : requested;
    }

    public static Vegetation ClampTileVegetation(RegionState region, HexTile tile)
    {
        // Tile-local moisture can change after elevation dries hills, mountains,
        // and peaks. Recompute the local base biome before clamping so the stored
        // tile vegetation cannot contradict the terrain name the resolver shows.
        var baseBiome = ResolveTileBaseBiome(region, tile);
        return ClampVegetation(baseBiome, region.Temperature, tile.Vegetation);
    }

    public static BaseBiome ResolveTileBaseBiome(RegionState region, HexTile tile)
    {
        var baseBiome = PickBaseBiome(tile.Moisture, region.WaterRetention, region.Temperature);

        // Elevation drying should roughen a region, but it should not create
        // tiny standalone desert pockets inside otherwise non-desert climates.
        if (baseBiome == BaseBiome.Desert && region.BaseBiome != BaseBiome.Desert)
        {
            return BaseBiome.Dryland;
        }

        return baseBiome;
    }

    public static Vegetation MaxVegetation(BaseBiome biome, TemperatureBand temperature)
    {
        // Arctic overrides every base biome and strips vegetation.
        if (temperature == TemperatureBand.Arctic)
        {
            return Vegetation.None;
        }

        return biome switch
        {
            BaseBiome.Desert => Vegetation.None,
            BaseBiome.Wasteland => Vegetation.None,
            BaseBiome.Badlands => Vegetation.Sparse,
            BaseBiome.Dryland => Vegetation.Sparse,
            BaseBiome.Plain => Vegetation.Lush,
            BaseBiome.Floodplain => Vegetation.Lush,
            BaseBiome.Barrens => Vegetation.Sparse,
            BaseBiome.Wetland => Vegetation.Lush,
            BaseBiome.Swamp => Vegetation.Lush,
            _ => Vegetation.None
        };
    }

    public static string ResolveRegionBiome(BaseBiome biome, TemperatureBand temperature, Vegetation vegetation)
    {
        // Final pass: base biome + temperature + clamped vegetation produces the
        // terrain name. Region defaults and tile-local elevated terrain both use
        // this same table.
        var clamped = ClampVegetation(biome, temperature, vegetation);
        return (biome, temperature, clamped) switch
        {
            (BaseBiome.Desert, TemperatureBand.Subarctic or TemperatureBand.Arctic, _) => "Ice Sheet",
            (BaseBiome.Desert, _, _) => "Desert",

            (BaseBiome.Wasteland, TemperatureBand.Arctic, _) => "Ice Sheet",
            (BaseBiome.Wasteland, TemperatureBand.Subarctic, _) => "Tundra",
            (BaseBiome.Wasteland, TemperatureBand.Temperate, _) => "Steppe",
            (BaseBiome.Wasteland, _, _) => "Wasteland",

            (BaseBiome.Badlands, TemperatureBand.Arctic, _) => "Ice Sheet",
            (BaseBiome.Badlands, _, _) => "Badlands",

            (BaseBiome.Dryland, TemperatureBand.Tropical, Vegetation.Sparse) => "Savanna",
            (BaseBiome.Dryland, TemperatureBand.Tropical, _) => "Dryland",
            (BaseBiome.Dryland, TemperatureBand.Subtropical, Vegetation.Sparse) => "Prairie",
            (BaseBiome.Dryland, TemperatureBand.Subtropical, _) => "Steppe",
            (BaseBiome.Dryland, TemperatureBand.Temperate, Vegetation.Sparse) => "Shrubland",
            (BaseBiome.Dryland, TemperatureBand.Temperate, _) => "Grassland",
            (BaseBiome.Dryland, TemperatureBand.Subarctic, _) => "Tundra",
            (BaseBiome.Dryland, TemperatureBand.Arctic, _) => "Ice Sheet",

            (BaseBiome.Plain, TemperatureBand.Tropical, Vegetation.Lush) => "Jungle",
            (BaseBiome.Plain, TemperatureBand.Tropical, Vegetation.Sparse) => "Savanna",
            (BaseBiome.Plain, TemperatureBand.Tropical, _) => "Plain",
            (BaseBiome.Plain, TemperatureBand.Subtropical, Vegetation.Lush) => "Rainforest",
            (BaseBiome.Plain, TemperatureBand.Subtropical, Vegetation.Sparse) => "Prairie",
            (BaseBiome.Plain, TemperatureBand.Subtropical, _) => "Steppe",
            (BaseBiome.Plain, TemperatureBand.Temperate, Vegetation.Lush) => "Forest",
            (BaseBiome.Plain, TemperatureBand.Temperate, Vegetation.Sparse) => "Shrubland",
            (BaseBiome.Plain, TemperatureBand.Temperate, _) => "Grassland",
            (BaseBiome.Plain, TemperatureBand.Subarctic, Vegetation.Lush) => "Taiga",
            (BaseBiome.Plain, TemperatureBand.Subarctic, _) => "Tundra",
            (BaseBiome.Plain, TemperatureBand.Arctic, _) => "Ice Sheet",

            (BaseBiome.Floodplain, TemperatureBand.Tropical, Vegetation.Lush) => "Jungle",
            (BaseBiome.Floodplain, TemperatureBand.Tropical, Vegetation.Sparse) => "Savanna",
            (BaseBiome.Floodplain, TemperatureBand.Tropical, _) => "Floodplain",
            (BaseBiome.Floodplain, TemperatureBand.Subtropical, Vegetation.Lush) => "Rainforest",
            (BaseBiome.Floodplain, TemperatureBand.Subtropical, Vegetation.Sparse) => "Prairie",
            (BaseBiome.Floodplain, TemperatureBand.Subtropical, _) => "Floodplain",
            (BaseBiome.Floodplain, TemperatureBand.Temperate, Vegetation.Lush) => "Forest",
            (BaseBiome.Floodplain, TemperatureBand.Temperate, _) => "Grassland",
            (BaseBiome.Floodplain, TemperatureBand.Subarctic, Vegetation.Lush) => "Taiga",
            (BaseBiome.Floodplain, TemperatureBand.Subarctic, _) => "Tundra",
            (BaseBiome.Floodplain, TemperatureBand.Arctic, _) => "Ice Sheet",

            (BaseBiome.Barrens, TemperatureBand.Arctic, _) => "Ice Sheet",
            (BaseBiome.Barrens, TemperatureBand.Subarctic, _) => "Tundra",
            (BaseBiome.Barrens, _, Vegetation.Sparse) => "Shrubland",
            (BaseBiome.Barrens, _, _) => "Wasteland",

            (BaseBiome.Wetland, TemperatureBand.Tropical, Vegetation.Lush) => "Jungle",
            (BaseBiome.Wetland, TemperatureBand.Tropical, _) => "Wetland",
            (BaseBiome.Wetland, TemperatureBand.Subtropical, Vegetation.Lush) => "Rainforest",
            (BaseBiome.Wetland, TemperatureBand.Temperate, Vegetation.Lush) => "Swamp",
            (BaseBiome.Wetland, TemperatureBand.Subtropical or TemperatureBand.Temperate, _) => "Wetland",
            (BaseBiome.Wetland, TemperatureBand.Subarctic, Vegetation.Lush) => "Taiga",
            (BaseBiome.Wetland, TemperatureBand.Subarctic, _) => "Tundra",
            (BaseBiome.Wetland, TemperatureBand.Arctic, _) => "Ice Sheet",

            (BaseBiome.Swamp, TemperatureBand.Tropical, Vegetation.Lush) => "Jungle",
            (BaseBiome.Swamp, TemperatureBand.Tropical, _) => "Swamp",
            (BaseBiome.Swamp, TemperatureBand.Subtropical, Vegetation.Lush) => "Rainforest",
            (BaseBiome.Swamp, TemperatureBand.Subtropical, _) => "Swamp",
            (BaseBiome.Swamp, TemperatureBand.Temperate, Vegetation.Lush) => "Forest",
            (BaseBiome.Swamp, TemperatureBand.Temperate, _) => "Swamp",
            (BaseBiome.Swamp, TemperatureBand.Subarctic, Vegetation.Lush) => "Taiga",
            (BaseBiome.Swamp, TemperatureBand.Subarctic, _) => "Wetland",
            (BaseBiome.Swamp, TemperatureBand.Arctic, _) => "Ice Sheet",

            _ => biome.ToString()
        };
    }

    public static string FormatFinalBiomeName(TemperatureBand temperature, string localBiomeName)
    {
        // Climate is stored on RegionState and shown separately in debug text.
        // Final terrain names stay clean and readable on the map.
        return localBiomeName;
    }
}
