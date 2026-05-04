using StrategyGame.Core;

var root = FindProjectRoot();
var database = GameDatabase.LoadFromDirectory(Path.Combine(root, "data"));

Run("hex distance and neighbors", HexDistanceAndNeighbors);
Run("database loads required catalogs", DatabaseLoadsRequiredCatalogs);
Run("sandbox generation is deterministic", SandboxGenerationIsDeterministic);
Run("movement rejects water and spends movement", MovementRejectsWaterAndSpendsMovement);
Run("agent joins and detaches from army", AgentJoinsAndDetachesFromArmy);
Run("combat removes losing stack", CombatRemovesLosingStack);
Run("director produces valid AI state", DirectorProducesValidAiState);

Console.WriteLine("All strategy foundation tests passed.");

void HexDistanceAndNeighbors()
{
    var origin = new HexCoord(0, 0);
    Assert(origin.Neighbors().Count() == 6, "origin should have six axial neighbors");
    Assert(origin.DistanceTo(new HexCoord(2, -1)) == 2, "distance should use cube-equivalent axial math");
}

void DatabaseLoadsRequiredCatalogs()
{
    Assert(database.Terrains.ContainsKey("water"), "water terrain missing");
    Assert(database.Features.ContainsKey("forest"), "forest feature missing");
    Assert(database.Resources.ContainsKey("gold"), "gold resource missing");
    Assert(database.Units.ContainsKey("captain"), "captain unit missing");
    Assert(database.Buildings["shelter"].UpgradesTo == "camp", "building chain should start shelter -> camp");
    Assert(database.Factions.Values.Count(f => f.IsPlayer) == 1, "exactly one player faction expected");
    Assert(database.Events.ContainsKey("attack_enemy"), "attack event missing");
}

void SandboxGenerationIsDeterministic()
{
    var first = MapGenerator.CreateSandbox(database, 42);
    var second = MapGenerator.CreateSandbox(database, 42);
    Assert(first.Map.Count == second.Map.Count, "map tile count should match");

    foreach (var firstTile in first.Map.Tiles)
    {
        var secondTile = second.Map.Get(firstTile.Coord);
        Assert(firstTile.TerrainId == secondTile.TerrainId, $"terrain mismatch at {firstTile.Coord}");
        Assert(firstTile.FeatureId == secondTile.FeatureId, $"feature mismatch at {firstTile.Coord}");
        Assert(firstTile.ResourceId == secondTile.ResourceId, $"resource mismatch at {firstTile.Coord}");
    }
}

void MovementRejectsWaterAndSpendsMovement()
{
    var state = MapGenerator.CreateSandbox(database, 42);
    var stack = state.StacksForFaction(state.PlayerFaction.Id).First();
    var range = GameRules.MovementRange(state, stack.Coord, stack.MovementLeft);
    Assert(range.Count > 1, "player stack should have reachable tiles");
    Assert(range.Keys.All(c => database.Terrains[state.Map.Get(c).TerrainId].Passable), "movement range should exclude impassable water");

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
