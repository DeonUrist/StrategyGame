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
            // worldgen settings. These are the "default" biome values for the
            // region before local elevation later dries individual tiles.
            var moisture = PickMoisture(center.Coord, seed, state.WorldGeneration.Wetness);
            var retention = PickWaterRetention(center.Coord, seed);
            var temperature = PickTemperature(center.Coord, seed, state.WorldGeneration.ClimateBias, NormalizeMapSize(state.WorldGeneration.MapSize));
            var baseBiome = TerrainResolver.PickBaseBiome(moisture, retention, temperature);
            var vegetation = PickVegetation(baseBiome, temperature, state.WorldGeneration.Vegetation, random);
            var finalName = TerrainResolver.ResolveRegionBiome(baseBiome, temperature, vegetation);

            state.Regions[i + 1] = new RegionState
            {
                Id = i + 1,
                Moisture = moisture,
                WaterRetention = retention,
                Temperature = temperature,
                BaseBiome = baseBiome,
                Vegetation = vegetation,
                FinalBiomeName = TerrainResolver.FormatFinalBiomeName(temperature, finalName)
            };
        }

        EnsurePolarIceCoverage(state, centers);
        RebalanceDesertRegions(state, centers, seed);

        foreach (var tile in land)
        {
            // Assign each land tile to its nearest center. Region identity is
            // saved and later used for world-info systems, while tile moisture
            // and vegetation start as copies that elevation can locally modify.
            var region = state.Regions.Values
                .OrderBy(r => EffectiveRegionDistance(tile.Coord, centers[r.Id - 1].Coord, r))
                .First();

            tile.RegionId = region.Id;
            tile.Moisture = region.Moisture;
            tile.Vegetation = region.Vegetation;
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

    private static WaterRetention PickWaterRetention(HexCoord coord, int seed)
    {
        // Water retention is separate from moisture: a wet draining area becomes
        // barrens, while a wet holding area becomes swamp.
        var col = ColumnOf(coord);
        var score = Math.Sin((col - seed) * 0.31) + Math.Cos((coord.R + seed) * 0.27);
        return score switch
        {
            < -0.35 => WaterRetention.Draining,
            > 0.45 => WaterRetention.Holding,
            _ => WaterRetention.Normal
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
            < 0.16 => TemperatureBand.Arctic,
            < 0.28 => TemperatureBand.Subarctic,
            < 0.49 => TemperatureBand.Temperate,
            < 0.70 => TemperatureBand.Subtropical,
            _ => TemperatureBand.Tropical
        };
    }

    private static Vegetation PickVegetation(BaseBiome biome, TemperatureBand temperature, int worldVegetation, Random random)
    {
        // Biome and temperature set the maximum allowed vegetation. The global
        // vegetation setting controls how often a region reaches sparse/lush.
        var max = TerrainResolver.MaxVegetation(biome, temperature);
        if (max == Vegetation.None)
        {
            return Vegetation.None;
        }

        var roll = random.Next(100);
        var vegetationChance = Math.Clamp(worldVegetation, 0, 100);
        if (max == Vegetation.Sparse)
        {
            return roll < vegetationChance ? Vegetation.Sparse : Vegetation.None;
        }

        var lushChance = vegetationChance * 45 / 100;
        // Lush is intentionally rarer than "any vegetation" so forests/jungles
        // exist as notable regions rather than covering every permissive biome.
        return roll < lushChance
            ? Vegetation.Lush
            : roll < vegetationChance
                ? Vegetation.Sparse
                : Vegetation.None;
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
            region.Vegetation = TerrainResolver.ClampVegetation(region.BaseBiome, region.Temperature, region.Vegetation);
            var finalName = TerrainResolver.ResolveRegionBiome(region.BaseBiome, region.Temperature, region.Vegetation);
            region.FinalBiomeName = TerrainResolver.FormatFinalBiomeName(region.Temperature, finalName);
        }
    }

    private static void RebalanceDesertRegions(GameState state, List<HexTile> centers, int seed)
    {
        // Deserts should be a bit rarer at the region level, but the surviving
        // ones should expand modestly beyond a neutral nearest-center split.
        foreach (var region in state.Regions.Values.Where(r => r.BaseBiome == BaseBiome.Desert))
        {
            var center = centers[region.Id - 1];
            var keepScore = Math.Sin((ColumnOf(center.Coord) + seed) * 0.41) + Math.Cos((center.Coord.R - seed) * 0.23);
            if (keepScore < 0.35)
            {
                region.Moisture = MoistureLevel.Normal;
                region.WaterRetention = WaterRetention.Draining;
                region.BaseBiome = BaseBiome.Dryland;
                region.Vegetation = TerrainResolver.ClampVegetation(region.BaseBiome, region.Temperature, region.Vegetation);
                var finalName = TerrainResolver.ResolveRegionBiome(region.BaseBiome, region.Temperature, region.Vegetation);
                region.FinalBiomeName = TerrainResolver.FormatFinalBiomeName(region.Temperature, finalName);
            }
        }
    }

    private static double EffectiveRegionDistance(HexCoord tile, HexCoord center, RegionState region)
    {
        var distance = tile.DistanceTo(center);
        return region.BaseBiome == BaseBiome.Desert ? distance * 0.84 : distance;
    }
}
