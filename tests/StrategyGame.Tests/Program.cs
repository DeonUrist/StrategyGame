using StrategyGame.Core;

var root = FindProjectRoot();
var database = GameDatabase.LoadFromDirectory(Path.Combine(root, "data"));

var tests = new (string Name, Action Test)[]
{
    ("hex distance and neighbors", HexDistanceAndNeighbors),
    ("database loads required catalogs", DatabaseLoadsRequiredCatalogs),
    ("locations start with population and support inventory", LocationsStartWithPopulationAndSupportInventory),
    ("group carry capacity uses unit strength", GroupCarryCapacityUsesUnitStrength),
    ("civilian relocate and settle uses population", CivilianRelocateAndSettleUsesPopulation),
    ("terrain resolver applies region biome tables", TerrainResolverAppliesRegionBiomeTables),
    ("terrain movement costs use terrain and elevation", TerrainMovementCostsUseTerrainAndElevation),
    ("terrain variant sliders control generated pairs", TerrainVariantSlidersControlGeneratedPairs),
    ("sandbox respects custom map size", SandboxRespectsCustomMapSize),
    ("civilizations slider controls faction count", CivilizationsSliderControlsFactionCount),
    ("allowed factions constrain civilization selection", AllowedFactionsConstrainCivilizationSelection),
    ("matching connected regions merge", MatchingConnectedRegionsMerge),
    ("sandbox generation is deterministic", SandboxGenerationIsDeterministic),
    ("sandbox has ocean border and region climate bands", SandboxHasOceanBorderAndRegionClimateBands),
    ("sandbox has inland lakes and elevation features", SandboxHasInlandLakesAndElevationFeatures),
    ("elevation variance controls rugged terrain", ElevationVarianceControlsRuggedTerrain),
    ("factions start on passable island tiles", FactionsStartOnPassableIslandTiles),
    ("movement rejects water and spends movement", MovementRejectsWaterAndSpendsMovement),
    ("group split and merge preserves indexes", GroupSplitAndMergePreservesIndexes),
    ("civilian split and transfer preserves categories", CivilianSplitAndTransferPreservesCategories),
    ("civilian and military transfer cannot mix", CivilianAndMilitaryTransferCannotMix),
    ("group station and deploy uses city garrison", GroupStationAndDeployUsesCityGarrison),
    ("partial deploy and station keeps one garrison", PartialDeployAndStationKeepsOneGarrison),
    ("group rename persists through save load", GroupRenamePersistsThroughSaveLoad),
    ("default group display uses generic names", DefaultGroupDisplayUsesGenericNames),
    ("city building upgrade replaces previous level", CityBuildingUpgradeReplacesPreviousLevel),
    ("combat removes losing group", CombatRemovesLosingGroup),
    ("director produces valid AI state", DirectorProducesValidAiState),
    ("stepwise director matches synchronous turn", StepwiseDirectorMatchesSynchronousTurn),
    ("save load preserves game state", SaveLoadPreservesGameState),
    ("fog visibility rules gate player vision", FogVisibilityRulesGatePlayerVision),
    ("loaded AI turn replays deterministically", LoadedAiTurnReplaysDeterministically),
    ("movement overspend allows last step", MovementOverspendAllowsLastStep),
    ("turn cycling advances faction and counts turns", TurnCyclingAdvancesFactionAndCountsTurns),
    ("combat applies casualties to winner", CombatAppliesCasualtiesToWinner),
    ("move group fails for invalid destination", MoveGroupFailsForInvalidDestination)
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
    Assert(!File.Exists(Path.Combine(root, "data", "units.json")), "units should not use a flat JSON catalog");
    Assert(database.Resources.ContainsKey("supplies"), "supplies inventory resource missing");
    Assert(database.Resources.ContainsKey("materials"), "materials inventory resource missing");
    Assert(database.Resources.ContainsKey("common_goods"), "common goods inventory resource missing");
    Assert(database.Resources.ContainsKey("luxury_goods"), "luxury goods inventory resource missing");
    Assert(database.Resources.ContainsKey("armaments"), "armaments inventory resource missing");
    Assert(database.Units.Count == 6, "six factions should each define embedded units");
    Assert(database.Units.Values.SelectMany(units => units.Values).All(unit => unit.Damage >= 0 && unit.Health > 0 && unit.Movement > 0 && unit.Strength > 0), "unit stats should load from JSON");
    Assert(database.Buildings[SettlementProgression.TownCenterId].Levels.Count == 6, "TownCenter should define six settlement levels");
    Assert(database.Buildings[SettlementProgression.TownCenterId].Levels[0].Name == "Campsite", "TownCenter should start at campsite");
    Assert(database.Buildings[SettlementProgression.TownCenterId].Levels[2].Sprite == "homestead", "TownCenter homestead level should use homestead sprite");
    Assert(database.Factions.Count == 6, "six faction type definitions expected");
    Assert(database.Factions.Keys.Order().SequenceEqual(["dwarves", "elves", "humans", "orcs", "ratmen", "undead"]), "factions should use simple race ids");
    Assert(database.Factions.Values.All(f => f.RaceId == f.Id), "current faction definitions should use race id matching faction id");
    Assert(database.Factions.Values.All(f => f.Name == f.Type), "factions should use simple visible names");
    Assert(database.Factions.Values.All(f => f.CityNames.Count > 0), "each faction should provide editable city names");
    Assert(database.Factions.Values.All(f => f.StartingArmy.Count > 0), "each faction should define a starting army");
    foreach (var faction in database.Factions.Values)
    {
        Assert(!Directory.Exists(Path.Combine(root, "data", faction.Id)), $"{faction.Id} unit folder should not be used");
        foreach (var unitId in new[] { "militia", "soldier", "ranged", "civilian" })
        {
            Assert(database.Units[faction.Id].Values.Any(unit => unit.Id == unitId), $"{faction.Id} {unitId} unit missing");
        }
        Assert(database.Units[faction.Id].Values.Select(unit => unit.Id).Order().SequenceEqual(["civilian", "militia", "ranged", "soldier"]), $"{faction.Id} unit ids mismatch");
        Assert(faction.StartingPopulation == (faction.Id == "ratmen" ? 15 : 10), $"{faction.Id} starting population mismatch");
        Assert(File.Exists(Path.Combine(root, "assets", "image", "locations", $"campsite_{faction.RaceId}.png")), $"{faction.Id} race-specific campsite sprite placeholder missing");
    }
    foreach (var size in new[] { "one", "few", "medium", "many" })
    {
        Assert(File.Exists(Path.Combine(root, "assets", "image", "units", $"squad_{size}.png")), $"squad {size} sprite missing");
        Assert(File.Exists(Path.Combine(root, "assets", "image", "units", $"civilians_{size}.png")), $"civilian {size} sprite missing");
    }
    Assert(database.Events.ContainsKey("attack_enemy"), "attack event missing");
}

void LocationsStartWithPopulationAndSupportInventory()
{
    var state = MapGenerator.CreateSandbox(database, 42, new WorldGenerationSettings { Civilizations = 6 });

    foreach (var location in state.Cities.Values)
    {
        Assert(location.Kind == LocationKind.Settlement, "worldgen should only place settlement locations for now");
        var expectedPopulation = location.FactionId == "ratmen" ? 15 : 10;
        Assert(location.Population == expectedPopulation, $"{location.FactionId} starting population mismatch");
        Assert(GameRules.TryAddLocationInventory(location, ResourceCategory.Materials, 10_000), "location inventory should accept large quantities");
    }

    Assert(File.Exists(Path.Combine(root, "assets", "image", "locations", "farm.png")), "farm default location sprite missing");
    Assert(File.Exists(Path.Combine(root, "assets", "image", "locations", "camp.png")), "camp default location sprite missing");
    Assert(File.Exists(Path.Combine(root, "assets", "image", "locations", "mine.png")), "mine default location sprite missing");

    foreach (var kind in new[] { LocationKind.Farm, LocationKind.Camp, LocationKind.Mine })
    {
        var coord = state.Map.Tiles
            .Where(tile => tile.LocationId is null && TerrainResolver.Resolve(state, tile).Passable)
            .OrderBy(tile => tile.Coord.Q)
            .ThenBy(tile => tile.Coord.R)
            .First()
            .Coord;
        var id = GameRules.TryCreateLocation(state, kind, state.PlayerFaction.Id, coord, $"Test {kind}")
            ?? throw new InvalidOperationException($"{kind} location creation failed");
        Assert(state.Cities[id].Kind == kind, $"{kind} should be created as requested");
        Assert(state.Map.Get(coord).LocationId == id, $"{kind} tile should reference created location");
    }
}

void GroupCarryCapacityUsesUnitStrength()
{
    var state = MapGenerator.CreateSandbox(database, 42, new WorldGenerationSettings
    {
        Civilizations = 1,
        AllowedFactionIds = ["orcs"]
    });
    var group = GarrisonGroup(state, "orcs");
    group.Units.Clear();
    group.Units.Add(new UnitInstance { Id = 100, TypeId = "soldier" });
    group.Units.Add(new UnitInstance { Id = 101, TypeId = "militia" });

    Assert(Math.Abs(GameRules.GroupStrength(state, group) - 2.4) < 0.0001, "orc group strength should sum unit strength");
    Assert(Math.Abs(GameRules.GroupCarryCapacity(state, group) - 12.0) < 0.0001, "carry capacity should be strength times five");
    Assert(GameRules.TryAddGroupInventory(state, group, ResourceCategory.Supplies, 12.0), "group should accept inventory at capacity");
    Assert(!GameRules.TryAddGroupInventory(state, group, ResourceCategory.Materials, 0.1), "group should reject inventory over capacity");
}

void CivilianRelocateAndSettleUsesPopulation()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var location = state.Cities.Values.Single(city => city.FactionId == state.PlayerFaction.Id);
    var originalPopulation = location.Population;

    var groupId = GameRules.TryRelocateCivilians(state, location.Id, 3)
        ?? throw new InvalidOperationException("civilian relocation did not return a group");
    var group = state.Groups[groupId];

    Assert(location.Population == originalPopulation - 3, "relocation should remove civilians from location population");
    Assert(group.Units.Count == 3, "relocation should create one unit per civilian");
    Assert(group.Units.All(unit => GameRules.IsCivilianUnit(state, group, unit)), "relocation should create civilian units only");
    Assert(GameRules.GroupDisplayName(state, group) == "Civilians", "civilian-only groups should use the generic display name");
    Assert(state.Map.Get(location.Coord).GroupIds.Contains(group.Id), "relocated civilian group should be indexed on the map");
    Assert(GameRules.TrySettleCivilians(state, group.Id), "civilian group should settle at an established friendly location");
    Assert(location.Population == originalPopulation, "settling should return civilians to location population");
    Assert(!state.Groups.ContainsKey(group.Id), "settling every civilian should remove the group");
    Assert(!state.Map.Get(location.Coord).GroupIds.Contains(group.Id), "settled group should leave tile index");
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
    Assert(Pick(MoistureLevel.Normal, TemperatureBand.Tropical) == BaseBiome.Grassland, "tropical normal should pick Grassland");
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

void CivilizationsSliderControlsFactionCount()
{
    var solo = MapGenerator.CreateSandbox(database, 42, new WorldGenerationSettings { Civilizations = 1 });
    var full = MapGenerator.CreateSandbox(database, 42, new WorldGenerationSettings { Civilizations = 6 });

    Assert(solo.Factions.Count == 1, "one civilization should create only the player faction");
    Assert(solo.Factions.Count(f => f.IsPlayer) == 1, "solo game should still have one player faction");
    Assert(solo.Cities.Count == 1, "one civilization should create one starting settlement");
    Assert(full.Factions.Count == 6, "six civilizations should use every faction type");
    Assert(full.Factions.Select(f => f.Id).Distinct().Count() == 6, "generated civilizations should not repeat faction types");
    Assert(full.Factions.Count(f => f.IsPlayer) == 1, "generated world should have exactly one player faction");
    Assert(full.Cities.Count == 6, "each civilization should start with one settlement");
}

void AllowedFactionsConstrainCivilizationSelection()
{
    var state = MapGenerator.CreateSandbox(database, 42, new WorldGenerationSettings
    {
        Civilizations = 6,
        AllowedFactionIds = ["humans", "elves"]
    });

    Assert(state.Factions.Count == 2, "allowed faction list should cap generated civilizations");
    Assert(state.Factions.Select(f => f.Id).Order().SequenceEqual(["elves", "humans"]), "generated factions should come from allowed list only");
    Assert(!state.Factions.Any(f => f.Id == "undead"), "disabled undead should not be generated when omitted");
}

void MatchingConnectedRegionsMerge()
{
    var state = new GameState
    {
        Database = database,
        Map = new HexMap()
    };

    AddRegion(state, 1, MoistureLevel.Normal, TemperatureBand.Subarctic, BaseBiome.Tundra, "Tundra");
    AddRegion(state, 2, MoistureLevel.Dry, TemperatureBand.Subarctic, BaseBiome.Tundra, "Tundra");
    AddRegion(state, 3, MoistureLevel.Wet, TemperatureBand.Temperate, BaseBiome.Swamp, "Swamp");
    AddRegion(state, 4, MoistureLevel.Normal, TemperatureBand.Subarctic, BaseBiome.Tundra, "Tundra");
    AddRegion(state, 5, MoistureLevel.Dry, TemperatureBand.Temperate, BaseBiome.Tundra, "Tundra");

    AddTile(state, new HexCoord(0, 0), 1);
    AddTile(state, new HexCoord(1, 0), 2);
    AddTile(state, new HexCoord(2, 0), 3);
    AddTile(state, new HexCoord(6, 0), 4);
    AddTile(state, new HexCoord(0, 1), 5);

    MapGenerator.MergeMatchingConnectedRegions(state);

    Assert(state.Regions.ContainsKey(1), "lowest connected matching region id should survive");
    Assert(!state.Regions.ContainsKey(2), "connected same-biome region should merge even when moisture differs");
    Assert(state.Regions.ContainsKey(3), "different-biome neighbor should remain separate");
    Assert(state.Regions.ContainsKey(4), "disconnected matching region should remain separate");
    Assert(state.Regions.ContainsKey(5), "same-biome neighbor with different temperature should remain separate");
    Assert(state.Map.Get(new HexCoord(0, 0)).RegionId == 1, "survivor tile should keep survivor region id");
    Assert(state.Map.Get(new HexCoord(1, 0)).RegionId == 1, "merged tile should update to survivor region id");
    Assert(state.Map.Get(new HexCoord(1, 0)).Moisture == state.Regions[1].Moisture, "merged tile moisture should normalize to survivor region moisture");
    Assert(state.Map.Get(new HexCoord(2, 0)).RegionId == 3, "different-biome tile should keep region id");
    Assert(state.Map.Get(new HexCoord(6, 0)).RegionId == 4, "disconnected matching tile should keep region id");
    Assert(state.Map.Get(new HexCoord(0, 1)).RegionId == 5, "different-temperature tile should keep region id");
    Assert(state.Regions[1].TileCoords.SequenceEqual([new HexCoord(0, 0), new HexCoord(1, 0)]), "survivor tile list should be rebuilt in map order");
    Assert(state.Regions[3].TileCoords.SequenceEqual([new HexCoord(2, 0)]), "unmerged tile list should be rebuilt");
    Assert(state.Regions[4].TileCoords.SequenceEqual([new HexCoord(6, 0)]), "disconnected matching tile list should be rebuilt");
    Assert(state.Regions[5].TileCoords.SequenceEqual([new HexCoord(0, 1)]), "different-temperature tile list should be rebuilt");
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
    Assert(state.Map.Tiles.Where(t => t.FeatureIds.Contains("volcano")).All(t => t.Elevation == Elevation.Mountains), "volcanoes should only appear on mountains");
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
    Assert(state.GroupsForFaction(state.PlayerFaction.Id).Count() == 1, "player should start with one garrison group");
    foreach (var faction in state.Factions)
    {
        var stack = GarrisonGroup(state, faction.Id);
        var expectedCount = database.Factions[faction.Id].StartingArmy.Values.Sum();
        Assert(stack.StationedCityId is not null, $"{faction.Id} starting group should be stationed");
        Assert(stack.Units.Count == expectedCount, $"{faction.Id} starting garrison should follow factions.json exactly");
        Assert(stack.Units.All(unit => database.Factions[faction.Id].StartingArmy.ContainsKey(unit.TypeId)), $"{faction.Id} group should use local starting unit ids");
    }

    foreach (var city in state.Cities.Values)
    {
        Assert(TerrainResolver.Resolve(state, state.Map.Get(city.Coord)).Passable, $"{city.Name} should start on passable land");
    }

    foreach (var group in state.Groups.Values)
    {
        Assert(TerrainResolver.Resolve(state, state.Map.Get(group.Coord)).Passable, $"group {group.Id} should start on passable land");
        Assert(!state.Map.Get(group.Coord).GroupIds.Contains(group.Id), $"group {group.Id} should start in garrison");
    }
}

void MovementRejectsWaterAndSpendsMovement()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var group = DeployGarrison(state, state.PlayerFaction.Id);
    var range = GameRules.MovementRange(state, group.Coord, group.MovementLeft);
    Assert(range.Count > 1, "player group should have reachable tiles");
    Assert(range.Keys.All(c => TerrainResolver.Resolve(state, state.Map.Get(c)).Passable), "movement range should exclude impassable water");

    var destination = range.Where(kv => kv.Value > 0).OrderBy(kv => kv.Value).First();
    var moved = GameRules.TryMoveGroup(state, group.Id, destination.Key);
    Assert(moved, "group should move to reachable destination");
    Assert(group.Coord == destination.Key, "group coord should update");
    Assert(group.MovementLeft == 2 - destination.Value, "group movement should be reduced by movement cost");
}

void GroupSplitAndMergePreservesIndexes()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var group = DeployGarrison(state, state.PlayerFaction.Id);
    var origin = group.Coord;
    var splitUnit = group.Units[0];

    var splitId = GameRules.TrySplitGroup(state, group.Id, [splitUnit.Id])
        ?? throw new InvalidOperationException("split did not return a group id");

    Assert(state.Groups.ContainsKey(splitId), "split should create a new group");
    Assert(state.Groups[splitId].Units.Single().Id == splitUnit.Id, "split should preserve unit identity");
    Assert(state.Map.Get(origin).GroupIds.Contains(group.Id), "source group should remain indexed on tile");
    Assert(state.Map.Get(origin).GroupIds.Contains(splitId), "split group should be indexed on tile");

    var merged = GameRules.TryMergeGroups(state, splitId, group.Id);
    Assert(merged, "same-tile friendly groups should merge");
    Assert(!state.Groups.ContainsKey(splitId), "merged source group should be removed");
    Assert(group.Units.Any(unit => unit.Id == splitUnit.Id), "merged target should regain split unit");
    Assert(!state.Map.Get(origin).GroupIds.Contains(splitId), "merged source group should leave tile index");
}

void CivilianSplitAndTransferPreservesCategories()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var location = state.Cities.Values.Single(city => city.FactionId == state.PlayerFaction.Id);
    var civilianGroupId = GameRules.TryRelocateCivilians(state, location.Id, 3)
        ?? throw new InvalidOperationException("civilian relocation did not return a group");
    var civilianGroup = state.Groups[civilianGroupId];
    var splitUnitId = civilianGroup.Units[0].Id;

    var splitId = GameRules.TrySplitGroup(state, civilianGroup.Id, [splitUnitId])
        ?? throw new InvalidOperationException("civilian split did not return a group");
    var splitGroup = state.Groups[splitId];

    Assert(GameRules.IsCivilianOnlyGroup(state, civilianGroup), "source should remain civilian-only after split");
    Assert(GameRules.IsCivilianOnlyGroup(state, splitGroup), "created split group should be civilian-only");
    Assert(state.Map.Get(location.Coord).GroupIds.Contains(civilianGroup.Id), "source civilian group should remain indexed");
    Assert(state.Map.Get(location.Coord).GroupIds.Contains(splitGroup.Id), "split civilian group should be indexed");

    var transferred = GameRules.TryTransferUnits(state, splitGroup.Id, civilianGroup.Id, [splitUnitId]);

    Assert(transferred, "civilian units should transfer between civilian groups");
    Assert(!state.Groups.ContainsKey(splitGroup.Id), "empty civilian source group should be removed after transfer");
    Assert(civilianGroup.Units.Count == 3, "target civilian group should regain transferred unit");
    Assert(GameRules.IsCivilianOnlyGroup(state, civilianGroup), "target should remain civilian-only after transfer");
}

void CivilianAndMilitaryTransferCannotMix()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var location = state.Cities.Values.Single(city => city.FactionId == state.PlayerFaction.Id);
    var military = DeployGarrison(state, state.PlayerFaction.Id);
    var civilianGroupId = GameRules.TryRelocateCivilians(state, location.Id, 2)
        ?? throw new InvalidOperationException("civilian relocation did not return a group");
    var civilians = state.Groups[civilianGroupId];
    var civilianUnitId = civilians.Units[0].Id;
    var militaryUnitId = military.Units[0].Id;

    Assert(!GameRules.TryTransferUnits(state, civilians.Id, military.Id, [civilianUnitId]), "civilian units should not transfer into military groups");
    Assert(!GameRules.TryTransferUnits(state, military.Id, civilians.Id, [militaryUnitId]), "military units should not transfer into civilian groups");
    Assert(civilians.Units.Count == 2, "failed civilian transfer should not remove units");
    Assert(military.Units.Any(unit => unit.Id == militaryUnitId), "failed military transfer should not remove units");
}

void GroupStationAndDeployUsesCityGarrison()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var group = DeployGarrison(state, state.PlayerFaction.Id);
    var city = state.Cities.Values.Single(city => city.FactionId == state.PlayerFaction.Id);

    var stationed = GameRules.TryStationGroup(state, group.Id, city.Id);
    Assert(stationed, "group should station at friendly colocated city");
    Assert(group.StationedCityId == city.Id, "stationed group should reference city");
    Assert(city.StationedGroupIds.Contains(group.Id), "city should reference stationed group");
    Assert(!state.Map.Get(city.Coord).GroupIds.Contains(group.Id), "stationed group should leave tile index");
    Assert(!GameRules.TryMoveGroup(state, group.Id, state.Map.Neighbors(city.Coord).First().Coord), "stationed group should not move");

    var deployed = GameRules.TryDeployGroup(state, group.Id);
    Assert(deployed, "stationed group should deploy from city");
    Assert(group.StationedCityId is null, "deployed group should clear stationed city");
    Assert(!city.StationedGroupIds.Contains(group.Id), "deployed group should leave city garrison");
    Assert(state.Map.Get(city.Coord).GroupIds.Contains(group.Id), "deployed group should return to tile index");
}

void PartialDeployAndStationKeepsOneGarrison()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var city = state.Cities.Values.Single(city => city.FactionId == state.PlayerFaction.Id);
    var garrison = GarrisonGroup(state, state.PlayerFaction.Id);
    var originalCount = garrison.Units.Count;
    var unitId = garrison.Units.First().Id;

    var deployedId = GameRules.TryDeployUnits(state, city.Id, [unitId])
        ?? throw new InvalidOperationException("partial deploy did not return a group id");
    var deployed = state.Groups[deployedId];

    Assert(deployed.StationedCityId is null, "partial deploy should create a map group");
    Assert(state.Map.Get(city.Coord).GroupIds.Contains(deployed.Id), "partial deploy should index deployed group on city tile");
    Assert(city.StationedGroupIds.Count == 1, "city should keep one garrison group after partial deploy");
    Assert(garrison.Units.Count == originalCount - 1, "garrison should lose deployed unit");

    var stationed = GameRules.TryStationUnits(state, deployed.Id, city.Id, [unitId]);
    Assert(stationed, "partial station should return unit to garrison");
    Assert(!state.Groups.ContainsKey(deployed.Id), "empty deployed group should be removed after stationing all units");
    Assert(city.StationedGroupIds.Count == 1, "city should still have one garrison group after stationing");
    Assert(garrison.Units.Count == originalCount, "garrison should regain stationed unit");
    Assert(garrison.Units.Any(unit => unit.Id == unitId), "garrison should contain the same unit identity");
}

void GroupRenamePersistsThroughSaveLoad()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var group = DeployGarrison(state, state.PlayerFaction.Id);

    var renamed = GameRules.TryRenameGroup(state, group.Id, "First Patrol");
    Assert(renamed, "group rename should succeed");

    var loaded = GameStateSerializer.FromJson(database, GameStateSerializer.ToJson(state));
    Assert(loaded.Groups[group.Id].Name == "First Patrol", "renamed group should persist through save/load");
}

void DefaultGroupDisplayUsesGenericNames()
{
    var state = MapGenerator.CreateSandbox(database, 42, new WorldGenerationSettings
    {
        Civilizations = 1,
        AllowedFactionIds = ["elves"]
    });
    var group = DeployGarrison(state, "elves");

    Assert(GameRules.GroupDisplayName(state, group) == "Squad", "unnamed military groups should use the generic squad name");
}

void CityBuildingUpgradeReplacesPreviousLevel()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var city = state.Cities.Values.First(c => c.FactionId == state.PlayerFaction.Id);

    Assert(city.TownCenterLevel == 0, "city should start as a campsite");
    Assert(SettlementProgression.DisplayName(state, city) == "Campsite", "campsite should use generic display name");

    var upgraded = GameRules.TryUpgradeCityBuilding(state, city.Id);

    Assert(upgraded, "city should upgrade from campsite to encampment");
    Assert(city.TownCenterLevel == 1, "TownCenter level should advance to encampment");
    Assert(SettlementProgression.DisplayName(state, city) == "Encampment", "encampment should use generic display name");

    upgraded = GameRules.TryUpgradeCityBuilding(state, city.Id);

    Assert(upgraded, "city should upgrade from encampment to homestead");
    Assert(city.TownCenterLevel == 2, "TownCenter level should advance to homestead");
    Assert(SettlementProgression.DisplayName(state, city) == city.Name, "homestead and higher should display the generated city name");
    Assert(state.Log.Any(entry => entry.Text.Contains("upgraded to Homestead", StringComparison.OrdinalIgnoreCase)), "upgrade should be logged");
}

void CombatRemovesLosingGroup()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var attacker = DeployGarrison(state, state.PlayerFaction.Id);
    var enemyFactionId = state.Factions.First(f => !f.IsPlayer).Id;
    var defender = DeployGarrison(state, enemyFactionId);
    defender.Units.Clear();
    defender.Units.Add(new UnitInstance { Id = 10000, TypeId = "militia" });

    CombatResolver.Resolve(state, attacker, defender);
    Assert(state.Groups.ContainsKey(attacker.Id), "attacker should survive favorable combat");
    Assert(!state.Groups.ContainsKey(defender.Id), "weak defender should be removed");
}

void DirectorProducesValidAiState()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var director = new FactionDirector();
    GameRules.AdvanceTurn(state);
    director.TakeTurn(state, state.CurrentFaction.Id);

    foreach (var group in state.Groups.Values.Where(group => group.StationedCityId is null))
    {
        Assert(state.Map.TryGet(group.Coord, out var tile), "group coord should remain on map");
        Assert(tile.GroupIds.Contains(group.Id), "tile should reference resident group");
    }

    Assert(state.Log.Any(entry => entry.Text.Contains("director chose", StringComparison.OrdinalIgnoreCase)), "director should log weighted action");
}

void StepwiseDirectorMatchesSynchronousTurn()
{
    var synchronous = MapGenerator.CreateSandbox(database, 42);
    var stepwise = GameStateSerializer.FromJson(database, GameStateSerializer.ToJson(synchronous));
    GameRules.AdvanceTurn(synchronous);
    GameRules.AdvanceTurn(stepwise);

    var syncDirector = new FactionDirector();
    var stepDirector = new FactionDirector();
    syncDirector.TakeTurn(synchronous, synchronous.CurrentFaction.Id);
    foreach (var _ in stepDirector.TakeTurnSteps(stepwise, stepwise.CurrentFaction.Id))
    {
    }

    Assert(GameStateSerializer.ToJson(stepwise) == GameStateSerializer.ToJson(synchronous), "stepwise AI turn should match synchronous AI turn");
}

void SaveLoadPreservesGameState()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    state.FogOfWarEnabled = true;
    var stack = GarrisonGroup(state, state.PlayerFaction.Id);
    var playerLocation = state.Cities[stack.StationedCityId!.Value];
    playerLocation.Inventory[ResourceCategory.Supplies] = 25;
    stack.Inventory[ResourceCategory.Armaments] = 3;
    GameRules.AdvanceTurn(state);

    var json = GameStateSerializer.ToJson(state);
    var loaded = GameStateSerializer.FromJson(database, json);

    Assert(GameStateSerializer.ToJson(loaded) == json, "save/load round trip should preserve serialized state");
    Assert(loaded.WorldGeneration.ElevationVariance == state.WorldGeneration.ElevationVariance, "loaded state should retain world generation settings");
    Assert(loaded.FogOfWarEnabled == state.FogOfWarEnabled, "loaded state should retain fog of war setting");
    Assert(loaded.WorldGeneration.Civilizations == state.WorldGeneration.Civilizations, "loaded state should retain civilization count");
    Assert(loaded.WorldGeneration.GrasslandShrublandBias == state.WorldGeneration.GrasslandShrublandBias, "loaded state should retain grassland/shrubland bias");
    Assert(loaded.WorldGeneration.DesertBadlandsBias == state.WorldGeneration.DesertBadlandsBias, "loaded state should retain desert/badlands bias");
    Assert(loaded.WorldGeneration.ConiferBroadleafForestBias == state.WorldGeneration.ConiferBroadleafForestBias, "loaded state should retain conifer/broadleaf bias");
    Assert(loaded.Map.Tiles.Where(t => t.Elevation == Elevation.Coast).All(t => t.WaterBodyKind != WaterBodyKind.None), "loaded coast tiles should retain water-body classification");
    Assert(loaded.Cities[stack.StationedCityId!.Value].StationedGroupIds.Contains(stack.Id), "loaded city should retain garrison group index");
    Assert(loaded.Cities[playerLocation.Id].Population == playerLocation.Population, "loaded location should retain population");
    Assert(loaded.Cities[playerLocation.Id].Inventory[ResourceCategory.Supplies] == 25, "loaded location should retain inventory");
    Assert(loaded.Groups[stack.Id].Inventory[ResourceCategory.Armaments] == 3, "loaded group should retain inventory");
    Assert(!loaded.Map.Get(stack.Coord).GroupIds.Contains(stack.Id), "loaded garrison should not appear on map tile index");
    Assert(loaded.Groups[stack.Id].Units.Count == stack.Units.Count, "loaded garrison should retain unit count");
}

void FogVisibilityRulesGatePlayerVision()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var playerCity = state.Cities.Values.First(c => c.FactionId == state.PlayerFaction.Id);
    var farTile = state.Map.Tiles
        .Where(t => TerrainResolver.Resolve(state, t).Passable)
        .OrderByDescending(t => t.Coord.DistanceTo(playerCity.Coord))
        .First();

    Assert(VisibilityRules.IsVisibleToPlayer(state, farTile.Coord), "fog off should make every tile visible");
    state.FogOfWarEnabled = true;
    Assert(VisibilityRules.IsVisibleToPlayer(state, playerCity.Coord), "player city tile should be visible with fog on");
    Assert(!VisibilityRules.IsVisibleToPlayer(state, farTile.Coord), "far tile should be hidden with fog on");
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
    var stack = DeployGarrison(state, state.PlayerFaction.Id);

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
    var attacker = DeployGarrison(state, state.PlayerFaction.Id);
    var enemyFactionId = state.Factions.First(f => !f.IsPlayer).Id;
    var defender = DeployGarrison(state, enemyFactionId);

    attacker.Units.Clear();
    for (var i = 0; i < 20; i++)
    {
        attacker.Units.Add(new UnitInstance { Id = 20000 + i, TypeId = "soldier" });
    }
    defender.Units.Clear();
    defender.Units.Add(new UnitInstance { Id = 30000, TypeId = "militia" });

    var countBefore = attacker.Units.Count;
    CombatResolver.Resolve(state, attacker, defender);

    Assert(state.Groups.ContainsKey(attacker.Id), "dominant attacker should survive");
    Assert(!state.Groups.ContainsKey(defender.Id), "weak defender should be removed");
    var expected = Math.Max(1, (int)Math.Round(countBefore * 0.75));
    Assert(attacker.Units.Count == expected, $"winner should lose 25% of its units: {countBefore} -> {expected}");
}

void MoveGroupFailsForInvalidDestination()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var stack = DeployGarrison(state, state.PlayerFaction.Id);
    var originalCoord = stack.Coord;

    // Ocean is impassable — move should be rejected.
    var oceanCoord = state.Map.Tiles.First(t => t.Elevation == Elevation.Ocean).Coord;
    Assert(!GameRules.TryMoveGroup(state, stack.Id, oceanCoord), "move to ocean tile should fail");
    Assert(stack.Coord == originalCoord, "failed move should not change group position");
    Assert(state.Map.Get(originalCoord).GroupIds.Contains(stack.Id), "tile index should be unchanged after failed ocean move");

    // Destination too far to reach in one turn should also fail.
    var farCoord = state.Map.Tiles
        .Where(t => TerrainResolver.Resolve(state, t).Passable)
        .OrderByDescending(t => t.Coord.DistanceTo(originalCoord))
        .First().Coord;
    Assert(!GameRules.TryMoveGroup(state, stack.Id, farCoord), "move beyond movement range should fail");
    Assert(stack.Coord == originalCoord, "failed out-of-range move should not change group position");
}

GroupState GarrisonGroup(GameState state, string factionId)
{
    return state.GroupsForFaction(factionId).Single(group => group.StationedCityId is not null);
}

GroupState DeployGarrison(GameState state, string factionId)
{
    var group = GarrisonGroup(state, factionId);
    var deployed = GameRules.TryDeployGroup(state, group.Id);
    Assert(deployed, $"{factionId} garrison should deploy");
    return group;
}

void AddRegion(GameState state, int id, MoistureLevel moisture, TemperatureBand temperature, BaseBiome baseBiome, string finalBiomeName)
{
    state.Regions[id] = new RegionState
    {
        Id = id,
        Name = $"Region {id}",
        Moisture = moisture,
        Temperature = temperature,
        BaseBiome = baseBiome,
        FinalBiomeName = finalBiomeName
    };
}

void AddTile(GameState state, HexCoord coord, int regionId)
{
    state.Map.Add(new HexTile
    {
        Coord = coord,
        Elevation = Elevation.Flat,
        Moisture = state.Regions[regionId].Moisture,
        RegionId = regionId
    });
    state.Regions[regionId].TileCoords.Add(coord);
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
