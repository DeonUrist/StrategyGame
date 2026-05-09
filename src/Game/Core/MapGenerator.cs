namespace StrategyGame.Core;

public static partial class MapGenerator
{
    // Map size is a worldgen setting rather than a compile-time constant. The
    // generator still keeps the map square for now, so one value controls both
    // row and column count.

    public static GameState CreateSandbox(GameDatabase database, int seed) => CreateSandbox(database, seed, new WorldGenerationSettings());

    public static GameState CreateSandbox(GameDatabase database, int seed, WorldGenerationSettings settings)
    {
        // The sandbox generator is deterministic but built in layers.
        // This makes the final map easier to reason about:
        // 1. create the island and ocean,
        // 2. assign saved biome regions,
        // 3. add hills, mountains, and peaks along region edges,
        // 4. add seas, lakes, and polar ice,
        // 5. place resources, volcanoes, cities, armies, and agents.
        var random = new Random(seed);
        var state = new GameState
        {
            Database = database,
            Map = new HexMap(),
            WorldGeneration = settings
        };

        foreach (var faction in database.Factions.Values)
        {
            // Copy definitions into state so the generated world owns its turn
            // order and save/load has the faction data it needs.
            state.Factions.Add(new FactionState
            {
                Id = faction.Id,
                Name = faction.Name,
                Color = faction.Color,
                IsPlayer = faction.IsPlayer
            });
        }

        CreateBaseTiles(state, seed);
        GenerateRegions(state, seed, random);
        AddSeas(state, seed, random);
        CarveInlandLakes(state, random);
        ClassifyCoasts(state);
        ClassifyWaterBodies(state);
        AddElevationFeatures(state, random);
        EnsureNorthernIceSheetLand(state);
        AddMapDetails(state, random);
        ExpandDeepIce(state, seed);
        ClassifyWaterBodies(state);
        AddStartingPieces(state);

        state.AddLog("Sandbox world created.");
        GameRules.ResetFactionMovement(state, state.CurrentFaction.Id);
        return state;
    }

    private static void CreateBaseTiles(GameState state, int seed)
    {
        // Base tiles establish the coordinate grid and broad generated climate.
        // Later generation layers mutate these tiles in place with lakes,
        // mountains, vegetation, resources, and starting pieces.
        var mapSize = NormalizeMapSize(state.WorldGeneration.MapSize);
        for (var row = 0; row < mapSize; row++)
        {
            for (var col = 0; col < mapSize; col++)
            {
                // Rows are shifted in axial space so the rendered hex map looks
                // square-ish instead of like a long slanted rhombus.
                var coord = ToCoord(col, row);
                var water = IsOcean(col, row, mapSize, seed);

                state.Map.Add(new HexTile
                {
                    Coord = coord,
                    Elevation = water ? Elevation.Ocean : Elevation.Flat,
                    Moisture = MoistureLevel.Normal
                });
            }
        }
    }

    private static int NormalizeMapSize(int mapSize)
    {
        // UI sliders step by 16, but saved games or tests may supply any int.
        // Clamp here so every generator layer uses the same bounded map size.
        return Math.Clamp(mapSize, WorldGenerationSettings.MinMapSize, WorldGenerationSettings.MaxMapSize);
    }
}
