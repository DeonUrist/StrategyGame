namespace StrategyGame.Core;

// All catalog definition records implement IHasId so the generic database
// loader can key them by Id without reflection.
public interface IHasId { string Id { get; } }

// Definition records are the authored data shapes loaded from JSON or code.
// They describe what a thing is in the rules database, not a live instance on
// the board. Runtime state lives in WorldModels.cs.
public sealed record ResourceDefinition(string Id, string Name) : IHasId;
public sealed record UnitDefinition(string Id, string Name, string Kind, int Strength, double Movement) : IHasId;
public sealed record BuildingDefinition(string Id, string Name, int Level, string? UpgradesTo) : IHasId;
public sealed record FactionDefinition(string Id, string Name, string Color, bool IsPlayer) : IHasId;
public sealed record EventDefinition(string Id, string Name, int BaseWeight) : IHasId;

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

public enum WaterBodyKind
{
    None,
    Outer,
    Lake,
    Sea
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
