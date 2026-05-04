namespace StrategyGame.Core;

public readonly record struct HexCoord(int Q, int R)
{
    public int S => -Q - R;

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
        foreach (var direction in Directions)
        {
            yield return new HexCoord(Q + direction.Q, R + direction.R);
        }
    }

    public int DistanceTo(HexCoord other)
    {
        return (Math.Abs(Q - other.Q) + Math.Abs(R - other.R) + Math.Abs(S - other.S)) / 2;
    }
}
