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
        foreach (var _ in TakeTurnSteps(state, factionId))
        {
        }
    }

    public IEnumerable<AiTurnStep> TakeTurnSteps(GameState state, string factionId)
    {
        // The director is the simple AI brain for one faction turn.
        //
        // 1. Pick a broad goal from data/events.json, with weights adjusted by
        //    the current board state.
        // 2. Move each deployed group toward a target that matches that goal.
        // 4. Maybe upgrade a city.
        //
        // The random seed is based on turn + faction, so saving and loading before
        // an AI turn will replay the same AI choices.
        var random = new Random(TurnSeed(state, factionId));
        var chosenAction = ChooseWeightedAction(state, factionId, random);
        var factionName = state.GetFaction(factionId).Name;
        state.AddLog($"{factionName} director chose {state.Database.Events[chosenAction].Name}.");

        foreach (var group in state.GroupsForFaction(factionId).Where(g => g.StationedCityId is null).ToList())
        {
            // Copy groups to a list because movement can trigger combat, and
            // combat can remove groups while the AI turn is still iterating.
            if (!state.Groups.ContainsKey(group.Id))
            {
                continue;
            }

            var target = GameRules.IsSingleAgentGroup(state, group)
                ? ChooseScoutTarget(state, group.Coord, group.MovementLeft, random)
                : ChooseGroupTarget(state, group, chosenAction, random);
            if (target is not null)
            {
                var origin = group.Coord;
                if (GameRules.TryMoveGroup(state, group.Id, target.Value))
                {
                    yield return new AiTurnStep(
                        AiTurnStepKind.GroupMove,
                        group.Id,
                        factionId,
                        origin,
                        target.Value,
                        state.Groups.ContainsKey(group.Id));
                }
            }
        }

        var upgradedCityId = TryUpgradeCity(state, factionId, random);
        if (upgradedCityId is not null)
        {
            yield return new AiTurnStep(AiTurnStepKind.CityUpgrade, upgradedCityId.Value, factionId, null, null, true);
        }
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
