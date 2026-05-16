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
        var groupId = 1;
        var unitId = 1;
        for (var i = 0; i < state.Factions.Count; i++)
        {
            // Each faction starts with one city and one stationed garrison group
            // containing authored starting units plus the faction agent.
            // If the preferred start is water or rugged mountains, FindNearestStart
            // moves it to a flatter passable tile.
            var faction = state.Factions[i];
            var start = FindNearestStart(state, starts[i % starts.Length]);
            var cityName = CityNameGenerator.Generate(state.Database.Factions[faction.Id], seed ^ (i + 1));
            AddCity(state, i + 1, cityName, faction.Id, start);
            AddGarrisonGroup(
                state,
                groupId++,
                faction.Id,
                start,
                StartingArmyUnits(state, faction.Id).Concat([UnitIdForRole(state, faction.Id, "agent")]),
                ref unitId,
                $"{faction.Name} Agent",
                i + 1);
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

    private static void AddGarrisonGroup(GameState state, int id, string factionId, HexCoord coord, IEnumerable<string> units, ref int unitId, string agentName, int cityId)
    {
        var group = new GroupState { Id = id, FactionId = factionId, Coord = coord, StationedCityId = cityId };
        foreach (var typeId in units)
        {
            var isAgent = state.Database.Units[typeId].Role.Equals("agent", StringComparison.OrdinalIgnoreCase);
            group.Units.Add(new UnitInstance { Id = unitId++, TypeId = typeId, Name = isAgent ? agentName : null });
        }

        state.Groups[id] = group;
        state.Cities[cityId].StationedGroupIds.Add(id);
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
