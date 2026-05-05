using System.Text.Json;

namespace StrategyGame.Core;

public static class GameStateSerializer
{
    // Increment this when the save shape changes in a way older code cannot
    // safely read. The loader refuses unknown versions instead of guessing.
    private const int CurrentVersion = 9;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static void SaveToFile(GameState state, string path)
    {
        // Godot user:// paths may live in a directory that has not been created
        // yet, so create the parent before writing the JSON.
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
        // Deserialize into plain snapshot records/classes first. Runtime objects
        // have computed helpers and dictionaries that are easier to rebuild than
        // to serialize directly.
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
                Elevation = tile.Elevation,
                Moisture = tile.Moisture,
                Vegetation = tile.Vegetation,
                WaterBodyKind = tile.WaterBodyKind,
                RegionId = tile.RegionId,
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
            WorldGeneration = snapshot.WorldGeneration.ToState(),
            CurrentFactionIndex = snapshot.CurrentFactionIndex,
            Turn = snapshot.Turn
        };

        foreach (var faction in snapshot.Factions)
        {
            // Factions are restored before pieces because stacks, agents, and
            // cities refer to faction ids for ownership and coloring.
            state.Factions.Add(new FactionState
            {
                Id = faction.Id,
                Name = faction.Name,
                Color = faction.Color,
                IsPlayer = faction.IsPlayer
            });
        }

        foreach (var region in snapshot.Regions)
        {
            // Regions are saved as first-class world objects. Tiles refer back to
            // them by RegionId, but the region also keeps its tile list for
            // future world-info and population systems.
            var loadedRegion = new RegionState
            {
                Id = region.Id,
                Moisture = region.Moisture,
                WaterRetention = region.WaterRetention,
                Temperature = region.Temperature,
                BaseBiome = region.BaseBiome,
                Vegetation = region.Vegetation,
                FinalBiomeName = region.FinalBiomeName
            };

            loadedRegion.TileCoords.AddRange(region.TileCoords.Select(c => new HexCoord(c.Q, c.R)));
            state.Regions[loadedRegion.Id] = loadedRegion;
        }

        foreach (var stack in snapshot.Stacks)
        {
            // Stack unit rows are nested under the stack snapshot. The stack's
            // position is restored here; tile.StackIds were restored earlier.
            var loadedStack = new StackState
            {
                Id = stack.Id,
                FactionId = stack.FactionId,
                Coord = new HexCoord(stack.Q, stack.R),
                MovementLeft = stack.MovementLeft
            };
            loadedStack.JoinedAgentIds.AddRange(stack.JoinedAgentIds);

            foreach (var unit in stack.Units)
            {
                loadedStack.Units.Add(new UnitInstance { TypeId = unit.TypeId, Count = unit.Count });
            }

            state.Stacks[loadedStack.Id] = loadedStack;
        }

        foreach (var agent in snapshot.Agents)
        {
            // JoinedStackId restores the leader relationship. If it is set, the
            // agent should not also be present as a loose map piece in AgentIds.
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
            // CityState starts with campsite by default, so clear that default
            // before adding the saved building chain level.
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
        // Sort collections before saving so deterministic tests can compare JSON
        // strings directly and source control diffs stay stable.
        return new GameStateSnapshot
        {
            Version = CurrentVersion,
            WorldGeneration = WorldGenerationSnapshot.FromState(state.WorldGeneration),
            CurrentFactionIndex = state.CurrentFactionIndex,
            Turn = state.Turn,
            Factions = state.Factions
                .Select(f => new FactionSnapshot(f.Id, f.Name, f.Color, f.IsPlayer))
                .ToList(),
            Regions = state.Regions.Values
                .OrderBy(r => r.Id)
                .Select(r => new RegionSnapshot(
                    r.Id,
                    r.TileCoords.Select(c => new CoordSnapshot(c.Q, c.R)).ToList(),
                    r.Moisture,
                    r.WaterRetention,
                    r.Temperature,
                    r.BaseBiome,
                    r.Vegetation,
                    r.FinalBiomeName))
                .ToList(),
            Tiles = state.Map.Tiles
                .OrderBy(t => t.Coord.Q)
                .ThenBy(t => t.Coord.R)
                .Select(t => new TileSnapshot(
                    t.Coord.Q,
                    t.Coord.R,
                    t.Elevation,
                    t.Moisture,
                    t.Vegetation,
                    t.WaterBodyKind,
                    t.RegionId,
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
                    s.JoinedAgentIds.ToList(),
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
        // This DTO is the root save shape. It intentionally mirrors the runtime
        // state at a simple data level and avoids behavior-heavy game classes.
        public int Version { get; set; }
        public WorldGenerationSnapshot WorldGeneration { get; set; } = WorldGenerationSnapshot.FromState(new WorldGenerationSettings());
        public int CurrentFactionIndex { get; set; }
        public int Turn { get; set; }
        public List<FactionSnapshot> Factions { get; set; } = [];
        public List<RegionSnapshot> Regions { get; set; } = [];
        public List<TileSnapshot> Tiles { get; set; } = [];
        public List<StackSnapshot> Stacks { get; set; } = [];
        public List<AgentSnapshot> Agents { get; set; } = [];
        public List<CitySnapshot> Cities { get; set; } = [];
        public List<LogSnapshot> Log { get; set; } = [];
    }

    private sealed record FactionSnapshot(string Id, string Name, string Color, bool IsPlayer);
    private sealed record CoordSnapshot(int Q, int R);
    private sealed record WorldGenerationSnapshot(int MapSize, int Wetness, int Vegetation, int ElevationVariance, int MaxSeaNumber, ClimateBias ClimateBias)
    {
        public static WorldGenerationSnapshot FromState(WorldGenerationSettings settings)
        {
            // Use a snapshot record instead of serializing the runtime settings
            // object directly so the save format remains explicit and versioned.
            return new WorldGenerationSnapshot(settings.MapSize, settings.Wetness, settings.Vegetation, settings.ElevationVariance, settings.MaxSeaNumber, settings.ClimateBias);
        }

        public WorldGenerationSettings ToState()
        {
            // Rebuild the runtime settings object used by generation and HUD/debug
            // code after loading a save.
            return new WorldGenerationSettings
            {
                MapSize = MapSize,
                Wetness = Wetness,
                Vegetation = Vegetation,
                ElevationVariance = ElevationVariance,
                MaxSeaNumber = MaxSeaNumber,
                ClimateBias = ClimateBias
            };
        }
    }
    private sealed record RegionSnapshot(int Id, List<CoordSnapshot> TileCoords, MoistureLevel Moisture, WaterRetention WaterRetention, TemperatureBand Temperature, BaseBiome BaseBiome, Vegetation Vegetation, string FinalBiomeName);
    private sealed record TileSnapshot(int Q, int R, Elevation Elevation, MoistureLevel Moisture, Vegetation Vegetation, WaterBodyKind WaterBodyKind, int? RegionId, List<string> FeatureIds, string? ResourceId, int? CityId, List<int> StackIds, List<int> AgentIds);
    private sealed record StackSnapshot(int Id, string FactionId, int Q, int R, double MovementLeft, List<int> JoinedAgentIds, List<UnitSnapshot> Units);
    private sealed record UnitSnapshot(string TypeId, int Count);
    private sealed record AgentSnapshot(int Id, string FactionId, string TypeId, string Name, int Q, int R, double MovementLeft, int? JoinedStackId);
    private sealed record CitySnapshot(int Id, string Name, string FactionId, int Q, int R, List<string> BuildingIds);
    private sealed record LogSnapshot(int Turn, string Text);
}
