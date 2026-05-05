namespace StrategyGame.Core;

public sealed partial class FactionDirector
{
    // BaseSeed lets tests create deterministic directors while still deriving
    // per-turn randomness from current state.
    private readonly int _baseSeed;

    public FactionDirector(int baseSeed = 7781)
    {
        _baseSeed = baseSeed;
    }

    public void TakeTurn(GameState state, string factionId)
    {
        // The director is the simple AI brain for one faction turn.
        //
        // 1. Pick a broad goal from data/events.json, with weights adjusted by
        //    the current board state.
        // 2. Move each army toward a target that matches that goal.
        // 3. Move loose agents as scouts.
        // 4. Maybe upgrade a city.
        //
        // The random seed is based on turn + faction, so saving and loading before
        // an AI turn will replay the same AI choices.
        var random = new Random(TurnSeed(state, factionId));
        var chosenAction = ChooseWeightedAction(state, factionId, random);
        var factionName = state.GetFaction(factionId).Name;
        state.AddLog($"{factionName} director chose {state.Database.Events[chosenAction].Name}.");

        foreach (var stack in state.StacksForFaction(factionId).ToList())
        {
            // Copy stacks to a list because movement can trigger combat, and
            // combat can remove stacks while the AI turn is still iterating.
            if (!state.Stacks.ContainsKey(stack.Id))
            {
                continue;
            }

            var target = ChooseStackTarget(state, stack, chosenAction, random);
            if (target is not null)
            {
                GameRules.TryMoveStack(state, stack.Id, target.Value);
            }
        }

        foreach (var agent in state.AgentsForFaction(factionId).Where(a => a.JoinedStackId is null).ToList())
        {
            // Joined agents are leaders inside stacks, so only loose agents make
            // independent scouting moves.
            var target = ChooseScoutTarget(state, agent.Coord, agent.MovementLeft, random);
            if (target is not null)
            {
                GameRules.TryMoveAgent(state, agent.Id, target.Value);
            }
        }

        TryUpgradeCity(state, factionId, random);
    }

    private int TurnSeed(GameState state, string factionId)
    {
        // unchecked preserves deterministic overflow behavior. The exact number
        // is unimportant; what matters is that the same turn/faction produces
        // the same random sequence after a save/load round trip.
        unchecked
        {
            var seed = _baseSeed;
            seed = seed * 397 ^ state.Turn;
            seed = seed * 397 ^ state.CurrentFactionIndex;
            foreach (var character in factionId)
            {
                seed = seed * 397 ^ character;
            }

            return seed;
        }
    }
}
