namespace StrategyGame.Core;

public static partial class GameRules
{
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

        // Keep the fast tile lookup lists in sync with the stack's own position.
        var origin = state.Map.Get(stack.Coord);
        origin.StackIds.Remove(stack.Id);
        tile.StackIds.Add(stack.Id);
        stack.Coord = destination;
        stack.MovementLeft -= cost;

        // If the moved army enters a hex with enemy armies, resolve battles one by one.
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
}
