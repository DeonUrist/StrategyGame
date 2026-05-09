using StrategyGame.Core;

var root = FindProjectRoot();
var database = GameDatabase.LoadFromDirectory(Path.Combine(root, "data"));

var tests = new (string Name, Action Test)[]
{
    ("hex distance and neighbors", HexDistanceAndNeighbors),
    ("database loads required catalogs", DatabaseLoadsRequiredCatalogs),
    ("terrain resolver applies region biome tables", TerrainResolverAppliesRegionBiomeTables),
    ("terrain movement costs use terrain and elevation", TerrainMovementCostsUseTerrainAndElevation),
    ("terrain variant sliders control generated pairs", TerrainVariantSlidersControlGeneratedPairs),
    ("sandbox respects custom map size", SandboxRespectsCustomMapSize),
    ("sandbox generation is deterministic", SandboxGenerationIsDeterministic),
    ("sandbox has ocean border and region climate bands", SandboxHasOceanBorderAndRegionClimateBands),
    ("sandbox has inland lakes and elevation features", SandboxHasInlandLakesAndElevationFeatures),
    ("elevation variance controls rugged terrain", ElevationVarianceControlsRuggedTerrain),
    ("factions start on passable island tiles", FactionsStartOnPassableIslandTiles),
    ("movement rejects water and spends movement", MovementRejectsWaterAndSpendsMovement),
    ("agent move does not auto attach to army", AgentMoveDoesNotAutoAttachToArmy),
    ("agent joins and detaches from army", AgentJoinsAndDetachesFromArmy),
    ("city building upgrade replaces previous level", CityBuildingUpgradeReplacesPreviousLevel),
    ("combat removes losing stack", CombatRemovesLosingStack),
    ("director produces valid AI state", DirectorProducesValidAiState),
    ("save load preserves game state", SaveLoadPreservesGameState),
    ("loaded AI turn replays deterministically", LoadedAiTurnReplaysDeterministically),
    ("movement overspend allows last step", MovementOverspendAllowsLastStep),
    ("turn cycling advances faction and counts turns", TurnCyclingAdvancesFactionAndCountsTurns),
    ("combat applies casualties to winner", CombatAppliesCasualtiesToWinner),
    ("move stack fails for invalid destination", MoveStackFailsForInvalidDestination)
};

var requestedTests = SelectTests(tests, args);
foreach (var test in requestedTests)
{
    Run(test.Name, test.Test);
}

Console.WriteLine("All strategy foundation tests passed.");

void HexDistanceAndNeighbors()
{
    var origin = new HexCoord(0, 0);
    Assert(origin.Neighbors().Count() == 6, "origin should have six axial neighbors");
    Assert(origin.DistanceTo(new HexCoord(2, -1)) == 2, "distance should use cube-equivalent axial math");
}

void DatabaseLoadsRequiredCatalogs()
{
    Assert(database.Resources.ContainsKey("gold"), "gold resource missing");
    Assert(database.Resources.ContainsKey("copper"), "copper resource missing");
    Assert(database.Resources.ContainsKey("iron"), "iron resource missing");
    Assert(database.Resources.ContainsKey("silver"), "silver resource missing");
    Assert(database.Resources.ContainsKey("game"), "game resource missing");
    Assert(!File.Exists(Path.Combine(root, "data", "resources.json")), "resources should be code-defined, not JSON-authored");
    Assert(database.Units.ContainsKey("captain"), "captain unit missing");
    Assert(database.Buildings["campsite"].UpgradesTo == "shelter", "building chain should start campsite -> shelter");
    Assert(database.Factions.Values.Count(f => f.IsPlayer) == 1, "exactly one player faction expected");
    Assert(database.Events.ContainsKey("attack_enemy"), "attack event missing");
}

void TerrainResolverAppliesRegionBiomeTables()
{
    var oceanTile = new HexTile { Coord = new HexCoord(0, 0), Elevation = Elevation.Ocean };
    var coastTile = new HexTile { Coord = new HexCoord(1, 0), Elevation = Elevation.Coast };
    var deepIceTile = new HexTile { Coord = new HexCoord(2, 0), Elevation = Elevation.DeepIce };
    Assert(Pick(MoistureLevel.Dry, TemperatureBand.Arctic) == BaseBiome.IceSheet, "arctic dry should pick Ice Sheet");
    Assert(Pick(MoistureLevel.Normal, TemperatureBand.Arctic) == BaseBiome.IceSheet, "arctic normal should pick Ice Sheet");
    Assert(Pick(MoistureLevel.Wet, TemperatureBand.Arctic) == BaseBiome.IceSheet, "arctic wet should pick Ice Sheet");
    Assert(Pick(MoistureLevel.Dry, TemperatureBand.Subarctic) == BaseBiome.Tundra, "subarctic dry should pick Tundra");
    Assert(Pick(MoistureLevel.Normal, TemperatureBand.Subarctic) == BaseBiome.Tundra, "subarctic normal should pick Tundra");
    Assert(Pick(MoistureLevel.Wet, TemperatureBand.Subarctic) == BaseBiome.Taiga, "subarctic wet should pick Taiga");
    Assert(Pick(MoistureLevel.Wet, TemperatureBand.Temperate) == BaseBiome.Swamp, "temperate wet should pick Swamp");
    Assert(Pick(MoistureLevel.Normal, TemperatureBand.Tropical) == BaseBiome.Prairie, "tropical normal should pick Prairie");
    Assert(Pick(MoistureLevel.Wet, TemperatureBand.Tropical) == BaseBiome.Jungle, "tropical wet should pick Jungle");
    Assert(Pick(MoistureLevel.Dry, TemperatureBand.Temperate, grasslandShrubland: 0) == BaseBiome.Grassland, "0 grassland/shrubland bias should pick Grassland");
    Assert(Pick(MoistureLevel.Dry, TemperatureBand.Temperate, grasslandShrubland: 100) == BaseBiome.Shrubland, "100 grassland/shrubland bias should pick Shrubland");
    Assert(Pick(MoistureLevel.Dry, TemperatureBand.Tropical, desertBadlands: 0) == BaseBiome.Desert, "0 desert/badlands bias should pick Desert");
    Assert(Pick(MoistureLevel.Dry, TemperatureBand.Tropical, desertBadlands: 100) == BaseBiome.Badlands, "100 desert/badlands bias should pick Badlands");
    Assert(Pick(MoistureLevel.Normal, TemperatureBand.Temperate, coniferBroadleaf: 0) == BaseBiome.ConiferForest, "0 forest bias should pick Conifer Forest");
    Assert(Pick(MoistureLevel.Normal, TemperatureBand.Temperate, coniferBroadleaf: 100) == BaseBiome.BroadleafForest, "100 forest bias should pick Broadleaf Forest");
    Assert(TerrainResolver.ResolveRegionBiome(BaseBiome.IceSheet) == "Ice Sheet", "ice sheet name should have a space");
    Assert(TerrainResolver.ResolveRegionBiome(BaseBiome.ConiferForest) == "Conifer Forest", "conifer forest name should have a space");
    Assert(TerrainResolver.ResolveRegionBiome(BaseBiome.BroadleafForest) == "Broadleaf Forest", "broadleaf forest name should have a space");
    Assert(TerrainResolver.Resolve(oceanTile).Name == "Ocean", "ocean elevation should resolve to Ocean");
    Assert(TerrainResolver.Resolve(coastTile).Name == "Coast", "coast elevation should resolve to Coast without map context");
    Assert(TerrainResolver.Resolve(deepIceTile).Name == "Ocean Ice Sheet", "deep ice should resolve to ocean ice sheet");
    Assert(TerrainResolver.Resolve(deepIceTile).Passable, "deep ice should be land-passable");

    static BaseBiome Pick(MoistureLevel moisture, TemperatureBand temperature, int grasslandShrubland = 0, int desertBadlands = 0, int coniferBroadleaf = 0)
    {
        return TerrainResolver.PickBaseBiome(moisture, temperature, grasslandShrubland, desertBadlands, coniferBroadleaf);
    }
}

void TerrainMovementCostsUseTerrainAndElevation()
{
    Assert(Cost("Grassland") == 1.0, "flat grassland should cost 1.0");
    Assert(Cost("Shrubland") == 1.0, "flat shrubland should cost 1.0");
    Assert(Cost("Prairie") == 1.0, "flat prairie should cost 1.0");
    Assert(Cost("Desert") == 1.5, "flat desert should cost 1.5");
    Assert(Cost("Tundra") == 1.5, "flat tundra should cost 1.5");
    Assert(Cost("Badlands") == 1.5, "flat badlands should cost 1.5");
    Assert(Cost("Taiga") == 1.5, "flat taiga should cost 1.5");
    Assert(Cost("Conifer Forest") == 1.5, "flat conifer forest should cost 1.5");
    Assert(Cost("Broadleaf Forest") == 1.5, "flat broadleaf forest should cost 1.5");
    Assert(Cost("Jungle") == 1.5, "flat jungle should cost 1.5");
    Assert(Cost("Swamp") == 2.0, "flat swamp should stack bad and forested surcharges");
    Assert(Cost("Ice Sheet") == 2.0, "flat ice sheet should cost 2.0");
    Assert(Cost("Grassland", Elevation.Hills) == 1.5, "hills should add 0.5");
    Assert(Cost("Grassland", Elevation.Mountains) == 2.0, "mountains should add 1.0");
    Assert(Cost("Grassland", Elevation.Peaks) == 2.0, "peaks should add 1.0");
    Assert(Cost("Swamp", Elevation.Hills) == 2.5, "swamp hills should combine terrain and elevation cost");

    static double Cost(string terrainName, Elevation elevation = Elevation.Flat)
    {
        var region = new RegionState
        {
            Id = 1,
            Name = "Test",
            Moisture = MoistureLevel.Normal,
            Temperature = TemperatureBand.Temperate,
            BaseBiome = BaseBiome.Grassland,
            FinalBiomeName = terrainName
        };
        var tile = new HexTile
        {
            Coord = new HexCoord(0, 0),
            Elevation = elevation,
            Moisture = MoistureLevel.Normal,
            RegionId = 1
        };

        return TerrainResolver.ResolveLand(region, tile).MovementCost;
    }
}

void SandboxRespectsCustomMapSize()
{
    var state = MapGenerator.CreateSandbox(database, 42, new WorldGenerationSettings { MapSize = 32 });
    Assert(state.WorldGeneration.MapSize == 32, "state should retain requested map size");
    Assert(state.Map.Count == 32 * 32, "custom map size should control both map axes");

    var noSea = MapGenerator.CreateSandbox(database, 42, new WorldGenerationSettings { MaxSeaNumber = 0 });
    Assert(noSea.WorldGeneration.MaxSeaNumber == 0, "state should retain requested max sea number");
    Assert(noSea.Map.Tiles.All(t => TerrainResolver.Resolve(noSea, t).Name != "Sea"), "max sea number zero should generate no seas");
}

void TerrainVariantSlidersControlGeneratedPairs()
{
    var first = new WorldGenerationSettings
    {
        GrasslandShrublandBias = 0,
        DesertBadlandsBias = 0,
        ConiferBroadleafForestBias = 0
    };
    var second = new WorldGenerationSettings
    {
        GrasslandShrublandBias = 100,
        DesertBadlandsBias = 100,
        ConiferBroadleafForestBias = 100
    };
    var firstState = MapGenerator.CreateSandbox(database, 42, first);
    var secondState = MapGenerator.CreateSandbox(database, 42, second);

    Assert(firstState.Regions.Values.Where(IsLandRegion).Where(r => r.Temperature == TemperatureBand.Temperate && r.Moisture == MoistureLevel.Dry).All(r => r.FinalBiomeName == "Grassland"), "0 grassland/shrubland bias should generate Grassland for dry temperate regions");
    Assert(secondState.Regions.Values.Where(IsLandRegion).Where(r => r.Temperature == TemperatureBand.Temperate && r.Moisture == MoistureLevel.Dry).All(r => r.FinalBiomeName == "Shrubland"), "100 grassland/shrubland bias should generate Shrubland for dry temperate regions");
    Assert(firstState.Regions.Values.Where(IsLandRegion).Where(r => r.Temperature == TemperatureBand.Tropical && r.Moisture == MoistureLevel.Dry).All(r => r.FinalBiomeName == "Desert"), "0 desert/badlands bias should generate Desert for dry tropical regions");
    Assert(secondState.Regions.Values.Where(IsLandRegion).Where(r => r.Temperature == TemperatureBand.Tropical && r.Moisture == MoistureLevel.Dry).All(r => r.FinalBiomeName == "Badlands"), "100 desert/badlands bias should generate Badlands for dry tropical regions");
    Assert(firstState.Regions.Values.Where(IsLandRegion).Where(r => r.Temperature == TemperatureBand.Temperate && r.Moisture == MoistureLevel.Normal).All(r => r.FinalBiomeName == "Conifer Forest"), "0 conifer/broadleaf bias should generate Conifer Forest for normal temperate regions");
    Assert(secondState.Regions.Values.Where(IsLandRegion).Where(r => r.Temperature == TemperatureBand.Temperate && r.Moisture == MoistureLevel.Normal).All(r => r.FinalBiomeName == "Broadleaf Forest"), "100 conifer/broadleaf bias should generate Broadleaf Forest for normal temperate regions");

    static bool IsLandRegion(RegionState region) => region.FinalBiomeName != "Sea";
}

void SandboxGenerationIsDeterministic()
{
    var first = MapGenerator.CreateSandbox(database, 42);
    var second = MapGenerator.CreateSandbox(database, 42);
    Assert(first.Map.Count == second.Map.Count, "map tile count should match");

    foreach (var firstTile in first.Map.Tiles)
    {
        var secondTile = second.Map.Get(firstTile.Coord);
        Assert(firstTile.RegionId == secondTile.RegionId, $"region mismatch at {firstTile.Coord}");
        Assert(firstTile.Elevation == secondTile.Elevation, $"elevation mismatch at {firstTile.Coord}");
        Assert(firstTile.Moisture == secondTile.Moisture, $"moisture mismatch at {firstTile.Coord}");
        Assert(firstTile.FeatureIds.SequenceEqual(secondTile.FeatureIds), $"feature mismatch at {firstTile.Coord}");
        Assert(firstTile.ResourceId == secondTile.ResourceId, $"resource mismatch at {firstTile.Coord}");
    }

    foreach (var firstRegion in first.Regions.Values)
    {
        var secondRegion = second.Regions[firstRegion.Id];
        Assert(firstRegion.Moisture == secondRegion.Moisture, $"region moisture mismatch for {firstRegion.Id}");
        Assert(firstRegion.Temperature == secondRegion.Temperature, $"region temperature mismatch for {firstRegion.Id}");
        Assert(firstRegion.BaseBiome == secondRegion.BaseBiome, $"region biome mismatch for {firstRegion.Id}");
        Assert(firstRegion.FinalBiomeName == secondRegion.FinalBiomeName, $"region final biome mismatch for {firstRegion.Id}");
        Assert(firstRegion.TileCoords.SequenceEqual(secondRegion.TileCoords), $"region tiles mismatch for {firstRegion.Id}");
    }
}

void SandboxHasOceanBorderAndRegionClimateBands()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var mapSize = state.WorldGeneration.MapSize;
    Assert(state.Map.Count == mapSize * mapSize, "map should use the configured square-ish canvas size");
    Assert(state.Map.Count == 64 * 64, "sandbox map should be 64x64");

    foreach (var tile in state.Map.Tiles)
    {
        var row = tile.Coord.R;
        var col = ColumnOf(tile.Coord);
        var isEdge = col == 0 || row == 0 || col == mapSize - 1 || row == mapSize - 1;

        if (isEdge)
        {
            Assert(tile.Elevation == Elevation.Ocean, $"edge tile {tile.Coord} should be ocean");
            Assert(!TerrainResolver.Resolve(state, tile).Passable, $"edge tile {tile.Coord} should be impassable");
        }
    }

    var land = state.Map.Tiles.Where(t => !t.Elevation.IsWaterLike() && TerrainResolver.Resolve(state, t).Passable).ToList();
    Assert(land.Count > 0, "island should contain passable land");
    Assert(land.All(t => t.RegionId is not null), "every land tile should belong to a region");
    Assert(state.Regions.Count >= 8, "map should contain several biome regions");
    Assert(state.Regions.Values.All(r => r.TileCoords.Count > 0), "every region should own at least one tile");
    Assert(state.Regions.Values.Select(r => r.Temperature).Distinct().Count() >= 3, "regions should contain multiple temperature bands");
    Assert(state.Regions.Values.Select(r => r.Moisture).Distinct().Count() >= 2, "regions should contain multiple moisture levels");
    Assert(land.Any(t => t.Elevation == Elevation.Hills), "island should contain hills");
    Assert(land.Any(t => t.Elevation == Elevation.Mountains), "island should contain mountains");
    Assert(land.Any(t => t.Elevation == Elevation.Peaks), "island should contain peaks");
    Assert(land.Where(t => t.Coord.R >= mapSize * 3 / 4).Any(t => state.Regions[t.RegionId!.Value].Temperature == TemperatureBand.Tropical), "southern land should trend tropical");
    Assert(land.Where(t => t.Coord.R <= mapSize / 4).Any(t => state.Regions[t.RegionId!.Value].Temperature is TemperatureBand.Arctic or TemperatureBand.Subarctic), "northern land should trend arctic or subarctic");
    Assert(land.All(t => TerrainResolver.Resolve(state, t).Name.Length > 0), "every land tile should resolve to a named terrain");
    Assert(land.All(t => IsAllowedLandTerrain(TerrainResolver.Resolve(state, t).Name)), "every land tile should resolve to an approved simplified terrain");
    var landRegions = state.Regions.Values.Where(r => r.FinalBiomeName != "Sea").ToList();
    Assert(landRegions.All(r => r.Temperature != TemperatureBand.Tropical || r.Moisture != MoistureLevel.Dry || r.FinalBiomeName is "Desert" or "Badlands"), "dry tropical regions should resolve to desert or badlands");
    Assert(landRegions.All(r => r.Temperature != TemperatureBand.Temperate || r.Moisture != MoistureLevel.Dry || r.FinalBiomeName is "Grassland" or "Shrubland"), "dry temperate regions should resolve to grassland or shrubland");
    Assert(landRegions.All(r => r.Temperature != TemperatureBand.Temperate || r.Moisture != MoistureLevel.Normal || r.FinalBiomeName is "Conifer Forest" or "Broadleaf Forest"), "normal temperate regions should resolve to a forest variant");
    Assert(land.Any(t => TerrainResolver.Resolve(state, t).Name.Contains("Jungle", StringComparison.OrdinalIgnoreCase)), "larger world should expose at least one jungle tile for seed 42");
    Assert(land.Any(t => TerrainResolver.Resolve(state, t).Name.Contains("Taiga", StringComparison.OrdinalIgnoreCase)), "larger world should expose at least one taiga tile for seed 42");
    Assert(land.Any(t => TerrainResolver.Resolve(state, t).Name == "Ice Sheet"), "default world should expose at least one ice sheet tile for seed 42");
    Assert(state.Map.Tiles.Where(t => !t.Elevation.IsWaterLike() && t.Coord.R <= mapSize / 4).Any(t => TerrainResolver.Resolve(state, t).Name == "Ice Sheet"), "north part of the island should always contain a land ice sheet");
    Assert(state.Map.Tiles.Any(t => t.Elevation == Elevation.DeepIce), "generated polar ice should expand onto nearby ocean as deep ice");
    Assert(state.Map.Tiles.Where(t => t.Elevation == Elevation.DeepIce).All(t => !IsMapEdge(t)), "deep ice should not consume the outer ocean border");
    Assert(state.Map.Tiles.Where(t => TerrainResolver.Resolve(state, t).Name == "Ocean Ice Sheet").All(t => state.Map.Neighbors(t.Coord).Any(n => TerrainResolver.Resolve(state, n).Name is "Ice Sheet" or "Ocean Ice Sheet")), "ocean ice sheet should stay attached to coastal polar ice");
    Assert(DesertRegionsAreBroad(state), "default world desert tiles should concentrate into broad regions when deserts appear");
    Assert(state.Map.Tiles.Where(t => t.Elevation == Elevation.Coast && state.Map.IsCoastline(t)).Any(t => TerrainResolver.Resolve(state, t).Name == "Coast"), "coast elevation should produce coast terrain on outer water bodies");
    Assert(state.Map.Tiles.Where(t => t.Elevation == Elevation.Coast && !state.Map.IsOuterWaterBody(t)).Any(t => TerrainResolver.Resolve(state, t).Name == "Lake"), "inland coast elevation should resolve as lake");
    Assert(state.Map.Tiles.Where(t => t.Elevation == Elevation.Coast).All(t => t.WaterBodyKind != WaterBodyKind.None), "coast tiles should use cached water-body classification");
    var seaRegionCount = state.Map.Tiles
        .Where(t => TerrainResolver.Resolve(state, t).Name == "Sea")
        .Select(t => t.RegionId)
        .Where(id => id is not null)
        .Distinct()
        .Count();
    Assert(seaRegionCount <= state.WorldGeneration.MaxSeaNumber, "generated seas should not exceed the selected maximum");
}

void SandboxHasInlandLakesAndElevationFeatures()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var inlandWater = state.Map.Tiles
        .Where(t => t.Elevation == Elevation.Coast && !IsMapEdge(t) && !state.Map.IsOuterWaterBody(t))
        .ToList();
    var peaks = state.Map.Tiles.Where(t => t.Elevation == Elevation.Peaks).ToList();
    var hills = state.Map.Tiles.Where(t => t.Elevation == Elevation.Hills).ToList();
    var mountains = state.Map.Tiles.Where(t => t.Elevation == Elevation.Mountains).ToList();
    var rugged = state.Map.Tiles.Where(t => t.Elevation is Elevation.Mountains or Elevation.Peaks).ToList();
    var elevated = state.Map.Tiles
        .Where(t => t.Elevation is Elevation.Hills or Elevation.Mountains or Elevation.Peaks)
        .Where(t => t.RegionId is not null)
        .ToList();

    Assert(inlandWater.Count >= 4, "map should contain small inland lake/coastline water");
    Assert(hills.Count > 0, "elevation generation should include hills");
    Assert(mountains.Count > 0, "elevation generation should include mountains");
    Assert(peaks.Count > 0, "elevation generation should include a few peaks");
    Assert(peaks.Count < mountains.Count, "peaks should be fewer than mountains");
    Assert(mountains.All(tile => state.Map.Tiles.Any(other => other.Elevation == Elevation.Hills && other.Coord.DistanceTo(tile.Coord) <= 2)), "every mountain should have hills close by");
    Assert(peaks.All(tile => state.Map.Neighbors(tile.Coord).Any(n => n.Elevation == Elevation.Mountains)), "every peak should neighbor a mountain");
    Assert(hills.Where(IsRegionEdge).Count() >= hills.Count / 2, "most hills should be placed on region edges");
    Assert(rugged.Any(tile => state.Map.Neighbors(tile.Coord).Any(n => n.Elevation is Elevation.Mountains or Elevation.Peaks)), "rugged tiles should cluster");
    Assert(elevated.All(tile => tile.Moisture == state.Regions[tile.RegionId!.Value].Moisture), "elevation should not dry tile moisture");
    Assert(state.Map.Tiles.Where(t => t.FeatureIds.Contains("volcano")).All(t => t.Elevation is Elevation.Mountains or Elevation.Peaks), "volcanoes should only appear on mountains or peaks");
    Assert(state.Map.Tiles.Where(t => t.ResourceId == "game").All(t => TerrainResolver.Resolve(state, t).Passable && TerrainResolver.Resolve(state, t).Name is not "Desert" and not "Badlands" && !TerrainResolver.Resolve(state, t).Name.Contains("Ice", StringComparison.OrdinalIgnoreCase)), "game should not appear in deserts, badlands, or ice terrain");

    bool IsRegionEdge(HexTile tile)
    {
        return tile.RegionId is { } regionId
            && state.Map.Neighbors(tile.Coord).Any(n => n.RegionId is not null && n.RegionId != regionId);
    }
}

void ElevationVarianceControlsRuggedTerrain()
{
    var flat = MapGenerator.CreateSandbox(database, 42, new WorldGenerationSettings { ElevationVariance = 0 });
    var rugged = MapGenerator.CreateSandbox(database, 42, new WorldGenerationSettings { ElevationVariance = 100 });
    var flatElevated = flat.Map.Tiles.Count(t => t.Elevation is Elevation.Hills or Elevation.Mountains or Elevation.Peaks);
    var ruggedElevated = rugged.Map.Tiles.Count(t => t.Elevation is Elevation.Hills or Elevation.Mountains or Elevation.Peaks);

    Assert(flatElevated == 0, "zero elevation variance should produce no elevated tiles");
    Assert(ruggedElevated > flatElevated, "higher elevation variance should produce more elevated tiles");
}

void FactionsStartOnPassableIslandTiles()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    Assert(state.StacksForFaction(state.PlayerFaction.Id).Count() == 2, "player should start with two army stacks");
    Assert(state.AgentsForFaction(state.PlayerFaction.Id).Count() == 2, "player should start with two loose agents");

    foreach (var city in state.Cities.Values)
    {
        Assert(TerrainResolver.Resolve(state, state.Map.Get(city.Coord)).Passable, $"{city.Name} should start on passable land");
    }

    foreach (var stack in state.Stacks.Values)
    {
        Assert(TerrainResolver.Resolve(state, state.Map.Get(stack.Coord)).Passable, $"stack {stack.Id} should start on passable land");
    }

    foreach (var agent in state.Agents.Values)
    {
        Assert(TerrainResolver.Resolve(state, state.Map.Get(agent.Coord)).Passable, $"agent {agent.Id} should start on passable land");
    }
}

void MovementRejectsWaterAndSpendsMovement()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var stack = state.StacksForFaction(state.PlayerFaction.Id).First();
    var range = GameRules.MovementRange(state, stack.Coord, stack.MovementLeft);
    Assert(range.Count > 1, "player stack should have reachable tiles");
    Assert(range.Keys.All(c => TerrainResolver.Resolve(state, state.Map.Get(c)).Passable), "movement range should exclude impassable water");

    var destination = range.Where(kv => kv.Value > 0).OrderBy(kv => kv.Value).First();
    var moved = GameRules.TryMoveStack(state, stack.Id, destination.Key);
    Assert(moved, "stack should move to reachable destination");
    Assert(stack.Coord == destination.Key, "stack coord should update");
    Assert(stack.MovementLeft == 2 - destination.Value, "stack movement should be reduced by movement cost");
}

void AgentMoveDoesNotAutoAttachToArmy()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var stack = state.StacksForFaction(state.PlayerFaction.Id).First();
    var agent = state.AgentsForFaction(state.PlayerFaction.Id).First();

    state.Map.Get(agent.Coord).AgentIds.Remove(agent.Id);
    agent.Coord = stack.Coord;
    agent.MovementLeft = 10;
    state.Map.Get(stack.Coord).AgentIds.Add(agent.Id);

    var neighbor = state.Map.Neighbors(stack.Coord)
        .First(tile => TerrainResolver.Resolve(state, tile).Passable && !tile.StackIds.Contains(stack.Id));

    var movedAway = GameRules.TryMoveAgent(state, agent.Id, neighbor.Coord);
    Assert(movedAway, "agent should move away from army tile");
    Assert(agent.JoinedStackId is null, "moving agent should stay loose");

    var movedBack = GameRules.TryMoveAgent(state, agent.Id, stack.Coord);
    Assert(movedBack, "agent should move back onto army tile");
    Assert(agent.JoinedStackId is null, "agent should not auto-attach when entering friendly army tile");
    Assert(stack.JoinedAgentIds.Count == 0, "army should not gain attached agents without explicit attach");
    Assert(state.Map.Get(stack.Coord).AgentIds.Contains(agent.Id), "loose agent should remain on tile index after returning");
}

void AgentJoinsAndDetachesFromArmy()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var stack = state.StacksForFaction(state.PlayerFaction.Id).First();
    var agent = state.AgentsForFaction(state.PlayerFaction.Id).First();

    var joined = GameRules.TryJoinAgentToStack(state, agent.Id, stack.Id);
    Assert(joined, "agent should join colocated friendly army");
    Assert(stack.JoinedAgentIds.SequenceEqual([agent.Id]), "stack should track joined agent");
    Assert(agent.JoinedStackId == stack.Id, "agent should track joined stack");
    Assert(!state.Map.Get(stack.Coord).AgentIds.Contains(agent.Id), "joined agent should leave tile agent list");

    var detached = GameRules.TryDetachLeader(state, stack.Id);
    Assert(detached, "leader should detach");
    Assert(stack.JoinedAgentIds.Count == 0, "stack joined agents should clear");
    Assert(agent.JoinedStackId is null, "agent joined stack should clear");
    Assert(state.Map.Get(stack.Coord).AgentIds.Contains(agent.Id), "detached agent should return to tile agent list");
}

void CityBuildingUpgradeReplacesPreviousLevel()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var city = state.Cities.Values.First(c => c.FactionId == state.PlayerFaction.Id);

    Assert(city.BuildingIds.SequenceEqual(["campsite"]), "city should start with only campsite");

    var upgraded = GameRules.TryUpgradeCityBuilding(state, city.Id);

    Assert(upgraded, "city should upgrade from campsite to shelter");
    Assert(city.BuildingIds.SequenceEqual(["shelter"]), "shelter should replace campsite instead of being added beside it");
    Assert(state.Log.Any(entry => entry.Text.Contains("upgraded to Shelter", StringComparison.OrdinalIgnoreCase)), "upgrade should be logged");
}

void CombatRemovesLosingStack()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var attacker = state.StacksForFaction("player").First();
    var defender = state.StacksForFaction("ember").First();
    var leader = state.AgentsForFaction("ember").First();
    state.Map.Get(leader.Coord).AgentIds.Remove(leader.Id);
    leader.Coord = defender.Coord;
    GameRules.TryJoinAgentToStack(state, leader.Id, defender.Id);
    defender.Units.Clear();
    defender.Units.Add(new UnitInstance { TypeId = "militia", Count = 1 });

    CombatResolver.Resolve(state, attacker, defender);
    Assert(state.Stacks.ContainsKey(attacker.Id), "attacker should survive favorable combat");
    Assert(!state.Stacks.ContainsKey(defender.Id), "weak defender should be removed");
    Assert(leader.JoinedStackId is null, "defeated army should release attached agents");
    Assert(state.Map.Get(defender.Coord).AgentIds.Contains(leader.Id), "released leader should remain on the defeated army tile");
}

void DirectorProducesValidAiState()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var director = new FactionDirector();
    GameRules.AdvanceTurn(state);
    director.TakeTurn(state, state.CurrentFaction.Id);

    foreach (var stack in state.Stacks.Values)
    {
        Assert(state.Map.TryGet(stack.Coord, out var tile), "stack coord should remain on map");
        Assert(tile.StackIds.Contains(stack.Id), "tile should reference resident stack");
    }

    Assert(state.Log.Any(entry => entry.Text.Contains("director chose", StringComparison.OrdinalIgnoreCase)), "director should log weighted action");
}

void SaveLoadPreservesGameState()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var stack = state.StacksForFaction(state.PlayerFaction.Id).First();
    var agent = state.AgentsForFaction(state.PlayerFaction.Id).First();
    GameRules.TryJoinAgentToStack(state, agent.Id, stack.Id);
    GameRules.AdvanceTurn(state);

    var json = GameStateSerializer.ToJson(state);
    var loaded = GameStateSerializer.FromJson(database, json);

    Assert(GameStateSerializer.ToJson(loaded) == json, "save/load round trip should preserve serialized state");
    Assert(loaded.WorldGeneration.ElevationVariance == state.WorldGeneration.ElevationVariance, "loaded state should retain world generation settings");
    Assert(loaded.WorldGeneration.GrasslandShrublandBias == state.WorldGeneration.GrasslandShrublandBias, "loaded state should retain grassland/shrubland bias");
    Assert(loaded.WorldGeneration.DesertBadlandsBias == state.WorldGeneration.DesertBadlandsBias, "loaded state should retain desert/badlands bias");
    Assert(loaded.WorldGeneration.ConiferBroadleafForestBias == state.WorldGeneration.ConiferBroadleafForestBias, "loaded state should retain conifer/broadleaf bias");
    Assert(loaded.Map.Tiles.Where(t => t.Elevation == Elevation.Coast).All(t => t.WaterBodyKind != WaterBodyKind.None), "loaded coast tiles should retain water-body classification");
    Assert(loaded.Map.Get(stack.Coord).StackIds.Contains(stack.Id), "loaded map should retain stack tile index");
    Assert(loaded.Stacks[stack.Id].JoinedAgentIds.SequenceEqual([agent.Id]), "loaded stack should retain joined leader");
    Assert(loaded.Agents[agent.Id].JoinedStackId == stack.Id, "loaded agent should retain joined stack");
}

void LoadedAiTurnReplaysDeterministically()
{
    var original = MapGenerator.CreateSandbox(database, 42);
    GameRules.AdvanceTurn(original);

    var loaded = GameStateSerializer.FromJson(database, GameStateSerializer.ToJson(original));
    var originalDirector = new FactionDirector();
    var loadedDirector = new FactionDirector();

    originalDirector.TakeTurn(original, original.CurrentFaction.Id);
    loadedDirector.TakeTurn(loaded, loaded.CurrentFaction.Id);

    Assert(GameStateSerializer.ToJson(loaded) == GameStateSerializer.ToJson(original), "loaded AI turn should replay to the same state");
}

void MovementOverspendAllowsLastStep()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var stack = state.StacksForFaction(state.PlayerFaction.Id).First();

    // With 0.5 movement left, a unit should still be able to enter a tile
    // that costs 1.0 — spending the remainder and arriving with 0 left.
    stack.MovementLeft = 0.5;
    var range = GameRules.MovementRange(state, stack.Coord, stack.MovementLeft);
    var passableNeighbors = state.Map.Neighbors(stack.Coord)
        .Where(t => TerrainResolver.Resolve(state, t).Passable)
        .ToList();
    Assert(passableNeighbors.Count > 0, "player starting tile should have at least one passable neighbor");
    Assert(passableNeighbors.All(t => range.ContainsKey(t.Coord)), "unit with 0.5 movement should reach passable neighbors via overspend");

    // With exactly 0 movement, no tile beyond the origin is reachable.
    stack.MovementLeft = 0;
    var zeroRange = GameRules.MovementRange(state, stack.Coord, stack.MovementLeft);
    Assert(zeroRange.Count == 1 && zeroRange.ContainsKey(stack.Coord), "unit with zero movement should have no reachable tiles beyond its position");
}

void TurnCyclingAdvancesFactionAndCountsTurns()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var factionCount = state.Factions.Count;
    var startTurn = state.Turn;
    Assert(state.CurrentFactionIndex == 0, "game should start with the first faction active");

    // Advance through all but the last faction — turn counter must not change yet.
    for (var i = 1; i < factionCount; i++)
    {
        GameRules.AdvanceTurn(state);
        Assert(state.CurrentFactionIndex == i, $"after {i} advance(s), faction index should be {i}");
        Assert(state.Turn == startTurn, "turn counter should not increment mid-cycle");
    }

    // Final advance wraps back to faction 0 and increments the world turn.
    GameRules.AdvanceTurn(state);
    Assert(state.CurrentFactionIndex == 0, "faction index should wrap back to 0 after all factions played");
    Assert(state.Turn == startTurn + 1, "turn counter should increment once every faction has played");

    // A second full cycle increments again.
    for (var i = 0; i < factionCount; i++)
    {
        GameRules.AdvanceTurn(state);
    }
    Assert(state.Turn == startTurn + 2, "turn counter should increment once per full faction cycle");
}

void CombatAppliesCasualtiesToWinner()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var attacker = state.StacksForFaction("player").First();
    var defender = state.StacksForFaction("ember").First();

    attacker.Units.Clear();
    attacker.Units.Add(new UnitInstance { TypeId = "spearmen", Count = 20 });
    defender.Units.Clear();
    defender.Units.Add(new UnitInstance { TypeId = "militia", Count = 1 });

    var countBefore = attacker.Units[0].Count;
    CombatResolver.Resolve(state, attacker, defender);

    Assert(state.Stacks.ContainsKey(attacker.Id), "dominant attacker should survive");
    Assert(!state.Stacks.ContainsKey(defender.Id), "weak defender should be removed");
    var expected = Math.Max(1, (int)Math.Round(countBefore * 0.75));
    Assert(attacker.Units[0].Count == expected, $"winner should lose 25% of its units: {countBefore} → {expected}");
}

void MoveStackFailsForInvalidDestination()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var stack = state.StacksForFaction(state.PlayerFaction.Id).First();
    var originalCoord = stack.Coord;

    // Ocean is impassable — move should be rejected.
    var oceanCoord = state.Map.Tiles.First(t => t.Elevation == Elevation.Ocean).Coord;
    Assert(!GameRules.TryMoveStack(state, stack.Id, oceanCoord), "move to ocean tile should fail");
    Assert(stack.Coord == originalCoord, "failed move should not change stack position");
    Assert(state.Map.Get(originalCoord).StackIds.Contains(stack.Id), "tile index should be unchanged after failed ocean move");

    // Destination too far to reach in one turn should also fail.
    var farCoord = state.Map.Tiles
        .Where(t => TerrainResolver.Resolve(state, t).Passable)
        .OrderByDescending(t => t.Coord.DistanceTo(originalCoord))
        .First().Coord;
    Assert(!GameRules.TryMoveStack(state, stack.Id, farCoord), "move beyond movement range should fail");
    Assert(stack.Coord == originalCoord, "failed out-of-range move should not change stack position");
}

void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
        Environment.ExitCode = 1;
        throw;
    }
}

IEnumerable<(string Name, Action Test)> SelectTests(IEnumerable<(string Name, Action Test)> tests, string[] arguments)
{
    var listedTests = tests.ToList();
    if (arguments.Any(argument => string.Equals(argument, "--list", StringComparison.OrdinalIgnoreCase)))
    {
        foreach (var test in listedTests)
        {
            Console.WriteLine(test.Name);
        }

        Environment.Exit(0);
    }

    var filter = string.Join(' ', arguments.Where(argument => !string.Equals(argument, "--list", StringComparison.OrdinalIgnoreCase))).Trim();
    if (filter.Length == 0)
    {
        return listedTests;
    }

    var matches = listedTests
        .Where(test => test.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
        .ToList();

    if (matches.Count == 0)
    {
        throw new InvalidOperationException($"No tests matched filter '{filter}'. Use --list to see available names.");
    }

    return matches;
}

void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

string FindProjectRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "project.godot")) && Directory.Exists(Path.Combine(directory.FullName, "data")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not locate StrategyGame project root.");
}

int ColumnOf(HexCoord coord)
{
    return coord.Q + coord.R / 2;
}

bool IsMapEdge(HexTile tile)
{
    var col = ColumnOf(tile.Coord);
    var row = tile.Coord.R;
    return col == 0 || row == 0 || col == WorldGenerationSettings.DefaultMapSize - 1 || row == WorldGenerationSettings.DefaultMapSize - 1;
}

bool IsAllowedLandTerrain(string name)
{
    return name is "Ice Sheet"
        or "Tundra"
        or "Taiga"
        or "Grassland"
        or "Shrubland"
        or "Conifer Forest"
        or "Broadleaf Forest"
        or "Swamp"
        or "Desert"
        or "Badlands"
        or "Prairie"
        or "Jungle";
}

bool DesertRegionsAreBroad(GameState state)
{
    var desertTiles = state.Map.Tiles
        .Where(t => TerrainResolver.Resolve(state, t).Name.Contains("Desert", StringComparison.OrdinalIgnoreCase))
        .ToList();
    if (desertTiles.Count == 0)
    {
        return true;
    }

    var desertRegions = desertTiles
        .Where(t => t.RegionId is not null)
        .GroupBy(t => t.RegionId!.Value)
        .ToList();

    var averageSize = desertRegions.Average(group => group.Count());
    return desertRegions.Count <= 3 && averageSize >= 20;
}
