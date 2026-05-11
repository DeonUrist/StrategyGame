using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
    // Pointy-top corner offsets at HexSize radius, computed once. The -30° start
    // puts a vertex at the top and matches HexToPixel/PixelToHex math.
    private static readonly Vector2[] HexCornerOffsets = BuildHexCornerOffsets();
    private static readonly Vector2[] TileTextureHexCornerOffsets = BuildTileTextureHexCornerOffsets();
    internal const float TileTextureWidth = HexSize * 2f;
    internal const float TileTextureHexOccupancy = 0.5f;
    internal const float TileTextureVisibleHeight = TileTextureWidth * TileTextureHexOccupancy;
    internal const float TileTextureVisibleCenterYOffset = TileTextureVisibleHeight / 2f;

    // Reusable closed-polygon buffers. _Draw is single-threaded so sharing is safe.
    private static readonly Vector2[] HexBorderBuffer = new Vector2[7];
    private static readonly Vector2[] TriBorderBuffer = new Vector2[4];

    private static Vector2[] BuildHexCornerOffsets()
    {
        var halfHeight = TileTextureVisibleHeight / 2f;
        return
        [
            new(-HexSize / 2f, -halfHeight),
            new(HexSize / 2f, -halfHeight),
            new(HexSize, 0f),
            new(HexSize / 2f, halfHeight),
            new(-HexSize / 2f, halfHeight),
            new(-HexSize, 0f)
        ];
    }

    private static Vector2[] BuildTileTextureHexCornerOffsets()
    {
        var offsets = new Vector2[6];
        for (var i = 0; i < HexCornerOffsets.Length; i++)
        {
            offsets[i] = new Vector2(
                HexCornerOffsets[i].X,
                HexCornerOffsets[i].Y + TileTextureVisibleCenterYOffset);
        }

        return offsets;
    }

    private void ZoomCamera(float factor)
    {
        // Clamp zoom so the player cannot zoom so far in/out that the map becomes
        // impossible to navigate.
        var zoom = Mathf.Clamp(_camera.Zoom.X * factor, MinCameraZoom, MaxCameraZoom);
        _camera.Zoom = new Vector2(zoom, zoom);
    }

    private void CenterCameraOnPlayerTown()
    {
        if (_state is not { } state)
        {
            return;
        }

        var city = state.Cities.Values
            .Where(c => c.FactionId == state.PlayerFaction.Id)
            .OrderBy(c => c.Id)
            .FirstOrDefault();
        if (city is null)
        {
            return;
        }

        var zoom = Mathf.Clamp(3.0f, MinCameraZoom, MaxCameraZoom);
        _camera.Zoom = new Vector2(zoom, zoom);
        _camera.Position = HexToPixel(city.Coord);
    }

    private static Vector2 HexToPixel(HexCoord coord)
    {
        // Flat-top axial layout. The terrain art keeps the actual hex in the
        // lower half of a square tile, so vertical spacing follows that visible
        // half-height instead of the full texture height.
        var x = HexSize * 1.5f * coord.Q + 70f;
        var y = TileTextureVisibleHeight * (coord.R + coord.Q / 2f) + 70f;
        return new Vector2(x, y);
    }

    private static Vector2 HexContentToPixel(HexCoord coord)
    {
        return HexToPixel(coord) + new Vector2(0f, TileTextureVisibleCenterYOffset);
    }

    private static HexCoord PixelToHex(Vector2 point)
    {
        // This is the inverse of HexToPixel for flat-top axial hexes. It
        // produces fractional q/r values that RoundAxial snaps to a real hex.
        var x = point.X - 70f;
        var y = point.Y - 70f;
        var q = 2f / 3f * x / HexSize;
        var r = y / TileTextureVisibleHeight - q / 2f;
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
            points[i] = center + HexCornerOffsets[i];
        return points;
    }

    // Fills HexBorderBuffer with corners[0..5] + corners[0] and returns it.
    // Avoids a heap allocation per tile for the outline polyline.
    private static Vector2[] HexBorder(Vector2[] corners)
    {
        Array.Copy(corners, HexBorderBuffer, 6);
        HexBorderBuffer[6] = corners[0];
        return HexBorderBuffer;
    }

    // Same pattern for 3-vertex polygons (tents, roofs, mountains, tree canopies).
    private static Vector2[] TriBorder(Vector2[] tri)
    {
        Array.Copy(tri, TriBorderBuffer, 3);
        TriBorderBuffer[3] = tri[0];
        return TriBorderBuffer;
    }
}
