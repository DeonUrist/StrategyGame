namespace StrategyGame.Core;

public static class MapGenerator
{
    public static GameState CreateSandbox(GameDatabase database, int seed)
    {
        // The sandbox generator is deliberately simple and deterministic.
        // Same database + same seed means the same map, factions, cities, armies,
        // and agents every time. That makes bugs easier to reproduce.
        var random = new Random(seed);
        var state = new GameState { Database = database, Map = new HexMap() };

        foreach (var faction in database.Factions.Values)
        {
            state.Factions.Add(new FactionState
            {
                Id = faction.Id,
                Name = faction.Name,
                Color = faction.Color,
                IsPlayer = faction.IsPlayer
            });
        }

        for (var q = 0; q < 20; q++)
        {
            for (var r = 0; r < 16; r++)
            {
                // This creates rough water bands without a full noise library.
                // Land tiles then get random terrain, and sometimes a feature or resource.
                var waterScore = Math.Sin((q + seed) * 0.52) + Math.Cos((r - seed) * 0.43) + random.NextDouble() * 0.7;
                var terrain = waterScore < -0.85 ? "water" : PickLandTerrain(random);
                var tile = new HexTile { Coord = new HexCoord(q, r), TerrainId = terrain };

                if (terrain != "water")
                {
                    if (random.NextDouble() < 0.18)
                    {
                        tile.FeatureId = Pick(random, "forest", "hills", "mountains");
                    }

                    if (random.NextDouble() < 0.10)
                    {
                        tile.ResourceId = Pick(random, "rice", "gold", "copper");
                    }
                }

                state.Map.Add(tile);
            }
        }

        var starts = new[] { new HexCoord(3, 3), new HexCoord(15, 4), new HexCoord(10, 12) };
        for (var i = 0; i < state.Factions.Count; i++)
        {
            // Each faction starts with one city, one army stack, and one agent.
            // If the preferred start is water, FindNearestPassable moves it to land.
            var faction = state.Factions[i];
            var start = FindNearestPassable(state, starts[i % starts.Length]);
            AddCity(state, i + 1, $"{faction.Name} Hold", faction.Id, start);
            AddStack(state, i + 1, faction.Id, start, ("militia", 8), ("spearmen", 3));
            AddAgent(state, i + 1, faction.Id, i == 0 ? "captain" : "scout", i == 0 ? "Aldren" : $"{faction.Name} Scout", start);
        }

        state.AddLog("Sandbox world created.");
        GameRules.ResetFactionMovement(state, state.CurrentFaction.Id);
        return state;
    }

    private static string PickLandTerrain(Random random) => Pick(random, "plains", "grassland", "desert", "swamp");
    private static string Pick(Random random, params string[] values) => values[random.Next(values.Length)];

    private static HexCoord FindNearestPassable(GameState state, HexCoord preferred)
    {
        return state.Map.Tiles
            .Where(t => state.Database.Terrains[t.TerrainId].Passable)
            .OrderBy(t => t.Coord.DistanceTo(preferred))
            .First()
            .Coord;
    }

    private static void AddCity(GameState state, int id, string name, string factionId, HexCoord coord)
    {
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
        state.Agents[id] = new AgentState { Id = id, FactionId = factionId, TypeId = typeId, Name = name, Coord = coord };
        state.Map.Get(coord).AgentIds.Add(id);
    }
}
