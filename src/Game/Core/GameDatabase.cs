using System.Text.Json;

namespace StrategyGame.Core;

public sealed class GameDatabase
{
    public required IReadOnlyDictionary<string, TerrainDefinition> Terrains { get; init; }
    public required IReadOnlyDictionary<string, FeatureDefinition> Features { get; init; }
    public required IReadOnlyDictionary<string, ResourceDefinition> Resources { get; init; }
    public required IReadOnlyDictionary<string, UnitDefinition> Units { get; init; }
    public required IReadOnlyDictionary<string, BuildingDefinition> Buildings { get; init; }
    public required IReadOnlyDictionary<string, FactionDefinition> Factions { get; init; }
    public required IReadOnlyDictionary<string, EventDefinition> Events { get; init; }

    public static GameDatabase LoadFromDirectory(string directory)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return new GameDatabase
        {
            Terrains = Load<TerrainDefinition>(directory, "terrain.json", options),
            Features = Load<FeatureDefinition>(directory, "features.json", options),
            Resources = Load<ResourceDefinition>(directory, "resources.json", options),
            Units = Load<UnitDefinition>(directory, "units.json", options),
            Buildings = Load<BuildingDefinition>(directory, "buildings.json", options),
            Factions = Load<FactionDefinition>(directory, "factions.json", options),
            Events = Load<EventDefinition>(directory, "events.json", options)
        };
    }

    private static IReadOnlyDictionary<string, T> Load<T>(string directory, string file, JsonSerializerOptions options)
    {
        var path = Path.Combine(directory, file);
        var items = JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), options)
            ?? throw new InvalidOperationException($"Could not load {path}.");

        return items.ToDictionary(GetId, StringComparer.OrdinalIgnoreCase);
    }

    private static string GetId<T>(T item)
    {
        var property = typeof(T).GetProperty("Id") ?? throw new InvalidOperationException($"{typeof(T).Name} has no Id property.");
        return (string)(property.GetValue(item) ?? throw new InvalidOperationException($"{typeof(T).Name} has a null Id."));
    }
}
