using System.Text.Json;

namespace StrategyGame.Core;

public static class GameStateSerializer
{
    private const int CurrentVersion = 2;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static void SaveToFile(GameState state, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, ToJson(state));
    }

    public static GameState LoadFromFile(GameDatabase database, string path)
    {
        return FromJson(database, File.ReadAllText(path));
    }

    public static string ToJson(GameState state)
    {
        // Runtime objects contain dictionaries and lookup lists that are convenient
        // for gameplay. The snapshot below stores the same information in a stable,
        // versioned save format.
        return JsonSerializer.Serialize(ToSnapshot(state), Options);
    }

    public static GameState FromJson(GameDatabase database, string json)
    {
        var snapshot = JsonSerializer.Deserialize<GameStateSnapshot>(json, Options)
            ?? throw new InvalidOperationException("Could not load saved game state.");

        if (snapshot.Version != CurrentVersion)
        {
            throw new InvalidOperationException($"Unsupported save version {snapshot.Version}.");
        }

        var map = new HexMap();
        foreach (var tile in snapshot.Tiles.OrderBy(t => t.Q).ThenBy(t => t.R))
        {
            // Tile StackIds and AgentIds are restored from the save so the map can
            // answer "what is on this hex?" immediately after loading.
            var loadedTile = new HexTile
            {
                Coord = new HexCoord(tile.Q, tile.R),
                Climate = tile.Climate,
                Rainfall = tile.Rainfall,
                Elevation = tile.Elevation,
                Vegetation = tile.Vegetation,
                ResourceId = tile.ResourceId,
                CityId = tile.CityId
            };

            loadedTile.FeatureIds.AddRange(tile.FeatureIds);
            loadedTile.StackIds.AddRange(tile.StackIds);
            loadedTile.AgentIds.AddRange(tile.AgentIds);
            map.Add(loadedTile);
        }

        var state = new GameState
        {
            Database = database,
            Map = map,
            CurrentFactionIndex = snapshot.CurrentFactionIndex,
            Turn = snapshot.Turn
        };

        foreach (var faction in snapshot.Factions)
        {
            state.Factions.Add(new FactionState
            {
                Id = faction.Id,
                Name = faction.Name,
                Color = faction.Color,
                IsPlayer = faction.IsPlayer
            });
        }

        foreach (var stack in snapshot.Stacks)
        {
            var loadedStack = new StackState
            {
                Id = stack.Id,
                FactionId = stack.FactionId,
                Coord = new HexCoord(stack.Q, stack.R),
                MovementLeft = stack.MovementLeft,
                LeaderAgentId = stack.LeaderAgentId
            };

            foreach (var unit in stack.Units)
            {
                loadedStack.Units.Add(new UnitInstance { TypeId = unit.TypeId, Count = unit.Count });
            }

            state.Stacks[loadedStack.Id] = loadedStack;
        }

        foreach (var agent in snapshot.Agents)
        {
            state.Agents[agent.Id] = new AgentState
            {
                Id = agent.Id,
                FactionId = agent.FactionId,
                TypeId = agent.TypeId,
                Name = agent.Name,
                Coord = new HexCoord(agent.Q, agent.R),
                MovementLeft = agent.MovementLeft,
                JoinedStackId = agent.JoinedStackId
            };
        }

        foreach (var city in snapshot.Cities)
        {
            var loadedCity = new CityState
            {
                Id = city.Id,
                Name = city.Name,
                FactionId = city.FactionId,
                Coord = new HexCoord(city.Q, city.R)
            };
            loadedCity.BuildingIds.Clear();
            loadedCity.BuildingIds.AddRange(city.BuildingIds);
            state.Cities[loadedCity.Id] = loadedCity;
        }

        foreach (var entry in snapshot.Log)
        {
            state.Log.Add(new GameLogEntry { Turn = entry.Turn, Text = entry.Text });
        }

        return state;
    }

    private static GameStateSnapshot ToSnapshot(GameState state)
    {
        return new GameStateSnapshot
        {
            Version = CurrentVersion,
            CurrentFactionIndex = state.CurrentFactionIndex,
            Turn = state.Turn,
            Factions = state.Factions
                .Select(f => new FactionSnapshot(f.Id, f.Name, f.Color, f.IsPlayer))
                .ToList(),
            Tiles = state.Map.Tiles
                .OrderBy(t => t.Coord.Q)
                .ThenBy(t => t.Coord.R)
                .Select(t => new TileSnapshot(
                    t.Coord.Q,
                    t.Coord.R,
                    t.Climate,
                    t.Rainfall,
                    t.Elevation,
                    t.Vegetation,
                    t.FeatureIds.ToList(),
                    t.ResourceId,
                    t.CityId,
                    t.StackIds.ToList(),
                    t.AgentIds.ToList()))
                .ToList(),
            Stacks = state.Stacks.Values
                .OrderBy(s => s.Id)
                .Select(s => new StackSnapshot(
                    s.Id,
                    s.FactionId,
                    s.Coord.Q,
                    s.Coord.R,
                    s.MovementLeft,
                    s.LeaderAgentId,
                    s.Units.Select(u => new UnitSnapshot(u.TypeId, u.Count)).ToList()))
                .ToList(),
            Agents = state.Agents.Values
                .OrderBy(a => a.Id)
                .Select(a => new AgentSnapshot(
                    a.Id,
                    a.FactionId,
                    a.TypeId,
                    a.Name,
                    a.Coord.Q,
                    a.Coord.R,
                    a.MovementLeft,
                    a.JoinedStackId))
                .ToList(),
            Cities = state.Cities.Values
                .OrderBy(c => c.Id)
                .Select(c => new CitySnapshot(
                    c.Id,
                    c.Name,
                    c.FactionId,
                    c.Coord.Q,
                    c.Coord.R,
                    c.BuildingIds.ToList()))
                .ToList(),
            Log = state.Log
                .Select(e => new LogSnapshot(e.Turn, e.Text))
                .ToList()
        };
    }

    private sealed class GameStateSnapshot
    {
        public int Version { get; set; }
        public int CurrentFactionIndex { get; set; }
        public int Turn { get; set; }
        public List<FactionSnapshot> Factions { get; set; } = [];
        public List<TileSnapshot> Tiles { get; set; } = [];
        public List<StackSnapshot> Stacks { get; set; } = [];
        public List<AgentSnapshot> Agents { get; set; } = [];
        public List<CitySnapshot> Cities { get; set; } = [];
        public List<LogSnapshot> Log { get; set; } = [];
    }

    private sealed record FactionSnapshot(string Id, string Name, string Color, bool IsPlayer);
    private sealed record TileSnapshot(int Q, int R, Climate Climate, Rainfall Rainfall, Elevation Elevation, Vegetation Vegetation, List<string> FeatureIds, string? ResourceId, int? CityId, List<int> StackIds, List<int> AgentIds);
    private sealed record StackSnapshot(int Id, string FactionId, int Q, int R, int MovementLeft, int? LeaderAgentId, List<UnitSnapshot> Units);
    private sealed record UnitSnapshot(string TypeId, int Count);
    private sealed record AgentSnapshot(int Id, string FactionId, string TypeId, string Name, int Q, int R, int MovementLeft, int? JoinedStackId);
    private sealed record CitySnapshot(int Id, string Name, string FactionId, int Q, int R, List<string> BuildingIds);
    private sealed record LogSnapshot(int Turn, string Text);
}
