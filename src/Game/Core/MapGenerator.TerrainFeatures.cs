namespace StrategyGame.Core;

public static partial class MapGenerator
{
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
                    .Where(t => !t.Elevation.IsWaterLike() && !lakeTiles.Contains(t) && !IsNearMapEdge(t, NormalizeMapSize(state.WorldGeneration.MapSize)))
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
                if (tile.RegionId is { } regionId && state.Regions.TryGetValue(regionId, out var region))
                {
                    region.TileCoords.Remove(tile.Coord);
                }

                tile.Elevation = Elevation.Coast;
                tile.Vegetation = Vegetation.None;
                tile.RegionId = null;
                tile.FeatureIds.Clear();
                tile.ResourceId = null;
            }
        }
    }

    private static void AddElevationFeatures(GameState state, Random random)
    {
        // Elevation is generated after regions so hills and mountains can prefer
        // region borders and can locally dry the already-assigned biome tiles.
        var variance = Math.Clamp(state.WorldGeneration.ElevationVariance, 0, 100);
        if (variance == 0)
        {
            // A zero variance world is intentionally completely flat except for
            // water. Tests use this as the lower bound for the worldgen setting.
            return;
        }

        var land = state.Map.Tiles
            .Where(t => !t.Elevation.IsWaterLike() && t.RegionId is not null)
            .ToList();

        // ElevationVariance scales each tier. Hills are the broad base; mountains
        // derive from hill count; peaks derive from mountain count.
        var hillTarget = Math.Max(1, land.Count * variance * 22 / 10000);
        AddHills(state, land, hillTarget, random);

        var hillCount = land.Count(t => t.Elevation == Elevation.Hills);
        var mountainTarget = hillCount == 0 ? 0 : Math.Max(1, hillCount * variance * 35 / 10000);
        AddMountains(state, land, mountainTarget, random);

        var mountainCount = land.Count(t => t.Elevation == Elevation.Mountains);
        var peakTarget = mountainCount == 0 ? 0 : Math.Max(1, mountainCount * variance * 18 / 10000);
        AddPeaks(state, land, peakTarget, random);

        foreach (var tile in land.Where(t => t.Elevation is Elevation.Hills or Elevation.Mountains or Elevation.Peaks))
        {
            // Drying happens after all tiers are placed so a tile promoted from
            // hill -> mountain -> peak receives only its final elevation effect.
            ApplyElevationDrying(state, tile, random);
        }
    }

    private static void AddHills(GameState state, List<HexTile> land, int target, Random random)
    {
        // Hill anchors prefer region edges because borders are where terrain
        // transitions should most often appear. Random ordering still allows
        // some inland hills after edge candidates are exhausted.
        var anchors = land
            .Where(t => t.Elevation == Elevation.Flat)
            .OrderByDescending(t => IsRegionEdge(state, t) ? 1 : 0)
            .ThenBy(_ => random.Next())
            .Take(target)
            .ToList();

        foreach (var anchor in anchors)
        {
            anchor.Elevation = Elevation.Hills;

            // Hills may stand alone or pull in a few neighboring flat tiles to
            // form small readable clusters.
            var clusterSize = random.NextDouble() < 0.55 ? random.Next(2, 5) : 1;
            foreach (var neighbor in state.Map.Neighbors(anchor.Coord)
                         .Where(t => t.Elevation == Elevation.Flat && t.RegionId is not null)
                         .OrderBy(_ => random.Next())
                         .Take(clusterSize - 1))
            {
                neighbor.Elevation = Elevation.Hills;
            }
        }
    }

    private static void AddMountains(GameState state, List<HexTile> land, int target, Random random)
    {
        // Mountains can replace hills or nearby flat tiles, but every candidate
        // must have hills within distance two so mountains read as emerging from
        // rugged terrain instead of appearing in isolation.
        var candidates = land
            .Where(t => t.Elevation is Elevation.Hills or Elevation.Flat && HasElevationWithin(state, t, Elevation.Hills, 2))
            .OrderByDescending(t => IsRegionEdge(state, t) ? 1 : 0)
            .ThenBy(_ => random.Next())
            .Take(target)
            .ToList();

        foreach (var tile in candidates)
        {
            tile.Elevation = Elevation.Mountains;
        }
    }

    private static void AddPeaks(GameState state, List<HexTile> land, int target, Random random)
    {
        // Peaks are promoted mountains. Requiring a neighboring mountain keeps
        // them embedded in a range and prevents single-tile spike noise.
        var orderedCandidates = land
            .Where(t => t.Elevation == Elevation.Mountains && state.Map.Neighbors(t.Coord).Any(n => n.Elevation == Elevation.Mountains))
            .OrderBy(_ => random.Next())
            .ToList();
        var candidates = new List<HexTile>();
        foreach (var tile in orderedCandidates)
        {
            if (candidates.Count >= target)
            {
                break;
            }

            if (state.Map.Neighbors(tile.Coord).Any(n => n.Elevation == Elevation.Mountains && !candidates.Contains(n)))
            {
                candidates.Add(tile);
            }
        }

        foreach (var tile in candidates)
        {
            tile.Elevation = Elevation.Peaks;
        }
    }

    private static bool IsRegionEdge(GameState state, HexTile tile)
    {
        // Region edge means "touches any other land region". Water neighbors do
        // not count because coastlines already have their own visual identity.
        return tile.RegionId is { } regionId
            && state.Map.Neighbors(tile.Coord).Any(n => !n.Elevation.IsWaterLike() && n.RegionId is not null && n.RegionId != regionId);
    }

    private static bool HasElevationWithin(GameState state, HexTile tile, Elevation elevation, int distance)
    {
        // The map is small enough that this direct scan is fine for generation
        // and keeps the rule easy to read.
        return state.Map.Tiles.Any(other => other.Elevation == elevation && other.Coord.DistanceTo(tile.Coord) <= distance);
    }

    private static void ApplyElevationDrying(GameState state, HexTile tile, Random random)
    {
        // Elevated tiles keep their parent RegionId, but their effective tile
        // moisture/vegetation is reduced. TerrainResolver uses these tile-local
        // values, which prevents wet/lush terrain such as swamp or jungle on
        // peaks without splitting the region.
        switch (tile.Elevation)
        {
            case Elevation.Hills:
                tile.Moisture = DecreaseMoisture(tile.Moisture, 1);
                tile.Vegetation = DecreaseVegetation(tile.Vegetation, random.Next(0, 2));
                break;
            case Elevation.Mountains:
                tile.Moisture = DecreaseMoisture(tile.Moisture, random.Next(1, 3));
                tile.Vegetation = DecreaseVegetation(tile.Vegetation, random.Next(0, 3));
                break;
            case Elevation.Peaks:
                tile.Moisture = DecreaseMoisture(tile.Moisture, random.Next(1, 3));
                tile.Vegetation = DecreaseVegetation(tile.Vegetation, random.Next(1, 3));
                break;
        }

        if (tile.RegionId is { } regionId && state.Regions.TryGetValue(regionId, out var region))
        {
            tile.Vegetation = TerrainResolver.ClampTileVegetation(region, tile);
        }
    }

    private static MoistureLevel DecreaseMoisture(MoistureLevel moisture, int steps)
    {
        // Enum order is Dry=0, Normal=1, Wet=2, so subtracting steps dries the
        // tile and clamping prevents underflow.
        return (MoistureLevel)Math.Max((int)MoistureLevel.Dry, (int)moisture - steps);
    }

    private static Vegetation DecreaseVegetation(Vegetation vegetation, int steps)
    {
        // Enum order is None=0, Sparse=1, Lush=2, so subtracting steps removes
        // vegetation in the same way moisture drying works.
        return (Vegetation)Math.Max((int)Vegetation.None, (int)vegetation - steps);
    }

    private static void AddMapDetails(GameState state, Random random)
    {
        // Details are lower-frequency decorations placed after major terrain is
        // settled. They depend on final-ish terrain, so they run after vegetation
        // and elevation changes.
        foreach (var tile in state.Map.Tiles)
        {
            if (tile.Elevation.IsWaterLike())
            {
                continue;
            }

            if (tile.Elevation is Elevation.Mountains or Elevation.Peaks && random.NextDouble() < 0.08)
            {
                tile.FeatureIds.Add("volcano");
            }

            if (random.NextDouble() < 0.10)
            {
                tile.ResourceId = PickResource(state, random, tile);
            }
        }
    }

    private static void EnsureNorthernIceSheetLand(GameState state)
    {
        var mapSize = NormalizeMapSize(state.WorldGeneration.MapSize);
        var polarLimit = Math.Max(2, mapSize / 4);
        var northernLand = state.Map.Tiles
            .Where(t => !t.Elevation.IsWaterLike() && t.Coord.R <= polarLimit)
            .ToList();

        if (northernLand.Count == 0 || northernLand.Any(t => TerrainResolver.Resolve(state, t).Name == "Ice Sheet"))
        {
            return;
        }

        var anchor = northernLand
            .OrderByDescending(t => state.Map.Neighbors(t.Coord).Count(n => n.Elevation.IsLiquidWater()))
            .ThenBy(t => t.Coord.R)
            .ThenBy(t => ColumnOf(t.Coord))
            .First();
        var cluster = new List<HexTile> { anchor };
        cluster.AddRange(state.Map.Neighbors(anchor.Coord)
            .Where(t => !t.Elevation.IsWaterLike() && t.Coord.R <= polarLimit)
            .OrderBy(t => t.Coord.R)
            .ThenBy(t => ColumnOf(t.Coord))
            .Take(2));

        var regionId = state.Regions.Count == 0 ? 1 : state.Regions.Keys.Max() + 1;
        var region = new RegionState
        {
            Id = regionId,
            Moisture = MoistureLevel.Normal,
            WaterRetention = WaterRetention.Normal,
            Temperature = TemperatureBand.Arctic,
            BaseBiome = BaseBiome.Plain,
            Vegetation = Vegetation.None,
            FinalBiomeName = "Ice Sheet"
        };

        foreach (var tile in cluster.Distinct())
        {
            if (tile.RegionId is { } previousRegionId && state.Regions.TryGetValue(previousRegionId, out var previousRegion))
            {
                previousRegion.TileCoords.Remove(tile.Coord);
            }

            tile.RegionId = regionId;
            tile.Moisture = region.Moisture;
            tile.Vegetation = Vegetation.None;
            region.TileCoords.Add(tile.Coord);
        }

        state.Regions[regionId] = region;
    }

    private static void AddSeas(GameState state, int seed, Random random)
    {
        var max = Math.Clamp(state.WorldGeneration.MaxSeaNumber, 0, 5);
        if (max == 0)
        {
            return;
        }

        var target = random.Next((max + 1) / 2, max + 1);
        var converted = 0;
        var biomeCounts = state.Regions.Values
            .GroupBy(r => r.FinalBiomeName)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var region in SeaCandidates(state, biomeCounts)
                     .OrderByDescending(r => SeaShapeScore(state, r))
                     .ThenBy(r => Math.Sin((r.Id + seed) * 0.73))
                     .ToList())
        {
            if (converted >= target)
            {
                break;
            }

            if (!IsSeaShaped(state, region))
            {
                continue;
            }

            ConvertRegionToSea(state, region);
            converted++;
        }

        foreach (var region in SeaCandidates(state, biomeCounts)
                     .Where(r => TouchesSeaThroughOneTile(state, r))
                     .OrderBy(r => r.TileCoords.Count)
                     .ToList())
        {
            if (converted >= target)
            {
                break;
            }

            if (!IsSeaShaped(state, region))
            {
                continue;
            }

            ConvertRegionToSea(state, region);
            converted++;
        }
    }

    private static IEnumerable<RegionState> SeaCandidates(GameState state, Dictionary<string, int> biomeCounts)
    {
        return state.Regions.Values
            .Where(r => r.FinalBiomeName != "Sea")
            .Where(r => r.TileCoords.Count is > 0 and < 20)
            .Where(r => biomeCounts.TryGetValue(r.FinalBiomeName, out var count) && count > 1)
            .Where(r => r.TileCoords.Select(state.Map.Get).Any(t => state.Map.Neighbors(t.Coord).Any(n => n.Elevation.IsLiquidWater() && state.Map.IsOuterWaterBody(n))));
    }

    private static bool IsSeaShaped(GameState state, RegionState region)
    {
        var tiles = region.TileCoords.Select(state.Map.Get).ToList();
        var mouths = tiles
            .Where(t => state.Map.Neighbors(t.Coord).Any(n => n.Elevation.IsLiquidWater() && state.Map.IsOuterWaterBody(n)))
            .ToList();
        if (mouths.Count == 0)
        {
            return false;
        }

        var depths = tiles.ToDictionary(t => t.Coord, _ => int.MaxValue);
        var queue = new Queue<HexTile>();
        foreach (var mouth in mouths)
        {
            depths[mouth.Coord] = 0;
            queue.Enqueue(mouth);
        }

        while (queue.Count > 0)
        {
            var tile = queue.Dequeue();
            foreach (var neighbor in state.Map.Neighbors(tile.Coord)
                         .Where(n => n.RegionId == region.Id && depths[n.Coord] == int.MaxValue))
            {
                depths[neighbor.Coord] = depths[tile.Coord] + 1;
                queue.Enqueue(neighbor);
            }
        }

        var maxDepth = depths.Values.Where(v => v < int.MaxValue).DefaultIfEmpty(0).Max();
        if (maxDepth < 2)
        {
            return false;
        }

        var connectionWidth = Math.Max(1, mouths.Count);
        var maxInteriorWidth = depths
            .Where(kv => kv.Value > 0 && kv.Value < int.MaxValue)
            .GroupBy(kv => kv.Value)
            .Select(g => g.Count())
            .DefaultIfEmpty(0)
            .Max();

        return maxInteriorWidth >= connectionWidth;
    }

    private static int SeaShapeScore(GameState state, RegionState region)
    {
        return region.TileCoords.Select(state.Map.Get)
            .Count(t => state.Map.Neighbors(t.Coord).Any(n => n.Elevation.IsLiquidWater()));
    }

    private static bool TouchesSeaThroughOneTile(GameState state, RegionState region)
    {
        return region.TileCoords.Select(state.Map.Get)
            .Any(tile => state.Map.Neighbors(tile.Coord)
                .Any(n => n.Elevation == Elevation.Coast
                       && n.RegionId is { } regionId
                       && state.Regions.TryGetValue(regionId, out var sea)
                       && sea.FinalBiomeName == "Sea"));
    }

    private static void ConvertRegionToSea(GameState state, RegionState region)
    {
        region.Moisture = MoistureLevel.Normal;
        region.WaterRetention = WaterRetention.Holding;
        region.Temperature = TemperatureBand.Temperate;
        region.BaseBiome = BaseBiome.Floodplain;
        region.Vegetation = Vegetation.None;
        region.FinalBiomeName = "Sea";

        foreach (var coord in region.TileCoords)
        {
            var tile = state.Map.Get(coord);
            tile.Elevation = Elevation.Coast;
            tile.Moisture = region.Moisture;
            tile.Vegetation = Vegetation.None;
            tile.WaterBodyKind = WaterBodyKind.Sea;
            tile.ResourceId = null;
            tile.FeatureIds.Clear();
        }
    }

    private static void ClassifyCoasts(GameState state)
    {
        foreach (var tile in state.Map.Tiles.Where(t => t.Elevation == Elevation.Ocean).ToList())
        {
            if (state.Map.Neighbors(tile.Coord).Any(n => !n.Elevation.IsWaterLike()))
            {
                tile.Elevation = Elevation.Coast;
            }
        }
    }

    private static void ClassifyWaterBodies(GameState state)
    {
        foreach (var tile in state.Map.Tiles)
        {
            tile.WaterBodyKind = WaterBodyKind.None;
        }

        var visited = new HashSet<HexCoord>();
        foreach (var start in state.Map.Tiles.Where(t => t.Elevation.IsLiquidWater()))
        {
            if (!visited.Add(start.Coord))
            {
                continue;
            }

            var body = new List<HexTile>();
            var queue = new Queue<HexTile>();
            queue.Enqueue(start);
            var touchesOuterEdge = false;

            while (queue.Count > 0)
            {
                var tile = queue.Dequeue();
                body.Add(tile);

                if (IsMapEdgeTile(tile, NormalizeMapSize(state.WorldGeneration.MapSize)))
                {
                    touchesOuterEdge = true;
                }

                foreach (var neighbor in state.Map.Neighbors(tile.Coord))
                {
                    if (!neighbor.Elevation.IsLiquidWater() || !visited.Add(neighbor.Coord))
                    {
                        continue;
                    }

                    queue.Enqueue(neighbor);
                }
            }

            var bodyKind = touchesOuterEdge ? WaterBodyKind.Outer : WaterBodyKind.Lake;
            foreach (var tile in body)
            {
                tile.WaterBodyKind = tile.RegionId is { } regionId
                                      && state.Regions.TryGetValue(regionId, out var region)
                                      && region.FinalBiomeName == "Sea"
                    ? WaterBodyKind.Sea
                    : bodyKind;
            }
        }
    }

    private static void ExpandDeepIce(GameState state, int seed)
    {
        var mapSize = NormalizeMapSize(state.WorldGeneration.MapSize);
        var expansionLimit = Math.Max(3, mapSize / 3);
        var iceSeedGroups = state.Map.Tiles
            .Where(t => !t.Elevation.IsWaterLike())
            .Where(t => t.Coord.R <= expansionLimit)
            .Where(t => TerrainResolver.Resolve(state, t).Name == "Ice Sheet")
            .Where(t => t.RegionId is not null)
            .GroupBy(t => t.RegionId!.Value)
            .ToList();

        foreach (var group in iceSeedGroups)
        {
            var queue = new Queue<(HexTile Tile, int Depth)>();
            var queued = new HashSet<HexCoord>();

            foreach (var waterTile in group
                         .SelectMany(tile => state.Map.Neighbors(tile.Coord))
                         .Where(tile => tile.Elevation.IsLiquidWater())
                         .Where(tile => !IsMapEdgeTile(tile, mapSize))
                         .Where(tile => tile.Coord.R <= expansionLimit)
                         .Where(tile => tile.RegionId is null || state.Regions[tile.RegionId.Value].FinalBiomeName != "Sea")
                         .Where(state.Map.IsOuterWaterBody)
                         .Distinct())
            {
                queue.Enqueue((Tile: waterTile, Depth: 0));
                queued.Add(waterTile.Coord);
            }

            while (queue.Count > 0)
            {
                var (tile, depth) = queue.Dequeue();
                if (depth > 2 || tile.Elevation == Elevation.DeepIce)
                {
                    continue;
                }

                var coldScore = Math.Sin((ColumnOf(tile.Coord) + seed) * 0.33) + Math.Cos((tile.Coord.R - seed) * 0.21);
                if (depth > 0 && coldScore < 0.10)
                {
                    continue;
                }

                tile.Elevation = Elevation.DeepIce;
                tile.RegionId = null;
                tile.Moisture = MoistureLevel.Normal;
                tile.Vegetation = Vegetation.None;
                tile.WaterBodyKind = WaterBodyKind.None;
                tile.FeatureIds.Clear();

                if (depth == 2)
                {
                    continue;
                }

                foreach (var neighbor in state.Map.Neighbors(tile.Coord)
                             .Where(n => n.Elevation.IsLiquidWater())
                             .Where(n => !IsMapEdgeTile(n, mapSize))
                             .Where(n => n.Coord.R <= expansionLimit)
                             .Where(n => n.RegionId is null || state.Regions[n.RegionId.Value].FinalBiomeName != "Sea")
                             .Where(state.Map.IsOuterWaterBody)
                             .Where(n => !queued.Contains(n.Coord))
                             .OrderBy(n => n.Coord.R)
                             .ThenBy(n => ColumnOf(n.Coord)))
                {
                    queue.Enqueue((Tile: neighbor, Depth: depth + 1));
                    queued.Add(neighbor.Coord);
                }
            }
        }
    }

    private static bool IsMapEdgeTile(HexTile tile, int mapSize)
    {
        var col = ColumnOf(tile.Coord);
        var row = tile.Coord.R;
        return col == 0 || row == 0 || col == mapSize - 1 || row == mapSize - 1;
    }

    private static string PickResource(GameState state, Random random, HexTile tile)
    {
        // Minerals can appear in harsh terrain, but "game" represents wildlife
        // and is excluded from desert, ice, and polar tiles.
        var resources = new List<string> { "copper", "iron", "gold", "silver" };
        var terrain = TerrainResolver.Resolve(state, tile);
        var isDesertOrPolar = terrain.Name.Contains("Desert", StringComparison.OrdinalIgnoreCase)
                            || terrain.Name.Contains("Ice", StringComparison.OrdinalIgnoreCase)
                            || terrain.Name.Contains("Arctic", StringComparison.OrdinalIgnoreCase);

        if (!isDesertOrPolar)
        {
            resources.Add("game");
        }

        return resources[random.Next(resources.Count)];
    }
}
