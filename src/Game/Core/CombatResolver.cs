namespace StrategyGame.Core;

public static class CombatResolver
{
    public static void Resolve(GameState state, StackState attacker, StackState defender)
    {
        if (!state.Stacks.ContainsKey(attacker.Id) || !state.Stacks.ContainsKey(defender.Id))
        {
            return;
        }

        // Combat is currently a one-step strategic autoresolve:
        // compare total army strength, add defender terrain/city bonuses, remove
        // the losing stack, and apply light casualties to the winner.
        var attack = StackStrength(state, attacker);
        var defense = StackStrength(state, defender) + DefenseBonus(state, defender.Coord);
        var attackerWins = attack >= defense;

        // The winner remains on the battlefield and takes partial losses. The
        // loser is removed entirely for now; later tactical combat can replace
        // this with more granular per-unit casualties.
        var winner = attackerWins ? attacker : defender;
        var loser = attackerWins ? defender : attacker;

        ApplyCasualties(state, winner, 0.25);
        RemoveStack(state, loser);
        state.AddLog($"{state.Factions.First(f => f.Id == winner.FactionId).Name} won combat at {winner.Coord.Q},{winner.Coord.R}.");
    }

    public static int StackStrength(GameState state, StackState stack)
    {
        return stack.Units.Sum(u => state.Database.Units[u.TypeId].Strength * u.Count);
    }

    private static int DefenseBonus(GameState state, HexCoord coord)
    {
        // Defensive bonuses come from terrain first, with a small fixed city
        // bonus if the defender is standing in any city.
        var tile = state.Map.Get(coord);
        var terrain = TerrainResolver.Resolve(state, tile);
        var city = tile.CityId is null ? 0 : 3;
        return terrain.DefenseModifier + city;
    }

    private static void ApplyCasualties(GameState state, StackState stack, double fraction)
    {
        // Casualties are proportional but never reduce a surviving unit row below
        // one. That keeps the winning stack alive and easy to reason about.
        foreach (var unit in stack.Units)
        {
            unit.Count = Math.Max(1, (int)Math.Round(unit.Count * (1.0 - fraction)));
        }
    }

    private static void RemoveStack(GameState state, StackState stack)
    {
        // Defeated armies release attached agents back onto the same tile before
        // the stack record is removed.
        var tile = state.Map.Get(stack.Coord);
        foreach (var agentId in stack.JoinedAgentIds.ToList())
        {
            if (!state.Agents.TryGetValue(agentId, out var agent))
            {
                continue;
            }

            agent.JoinedStackId = null;
            agent.Coord = stack.Coord;
            if (!tile.AgentIds.Contains(agentId))
            {
                tile.AgentIds.Add(agentId);
            }
        }

        stack.JoinedAgentIds.Clear();

        // Remove from both the authoritative stack dictionary and the tile index
        // so later draws/clicks do not find a stale stack id.
        tile.StackIds.Remove(stack.Id);
        state.Stacks.Remove(stack.Id);
    }
}
