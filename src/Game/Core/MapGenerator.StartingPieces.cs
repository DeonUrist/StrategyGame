namespace StrategyGame.Core;

public static partial class MapGenerator
{
    private static void AddStartingPieces(GameState state)
    {
        var mapSize = NormalizeMapSize(state.WorldGeneration.MapSize);
        var starts = new[]
        {
            ToCoord(mapSize * 28 / 100, mapSize * 47 / 100),
            ToCoord(mapSize * 50 / 100, mapSize * 31 / 100),
            ToCoord(mapSize * 34 / 100, mapSize * 22 / 100)
        };
        var stackId = 1;
        var agentId = 1;
        for (var i = 0; i < state.Factions.Count; i++)
        {
            // Each faction starts with one city, two army stacks, and two agents.
            // If the preferred start is water or rugged mountains, FindNearestStart
            // moves it to a flatter passable tile.
            var faction = state.Factions[i];
            var start = FindNearestStart(state, starts[i % starts.Length]);
            AddCity(state, i + 1, $"{faction.Name} Hold", faction.Id, start);
            AddStack(state, stackId++, faction.Id, start, ("militia", 8), ("spearmen", 3));
            AddStack(state, stackId++, faction.Id, start, ("militia", 5), ("spearmen", 2));
            AddAgent(state, agentId++, faction.Id, i == 0 ? "captain" : "scout", i == 0 ? "Aldren" : $"{faction.Name} Scout", start);
            AddAgent(state, agentId++, faction.Id, "scout", i == 0 ? "Mira" : $"{faction.Name} Agent", start);
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
        state.Cities[id] = new CityState { Id = id, Name = name, FactionId = factionId, Coord = coord };
        state.Map.Get(coord).CityId = id;
    }

    private static void AddStack(GameState state, int id, string factionId, HexCoord coord, params (string TypeId, int Count)[] units)
    {
        var stack = new StackState { Id = id, FactionId = factionId, Coord = coord };
        foreach (var unit in units)
        {
            stack.Units.Add(new UnitInstance { TypeId = unit.TypeId, Count = unit.Count });
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
}
