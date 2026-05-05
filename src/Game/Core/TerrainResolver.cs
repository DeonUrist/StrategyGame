namespace StrategyGame.Core;

public static class TerrainResolver
{
    private const string OceanColor = "#2f80c9";
    private const string CoastColor = "#62b7e8";

    public static ResolvedTerrain Resolve(GameState state, HexTile tile)
    {
        var coastline = state.Map.IsCoastline(tile);
        return Resolve(tile, coastline);
    }

    public static ResolvedTerrain Resolve(HexTile tile, bool coastline)
    {
        if (tile.Elevation == Elevation.Water)
        {
            return ResolveWater(tile, coastline);
        }

        var baseBiome = BaseBiome(tile.Climate, tile.Rainfall);
        var name = ApplyVegetation(baseBiome, tile.Vegetation);
        var color = ColorFor(name);
        var movementCost = MovementCost(tile.Elevation, tile.Vegetation, tile.FeatureIds);
        var defense = DefenseModifier(tile.Elevation, tile.Vegetation, tile.FeatureIds);
        return new ResolvedTerrain(name, color, movementCost, true, defense);
    }

    public static Vegetation NormalizeVegetation(Climate climate, Rainfall rainfall, Vegetation vegetation)
    {
        var baseBiome = BaseBiome(climate, rainfall);
        if (IsValidVegetation(baseBiome, vegetation))
        {
            return vegetation;
        }

        return baseBiome switch
        {
            "Tropical Desert" => Vegetation.Sparse,
            "Tropical Rainforest" => Vegetation.Forest,
            "Temperate Forest" => Vegetation.Forest,
            "Taiga" => Vegetation.Forest,
            "Ice Tundra" or "Ice Sheet" => Vegetation.None,
            _ => Vegetation.None
        };
    }

    private static ResolvedTerrain ResolveWater(HexTile tile, bool coastline)
    {
        var climateName = tile.Climate switch
        {
            Climate.Tropical => "Tropical",
            Climate.Temperate => "Temperate",
            Climate.Boreal => "Subpolar",
            Climate.Polar => "Polar",
            _ => tile.Climate.ToString()
        };

        var suffix = tile.FeatureIds.Contains("volcano", StringComparer.OrdinalIgnoreCase) ? " Volcano" : "";
        var name = coastline ? $"{climateName} Coastline{suffix}" : $"{climateName} Ocean{suffix}";
        if (tile.Climate == Climate.Polar && !coastline && suffix.Length == 0)
        {
            name = "Polar Ocean (Icebergs)";
        }

        return new ResolvedTerrain(name, coastline ? CoastColor : OceanColor, int.MaxValue, false, 0);
    }

    private static string BaseBiome(Climate climate, Rainfall rainfall)
    {
        // Base biome table. This is the first pass: climate + rainfall says what
        // kind of environment exists before vegetation turns it into a final tile.
        return (climate, rainfall) switch
        {
            (Climate.Tropical, Rainfall.Low) => "Tropical Desert",
            (Climate.Tropical, Rainfall.Medium) => "Savanna",
            (Climate.Tropical, Rainfall.High) => "Tropical Rainforest",
            (Climate.Temperate, Rainfall.Low) => "Steppe",
            (Climate.Temperate, Rainfall.Medium) => "Grassland",
            (Climate.Temperate, Rainfall.High) => "Temperate Forest",
            (Climate.Boreal, Rainfall.Low) => "Dry Tundra",
            (Climate.Boreal, Rainfall.Medium) => "Tundra",
            (Climate.Boreal, Rainfall.High) => "Taiga",
            (Climate.Polar, Rainfall.Low) => "Polar Desert",
            (Climate.Polar, Rainfall.Medium) => "Ice Tundra",
            (Climate.Polar, Rainfall.High) => "Ice Sheet",
            _ => "Grassland"
        };
    }

    private static string ApplyVegetation(string baseBiome, Vegetation vegetation)
    {
        // Vegetation modifier table. Some combinations intentionally collapse to
        // the same final tile name, for example Grassland + Forest and Temperate
        // Forest + Forest both become Forest.
        return (baseBiome, vegetation) switch
        {
            ("Tropical Desert", Vegetation.None) => "Desert",
            ("Tropical Desert", Vegetation.Sparse) => "Scrubland",
            ("Savanna", Vegetation.None) => "Savanna",
            ("Savanna", Vegetation.Sparse) => "Dry Savanna",
            ("Savanna", Vegetation.Forest) => "Wooded Savanna",
            ("Tropical Rainforest", Vegetation.Sparse) => "Wetlands",
            ("Tropical Rainforest", Vegetation.Forest) => "Jungle",
            ("Steppe", Vegetation.None) => "Steppe",
            ("Steppe", Vegetation.Sparse) => "Shrub Steppe",
            ("Steppe", Vegetation.Forest) => "Sparse Woodland",
            ("Grassland", Vegetation.None) => "Grassland",
            ("Grassland", Vegetation.Sparse) => "Shrubland",
            ("Grassland", Vegetation.Forest) => "Forest",
            ("Temperate Forest", Vegetation.Sparse) => "Meadows",
            ("Temperate Forest", Vegetation.Forest) => "Forest",
            ("Dry Tundra", Vegetation.None) => "Polar Desert",
            ("Dry Tundra", Vegetation.Sparse) => "Lichen Tundra",
            ("Dry Tundra", Vegetation.Forest) => "Sparse Taiga",
            ("Tundra", Vegetation.None) => "Lichen Tundra",
            ("Tundra", Vegetation.Sparse) => "Shrub Tundra",
            ("Tundra", Vegetation.Forest) => "Taiga",
            ("Taiga", Vegetation.Sparse) => "Boreal Meadows",
            ("Taiga", Vegetation.Forest) => "Taiga",
            ("Polar Desert", Vegetation.None) => "Polar Desert",
            ("Polar Desert", Vegetation.Sparse) => "Polar Scrub",
            ("Polar Desert", Vegetation.Forest) => "Ice Sheet",
            ("Ice Tundra", Vegetation.None) => "Ice Tundra",
            ("Ice Sheet", Vegetation.None) => "Ice Sheet",
            _ => baseBiome
        };
    }

    private static bool IsValidVegetation(string baseBiome, Vegetation vegetation)
    {
        return (baseBiome, vegetation) switch
        {
            ("Tropical Desert", Vegetation.Forest) => false,
            ("Tropical Rainforest", Vegetation.None) => false,
            ("Temperate Forest", Vegetation.None) => false,
            ("Taiga", Vegetation.None) => false,
            ("Ice Tundra", Vegetation.Sparse or Vegetation.Forest) => false,
            ("Ice Sheet", Vegetation.Sparse or Vegetation.Forest) => false,
            _ => true
        };
    }

    private static Vegetation NormalizeVegetationForBaseBiome(string baseBiome)
    {
        return baseBiome switch
        {
            "Tropical Desert" => Vegetation.Sparse,
            "Tropical Rainforest" => Vegetation.Forest,
            "Temperate Forest" => Vegetation.Forest,
            "Taiga" => Vegetation.Forest,
            _ => Vegetation.None
        };
    }

    private static string ColorFor(string name)
    {
        return name switch
        {
            "Desert" => "#d8bd72",
            "Scrubland" => "#b8aa62",
            "Savanna" => "#9da64b",
            "Dry Savanna" => "#aaa34e",
            "Wooded Savanna" => "#748f42",
            "Wetlands" => "#4f8d69",
            "Jungle" => "#1f6f35",
            "Steppe" => "#b7a76a",
            "Shrub Steppe" => "#9d965f",
            "Sparse Woodland" => "#6f8d55",
            "Grassland" => "#8fca5a",
            "Shrubland" => "#6fa85a",
            "Forest" => "#2f8a3e",
            "Meadows" => "#7fbd68",
            "Polar Desert" => "#c8b78f",
            "Lichen Tundra" => "#9b7f5e",
            "Sparse Taiga" => "#4d7d68",
            "Shrub Tundra" => "#7b6f52",
            "Taiga" => "#1f5f58",
            "Boreal Meadows" => "#6f9274",
            "Polar Scrub" => "#b6aa92",
            "Ice Tundra" => "#cdd9d6",
            "Ice Sheet" => "#eef4f7",
            _ => "#8fca5a"
        };
    }

    private static int MovementCost(Elevation elevation, Vegetation vegetation, List<string> features)
    {
        var cost = elevation switch
        {
            Elevation.Flat => 1,
            Elevation.Hills => 2,
            Elevation.Mountains => 4,
            Elevation.Peaks => 5,
            _ => 1
        };

        cost += vegetation switch
        {
            Vegetation.Sparse => 1,
            Vegetation.Forest => 1,
            _ => 0
        };

        if (features.Contains("volcano", StringComparer.OrdinalIgnoreCase))
        {
            cost += 1;
        }

        return cost;
    }

    private static int DefenseModifier(Elevation elevation, Vegetation vegetation, List<string> features)
    {
        var defense = elevation switch
        {
            Elevation.Hills => 2,
            Elevation.Mountains => 4,
            Elevation.Peaks => 5,
            _ => 0
        };

        defense += vegetation switch
        {
            Vegetation.Sparse => 1,
            Vegetation.Forest => 1,
            _ => 0
        };

        if (features.Contains("volcano", StringComparer.OrdinalIgnoreCase))
        {
            defense += 1;
        }

        return defense;
    }
}
