namespace StrategyGame.Core;

public static class MapGenerator
{
    public const int MapWidth = 32;
    public const int MapHeight = 32;

    public static GameState CreateSandbox(GameDatabase database, int seed)
    {
        // The sandbox generator is deterministic but built in layers.
        // This makes the final map easier to reason about:
        // 1. create the island and ocean,
        // 2. carve small inland lakes,
        // 3. add mountain chains and peaks,
        // 4. apply rainfall-driven vegetation clusters,
        // 5. place resources, volcanoes, cities, armies, and agents.
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

        CreateBaseTiles(state, seed);
        CarveInlandLakes(state, random);
        AddMountainChains(state, random);
        ApplyVegetationClusters(state, seed, random);
        AddMapDetails(state, random);
        AddStartingPieces(state);

        state.AddLog("Sandbox world created.");
        GameRules.ResetFactionMovement(state, state.CurrentFaction.Id);
        return state;
    }

    private static void CreateBaseTiles(GameState state, int seed)
    {
        for (var row = 0; row < MapHeight; row++)
        {
            for (var col = 0; col < MapWidth; col++)
            {
                // Rows are shifted in axial space so the rendered hex map looks
                // square-ish instead of like a long slanted rhombus.
                var coord = ToCoord(col, row);
                var water = IsOcean(col, row, seed);
                var climate = PickClimate(row, col, seed);
                var rainfall = PickRainfall(row, col, seed);

                state.Map.Add(new HexTile
                {
                    Coord = coord,
                    Climate = climate,
                    Rainfall = rainfall,
                    Elevation = water ? Elevation.Water : Elevation.Flat,
                    Vegetation = Vegetation.None
                });
            }
        }
    }

    private static void CarveInlandLakes(GameState state, Random random)
    {
        // Lakes are intentionally tiny: 1-4 water tiles. Because coastline is
        // derived from water touching land, these become little inland coastlines.
        for (var lake = 0; lake < 4; lake++)
        {
            var center = RandomLandTile(state, random, avoidEdge: true);
            if (center is null)
            {
                continue;
            }

            var lakeTiles = new List<HexTile> { center };
            var size = random.Next(1, 5);
            while (lakeTiles.Count < size)
            {
                var source = lakeTiles[random.Next(lakeTiles.Count)];
                var option = state.Map.Neighbors(source.Coord)
                    .Where(t => t.Elevation != Elevation.Water && !lakeTiles.Contains(t) && !IsNearMapEdge(t))
                    .OrderBy(_ => random.Next())
                    .FirstOrDefault();

                if (option is null)
                {
                    break;
                }

                lakeTiles.Add(option);
            }

            foreach (var tile in lakeTiles)
            {
                tile.Elevation = Elevation.Water;
                tile.Vegetation = Vegetation.None;
                tile.FeatureIds.Clear();
                tile.ResourceId = null;
            }
        }
    }

    private static void AddMountainChains(GameState state, Random random)
    {
        for (var chain = 0; chain < 3; chain++)
        {
            var current = RandomLandTile(state, random, avoidEdge: true);
            if (current is null)
            {
                continue;
            }

            var path = new List<HexTile>();
            var length = random.Next(5, 9);
            for (var step = 0; step < length; step++)
            {
                if (current.Elevation == Elevation.Water)
                {
                    break;
                }

                current.Elevation = Elevation.Mountains;
                current.Vegetation = TerrainResolver.NormalizeVegetation(current.Climate, current.Rainfall, current.Vegetation);
                path.Add(current);

                foreach (var neighbor in state.Map.Neighbors(current.Coord).Where(t => t.Elevation != Elevation.Water))
                {
                    if (neighbor.Elevation == Elevation.Flat && random.NextDouble() < 0.45)
                    {
                        neighbor.Elevation = Elevation.Hills;
                    }
                }

                current = state.Map.Neighbors(current.Coord)
                    .Where(t => t.Elevation != Elevation.Water && !path.Contains(t))
                    .OrderBy(t => t.Coord.DistanceTo(ToCoord(MapWidth / 2, MapHeight / 2)) + random.Next(0, 4))
                    .FirstOrDefault() ?? current;
            }

            // Put 1-2 peaks near the middle of each chain so they read as a
            // mountain spine rather than random isolated mountain tiles.
            foreach (var peak in path.Skip(Math.Max(0, path.Count / 2 - 1)).Take(random.Next(1, 3)))
            {
                peak.Elevation = Elevation.Peaks;
            }
        }
    }

    private static void ApplyVegetationClusters(GameState state, int seed, Random random)
    {
        var clusterCenters = state.Map.Tiles
            .Where(t => t.Elevation != Elevation.Water && t.Climate is Climate.Tropical or Climate.Temperate or Climate.Boreal)
            .OrderBy(_ => random.Next())
            .Take(12)
            .ToList();

        foreach (var tile in state.Map.Tiles.Where(t => t.Elevation != Elevation.Water))
        {
            // Fractal-like value noise: a broad wave creates large wet/dry regions,
            // a smaller wave breaks the borders, and cluster centers pull nearby
            // tiles toward forest/sparse vegetation. A small random pop keeps the
            // map from looking too clean.
            var col = ColumnOf(tile.Coord);
            var row = tile.Coord.R;
            var broad = Math.Sin((col + seed) * 0.18) + Math.Cos((row - seed) * 0.16);
            var detail = Math.Sin((col - seed) * 0.73 + row * 0.41) * 0.35;
            var cluster = clusterCenters.Sum(center => Math.Max(0.0, 1.0 - tile.Coord.DistanceTo(center.Coord) / 4.0));
            var pop = random.NextDouble() < 0.05 ? random.NextDouble() * 1.2 : 0.0;
            var score = broad + detail + cluster + pop;

            var vegetation = score switch
            {
                > 1.85 => Vegetation.Forest,
                > 0.55 => Vegetation.Sparse,
                _ => Vegetation.None
            };

            tile.Vegetation = TerrainResolver.NormalizeVegetation(tile.Climate, tile.Rainfall, vegetation);
        }
    }

    private static void AddMapDetails(GameState state, Random random)
    {
        foreach (var tile in state.Map.Tiles)
        {
            if (tile.Elevation == Elevation.Water)
            {
                continue;
            }

            if (tile.Elevation is Elevation.Mountains or Elevation.Peaks && random.NextDouble() < 0.08)
            {
                tile.FeatureIds.Add("volcano");
            }

            if (random.NextDouble() < 0.10)
            {
                tile.ResourceId = PickResource(random, tile);
            }
        }
    }

    private static void AddStartingPieces(GameState state)
    {
        var starts = new[] { ToCoord(9, 15), ToCoord(16, 10), ToCoord(11, 7) };
        for (var i = 0; i < state.Factions.Count; i++)
        {
            // Each faction starts with one city, one army stack, and one agent.
            // If the preferred start is water or rugged mountains, FindNearestStart
            // moves it to a flatter passable tile.
            var faction = state.Factions[i];
            var start = FindNearestStart(state, starts[i % starts.Length]);
            AddCity(state, i + 1, $"{faction.Name} Hold", faction.Id, start);
            AddStack(state, i + 1, faction.Id, start, ("militia", 8), ("spearmen", 3));
            AddAgent(state, i + 1, faction.Id, i == 0 ? "captain" : "scout", i == 0 ? "Aldren" : $"{faction.Name} Scout", start);
        }
    }

    private static string Pick(Random random, params string[] values) => values[random.Next(values.Length)];

    private static HexCoord ToCoord(int col, int row)
    {
        return new HexCoord(col - row / 2, row);
    }

    private static int ColumnOf(HexCoord coord)
    {
        return coord.Q + coord.R / 2;
    }

    private static bool IsOcean(int col, int row, int seed)
    {
        if (col == 0 || row == 0 || col == MapWidth - 1 || row == MapHeight - 1)
        {
            return true;
        }

        // The island is an oval in row/column space. A little deterministic wave
        // noise makes the coast uneven while still keeping one main island. The
        // map canvas is larger than the landmass, so the player sees more ocean.
        var centerX = (MapWidth - 1) / 2.0;
        var centerY = (MapHeight - 1) / 2.0;
        var angle = Math.Atan2(row - centerY, col - centerX);
        var elongation = 1.0 + Math.Sin(seed * 0.17) * 0.22;
        var radiusX = 10.5 * elongation;
        var radiusY = 9.4 / elongation;
        var dx = (col - centerX) / radiusX;
        var dy = (row - centerY) / radiusY;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var broadCoast = Math.Sin(angle * 2.0 + seed * 0.13) * 0.12;
        var peninsula = Math.Max(0.0, Math.Sin(angle * 5.0 + seed * 0.31)) * 0.12;
        var coastNoise = broadCoast + peninsula
                       + Math.Sin((col + seed) * 0.53) * 0.06
                       + Math.Cos((row - seed) * 0.47) * 0.06;

        return distance > 0.92 + coastNoise;
    }

    private static Climate PickClimate(int row, int col, int seed)
    {
        // North is row 0 and south is the highest row.
        // Climate bands are broad and deterministic:
        // south Tropical -> Temperate -> Boreal -> north Polar.
        var latitude = (double)row / (MapHeight - 1);
        var borderNoise = Math.Sin((col + seed) * 0.37 + row * 0.19) * 0.035;
        var shiftedLatitude = Math.Clamp(latitude + borderNoise, 0.0, 1.0);

        return shiftedLatitude switch
        {
            < 0.25 => Climate.Polar,
            < 0.45 => Climate.Boreal,
            < 0.72 => Climate.Temperate,
            _ => Climate.Tropical
        };
    }

    private static Rainfall PickRainfall(int row, int col, int seed)
    {
        var broad = Math.Sin((col + seed) * 0.22) + Math.Cos((row - seed) * 0.18);
        var detail = Math.Sin((col - seed) * 0.71 + row * 0.39) * 0.45;
        var rainScore = broad + detail;

        return rainScore switch
        {
            < -0.45 => Rainfall.Low,
            > 0.55 => Rainfall.High,
            _ => Rainfall.Medium
        };
    }

    private static HexTile? RandomLandTile(GameState state, Random random, bool avoidEdge)
    {
        var options = state.Map.Tiles
            .Where(t => t.Elevation != Elevation.Water && (!avoidEdge || !IsNearMapEdge(t)))
            .ToList();

        return options.Count == 0 ? null : options[random.Next(options.Count)];
    }

    private static bool IsNearMapEdge(HexTile tile)
    {
        var col = ColumnOf(tile.Coord);
        var row = tile.Coord.R;
        return col <= 2 || row <= 2 || col >= MapWidth - 3 || row >= MapHeight - 3;
    }

    private static HexCoord FindNearestStart(GameState state, HexCoord preferred)
    {
        return state.Map.Tiles
            .Where(t => TerrainResolver.Resolve(state, t).Passable && t.Elevation is Elevation.Flat or Elevation.Hills)
            .OrderBy(t => t.Coord.DistanceTo(preferred))
            .First()
            .Coord;
    }

    private static string PickResource(Random random, HexTile tile)
    {
        var resources = new List<string> { "copper", "iron", "gold", "silver" };
        var terrain = TerrainResolver.Resolve(tile, coastline: false);
        var isDesertOrPolar = terrain.Name.Contains("Desert", StringComparison.OrdinalIgnoreCase)
                            || terrain.Name.Contains("Ice", StringComparison.OrdinalIgnoreCase)
                            || tile.Climate == Climate.Polar;

        if (!isDesertOrPolar)
        {
            resources.Add("game");
        }

        return resources[random.Next(resources.Count)];
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
