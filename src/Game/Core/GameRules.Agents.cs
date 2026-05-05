namespace StrategyGame.Core;

public static partial class GameRules
{
    public static bool TryMoveAgent(GameState state, int agentId, HexCoord destination)
    {
        // Joined agents are no longer independent map pieces. They move only as
        // part of their stack until detached.
        if (!state.Agents.TryGetValue(agentId, out var agent) || agent.JoinedStackId is not null || !state.Map.TryGet(destination, out var tile))
        {
            return false;
        }

        var range = MovementRange(state, agent.Coord, agent.MovementLeft);
        if (!range.TryGetValue(destination, out var cost) || cost == 0)
        {
            // Agents cannot move to unreachable tiles or "move" onto their
            // current tile.
            return false;
        }

        // Keep the tile index and the agent's own coordinate in sync.
        state.Map.Get(agent.Coord).AgentIds.Remove(agent.Id);
        tile.AgentIds.Add(agent.Id);
        agent.Coord = destination;
        agent.MovementLeft = Math.Max(0.0, agent.MovementLeft - cost);

        return true;
    }

    public static bool TryJoinAgentToStack(GameState state, int agentId, int stackId)
    {
        // Joining is only valid for a colocated friendly stack. The agent leaves
        // the tile index because it is represented by the stack while joined.
        if (!state.Agents.TryGetValue(agentId, out var agent) || !state.Stacks.TryGetValue(stackId, out var stack))
        {
            return false;
        }

        if (agent.FactionId != stack.FactionId || agent.Coord != stack.Coord || agent.JoinedStackId is not null)
        {
            return false;
        }

        state.Map.Get(agent.Coord).AgentIds.Remove(agent.Id);
        agent.JoinedStackId = stack.Id;
        stack.JoinedAgentIds.Add(agent.Id);
        state.AddLog($"{agent.Name} joined army {stack.Id}.");
        return true;
    }

    public static bool TryDetachLeader(GameState state, int stackId)
    {
        // Backward-compatible helper: detach the first joined agent from a stack.
        if (!state.Stacks.TryGetValue(stackId, out var stack) || stack.JoinedAgentIds.Count == 0)
        {
            return false;
        }

        return TryDetachAgentFromStack(state, stack.JoinedAgentIds[0]);
    }

    public static bool TryDetachAgentFromStack(GameState state, int agentId)
    {
        // Detaching reverses the join relationship and places the agent back on
        // the stack's current tile as an independent piece.
        if (!state.Agents.TryGetValue(agentId, out var agent)
            || agent.JoinedStackId is not { } stackId
            || !state.Stacks.TryGetValue(stackId, out var stack)
            || !stack.JoinedAgentIds.Contains(agentId))
        {
            return false;
        }

        stack.JoinedAgentIds.Remove(agentId);
        agent.JoinedStackId = null;
        agent.Coord = stack.Coord;
        state.Map.Get(stack.Coord).AgentIds.Add(agent.Id);
        state.AddLog($"{agent.Name} left army {stack.Id}.");
        return true;
    }
}
