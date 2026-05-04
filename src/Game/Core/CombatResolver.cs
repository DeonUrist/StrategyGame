namespace StrategyGame.Core;

public static class CombatResolver
{
    public static void Resolve(GameState state, StackState attacker, StackState defender)
    {
        if (!state.Stacks.ContainsKey(attacker.Id) || !state.Stacks.ContainsKey(defender.Id))
        {
            return;
        }

        var attack = StackStrength(state, attacker);
        var defense = StackStrength(state, defender) + DefenseBonus(state, defender.Coord);
        var attackerWins = attack >= defense;
        var winner = attackerWins ? attacker : defender;
        var loser = attackerWins ? defender : attacker;

        ApplyCasualties(state, winner, 0.25);
        RemoveStack(state, loser);
        state.AddLog($"{state.Factions.First(f => f.Id == winner.FactionId).Name} won combat at {winner.Coord.Q},{winner.Coord.R}.");
    }

    public static int StackStrength(GameState state, StackState stack)
    {
        var strength = stack.Units.Sum(u => state.Database.Units[u.TypeId].Strength * u.Count);
        if (stack.LeaderAgentId is { } leaderId && state.Agents.TryGetValue(leaderId, out var leader))
        {
            strength += state.Database.Units[leader.TypeId].LeadershipBonus;
        }

        return strength;
    }

    private static int DefenseBonus(GameState state, HexCoord coord)
    {
        var tile = state.Map.Get(coord);
        var feature = tile.FeatureId is null ? 0 : state.Database.Features[tile.FeatureId].DefenseModifier;
        var city = tile.CityId is null ? 0 : 3;
        return feature + city;
    }

    private static void ApplyCasualties(GameState state, StackState stack, double fraction)
    {
        foreach (var unit in stack.Units)
        {
            unit.Count = Math.Max(1, (int)Math.Round(unit.Count * (1.0 - fraction)));
        }
    }

    private static void RemoveStack(GameState state, StackState stack)
    {
        state.Map.Get(stack.Coord).StackIds.Remove(stack.Id);
        state.Stacks.Remove(stack.Id);
    }
}
