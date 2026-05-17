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
            // containing authored starting military units.
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
                StartingArmyUnits(state, faction.Id),
                ref unitId,
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
        // Location ownership is stored on the LocationState. The tile stores only
        // the id so map lookups can quickly find the location on a clicked hex.
        var city = new LocationState
        {
            Id = id,
            Kind = LocationKind.Settlement,
            Name = name,
            FactionId = factionId,
            Coord = coord,
            Population = state.Database.Factions[factionId].StartingPopulation
        };
        state.Cities[id] = city;
        state.Map.Get(coord).LocationId = id;
    }

    private static void AddGarrisonGroup(GameState state, int id, string factionId, HexCoord coord, IEnumerable<string> units, ref int unitId, int cityId)
    {
        var group = new GroupState { Id = id, FactionId = factionId, Coord = coord, StationedCityId = cityId };
        foreach (var typeId in units)
        {
            group.Units.Add(new UnitInstance { Id = unitId++, TypeId = typeId });
        }

        state.Groups[id] = group;
        state.Cities[cityId].StationedGroupIds.Add(id);
    }

    private static IEnumerable<string> StartingArmyUnits(GameState state, string factionId)
    {
        var faction = state.Database.Factions[factionId];
        foreach (var (unitId, quantity) in faction.StartingArmy)
        {
            var typeId = ValidateStartingUnitId(state, factionId, unitId);
            for (var i = 0; i < quantity; i++)
            {
                yield return typeId;
            }
        }
    }

    private static string ValidateStartingUnitId(GameState state, string factionId, string unitId)
    {
        if (!state.Database.Units.TryGetValue(factionId, out var units) || !units.ContainsKey(unitId))
        {
            throw new InvalidOperationException($"Faction {factionId} does not define starting unit {unitId}.");
        }

        return unitId;
    }
}
