using System.Text.Json;

namespace StrategyGame.Core;

public static class GameStateSerializer
{
    // Increment this when the save shape changes in a way older code cannot
    // safely read. The loader refuses unknown versions instead of guessing.
    private const int CurrentVersion = 17;

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
            // Tile GroupIds are restored from the save so the map can
            // answer "what is on this hex?" immediately after loading.
            var loadedTile = new HexTile
            {
                Coord = new HexCoord(tile.Q, tile.R),
                Elevation = tile.Elevation,
                Moisture = tile.Moisture,
                WaterBodyKind = tile.WaterBodyKind,
                RegionId = tile.RegionId,
                ResourceId = tile.ResourceId,
                CityId = tile.CityId
            };

            loadedTile.FeatureIds.AddRange(tile.FeatureIds);
            loadedTile.GroupIds.AddRange(tile.GroupIds);
            map.Add(loadedTile);
        }

        var state = new GameState
        {
            Database = database,
            Map = map,
            WorldGeneration = snapshot.WorldGeneration.ToState(),
            CurrentFactionIndex = snapshot.CurrentFactionIndex,
            Turn = snapshot.Turn,
            FogOfWarEnabled = snapshot.FogOfWarEnabled
        };

        foreach (var faction in snapshot.Factions)
        {
            // Factions are restored before pieces because groups and
            // cities refer to faction ids for ownership and coloring.
            state.Factions.Add(new FactionState
            {
                Id = faction.Id,
                Type = faction.Type,
                Name = faction.Name,
                Color = faction.Color,
                Description = faction.Description,
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
                Name = region.Name,
                Moisture = region.Moisture,
                Temperature = region.Temperature,
                BaseBiome = region.BaseBiome,
                FinalBiomeName = region.FinalBiomeName
            };

            loadedRegion.TileCoords.AddRange(region.TileCoords.Select(c => new HexCoord(c.Q, c.R)));
            state.Regions[loadedRegion.Id] = loadedRegion;
        }

        foreach (var group in snapshot.Groups)
        {
            // Group unit rows are nested under the group snapshot. The group's
            // position is restored here; tile.GroupIds were restored earlier.
            var loadedGroup = new GroupState
            {
                Id = group.Id,
                Name = group.Name,
                FactionId = group.FactionId,
                Coord = new HexCoord(group.Q, group.R),
                MovementLeft = group.MovementLeft,
                StationedCityId = group.StationedCityId
            };

            foreach (var unit in group.Units)
            {
                loadedGroup.Units.Add(new UnitInstance { Id = unit.Id, TypeId = unit.TypeId, Name = unit.Name });
            }

            state.Groups[loadedGroup.Id] = loadedGroup;
        }

        foreach (var city in snapshot.Cities)
        {
            var loadedCity = new CityState
            {
                Id = city.Id,
                Name = city.Name,
                FactionId = city.FactionId,
                Coord = new HexCoord(city.Q, city.R),
                TownCenterLevel = city.TownCenterLevel
            };
            loadedCity.StationedGroupIds.AddRange(city.StationedGroupIds);
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
            FogOfWarEnabled = state.FogOfWarEnabled,
            Factions = state.Factions
                .Select(f => new FactionSnapshot(f.Id, f.Type, f.Name, f.Color, f.Description, f.IsPlayer))
                .ToList(),
            Regions = state.Regions.Values
                .OrderBy(r => r.Id)
                .Select(r => new RegionSnapshot(
                    r.Id,
                    r.Name,
                    r.TileCoords.Select(c => new CoordSnapshot(c.Q, c.R)).ToList(),
                    r.Moisture,
                    r.Temperature,
                    r.BaseBiome,
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
                    t.WaterBodyKind,
                    t.RegionId,
                    t.FeatureIds.ToList(),
                    t.ResourceId,
                    t.CityId,
                    t.GroupIds.ToList()))
                .ToList(),
            Groups = state.Groups.Values
                .OrderBy(g => g.Id)
                .Select(g => new GroupSnapshot(
                    g.Id,
                    g.Name,
                    g.FactionId,
                    g.Coord.Q,
                    g.Coord.R,
                    g.MovementLeft,
                    g.StationedCityId,
                    g.Units.Select(u => new UnitSnapshot(u.Id, u.TypeId, u.Name)).ToList()))
                .ToList(),
            Cities = state.Cities.Values
                .OrderBy(c => c.Id)
                .Select(c => new CitySnapshot(
                    c.Id,
                    c.Name,
                    c.FactionId,
                    c.Coord.Q,
                    c.Coord.R,
                    c.TownCenterLevel,
                    c.StationedGroupIds.ToList()))
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
        public bool FogOfWarEnabled { get; set; }
        public List<FactionSnapshot> Factions { get; set; } = [];
        public List<RegionSnapshot> Regions { get; set; } = [];
        public List<TileSnapshot> Tiles { get; set; } = [];
        public List<GroupSnapshot> Groups { get; set; } = [];
        public List<CitySnapshot> Cities { get; set; } = [];
        public List<LogSnapshot> Log { get; set; } = [];
    }

    private sealed record FactionSnapshot(string Id, string Type, string Name, string Color, string Description, bool IsPlayer);
    private sealed record CoordSnapshot(int Q, int R);
    private sealed record WorldGenerationSnapshot(int MapSize, int Civilizations, int Wetness, int GrasslandShrublandBias, int DesertBadlandsBias, int ConiferBroadleafForestBias, int ElevationVariance, int MaxSeaNumber, ClimateBias ClimateBias, List<string> AllowedFactionIds)
    {
        public static WorldGenerationSnapshot FromState(WorldGenerationSettings settings)
        {
            // Use a snapshot record instead of serializing the runtime settings
            // object directly so the save format remains explicit and versioned.
            return new WorldGenerationSnapshot(
                settings.MapSize,
                settings.Civilizations,
                settings.Wetness,
                settings.GrasslandShrublandBias,
                settings.DesertBadlandsBias,
                settings.ConiferBroadleafForestBias,
                settings.ElevationVariance,
                settings.MaxSeaNumber,
                settings.ClimateBias,
                settings.AllowedFactionIds.ToList());
        }

        public WorldGenerationSettings ToState()
        {
            // Rebuild the runtime settings object used by generation and HUD/debug
            // code after loading a save.
            return new WorldGenerationSettings
            {
                MapSize = MapSize,
                Civilizations = Civilizations,
                Wetness = Wetness,
                GrasslandShrublandBias = GrasslandShrublandBias,
                DesertBadlandsBias = DesertBadlandsBias,
                ConiferBroadleafForestBias = ConiferBroadleafForestBias,
                ElevationVariance = ElevationVariance,
                MaxSeaNumber = MaxSeaNumber,
                ClimateBias = ClimateBias,
                AllowedFactionIds = AllowedFactionIds.ToList()
            };
        }
    }
    private sealed record RegionSnapshot(int Id, string Name, List<CoordSnapshot> TileCoords, MoistureLevel Moisture, TemperatureBand Temperature, BaseBiome BaseBiome, string FinalBiomeName);
    private sealed record TileSnapshot(int Q, int R, Elevation Elevation, MoistureLevel Moisture, WaterBodyKind WaterBodyKind, int? RegionId, List<string> FeatureIds, string? ResourceId, int? CityId, List<int> GroupIds);
    private sealed record GroupSnapshot(int Id, string Name, string FactionId, int Q, int R, double MovementLeft, int? StationedCityId, List<UnitSnapshot> Units);
    private sealed record UnitSnapshot(int Id, string TypeId, string? Name);
    private sealed record CitySnapshot(int Id, string Name, string FactionId, int Q, int R, int TownCenterLevel, List<int> StationedGroupIds);
    private sealed record LogSnapshot(int Turn, string Text);
}
