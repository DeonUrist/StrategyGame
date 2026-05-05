namespace StrategyGame.Core;

// Definition records are the authored data shapes loaded from JSON or code.
// They describe what a thing is in the rules database, not a live instance on
// the board. Runtime state lives in WorldModels.cs.
public sealed record ResourceDefinition(string Id, string Name);
public sealed record UnitDefinition(string Id, string Name, string Kind, int Strength, double Movement, int LeadershipBonus = 0);
public sealed record BuildingDefinition(string Id, string Name, int Level, string? UpgradesTo);
public sealed record FactionDefinition(string Id, string Name, string Color, bool IsPlayer);
public sealed record EventDefinition(string Id, string Name, int BaseWeight);

public enum Elevation
{
    Ocean,
    Coast,
    DeepIce,
    Flat,
    Hills,
    Mountains,
    Peaks
}

public static class ElevationExtensions
{
    public static bool IsWaterLike(this Elevation elevation)
    {
        return elevation is Elevation.Ocean or Elevation.Coast or Elevation.DeepIce;
    }

    public static bool IsLiquidWater(this Elevation elevation)
    {
        return elevation is Elevation.Ocean or Elevation.Coast;
    }
}

// Regions use moisture and water retention to choose a base biome. Temperature
// is then applied as a north/south climate flavor layer.
public enum MoistureLevel
{
    Dry,
    Normal,
    Wet
}

public enum WaterRetention
{
    Draining,
    Normal,
    Holding
}

public enum TemperatureBand
{
    Tropical,
    Subtropical,
    Temperate,
    Subarctic,
    Arctic
}

public enum ClimateBias
{
    Hot,
    Normal,
    Cold
}

public enum BaseBiome
{
    Desert,
    Wasteland,
    Badlands,
    Dryland,
    Plain,
    Floodplain,
    Barrens,
    Wetland,
    Swamp
}

public enum Vegetation
{
    None,
    Sparse,
    Lush
}

// ResolvedTerrain is the final gameplay-facing terrain result after the region
// biome, elevation, vegetation, and features are combined.
public sealed record ResolvedTerrain(string Name, string Color, double MovementCost, bool Passable, int DefenseModifier);
