namespace StrategyGame.Core;

public sealed partial class FactionDirector
{
    private HexCoord? ChooseStackTarget(GameState state, StackState stack, string actionId, Random random)
    {
        var range = GameRules.MovementRange(state, stack.Coord, stack.MovementLeft);

        if (actionId is "attack_enemy" or "defend_city")
        {
            var enemies = state.Stacks.Values
                .Where(s => s.FactionId != stack.FactionId)
                .OrderBy(s => s.Coord.DistanceTo(stack.Coord))
                .ToList();

            foreach (var enemy in enemies)
            {
                return ClosestReachable(range, enemy.Coord);
            }
        }

        if (actionId == "claim_resource")
        {
            var resource = state.Map.Tiles
                .Where(t => t.ResourceId is not null)
                .OrderBy(t => t.Coord.DistanceTo(stack.Coord) + random.Next(0, 3))
                .FirstOrDefault();

            return resource is null ? RandomReachable(range, random) : ClosestReachable(range, resource.Coord);
        }

        var ownedCity = state.Cities.Values
            .Where(c => c.FactionId == stack.FactionId)
            .OrderBy(c => c.Coord.DistanceTo(stack.Coord))
            .FirstOrDefault();

        return actionId == "upgrade_city" && ownedCity is not null
            ? ClosestReachable(range, ownedCity.Coord)
            : RandomReachable(range, random);
    }

    private HexCoord? ChooseScoutTarget(GameState state, HexCoord origin, int movement, Random random)
    {
        var range = GameRules.MovementRange(state, origin, movement);
        return RandomReachable(range, random);
    }

    private static HexCoord? RandomReachable(Dictionary<HexCoord, int> range, Random random)
    {
        var options = range.Keys.Where(c => range[c] > 0).ToList();
        return options.Count == 0 ? null : options[random.Next(options.Count)];
    }

    private static HexCoord? ClosestReachable(Dictionary<HexCoord, int> range, HexCoord target)
    {
        var options = range.Keys.Where(c => range[c] > 0).ToList();
        return options.Count == 0 ? null : options.OrderBy(c => c.DistanceTo(target)).First();
    }

    private void TryUpgradeCity(GameState state, string factionId, Random random)
    {
        var city = state.Cities.Values.FirstOrDefault(c => c.FactionId == factionId);
        if (city is null || random.NextDouble() > 0.35)
        {
            return;
        }

        GameRules.TryUpgradeCityBuilding(state, city.Id);
    }
}
