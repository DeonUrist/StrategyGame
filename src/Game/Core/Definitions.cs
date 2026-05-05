namespace StrategyGame.Core;

public sealed record ResourceDefinition(string Id, string Name);
public sealed record UnitDefinition(string Id, string Name, string Kind, int Strength, int Movement, int LeadershipBonus = 0);
public sealed record BuildingDefinition(string Id, string Name, int Level, string? UpgradesTo);
public sealed record FactionDefinition(string Id, string Name, string Color, bool IsPlayer);
public sealed record EventDefinition(string Id, string Name, int BaseWeight);

public enum Climate
{
    Tropical,
    Temperate,
    Boreal,
    Polar
}

public enum Rainfall
{
    Low,
    Medium,
    High
}

public enum Elevation
{
    Water,
    Flat,
    Hills,
    Mountains,
    Peaks
}

public enum Vegetation
{
    None,
    Sparse,
    Forest
}

public sealed record ResolvedTerrain(string Name, string Color, int MovementCost, bool Passable, int DefenseModifier);
