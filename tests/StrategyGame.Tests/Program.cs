using StrategyGame.Core;

var root = FindProjectRoot();
var database = GameDatabase.LoadFromDirectory(Path.Combine(root, "data"));

Run("hex distance and neighbors", HexDistanceAndNeighbors);
Run("database loads required catalogs", DatabaseLoadsRequiredCatalogs);
Run("terrain resolver applies region biome tables", TerrainResolverAppliesRegionBiomeTables);
Run("sandbox respects custom map size", SandboxRespectsCustomMapSize);
Run("sandbox generation is deterministic", SandboxGenerationIsDeterministic);
Run("sandbox has ocean border and region climate bands", SandboxHasOceanBorderAndRegionClimateBands);
Run("sandbox has inland lakes and elevation features", SandboxHasInlandLakesAndElevationFeatures);
Run("elevation variance controls rugged terrain", ElevationVarianceControlsRuggedTerrain);
Run("factions start on passable island tiles", FactionsStartOnPassableIslandTiles);
Run("movement rejects water and spends movement", MovementRejectsWaterAndSpendsMovement);
Run("agent move does not auto attach to army", AgentMoveDoesNotAutoAttachToArmy);
Run("agent joins and detaches from army", AgentJoinsAndDetachesFromArmy);
Run("city building upgrade replaces previous level", CityBuildingUpgradeReplacesPreviousLevel);
Run("combat removes losing stack", CombatRemovesLosingStack);
Run("director produces valid AI state", DirectorProducesValidAiState);
Run("save load preserves game state", SaveLoadPreservesGameState);
Run("loaded AI turn replays deterministically", LoadedAiTurnReplaysDeterministically);
Run("movement overspend allows last step", MovementOverspendAllowsLastStep);
Run("turn cycling advances faction and counts turns", TurnCyclingAdvancesFactionAndCountsTurns);
Run("combat applies casualties to winner", CombatAppliesCasualtiesToWinner);
Run("move stack fails for invalid destination", MoveStackFailsForInvalidDestination);

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
    Assert(TerrainResolver.PickBaseBiome(MoistureLevel.Dry, WaterRetention.Draining) == BaseBiome.Desert, "dry/draining should pick Desert");
    Assert(TerrainResolver.PickBaseBiome(MoistureLevel.Dry, WaterRetention.Draining, TemperatureBand.Tropical) == BaseBiome.Desert, "tropical dry/draining should stay Desert");
    Assert(TerrainResolver.PickBaseBiome(MoistureLevel.Dry, WaterRetention.Draining, TemperatureBand.Temperate) == BaseBiome.Dryland, "temperate dry/draining should become Dryland");
    Assert(TerrainResolver.PickBaseBiome(MoistureLevel.Dry, WaterRetention.Draining, TemperatureBand.Subarctic) == BaseBiome.Dryland, "subarctic dry/draining should become Dryland");
    Assert(TerrainResolver.PickBaseBiome(MoistureLevel.Normal, WaterRetention.Holding) == BaseBiome.Floodplain, "normal/holding should pick Floodplain");
    Assert(TerrainResolver.PickBaseBiome(MoistureLevel.Wet, WaterRetention.Holding) == BaseBiome.Swamp, "wet/holding should pick Swamp");
    Assert(ResolveName(BaseBiome.Plain, TemperatureBand.Tropical, Vegetation.Lush) == "Jungle", "tropical/lush plain should become Jungle");
    Assert(ResolveName(BaseBiome.Plain, TemperatureBand.Subtropical, Vegetation.Lush) == "Rainforest", "subtropical/lush plain should become Rainforest");
    Assert(ResolveName(BaseBiome.Wetland, TemperatureBand.Subtropical, Vegetation.Lush) == "Rainforest", "subtropical/lush wetland should become Rainforest");
    Assert(ResolveName(BaseBiome.Swamp, TemperatureBand.Subtropical, Vegetation.Lush) == "Rainforest", "subtropical/lush swamp should become Rainforest");
    Assert(ResolveName(BaseBiome.Plain, TemperatureBand.Subarctic, Vegetation.Lush) == "Taiga", "subarctic/lush plain should become Taiga");
    Assert(ResolveName(BaseBiome.Dryland, TemperatureBand.Temperate, Vegetation.Sparse) == "Shrubland", "temperate sparse dryland should become Shrubland");
    Assert(ResolveName(BaseBiome.Swamp, TemperatureBand.Temperate, Vegetation.Lush) == "Forest", "temperate lush swamp should become Forest");
    Assert(ResolveName(BaseBiome.Floodplain, TemperatureBand.Temperate, Vegetation.None) == "Grassland", "temperate floodplain without vegetation should become Grassland");
    Assert(ResolveName(BaseBiome.Floodplain, TemperatureBand.Temperate, Vegetation.Sparse) == "Grassland", "temperate sparse floodplain should become Grassland");
    Assert(ResolveName(BaseBiome.Wasteland, TemperatureBand.Temperate, Vegetation.None) == "Steppe", "temperate wasteland should become Steppe");
    Assert(ResolveName(BaseBiome.Wasteland, TemperatureBand.Subarctic, Vegetation.None) == "Tundra", "subarctic wasteland should become Tundra");
    Assert(ResolveName(BaseBiome.Wasteland, TemperatureBand.Arctic, Vegetation.None) == "Ice Sheet", "arctic wasteland should become Ice Sheet");
    Assert(ResolveName(BaseBiome.Badlands, TemperatureBand.Temperate, Vegetation.Sparse) == "Badlands", "sparse badlands should stay Badlands");
    Assert(ResolveName(BaseBiome.Barrens, TemperatureBand.Temperate, Vegetation.Sparse) == "Shrubland", "temperate sparse barrens should merge into Shrubland");
    Assert(ResolveName(BaseBiome.Barrens, TemperatureBand.Temperate, Vegetation.None) == "Wasteland", "temperate barrens without vegetation should merge into Wasteland");
    Assert(ResolveName(BaseBiome.Barrens, TemperatureBand.Subarctic, Vegetation.Sparse) == "Tundra", "subarctic barrens should merge into Tundra");
    Assert(TerrainResolver.ClampVegetation(BaseBiome.Desert, TemperatureBand.Tropical, Vegetation.Lush) == Vegetation.None, "desert should clamp vegetation to none");
    Assert(TerrainResolver.ClampVegetation(BaseBiome.Badlands, TemperatureBand.Temperate, Vegetation.Lush) == Vegetation.Sparse, "badlands should clamp vegetation to sparse");
    Assert(TerrainResolver.FormatFinalBiomeName(TemperatureBand.Arctic, "Ice Sheet") == "Ice Sheet", "ice sheet should not receive a climate prefix");
    Assert(TerrainResolver.FormatFinalBiomeName(TemperatureBand.Subtropical, "Rainforest") == "Rainforest", "rainforest should not receive a climate prefix");
    Assert(TerrainResolver.FormatFinalBiomeName(TemperatureBand.Temperate, "Steppe") == "Steppe", "steppe should not receive a climate prefix");
    Assert(TerrainResolver.Resolve(oceanTile).Name == "Ocean", "ocean elevation should resolve to Ocean");
    Assert(TerrainResolver.Resolve(coastTile).Name == "Coast", "coast elevation should resolve to Coast without map context");
    Assert(TerrainResolver.Resolve(deepIceTile).Name == "Ocean Ice Sheet", "deep ice should resolve to ocean ice sheet");
    Assert(TerrainResolver.Resolve(deepIceTile).Passable, "deep ice should be land-passable");
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
        Assert(firstTile.Vegetation == secondTile.Vegetation, $"vegetation mismatch at {firstTile.Coord}");
        Assert(firstTile.FeatureIds.SequenceEqual(secondTile.FeatureIds), $"feature mismatch at {firstTile.Coord}");
        Assert(firstTile.ResourceId == secondTile.ResourceId, $"resource mismatch at {firstTile.Coord}");
    }

    foreach (var firstRegion in first.Regions.Values)
    {
        var secondRegion = second.Regions[firstRegion.Id];
        Assert(firstRegion.Moisture == secondRegion.Moisture, $"region moisture mismatch for {firstRegion.Id}");
        Assert(firstRegion.WaterRetention == secondRegion.WaterRetention, $"region retention mismatch for {firstRegion.Id}");
        Assert(firstRegion.Temperature == secondRegion.Temperature, $"region temperature mismatch for {firstRegion.Id}");
        Assert(firstRegion.BaseBiome == secondRegion.BaseBiome, $"region biome mismatch for {firstRegion.Id}");
        Assert(firstRegion.Vegetation == secondRegion.Vegetation, $"region vegetation mismatch for {firstRegion.Id}");
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
    Assert(state.Regions.Values.Select(r => r.WaterRetention).Distinct().Count() >= 2, "regions should contain multiple water-retention levels");
    Assert(land.Any(t => t.Elevation == Elevation.Hills), "island should contain hills");
    Assert(land.Any(t => t.Elevation == Elevation.Mountains), "island should contain mountains");
    Assert(land.Any(t => t.Elevation == Elevation.Peaks), "island should contain peaks");
    Assert(land.Where(t => t.Coord.R >= mapSize * 3 / 4).Any(t => state.Regions[t.RegionId!.Value].Temperature is TemperatureBand.Tropical or TemperatureBand.Subtropical), "southern land should trend tropical or subtropical");
    Assert(land.Where(t => t.Coord.R <= mapSize / 4).Any(t => state.Regions[t.RegionId!.Value].Temperature is TemperatureBand.Arctic or TemperatureBand.Subarctic), "northern land should trend arctic or subarctic");
    Assert(land.All(t => TerrainResolver.Resolve(state, t).Name.Length > 0), "every land tile should resolve to a named terrain");
    Assert(land.All(t => TerrainResolver.Resolve(state, t).Name != "Temperate Desert"), "temperate desert should be impossible after temperature-aware base biome adjustment");
    Assert(land.Any(t => TerrainResolver.Resolve(state, t).Name.Contains("Jungle", StringComparison.OrdinalIgnoreCase)), "larger world should expose at least one jungle tile for seed 42");
    Assert(land.Any(t => TerrainResolver.Resolve(state, t).Name.Contains("Rainforest", StringComparison.OrdinalIgnoreCase)), "larger world should expose at least one rainforest tile for seed 42");
    Assert(land.Any(t => TerrainResolver.Resolve(state, t).Name.Contains("Taiga", StringComparison.OrdinalIgnoreCase)), "larger world should expose at least one taiga tile for seed 42");
    Assert(land.Any(t => TerrainResolver.Resolve(state, t).Name == "Ice Sheet"), "default world should expose at least one ice sheet tile for seed 42");
    Assert(state.Map.Tiles.Where(t => !t.Elevation.IsWaterLike() && t.Coord.R <= mapSize / 4).Any(t => TerrainResolver.Resolve(state, t).Name == "Ice Sheet"), "north part of the island should always contain a land ice sheet");
    Assert(state.Map.Tiles.Any(t => t.Elevation == Elevation.DeepIce), "generated polar ice should expand onto nearby ocean as deep ice");
    Assert(state.Map.Tiles.Where(t => t.Elevation == Elevation.DeepIce).All(t => !IsMapEdge(t)), "deep ice should not consume the outer ocean border");
    Assert(state.Map.Tiles.Where(t => TerrainResolver.Resolve(state, t).Name == "Ocean Ice Sheet").All(t => state.Map.Neighbors(t.Coord).Any(n => TerrainResolver.Resolve(state, n).Name is "Ice Sheet" or "Ocean Ice Sheet")), "ocean ice sheet should stay attached to coastal polar ice");
    Assert(land.All(t => !StartsWithTemperature(TerrainResolver.Resolve(state, t).Name)), "land terrain names should not include climate prefixes");
    Assert(land.All(t => TerrainResolver.Resolve(state, t).Name != "Thickets"), "thickets should not appear as a final terrain name");
    Assert(land.All(t => TerrainResolver.Resolve(state, t).Name != "Barrens"), "barrens should not appear as a final terrain name");
    Assert(land.All(t => TerrainResolver.Resolve(state, t).Name != "Bog"), "bog should not appear as a final terrain name");
    Assert(land.All(t => t.Vegetation == TerrainResolver.ClampTileVegetation(state.Regions[t.RegionId!.Value], t)), "every land tile should store vegetation allowed by its final biome inputs");
    Assert(land.Where(t => TerrainResolver.Resolve(state, t).Name == "Ice Sheet").All(t => t.Vegetation == Vegetation.None), "ice sheet tiles should not keep vegetation");
    Assert(DesertRegionsAreBroad(state), "default world desert tiles should concentrate into broad regions when deserts appear");
    Assert(state.Map.Tiles.Where(t => t.Elevation == Elevation.Coast && state.Map.IsCoastline(t)).Any(t => TerrainResolver.Resolve(state, t).Name == "Coast"), "coast elevation should produce coast terrain on outer water bodies");
    Assert(state.Map.Tiles.Where(t => t.Elevation == Elevation.Coast && !state.Map.IsOuterWaterBody(t)).Any(t => TerrainResolver.Resolve(state, t).Name == "Lake"), "inland coast elevation should resolve as lake");
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
    var forested = state.Map.Tiles.Where(t => t.Vegetation == Vegetation.Lush).ToList();

    Assert(inlandWater.Count >= 4, "map should contain small inland lake/coastline water");
    Assert(hills.Count > 0, "elevation generation should include hills");
    Assert(mountains.Count > 0, "elevation generation should include mountains");
    Assert(peaks.Count > 0, "elevation generation should include a few peaks");
    Assert(peaks.Count < mountains.Count, "peaks should be fewer than mountains");
    Assert(mountains.All(tile => state.Map.Tiles.Any(other => other.Elevation == Elevation.Hills && other.Coord.DistanceTo(tile.Coord) <= 2)), "every mountain should have hills close by");
    Assert(peaks.All(tile => state.Map.Neighbors(tile.Coord).Any(n => n.Elevation == Elevation.Mountains)), "every peak should neighbor a mountain");
    Assert(peaks.All(tile => !TerrainResolver.Resolve(state, tile).Name.Contains("Swamp", StringComparison.OrdinalIgnoreCase) && !TerrainResolver.Resolve(state, tile).Name.Contains("Jungle", StringComparison.OrdinalIgnoreCase)), "peaks should not resolve to swamp or jungle");
    Assert(hills.Where(IsRegionEdge).Count() >= hills.Count / 2, "most hills should be placed on region edges");
    Assert(rugged.Any(tile => state.Map.Neighbors(tile.Coord).Any(n => n.Elevation is Elevation.Mountains or Elevation.Peaks)), "rugged tiles should cluster");
    Assert(forested.Any(tile => state.Map.Neighbors(tile.Coord).Any(n => n.Vegetation == Vegetation.Lush)), "lush vegetation should form clusters");
    Assert(state.Map.Tiles.Where(t => t.FeatureIds.Contains("volcano")).All(t => t.Elevation is Elevation.Mountains or Elevation.Peaks), "volcanoes should only appear on mountains or peaks");
    Assert(state.Map.Tiles.Where(t => t.ResourceId == "game").All(t => TerrainResolver.Resolve(state, t).Passable && !TerrainResolver.Resolve(state, t).Name.Contains("Desert", StringComparison.OrdinalIgnoreCase) && !TerrainResolver.Resolve(state, t).Name.Contains("Ice", StringComparison.OrdinalIgnoreCase)), "game should not appear in deserts or ice terrain");

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
    var flatNeighbors = state.Map.Neighbors(stack.Coord)
        .Where(t => TerrainResolver.Resolve(state, t).Passable && GameRules.TileMovementCost(state, t) == 1.0)
        .ToList();
    Assert(flatNeighbors.Count > 0, "player starting tile should have at least one flat passable neighbor");
    Assert(flatNeighbors.All(t => range.ContainsKey(t.Coord)), "unit with 0.5 movement should reach flat neighbors via overspend");

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

string ResolveName(BaseBiome biome, TemperatureBand temperature, Vegetation vegetation)
{
    return TerrainResolver.ResolveRegionBiome(biome, temperature, vegetation);
}

bool StartsWithTemperature(string name)
{
    return Enum.GetNames<TemperatureBand>().Any(temperature => name.StartsWith(temperature + " ", StringComparison.OrdinalIgnoreCase));
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
