namespace StrategyGame.Core;

public static partial class TerrainResolver
{
    private static ResolvedTerrain ResolveWater(GameState state, HexTile tile)
    {
        if (tile.Elevation == Elevation.DeepIce)
        {
            return new ResolvedTerrain("Ocean Ice Sheet", "#dfeaf3", 1.0, true, 0);
        }

        if (tile.Elevation == Elevation.Coast)
        {
            var name = tile.RegionId is { } regionId
                    && state.Regions.TryGetValue(regionId, out var region)
                    && region.FinalBiomeName == "Sea"
                ? "Sea"
                : state.Map.IsOuterWaterBody(tile) ? "Coast" : "Lake";
            return new ResolvedTerrain(name, name == "Lake" ? "#4fa7d8" : CoastColor, int.MaxValue, false, 0);
        }

        return new ResolvedTerrain("Ocean", OceanColor, int.MaxValue, false, 0);
    }

    private static ResolvedTerrain ResolveWater(HexTile tile)
    {
        return tile.Elevation switch
        {
            Elevation.DeepIce => new ResolvedTerrain("Ocean Ice Sheet", "#dfeaf3", 1.0, true, 0),
            Elevation.Coast => new ResolvedTerrain("Coast", CoastColor, int.MaxValue, false, 0),
            _ => new ResolvedTerrain("Ocean", OceanColor, int.MaxValue, false, 0)
        };
    }

    private static string ColorFor(string name)
    {
        // Colors are keyed by final terrain name because vegetation modifiers can
        // turn different base biomes into the same gameplay terrain.
        var key = StripTemperaturePrefix(name);
        return key switch
        {
            "Desert" => "#d8bd72",
            "Wasteland" => "#9b9278",
            "Badlands" => "#b07a4f",
            "Dryland" => "#bda463",
            "Plain" => "#8fca5a",
            "Floodplain" => "#7fb66a",
            "Wetland" => "#4f8d69",
            "Swamp" => "#356f49",
            "Savanna" => "#9da64b",
            "Jungle" => "#1f6f35",
            "Rainforest" => "#247f3d",
            "Steppe" => "#b7a76a",
            "Prairie" => "#a6bd63",
            "Grassland" => "#8fca5a",
            "Shrubland" => "#6fa85a",
            "Forest" => "#2f8a3e",
            "Taiga" => "#1f5f58",
            "Tundra" => "#9b8f72",
            "Ice Sheet" => "#eef4f7",
            "Ocean Ice Sheet" => "#dfeaf3",
            "Sea" => "#4f9fd1",
            "Lake" => "#4fa7d8",
            _ => "#8fca5a"
        };
    }

    private static string StripTemperaturePrefix(string name)
    {
        foreach (var temperature in Enum.GetNames<TemperatureBand>())
        {
            var prefix = temperature + " ";
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return name[prefix.Length..];
            }
        }

        return name;
    }

    private static double MovementCost(Elevation elevation, Vegetation vegetation, List<string> features)
    {
        // Elevation supplies the base cost. Vegetation and special features add
        // friction on top of that base.
        var cost = elevation switch
        {
            Elevation.DeepIce => 1.0,
            Elevation.Flat => 1,
            Elevation.Hills => 1.5,
            Elevation.Mountains => 2.0,
            Elevation.Peaks => 2.0,
            _ => 1
        };

        cost += vegetation switch
        {
            Vegetation.Sparse => 0.5,
            Vegetation.Lush => 1.0,
            _ => 0
        };

        if (features.Contains("volcano", StringComparer.OrdinalIgnoreCase))
        {
            cost += 1.0;
        }

        return cost;
    }

    private static int DefenseModifier(Elevation elevation, Vegetation vegetation, List<string> features)
    {
        // Defense uses the same terrain ingredients as movement, but with larger
        // bonuses for rugged elevation.
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
            Vegetation.Lush => 1,
            _ => 0
        };

        if (features.Contains("volcano", StringComparer.OrdinalIgnoreCase))
        {
            defense += 1;
        }

        return defense;
    }
}
