using System.Text.Json;

namespace StrategyGame.Core;

public sealed class GameDatabase
{
    // Resources are code-defined for now because placement rules are also code
    // driven. The other catalogs are JSON-authored so they can grow without
    // touching C# rule code.
    private static readonly IReadOnlyDictionary<string, ResourceDefinition> CodeResources =
        new[]
        {
            new ResourceDefinition("copper", "Copper"),
            new ResourceDefinition("iron", "Iron"),
            new ResourceDefinition("gold", "Gold"),
            new ResourceDefinition("silver", "Silver"),
            new ResourceDefinition("game", "Game")
        }.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);

    public required IReadOnlyDictionary<string, ResourceDefinition> Resources { get; init; }
    public required IReadOnlyDictionary<string, UnitDefinition> Units { get; init; }
    public required IReadOnlyDictionary<string, BuildingDefinition> Buildings { get; init; }
    public required IReadOnlyDictionary<string, FactionDefinition> Factions { get; init; }
    public required IReadOnlyDictionary<string, EventDefinition> Events { get; init; }

    public static GameDatabase LoadFromDirectory(string directory)
    {
        // All game content is loaded from JSON catalogs.
        // Terrain and resources are intentionally code-defined because terrain is
        // built from generated properties and resources currently have map rules.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return new GameDatabase
        {
            Resources = CodeResources,
            Units = Load<UnitDefinition>(directory, "units.json", options),
            Buildings = Load<BuildingDefinition>(directory, "buildings.json", options),
            Factions = Load<FactionDefinition>(directory, "factions.json", options),
            Events = Load<EventDefinition>(directory, "events.json", options)
        };
    }

    private static IReadOnlyDictionary<string, T> Load<T>(string directory, string file, JsonSerializerOptions options)
        where T : IHasId
    {
        // Catalog files are arrays of records. They are converted to dictionaries
        // by Id so rule code can do stable lookups like Units["militia"].
        var path = Path.Combine(directory, file);
        var items = JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), options)
            ?? throw new InvalidOperationException($"Could not load {path}.");

        return items.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
    }
}
