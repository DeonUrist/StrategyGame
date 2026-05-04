namespace StrategyGame.Core;

public static class GameRules
{
    public static int TileMovementCost(GameState state, HexTile tile)
    {
        var terrain = state.Database.Terrains[tile.TerrainId];
        if (!terrain.Passable)
        {
            return int.MaxValue;
        }

        var featureCost = tile.FeatureId is null ? 0 : state.Database.Features[tile.FeatureId].MovementCostModifier;
        return Math.Max(1, terrain.MovementCost + featureCost);
    }

    public static Dictionary<HexCoord, int> MovementRange(GameState state, HexCoord origin, int movement)
    {
        var frontier = new Queue<HexCoord>();
        var costs = new Dictionary<HexCoord, int> { [origin] = 0 };
        frontier.Enqueue(origin);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var neighbor in state.Map.Neighbors(current))
            {
                var step = TileMovementCost(state, neighbor);
                if (step == int.MaxValue)
                {
                    continue;
                }

                var nextCost = costs[current] + step;
                if (nextCost > movement || costs.TryGetValue(neighbor.Coord, out var existing) && existing <= nextCost)
                {
                    continue;
                }

                costs[neighbor.Coord] = nextCost;
                frontier.Enqueue(neighbor.Coord);
            }
        }

        return costs;
    }

    public static bool TryMoveStack(GameState state, int stackId, HexCoord destination)
    {
        if (!state.Stacks.TryGetValue(stackId, out var stack) || !state.Map.TryGet(destination, out var tile))
        {
            return false;
        }

        var range = MovementRange(state, stack.Coord, stack.MovementLeft);
        if (!range.TryGetValue(destination, out var cost) || cost == 0)
        {
            return false;
        }

        var origin = state.Map.Get(stack.Coord);
        origin.StackIds.Remove(stack.Id);
        tile.StackIds.Add(stack.Id);
        stack.Coord = destination;
        stack.MovementLeft -= cost;

        foreach (var enemy in tile.StackIds.Select(id => state.Stacks[id]).Where(s => s.FactionId != stack.FactionId).ToList())
        {
            CombatResolver.Resolve(state, stack, enemy);
            if (!state.Stacks.ContainsKey(stack.Id))
            {
                break;
            }
        }

        return true;
    }

    public static bool TryMoveAgent(GameState state, int agentId, HexCoord destination)
    {
        if (!state.Agents.TryGetValue(agentId, out var agent) || agent.JoinedStackId is not null || !state.Map.TryGet(destination, out var tile))
        {
            return false;
        }

        var range = MovementRange(state, agent.Coord, agent.MovementLeft);
        if (!range.TryGetValue(destination, out var cost) || cost == 0)
        {
            return false;
        }

        state.Map.Get(agent.Coord).AgentIds.Remove(agent.Id);
        tile.AgentIds.Add(agent.Id);
        agent.Coord = destination;
        agent.MovementLeft -= cost;

        var friendlyStack = tile.StackIds.Select(id => state.Stacks[id]).FirstOrDefault(s => s.FactionId == agent.FactionId && s.LeaderAgentId is null);
        if (friendlyStack is not null)
        {
            TryJoinAgentToStack(state, agent.Id, friendlyStack.Id);
        }

        return true;
    }

    public static bool TryJoinAgentToStack(GameState state, int agentId, int stackId)
    {
        if (!state.Agents.TryGetValue(agentId, out var agent) || !state.Stacks.TryGetValue(stackId, out var stack))
        {
            return false;
        }

        if (agent.FactionId != stack.FactionId || agent.Coord != stack.Coord || agent.JoinedStackId is not null || stack.LeaderAgentId is not null)
        {
            return false;
        }

        state.Map.Get(agent.Coord).AgentIds.Remove(agent.Id);
        agent.JoinedStackId = stack.Id;
        stack.LeaderAgentId = agent.Id;
        state.AddLog($"{agent.Name} joined army {stack.Id}.");
        return true;
    }

    public static bool TryDetachLeader(GameState state, int stackId)
    {
        if (!state.Stacks.TryGetValue(stackId, out var stack) || stack.LeaderAgentId is not { } agentId || !state.Agents.TryGetValue(agentId, out var agent))
        {
            return false;
        }

        stack.LeaderAgentId = null;
        agent.JoinedStackId = null;
        agent.Coord = stack.Coord;
        state.Map.Get(stack.Coord).AgentIds.Add(agent.Id);
        state.AddLog($"{agent.Name} left army {stack.Id}.");
        return true;
    }

    public static void ResetFactionMovement(GameState state, string factionId)
    {
        foreach (var stack in state.StacksForFaction(factionId))
        {
            stack.MovementLeft = stack.Units.Count == 0 ? 0 : stack.Units.Min(u => state.Database.Units[u.TypeId].Movement);
        }

        foreach (var agent in state.AgentsForFaction(factionId))
        {
            agent.MovementLeft = state.Database.Units[agent.TypeId].Movement;
        }
    }

    public static void AdvanceTurn(GameState state)
    {
        state.CurrentFactionIndex = (state.CurrentFactionIndex + 1) % state.Factions.Count;
        if (state.CurrentFactionIndex == 0)
        {
            state.Turn++;
        }

        ResetFactionMovement(state, state.CurrentFaction.Id);
        state.AddLog($"{state.CurrentFaction.Name}'s turn begins.");
    }
}
