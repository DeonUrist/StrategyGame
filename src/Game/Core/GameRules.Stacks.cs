namespace StrategyGame.Core;

public static partial class GameRules
{
    public static bool TryMoveStack(GameState state, int stackId, HexCoord destination)
    {
        // All Try* rule methods return false for invalid commands instead of
        // throwing. That lets player input and AI use the same safe command path.
        if (!state.Stacks.TryGetValue(stackId, out var stack) || !state.Map.TryGet(destination, out var tile))
        {
            return false;
        }

        var range = MovementRange(state, stack.Coord, stack.MovementLeft);
        if (!range.TryGetValue(destination, out var cost) || cost == 0)
        {
            // cost == 0 is the origin tile. A click on the current tile should
            // not spend movement or trigger combat.
            return false;
        }

        // Keep the fast tile lookup lists in sync with the stack's own position.
        var origin = state.Map.Get(stack.Coord);
        origin.StackIds.Remove(stack.Id);
        tile.StackIds.Add(stack.Id);
        stack.Coord = destination;
        stack.MovementLeft = Math.Max(0.0, stack.MovementLeft - cost);
        foreach (var joinedAgentId in stack.JoinedAgentIds)
        {
            if (state.Agents.TryGetValue(joinedAgentId, out var joinedAgent))
            {
                joinedAgent.Coord = destination;
            }
        }

        // If the moved army enters a hex with enemy armies, resolve battles one by one.
        foreach (var enemy in tile.StackIds.Select(id => state.Stacks[id]).Where(s => s.FactionId != stack.FactionId).ToList())
        {
            // ToList snapshots the enemies before combat starts because combat
            // can remove stacks and mutate tile.StackIds.
            CombatResolver.Resolve(state, stack, enemy);
            if (!state.Stacks.ContainsKey(stack.Id))
            {
                break;
            }
        }

        return true;
    }
}
