namespace StrategyGame.Core;

public static partial class GameRules
{
    public static int TileMovementCost(GameState state, HexTile tile)
    {
        var terrain = TerrainResolver.Resolve(state, tile);
        if (!terrain.Passable)
        {
            return int.MaxValue;
        }

        return Math.Max(1, terrain.MovementCost);
    }

    public static Dictionary<HexCoord, int> MovementRange(GameState state, HexCoord origin, int movement)
    {
        // This is breadth-first pathfinding with movement costs.
        //
        // The dictionary stores "best known cost to reach this hex".
        // If a cheaper route to a hex is found later, that cheaper route replaces
        // the older one and the hex is checked again from there.
        var frontier = new Queue<HexCoord>();
        var costs = new Dictionary<HexCoord, int> { [origin] = 0 };
        frontier.Enqueue(origin);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var neighbor in state.Map.Neighbors(current))
            {
                var step = TileMovementCost(state, neighbor);
                if (step == int.MaxValue)
                {
                    continue;
                }

                var nextCost = costs[current] + step;
                if (nextCost > movement || costs.TryGetValue(neighbor.Coord, out var existing) && existing <= nextCost)
                {
                    continue;
                }

                costs[neighbor.Coord] = nextCost;
                frontier.Enqueue(neighbor.Coord);
            }
        }

        return costs;
    }
}
