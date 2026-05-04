namespace StrategyGame.Core;

public sealed record TerrainDefinition(string Id, string Name, string Color, int MovementCost, bool Passable);
public sealed record FeatureDefinition(string Id, string Name, int MovementCostModifier, int DefenseModifier);
public sealed record ResourceDefinition(string Id, string Name);
public sealed record UnitDefinition(string Id, string Name, string Kind, int Strength, int Movement, int LeadershipBonus = 0);
public sealed record BuildingDefinition(string Id, string Name, int Level, string? UpgradesTo);
public sealed record FactionDefinition(string Id, string Name, string Color, bool IsPlayer);
public sealed record EventDefinition(string Id, string Name, int BaseWeight);
