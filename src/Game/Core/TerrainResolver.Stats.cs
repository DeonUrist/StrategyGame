namespace StrategyGame.Core;

public static partial class TerrainResolver
{
    private static ResolvedTerrain ResolveWater(GameState state, HexTile tile)
    {
        if (tile.Elevation == Elevation.DeepIce)
        {
            return new ResolvedTerrain("Ocean Ice Sheet", 1.0, true, 0);
        }

        if (tile.Elevation == Elevation.Coast)
        {
            var name = tile.WaterBodyKind switch
            {
                WaterBodyKind.Sea => "Sea",
                WaterBodyKind.Lake => "Lake",
                _ => "Coast"
            };
            return new ResolvedTerrain(name, int.MaxValue, false, 0);
        }

        return new ResolvedTerrain("Ocean", int.MaxValue, false, 0);
    }

    private static ResolvedTerrain ResolveWater(HexTile tile)
    {
        return tile.Elevation switch
        {
            Elevation.DeepIce => new ResolvedTerrain("Ocean Ice Sheet", 1.0, true, 0),
            Elevation.Coast => new ResolvedTerrain("Coast", int.MaxValue, false, 0),
            _ => new ResolvedTerrain("Ocean", int.MaxValue, false, 0)
        };
    }

    private static double MovementCost(string terrainName, Elevation elevation, List<string> features)
    {
        var cost = 1.0;

        cost += terrainName switch
        {
            "Desert" or "Tundra" or "Badlands" or "Swamp" => 0.5,
            _ => 0
        };

        cost += terrainName switch
        {
            "Taiga" or "Conifer Forest" or "Broadleaf Forest" or "Jungle" or "Swamp" => 0.5,
            _ => 0
        };

        cost += terrainName switch
        {
            "Ice Sheet" => 1.0,
            _ => 0
        };

        cost += elevation switch
        {
            Elevation.Hills => 0.5,
            Elevation.Mountains or Elevation.Peaks => 1.0,
            _ => 0
        };

        if (features.Contains("volcano", StringComparer.OrdinalIgnoreCase))
        {
            cost += 1.0;
        }

        return cost;
    }

    private static int DefenseModifier(Elevation elevation, List<string> features)
    {
        var defense = elevation switch
        {
            Elevation.Hills => 2,
            Elevation.Mountains => 4,
            Elevation.Peaks => 5,
            _ => 0
        };

        if (features.Contains("volcano", StringComparer.OrdinalIgnoreCase))
        {
            defense += 1;
        }

        return defense;
    }
}
