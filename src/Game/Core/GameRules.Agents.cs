namespace StrategyGame.Core;

public static partial class GameRules
{
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

        // Agents automatically become leaders when they step onto a friendly army
        // that does not already have a leader.
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
}
