using StrategyGame.Core;

var root = FindProjectRoot();
var database = GameDatabase.LoadFromDirectory(Path.Combine(root, "data"));

Run("hex distance and neighbors", HexDistanceAndNeighbors);
Run("database loads required catalogs", DatabaseLoadsRequiredCatalogs);
Run("terrain resolver applies biome and vegetation tables", TerrainResolverAppliesBiomeAndVegetationTables);
Run("sandbox generation is deterministic", SandboxGenerationIsDeterministic);
Run("sandbox has ocean border and climate bands", SandboxHasOceanBorderAndClimateBands);
Run("sandbox has inland lakes and mountain chains", SandboxHasInlandLakesAndMountainChains);
Run("factions start on passable island tiles", FactionsStartOnPassableIslandTiles);
Run("movement rejects water and spends movement", MovementRejectsWaterAndSpendsMovement);
Run("agent joins and detaches from army", AgentJoinsAndDetachesFromArmy);
Run("city building upgrade replaces previous level", CityBuildingUpgradeReplacesPreviousLevel);
Run("combat removes losing stack", CombatRemovesLosingStack);
Run("director produces valid AI state", DirectorProducesValidAiState);
Run("save load preserves game state", SaveLoadPreservesGameState);
Run("loaded AI turn replays deterministically", LoadedAiTurnReplaysDeterministically);

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
    Assert(database.Buildings["shelter"].UpgradesTo == "camp", "building chain should start shelter -> camp");
    Assert(database.Factions.Values.Count(f => f.IsPlayer) == 1, "exactly one player faction expected");
    Assert(database.Events.ContainsKey("attack_enemy"), "attack event missing");
}

void TerrainResolverAppliesBiomeAndVegetationTables()
{
    Assert(ResolveName(Climate.Tropical, Rainfall.Low, Vegetation.None) == "Desert", "tropical low/no vegetation should be Desert");
    Assert(ResolveName(Climate.Tropical, Rainfall.Low, Vegetation.Sparse) == "Scrubland", "tropical low/sparse should be Scrubland");
    Assert(ResolveName(Climate.Tropical, Rainfall.High, Vegetation.Forest) == "Jungle", "tropical high/forest should be Jungle");
    Assert(ResolveName(Climate.Temperate, Rainfall.Medium, Vegetation.Forest) == "Forest", "temperate medium/forest should be Forest");
    Assert(ResolveName(Climate.Boreal, Rainfall.Medium, Vegetation.Forest) == "Taiga", "boreal medium/forest should be Taiga");
    Assert(ResolveName(Climate.Polar, Rainfall.High, Vegetation.None) == "Ice Sheet", "polar high/no vegetation should be Ice Sheet");
    Assert(TerrainResolver.NormalizeVegetation(Climate.Tropical, Rainfall.High, Vegetation.None) == Vegetation.Forest, "invalid rainforest/no vegetation should normalize to forest");
}

void SandboxGenerationIsDeterministic()
{
    var first = MapGenerator.CreateSandbox(database, 42);
    var second = MapGenerator.CreateSandbox(database, 42);
    Assert(first.Map.Count == second.Map.Count, "map tile count should match");

    foreach (var firstTile in first.Map.Tiles)
    {
        var secondTile = second.Map.Get(firstTile.Coord);
        Assert(firstTile.Climate == secondTile.Climate, $"climate mismatch at {firstTile.Coord}");
        Assert(firstTile.Rainfall == secondTile.Rainfall, $"rainfall mismatch at {firstTile.Coord}");
        Assert(firstTile.Elevation == secondTile.Elevation, $"elevation mismatch at {firstTile.Coord}");
        Assert(firstTile.Vegetation == secondTile.Vegetation, $"vegetation mismatch at {firstTile.Coord}");
        Assert(firstTile.FeatureIds.SequenceEqual(secondTile.FeatureIds), $"feature mismatch at {firstTile.Coord}");
        Assert(firstTile.ResourceId == secondTile.ResourceId, $"resource mismatch at {firstTile.Coord}");
    }
}

void SandboxHasOceanBorderAndClimateBands()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    Assert(state.Map.Count == MapGenerator.MapWidth * MapGenerator.MapHeight, "map should use the configured square-ish canvas size");

    foreach (var tile in state.Map.Tiles)
    {
        var row = tile.Coord.R;
        var col = ColumnOf(tile.Coord);
        var isEdge = col == 0 || row == 0 || col == MapGenerator.MapWidth - 1 || row == MapGenerator.MapHeight - 1;

        if (isEdge)
        {
            Assert(tile.Elevation == Elevation.Water, $"edge tile {tile.Coord} should be water");
            Assert(!TerrainResolver.Resolve(state, tile).Passable, $"edge tile {tile.Coord} should be impassable");
        }
    }

    var land = state.Map.Tiles.Where(t => TerrainResolver.Resolve(state, t).Passable).ToList();
    Assert(land.Count > 0, "island should contain passable land");
    Assert(land.Select(t => t.Climate).Distinct().Order().SequenceEqual([Climate.Tropical, Climate.Temperate, Climate.Boreal, Climate.Polar]), "island should contain all requested climates");
    Assert(land.Any(t => t.Rainfall == Rainfall.Low), "island should contain low rainfall");
    Assert(land.Any(t => t.Rainfall == Rainfall.Medium), "island should contain medium rainfall");
    Assert(land.Any(t => t.Rainfall == Rainfall.High), "island should contain high rainfall");
    Assert(land.Any(t => t.Elevation == Elevation.Hills), "island should contain hills");
    Assert(land.Any(t => t.Elevation == Elevation.Mountains), "island should contain mountains");
    Assert(land.Any(t => t.Elevation == Elevation.Peaks), "island should contain peaks");
    Assert(land.Where(t => t.Coord.R >= MapGenerator.MapHeight * 3 / 4).Any(t => t.Climate == Climate.Tropical), "southern land should contain tropical climate");
    Assert(land.Where(t => t.Coord.R <= MapGenerator.MapHeight / 4).Any(t => t.Climate == Climate.Polar), "northern land should contain polar climate");
    Assert(land.All(t => TerrainResolver.Resolve(state, t).Name.Length > 0), "every land tile should resolve to a named terrain");
    Assert(state.Map.Tiles.Where(t => t.Elevation == Elevation.Water && state.Map.IsCoastline(t)).Any(t => TerrainResolver.Resolve(state, t).Color == "#62b7e8"), "coastline water should use brighter blue");
}

void SandboxHasInlandLakesAndMountainChains()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var inlandWater = state.Map.Tiles
        .Where(t => t.Elevation == Elevation.Water && !IsMapEdge(t) && state.Map.IsCoastline(t))
        .ToList();
    var peaks = state.Map.Tiles.Where(t => t.Elevation == Elevation.Peaks).ToList();
    var rugged = state.Map.Tiles.Where(t => t.Elevation is Elevation.Mountains or Elevation.Peaks).ToList();
    var forested = state.Map.Tiles.Where(t => t.Vegetation == Vegetation.Forest).ToList();

    Assert(inlandWater.Count >= 4, "map should contain small inland lake/coastline water");
    Assert(peaks.Count >= 3, "mountain chains should include peaks");
    Assert(rugged.Any(tile => state.Map.Neighbors(tile.Coord).Any(n => n.Elevation is Elevation.Mountains or Elevation.Peaks)), "rugged tiles should cluster into chains");
    Assert(forested.Any(tile => state.Map.Neighbors(tile.Coord).Any(n => n.Vegetation == Vegetation.Forest)), "forest/taiga/jungle vegetation should form clusters");
    Assert(state.Map.Tiles.Where(t => t.FeatureIds.Contains("volcano")).All(t => t.Elevation is Elevation.Mountains or Elevation.Peaks), "volcanoes should only appear on mountains or peaks");
    Assert(state.Map.Tiles.Where(t => t.ResourceId == "game").All(t => TerrainResolver.Resolve(state, t).Passable && t.Climate != Climate.Polar && !TerrainResolver.Resolve(state, t).Name.Contains("Desert", StringComparison.OrdinalIgnoreCase)), "game should not appear in deserts or polar terrain");
}

void FactionsStartOnPassableIslandTiles()
{
    var state = MapGenerator.CreateSandbox(database, 42);

    foreach (var city in state.Cities.Values)
    {
        Assert(TerrainResolver.Resolve(state, state.Map.Get(city.Coord)).Passable, $"{city.Name} should start on passable land");
    }

    foreach (var stack in state.Stacks.Values)
    {
        Assert(TerrainResolver.Resolve(state, state.Map.Get(stack.Coord)).Passable, $"stack {stack.Id} should start on passable land");
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

void AgentJoinsAndDetachesFromArmy()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var stack = state.StacksForFaction(state.PlayerFaction.Id).First();
    var agent = state.AgentsForFaction(state.PlayerFaction.Id).First();

    var joined = GameRules.TryJoinAgentToStack(state, agent.Id, stack.Id);
    Assert(joined, "agent should join colocated friendly army");
    Assert(stack.LeaderAgentId == agent.Id, "stack should track leader");
    Assert(agent.JoinedStackId == stack.Id, "agent should track joined stack");
    Assert(!state.Map.Get(stack.Coord).AgentIds.Contains(agent.Id), "joined agent should leave tile agent list");

    var detached = GameRules.TryDetachLeader(state, stack.Id);
    Assert(detached, "leader should detach");
    Assert(stack.LeaderAgentId is null, "stack leader should clear");
    Assert(agent.JoinedStackId is null, "agent joined stack should clear");
    Assert(state.Map.Get(stack.Coord).AgentIds.Contains(agent.Id), "detached agent should return to tile agent list");
}

void CityBuildingUpgradeReplacesPreviousLevel()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var city = state.Cities.Values.First(c => c.FactionId == state.PlayerFaction.Id);

    Assert(city.BuildingIds.SequenceEqual(["shelter"]), "city should start with only shelter");

    var upgraded = GameRules.TryUpgradeCityBuilding(state, city.Id);

    Assert(upgraded, "city should upgrade from shelter to camp");
    Assert(city.BuildingIds.SequenceEqual(["camp"]), "camp should replace shelter instead of being added beside it");
    Assert(state.Log.Any(entry => entry.Text.Contains("upgraded to Camp", StringComparison.OrdinalIgnoreCase)), "upgrade should be logged");
}

void CombatRemovesLosingStack()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var attacker = state.StacksForFaction("player").First();
    var defender = state.StacksForFaction("ember").First();
    defender.Units.Clear();
    defender.Units.Add(new UnitInstance { TypeId = "militia", Count = 1 });

    CombatResolver.Resolve(state, attacker, defender);
    Assert(state.Stacks.ContainsKey(attacker.Id), "attacker should survive favorable combat");
    Assert(!state.Stacks.ContainsKey(defender.Id), "weak defender should be removed");
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
    Assert(loaded.Map.Get(stack.Coord).StackIds.Contains(stack.Id), "loaded map should retain stack tile index");
    Assert(loaded.Stacks[stack.Id].LeaderAgentId == agent.Id, "loaded stack should retain joined leader");
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
    return col == 0 || row == 0 || col == MapGenerator.MapWidth - 1 || row == MapGenerator.MapHeight - 1;
}

string ResolveName(Climate climate, Rainfall rainfall, Vegetation vegetation)
{
    var tile = new HexTile
    {
        Coord = new HexCoord(0, 0),
        Climate = climate,
        Rainfall = rainfall,
        Elevation = Elevation.Flat,
        Vegetation = vegetation
    };

    return TerrainResolver.Resolve(tile, coastline: false).Name;
}
