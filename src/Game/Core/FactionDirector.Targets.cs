namespace StrategyGame.Core;

public sealed partial class FactionDirector
{
    private HexCoord? ChooseGroupTarget(GameState state, GroupState group, string actionId, Random random)
    {
        // Targets are selected from the group's current movement range. The AI
        // never plans multi-turn paths yet; it chooses the best reachable hex
        // for this one turn.
        var range = GameRules.MovementRange(state, group.Coord, group.MovementLeft);

        if (actionId is "attack_enemy" or "defend_city")
        {
            // Both attack and defense currently move toward enemy groups. This
            // makes "defend city" a reactive military posture until stronger
            // garrison/zone rules exist.
            var nearest = state.Groups.Values
                .Where(g => g.FactionId != group.FactionId && g.StationedCityId is null)
                .OrderBy(g => g.Coord.DistanceTo(group.Coord))
                .FirstOrDefault();

            if (nearest is not null)
                return ClosestReachable(range, nearest.Coord);
        }

        if (actionId == "claim_resource")
        {
            // Resource claiming moves toward the nearest visible resource with a
            // small random tie-breaker so AI factions do not always pick the same
            // exact target in similar positions.
            var resource = state.Map.Tiles
                .Where(t => t.ResourceId is not null)
                .OrderBy(t => t.Coord.DistanceTo(group.Coord) + random.Next(0, 3))
                .FirstOrDefault();

            return resource is null ? RandomReachable(range, random) : ClosestReachable(range, resource.Coord);
        }

        var ownedCity = state.Cities.Values
            .Where(c => c.FactionId == group.FactionId)
            .OrderBy(c => c.Coord.DistanceTo(group.Coord))
            .FirstOrDefault();

        // Upgrade-focused groups drift toward their own city; otherwise they
        // wander within range to keep the board moving.
        return actionId == "upgrade_city" && ownedCity is not null
            ? ClosestReachable(range, ownedCity.Coord)
            : RandomReachable(range, random);
    }

    private HexCoord? ChooseScoutTarget(GameState state, HexCoord origin, double movement, Random random)
    {
        // Scouts are intentionally simple: pick any reachable non-origin tile.
        // Fog of war can later replace this with unknown-tile exploration.
        var range = GameRules.MovementRange(state, origin, movement);
        return RandomReachable(range, random);
    }

    private static List<HexCoord> ReachableDestinations(Dictionary<HexCoord, double> range)
    {
        // Excludes the origin tile, which is always present with cost 0.
        return range.Keys.Where(c => range[c] > 0).ToList();
    }

    private static HexCoord? RandomReachable(Dictionary<HexCoord, double> range, Random random)
    {
        var options = ReachableDestinations(range);
        return options.Count == 0 ? null : options[random.Next(options.Count)];
    }

    private static HexCoord? ClosestReachable(Dictionary<HexCoord, double> range, HexCoord target)
    {
        // The reachable hex closest to the target is the one-turn approximation
        // of pathfinding toward that target.
        var options = ReachableDestinations(range);
        return options.Count == 0 ? null : options.OrderBy(c => c.DistanceTo(target)).First();
    }

    private int? TryUpgradeCity(GameState state, string factionId, Random random)
    {
        // The director's chosen action can bias movement toward a city, but the
        // actual upgrade still has a chance gate to keep AI turns varied.
        var city = state.Cities.Values.FirstOrDefault(c => c.FactionId == factionId);
        if (city is null || random.NextDouble() > 0.35)
        {
            return null;
        }

        return GameRules.TryUpgradeCityBuilding(state, city.Id) ? city.Id : null;
    }
}
