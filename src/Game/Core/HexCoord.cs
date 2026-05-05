namespace StrategyGame.Core;

// Axial hex coordinates use Q and R axes. The third cube coordinate S is
// derived when distance math needs all three cube axes.
public readonly record struct HexCoord(int Q, int R)
{
    public int S => -Q - R;

    // The six neighbor offsets in axial coordinates, ordered clockwise.
    public static readonly HexCoord[] Directions =
    [
        new(1, 0),
        new(1, -1),
        new(0, -1),
        new(-1, 0),
        new(-1, 1),
        new(0, 1)
    ];

    public IEnumerable<HexCoord> Neighbors()
    {
        // Yield coordinates only. HexMap decides which of these actually exist
        // inside the finite generated map.
        foreach (var direction in Directions)
        {
            yield return new HexCoord(Q + direction.Q, R + direction.R);
        }
    }

    public int DistanceTo(HexCoord other)
    {
        // Axial distance is cube-coordinate Manhattan distance divided by two.
        return (Math.Abs(Q - other.Q) + Math.Abs(R - other.R) + Math.Abs(S - other.S)) / 2;
    }
}
