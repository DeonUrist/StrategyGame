namespace StrategyGame.Core;

public static partial class GameRules
{
    public static double TileMovementCost(GameState state, HexTile tile)
    {
        // Movement uses the resolved terrain rather than raw tile properties so
        // features, elevation, and water passability all share one source of
        // truth with combat and drawing.
        var terrain = TerrainResolver.Resolve(state, tile);
        if (!terrain.Passable)
        {
            return double.PositiveInfinity;
        }

        return Math.Max(1, terrain.MovementCost);
    }

    public static Dictionary<HexCoord, double> MovementRange(GameState state, HexCoord origin, double movement)
    {
        // This is Dijkstra-style pathfinding with fractional movement costs.
        //
        // The dictionary stores "effective movement spent to reach this hex".
        // The last step may overspend and consume all remaining movement without
        // blocking entry onto the destination tile.
        var frontier = new PriorityQueue<HexCoord, double>();
        var costs = new Dictionary<HexCoord, double> { [origin] = 0.0 };
        frontier.Enqueue(origin, 0.0);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            var currentCost = costs[current];
            foreach (var neighbor in state.Map.Neighbors(current))
            {
                // Impassable tiles are represented by infinity and never
                // enter the range map. Later selection/move checks can then ask
                // only whether the destination appears in the range dictionary.
                var step = TileMovementCost(state, neighbor);
                if (double.IsPositiveInfinity(step))
                {
                    continue;
                }

                var remaining = movement - currentCost;
                if (remaining <= 0)
                {
                    continue;
                }

                var effectiveStep = Math.Min(step, remaining);
                var nextCost = currentCost + effectiveStep;
                if (costs.TryGetValue(neighbor.Coord, out var existing) && existing <= nextCost)
                {
                    // Skip paths that fail to improve the best known route.
                    continue;
                }

                costs[neighbor.Coord] = nextCost;
                frontier.Enqueue(neighbor.Coord, nextCost);
            }
        }

        return costs;
    }
}
