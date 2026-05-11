namespace StrategyGame.Core;

public static partial class MapGenerator
{
    private static void AddStartingPieces(GameState state, int seed)
    {
        var mapSize = NormalizeMapSize(state.WorldGeneration.MapSize);
        var starts = new[]
        {
            ToCoord(mapSize * 28 / 100, mapSize * 47 / 100),
            ToCoord(mapSize * 50 / 100, mapSize * 31 / 100),
            ToCoord(mapSize * 34 / 100, mapSize * 22 / 100),
            ToCoord(mapSize * 68 / 100, mapSize * 45 / 100),
            ToCoord(mapSize * 56 / 100, mapSize * 70 / 100),
            ToCoord(mapSize * 22 / 100, mapSize * 70 / 100)
        };
        var stackId = 1;
        var agentId = 1;
        for (var i = 0; i < state.Factions.Count; i++)
        {
            // Each faction starts with one city, one authored army stack, and one agent.
            // If the preferred start is water or rugged mountains, FindNearestStart
            // moves it to a flatter passable tile.
            var faction = state.Factions[i];
            var start = FindNearestStart(state, starts[i % starts.Length]);
            var cityName = CityNameGenerator.Generate(state.Database.Factions[faction.Id], seed ^ (i + 1));
            AddCity(state, i + 1, cityName, faction.Id, start);
            AddStack(state, stackId++, faction.Id, start, StartingArmyUnits(state, faction.Id));
            AddAgent(state, agentId++, faction.Id, UnitIdForRole(state, faction.Id, "agent"), $"{faction.Name} Agent", start);
        }
    }

    private static HexCoord FindNearestStart(GameState state, HexCoord preferred)
    {
        // Starts are designer-preferred coordinates, but the generated terrain
        // can turn them into water or peaks. Pick the nearest practical land tile
        // so every faction starts playable.
        return state.Map.Tiles
            .Where(t => TerrainResolver.Resolve(state, t).Passable && t.Elevation is Elevation.Flat or Elevation.Hills)
            .OrderBy(t => t.Coord.DistanceTo(preferred))
            .First()
            .Coord;
    }

    private static void AddCity(GameState state, int id, string name, string factionId, HexCoord coord)
    {
        // City ownership is stored on the CityState. The tile stores only the id
        // so map lookups can quickly find the city on a clicked hex.
        var city = new CityState { Id = id, Name = name, FactionId = factionId, Coord = coord };
        state.Cities[id] = city;
        state.Map.Get(coord).CityId = id;
    }

    private static void AddStack(GameState state, int id, string factionId, HexCoord coord, IEnumerable<string> units)
    {
        var stack = new StackState { Id = id, FactionId = factionId, Coord = coord };
        foreach (var typeId in units)
        {
            stack.Units.Add(new UnitInstance { TypeId = typeId });
        }

        state.Stacks[id] = stack;
        // Tiles keep ID lists so drawing and click lookup can quickly find units.
        state.Map.Get(coord).StackIds.Add(id);
    }

    private static void AddAgent(GameState state, int id, string factionId, string typeId, string name, HexCoord coord)
    {
        // New agents start loose on the map. Joining a stack later removes this
        // id from the tile's AgentIds list.
        state.Agents[id] = new AgentState { Id = id, FactionId = factionId, TypeId = typeId, Name = name, Coord = coord };
        state.Map.Get(coord).AgentIds.Add(id);
    }

    private static IEnumerable<string> StartingArmyUnits(GameState state, string factionId)
    {
        var faction = state.Database.Factions[factionId];
        foreach (var (role, quantity) in faction.StartingArmy)
        {
            var typeId = UnitIdForRole(state, factionId, role);
            for (var i = 0; i < quantity; i++)
            {
                yield return typeId;
            }
        }
    }

    private static string UnitIdForRole(GameState state, string factionId, string role)
    {
        return state.Database.Units.Values
            .Where(unit => string.Equals(unit.Role, role, StringComparison.OrdinalIgnoreCase))
            .First(unit => unit.Id.StartsWith($"{factionId}_", StringComparison.OrdinalIgnoreCase))
            .Id;
    }
}
