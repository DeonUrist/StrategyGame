namespace StrategyGame.Core;

public static partial class MapGenerator
{
    // Region count scales with map area. A 64x64 default world uses 56 regions,
    // while smaller/larger maps keep roughly comparable region sizes.
    private const int DefaultRegionCount = 56;

    private static void GenerateRegions(GameState state, int seed, Random random)
    {
        // Regions are generated only for land. Water keeps RegionId null and is
        // resolved by coastline/ocean rules instead of biome rules.
        var land = state.Map.Tiles
            .Where(t => !t.Elevation.IsWaterLike())
            .ToList();

        // Pick region centers by walking north-to-south through sorted land.
        // This intentionally spreads centers across temperature bands instead of
        // letting random noise cluster every region in one latitude.
        var sortedLand = land
            .OrderBy(t => t.Coord.R)
            .ThenBy(t => ColumnOf(t.Coord))
            .ToList();
        var regionCount = ScaledRegionCount(state.WorldGeneration.MapSize);
        var centerCount = Math.Min(regionCount, sortedLand.Count);
        var centers = Enumerable.Range(0, centerCount)
            .Select(i => sortedLand[Math.Clamp((int)Math.Round((i + 0.5) * sortedLand.Count / centerCount), 0, sortedLand.Count - 1)])
            .ToList();

        for (var i = 0; i < centers.Count; i++)
        {
            var center = centers[i];

            // Region properties come from broad deterministic noise and global
            // worldgen settings. Temperature and moisture pick terrain directly;
            // paired terrain cells use deterministic per-region variant rolls.
            var moisture = PickMoisture(center.Coord, seed, state.WorldGeneration.Wetness);
            var temperature = PickTemperature(center.Coord, seed, state.WorldGeneration.ClimateBias, NormalizeMapSize(state.WorldGeneration.MapSize));
            var regionId = i + 1;
            var variantRoll = RegionVariantRoll(center.Coord, seed, regionId);
            var baseBiome = TerrainResolver.PickBaseBiome(
                moisture,
                temperature,
                state.WorldGeneration.GrasslandShrublandBias,
                state.WorldGeneration.DesertBadlandsBias,
                state.WorldGeneration.ConiferBroadleafForestBias,
                variantRoll);
            var finalName = TerrainResolver.ResolveRegionBiome(baseBiome);

            state.Regions[regionId] = new RegionState
            {
                Id = regionId,
                Name = RegionNameGenerator.Generate(finalName, temperature, seed ^ regionId),
                Moisture = moisture,
                Temperature = temperature,
                BaseBiome = baseBiome,
                FinalBiomeName = finalName
            };
        }

        EnsurePolarIceCoverage(state, centers);

        foreach (var tile in land)
        {
            // Assign each land tile to its nearest center. Region identity is
            // saved and later used for world-info systems, while tile moisture
            // starts as a copy of the resolved regional moisture.
            var region = state.Regions.Values
                .OrderBy(r => EffectiveRegionDistance(tile.Coord, centers[r.Id - 1].Coord, r))
                .First();

            tile.RegionId = region.Id;
            tile.Moisture = region.Moisture;
            region.TileCoords.Add(tile.Coord);
        }
    }

    private static int ScaledRegionCount(int mapSize)
    {
        var normalizedSize = NormalizeMapSize(mapSize);
        return Math.Max(8, DefaultRegionCount * normalizedSize * normalizedSize / (WorldGenerationSettings.DefaultMapSize * WorldGenerationSettings.DefaultMapSize));
    }

    private static MoistureLevel PickMoisture(HexCoord coord, int seed, int wetness)
    {
        // Wetness shifts the whole world dry or wet. Noise still creates local
        // region variation around that global bias.
        var col = ColumnOf(coord);
        var wetnessBias = (Math.Clamp(wetness, 0, 100) - 50) / 35.0;
        var score = Math.Sin((col + seed) * 0.22) + Math.Cos((coord.R - seed) * 0.18) + wetnessBias;
        return score switch
        {
            < -0.45 => MoistureLevel.Dry,
            > 0.55 => MoistureLevel.Wet,
            _ => MoistureLevel.Normal
        };
    }

    private static TemperatureBand PickTemperature(HexCoord coord, int seed, ClimateBias climateBias, int mapSize)
    {
        // Row controls temperature: north is colder, south is hotter. A small
        // jitter prevents perfectly straight climate borders, and ClimateBias
        // shifts the whole map warmer or colder.
        var col = ColumnOf(coord);
        var latitude = (double)coord.R / (mapSize - 1);
        var jitter = Math.Sin((col + seed) * 0.37 + coord.R * 0.19) * 0.04;
        var bias = climateBias switch
        {
            ClimateBias.Hot => 0.10,
            ClimateBias.Cold => -0.10,
            _ => 0.0
        };
        var southHeat = Math.Clamp(latitude + jitter + bias, 0.0, 1.0);
        return southHeat switch
        {
            < 0.18 => TemperatureBand.Arctic,
            < 0.34 => TemperatureBand.Subarctic,
            < 0.66 => TemperatureBand.Temperate,
            _ => TemperatureBand.Tropical
        };
    }

    private static int RegionVariantRoll(HexCoord coord, int seed, int regionId)
    {
        unchecked
        {
            var value = seed;
            value = (value * 397) ^ regionId;
            value = (value * 397) ^ coord.Q;
            value = (value * 397) ^ coord.R;
            return Math.Abs(value == int.MinValue ? 0 : value) % 100;
        }
    }

    private static void EnsurePolarIceCoverage(GameState state, List<HexTile> centers)
    {
        if (state.Regions.Values.Any(r => r.Temperature == TemperatureBand.Arctic))
        {
            return;
        }

        var polarLimit = Math.Max(1, state.WorldGeneration.MapSize / 4);
        var candidates = state.Regions.Values
            .Where(r => centers[r.Id - 1].Coord.R <= polarLimit)
            .Where(r => r.Temperature == TemperatureBand.Subarctic)
            .OrderBy(r => centers[r.Id - 1].Coord.R)
            .Take(1)
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = state.Regions.Values
                .Where(r => r.Temperature == TemperatureBand.Subarctic)
                .OrderBy(r => centers[r.Id - 1].Coord.R)
                .Take(1)
                .ToList();
        }

        foreach (var region in candidates)
        {
            region.Temperature = TemperatureBand.Arctic;
            region.BaseBiome = BaseBiome.IceSheet;
            region.FinalBiomeName = TerrainResolver.ResolveRegionBiome(region.BaseBiome);
            region.Name = RegionNameGenerator.Generate(region.FinalBiomeName, region.Temperature, region.Id);
        }
    }

    private static double EffectiveRegionDistance(HexCoord tile, HexCoord center, RegionState region)
    {
        var distance = tile.DistanceTo(center);
        return region.BaseBiome is BaseBiome.Desert or BaseBiome.Badlands ? distance * 0.90 : distance;
    }
}
