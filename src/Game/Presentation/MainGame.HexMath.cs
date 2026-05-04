using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
    private void ZoomCamera(float factor)
    {
        var zoom = Mathf.Clamp(_camera.Zoom.X * factor, 0.45f, 2.0f);
        _camera.Zoom = new Vector2(zoom, zoom);
    }

    private static Vector2 HexToPixel(HexCoord coord)
    {
        // Axial hex coordinates use Q/R grid positions.
        // Godot drawing uses X/Y pixels, so this converts grid space to screen space.
        var x = HexSize * MathF.Sqrt(3f) * (coord.Q + coord.R / 2f) + 70f;
        var y = HexSize * 1.5f * coord.R + 70f;
        return new Vector2(x, y);
    }

    private static HexCoord PixelToHex(Vector2 point)
    {
        var x = point.X - 70f;
        var y = point.Y - 70f;
        var q = MathF.Sqrt(3f) / 3f * x / HexSize - 1f / 3f * y / HexSize;
        var r = 2f / 3f * y / HexSize;
        return RoundAxial(q, r);
    }

    private static HexCoord RoundAxial(float q, float r)
    {
        // Mouse positions rarely land exactly in the center of a hex.
        // Convert fractional hex coordinates to the nearest whole hex.
        var s = -q - r;
        var rq = MathF.Round(q);
        var rr = MathF.Round(r);
        var rs = MathF.Round(s);
        var qDiff = MathF.Abs(rq - q);
        var rDiff = MathF.Abs(rr - r);
        var sDiff = MathF.Abs(rs - s);

        if (qDiff > rDiff && qDiff > sDiff)
        {
            rq = -rr - rs;
        }
        else if (rDiff > sDiff)
        {
            rr = -rq - rs;
        }

        return new HexCoord((int)rq, (int)rr);
    }

    private static Vector2[] HexCorners(Vector2 center)
    {
        var points = new Vector2[6];
        for (var i = 0; i < 6; i++)
        {
            var angle = MathF.PI / 180f * (60f * i - 30f);
            points[i] = center + new Vector2(HexSize * MathF.Cos(angle), HexSize * MathF.Sin(angle));
        }

        return points;
    }
}
