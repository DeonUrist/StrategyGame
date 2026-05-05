namespace StrategyGame.Core;

public static partial class GameRules
{
    public static void ResetFactionMovement(GameState state, string factionId)
    {
        // Movement allowances come from unit definitions so changing unit data
        // updates both player and AI turns. A stack uses the slowest unit's move.
        foreach (var stack in state.StacksForFaction(factionId))
        {
            stack.MovementLeft = stack.Units.Count == 0 ? 0 : stack.Units.Min(u => state.Database.Units[u.TypeId].Movement);
        }

        foreach (var agent in state.AgentsForFaction(factionId))
        {
            // Agents use their own unit definition because they are single
            // character-style pieces rather than stacks of many unit rows.
            agent.MovementLeft = state.Database.Units[agent.TypeId].Movement;
        }
    }

    public static void AdvanceTurn(GameState state)
    {
        // Factions take turns in the order they appear in data/factions.json.
        // When the list wraps back to the first faction, a new world turn begins.
        state.CurrentFactionIndex = (state.CurrentFactionIndex + 1) % state.Factions.Count;
        if (state.CurrentFactionIndex == 0)
        {
            state.Turn++;
        }

        ResetFactionMovement(state, state.CurrentFaction.Id);
        state.AddLog($"{state.CurrentFaction.Name}'s turn begins.");
    }
}
