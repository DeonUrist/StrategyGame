namespace StrategyGame.Core;

// All catalog definition records implement IHasId so the generic database
// loader can key them by Id without reflection.
public interface IHasId { string Id { get; } }

// Definition records are the authored data shapes loaded from JSON or code.
// They describe what a thing is in the rules database, not a live instance on
// the board. Runtime state lives in WorldModels.cs.
public sealed record ResourceDefinition(string Id, string Name) : IHasId;
public sealed record UnitDefinition(
    string Id,
    string Name,
    int Damage,
    int Health,
    int Armor,
    double Movement,
    double Strength,
    string AttackType) : IHasId;
public sealed record BuildingDefinition(string Id, List<BuildingLevelDefinition> Levels) : IHasId;
public sealed record BuildingLevelDefinition(int Level, string Name, string Sprite);
public sealed record FactionDefinition(
    string Id,
    string Type,
    string Name,
    string RaceId,
    string Color,
    bool IsPlayer,
    string Description,
    List<string> CityNames,
    int StartingPopulation,
    Dictionary<string, int> StartingArmy,
    List<UnitDefinition> Units) : IHasId;
public sealed record EventDefinition(string Id, string Name, int BaseWeight) : IHasId;

public enum ResourceCategory
{
    Supplies,
    Materials,
    CommonGoods,
    LuxuryGoods,
    Armaments
}

public enum LocationKind
{
    Settlement,
    Farm,
    Camp,
    Mine
}

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

// Regions use moisture and temperature to choose a base biome. A few terrain
// pairs use explicit worldgen bias sliders to pick their regional variant.
public enum MoistureLevel
{
    Dry,
    Normal,
    Wet
}

public enum TemperatureBand
{
    Subarctic,
    Arctic,
    Temperate,
    Tropical
}

public enum ClimateBias
{
    Hot,
    Normal,
    Cold
}

public enum BaseBiome
{
    IceSheet,
    Tundra,
    Taiga,
    Desert,
    Badlands,
    Grassland,
    Shrubland,
    ConiferForest,
    BroadleafForest,
    Swamp,
    Jungle
}

// ResolvedTerrain is the final gameplay-facing terrain result after the region
// biome, elevation, and features are combined.
public sealed record ResolvedTerrain(string Name, double MovementCost, bool Passable, int DefenseModifier);
