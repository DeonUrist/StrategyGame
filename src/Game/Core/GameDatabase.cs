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
        var factions = Load<FactionDefinition>(directory, "factions.json", options);
        return new GameDatabase
        {
            Resources = CodeResources,
            Units = LoadFactionUnits(directory, factions.Keys, options),
            Buildings = Load<BuildingDefinition>(directory, "buildings.json", options),
            Factions = factions,
            Events = Load<EventDefinition>(directory, "events.json", options)
        };
    }

    private static IReadOnlyDictionary<string, T> Load<T>(string directory, string file, JsonSerializerOptions options)
        where T : IHasId
    {
        // Catalog files are arrays of records. They are converted to dictionaries
        // by Id so rule code can do stable lookups like Factions["elves"].
        var path = Path.Combine(directory, file);
        var items = JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), options)
            ?? throw new InvalidOperationException($"Could not load {path}.");

        return items.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, UnitDefinition> LoadFactionUnits(string directory, IEnumerable<string> factionIds, JsonSerializerOptions options)
    {
        var units = new List<UnitDefinition>();
        foreach (var factionId in factionIds.Order(StringComparer.OrdinalIgnoreCase))
        {
            var factionDirectory = Path.Combine(directory, factionId);
            if (!Directory.Exists(factionDirectory))
            {
                throw new InvalidOperationException($"Missing unit directory {factionDirectory}.");
            }

            foreach (var file in Directory.GetFiles(factionDirectory, "*.json").OrderBy(Path.GetFileName))
            {
                var unit = JsonSerializer.Deserialize<UnitDefinition>(File.ReadAllText(file), options)
                    ?? throw new InvalidOperationException($"Could not load {file}.");
                units.Add(unit);
            }
        }

        return units.ToDictionary(unit => unit.Id, StringComparer.OrdinalIgnoreCase);
    }
}
