namespace StrategyGame.Core;

public static partial class MapGenerator
{
    private static HexCoord ToCoord(int col, int row)
    {
        // Convert display-friendly row/column coordinates into axial q/r. The
        // row offset keeps the hex map visually square when drawn.
        return new HexCoord(col - row / 2, row);
    }

    private static int ColumnOf(HexCoord coord)
    {
        // Inverse of ToCoord. Generation often reasons in rows/columns because
        // latitude bands and edge checks are easier in that space.
        return coord.Q + coord.R / 2;
    }

    private static bool IsOcean(int col, int row, int mapSize, int seed)
    {
        if (col == 0 || row == 0 || col == mapSize - 1 || row == mapSize - 1)
        {
            return true;
        }

        // The island is an oval in row/column space. A little deterministic wave
        // noise makes the coast uneven while still keeping one main island. The
        // map canvas is larger than the landmass, so the player sees more ocean.
        var centerX = (mapSize - 1) / 2.0;
        var centerY = (mapSize - 1) / 2.0;
        var angle = Math.Atan2(row - centerY, col - centerX);
        var elongation = 1.0 + Math.Sin(seed * 0.17) * 0.22;
        var radiusX = mapSize * 0.33 * elongation;
        var radiusY = mapSize * 0.29 / elongation;
        var dx = (col - centerX) / radiusX;
        var dy = (row - centerY) / radiusY;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var broadCoast = Math.Sin(angle * 2.0 + seed * 0.13) * 0.12;
        var peninsula = Math.Max(0.0, Math.Sin(angle * 5.0 + seed * 0.31)) * 0.12;
        var coastNoise = broadCoast + peninsula
                       + Math.Sin((col + seed) * 0.53) * 0.06
                       + Math.Cos((row - seed) * 0.47) * 0.06;

        return distance > 0.92 + coastNoise;
    }

    private static HexTile? RandomLandTile(GameState state, Random random, bool avoidEdge)
    {
        // Generation features need seed-dependent but deterministic placement on
        // existing land. avoidEdge keeps lakes and mountain starts away from the
        // guaranteed ocean border.
        var options = state.Map.Tiles
            .Where(t => !t.Elevation.IsWaterLike() && (!avoidEdge || !IsNearMapEdge(t, NormalizeMapSize(state.WorldGeneration.MapSize))))
            .ToList();

        return options.Count == 0 ? null : options[random.Next(options.Count)];
    }

    private static bool IsNearMapEdge(HexTile tile, int mapSize)
    {
        // The outer edge is reserved for ocean, and the near-edge band prevents
        // inland features from punching awkward holes in that coastline.
        var col = ColumnOf(tile.Coord);
        var row = tile.Coord.R;
        return col <= 2 || row <= 2 || col >= mapSize - 3 || row >= mapSize - 3;
    }
}
