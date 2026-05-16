namespace StrategyGame.Core;

public static class CombatResolver
{
    public static void Resolve(GameState state, GroupState attacker, GroupState defender)
    {
        if (!state.Groups.ContainsKey(attacker.Id) || !state.Groups.ContainsKey(defender.Id))
        {
            return;
        }

        // Combat is currently a one-step strategic autoresolve: derive each
        // unit's power from JSON stats, add defender bonuses, remove the loser,
        // and apply light casualties to the winner.
        var attack = GroupStrength(state, attacker);
        var defense = GroupStrength(state, defender) + DefenseBonus(state, defender.Coord);
        var attackerWins = attack >= defense;

        // The winner remains on the battlefield and takes partial losses. The
        // loser is removed entirely for now; later tactical combat can replace
        // this with more granular per-unit casualties.
        var winner = attackerWins ? attacker : defender;
        var loser = attackerWins ? defender : attacker;

        ApplyCasualties(state, winner, 0.25);
        RemoveGroup(state, loser);
        state.AddLog($"{state.GetFaction(winner.FactionId).Name} won combat at {winner.Coord.Q},{winner.Coord.R}.");
    }

    public static int GroupStrength(GameState state, GroupState group)
    {
        return group.Units.Sum(u => UnitPower(state.Database.Units[u.TypeId]));
    }

    public static int UnitPower(UnitDefinition unit)
    {
        return Math.Max(1, unit.Damage + unit.Armor + unit.Health / 2);
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

    private static void ApplyCasualties(GameState state, GroupState group, double fraction)
    {
        // Casualties are proportional but never remove the final surviving unit.
        var keepCount = Math.Max(1, (int)Math.Round(group.Units.Count * (1.0 - fraction)));
        while (group.Units.Count > keepCount)
        {
            group.Units.RemoveAt(group.Units.Count - 1);
        }
    }

    private static void RemoveGroup(GameState state, GroupState group)
    {
        // Remove from both the authoritative group dictionary and the tile index
        // so later draws/clicks do not find a stale group id.
        state.Map.Get(group.Coord).GroupIds.Remove(group.Id);
        state.Groups.Remove(group.Id);
    }
}
