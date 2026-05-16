namespace StrategyGame.Core;

public static partial class GameRules
{
    public static void ResetFactionMovement(GameState state, string factionId)
    {
        // Movement allowances come from unit definitions so changing unit data
        // updates both player and AI turns. A group uses the slowest unit's move.
        foreach (var group in state.GroupsForFaction(factionId))
        {
            group.MovementLeft = GameRules.MaxGroupMovement(state, group);
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
