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
            new ResourceDefinition("game", "Game"),
            new ResourceDefinition("supplies", "Supplies"),
            new ResourceDefinition("materials", "Materials"),
            new ResourceDefinition("common_goods", "Common Goods"),
            new ResourceDefinition("luxury_goods", "Luxury Goods"),
            new ResourceDefinition("armaments", "Armaments")
        }.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);

    public required IReadOnlyDictionary<string, ResourceDefinition> Resources { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, UnitDefinition>> Units { get; init; }
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
            Units = LoadFactionUnits(factions),
            Buildings = Load<BuildingDefinition>(directory, "buildings.json", options),
            Factions = factions,
            Events = Load<EventDefinition>(directory, "events.json", options)
        };
    }

    public UnitDefinition Unit(string factionId, string unitId)
    {
        if (!Units.TryGetValue(factionId, out var factionUnits) || !factionUnits.TryGetValue(unitId, out var unit))
        {
            throw new KeyNotFoundException($"Unknown unit '{unitId}' for faction '{factionId}'.");
        }

        return unit;
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

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, UnitDefinition>> LoadFactionUnits(IReadOnlyDictionary<string, FactionDefinition> factions)
    {
        return factions.Values.ToDictionary(
            faction => faction.Id,
            faction => (IReadOnlyDictionary<string, UnitDefinition>)faction.Units.ToDictionary(unit => unit.Id, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }
}
