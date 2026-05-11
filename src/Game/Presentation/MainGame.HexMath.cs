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
        // Use fixed zoom steps that keep the 28 world-unit row spacing on whole
        // screen pixels. Arbitrary zoom values can produce 1px sampling seams
        // between separate terrain sprites.
        var current = NearestZoomStepIndex(_camera.Zoom.X);
        var next = factor > 1f
            ? Math.Min(current + 1, CameraZoomSteps.Length - 1)
            : Math.Max(current - 1, 0);
        var zoom = CameraZoomSteps[next];
        _camera.Zoom = new Vector2(zoom, zoom);
        SnapCameraToPixelGrid();
        UpdateBackgroundTransform();
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
        SnapCameraToPixelGrid();
        UpdateBackgroundTransform();
    }

    private void SetCameraPositionPixelSnapped(Vector2 position)
    {
        _camera.Position = ClampCameraPosition(SnapToCameraPixelGrid(ClampCameraPosition(position)));
        UpdateBackgroundTransform();
    }

    private void SnapCameraToPixelGrid()
    {
        _camera.Position = ClampCameraPosition(SnapToCameraPixelGrid(ClampCameraPosition(_camera.Position)));
        UpdateBackgroundTransform();
    }

    private Vector2 SnapToCameraPixelGrid(Vector2 position)
    {
        var zoom = MathF.Max(_camera.Zoom.X, 0.0001f);
        return new Vector2(
            MathF.Round(position.X * zoom) / zoom,
            MathF.Round(position.Y * zoom) / zoom);
    }

    private Vector2 ClampCameraPosition(Vector2 position)
    {
        if (_state is not { } state || !state.Map.Tiles.Any())
        {
            return position;
        }

        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var tile in state.Map.Tiles)
        {
            var tilePosition = HexToPixel(tile.Coord);
            min.X = MathF.Min(min.X, tilePosition.X - TileTextureWidth / 2f);
            min.Y = MathF.Min(min.Y, tilePosition.Y - TileTextureWidth / 2f);
            max.X = MathF.Max(max.X, tilePosition.X + TileTextureWidth / 2f);
            max.Y = MathF.Max(max.Y, tilePosition.Y + TileTextureWidth / 2f);
        }

        var viewportHalf = GetViewportRect().Size / (2f * MathF.Max(_camera.Zoom.X, 0.0001f));
        return new Vector2(
            ClampWithFallback(position.X, min.X - viewportHalf.X, max.X + viewportHalf.X),
            ClampWithFallback(position.Y, min.Y - viewportHalf.Y, max.Y + viewportHalf.Y));
    }

    private static float ClampWithFallback(float value, float min, float max)
    {
        return min > max ? (min + max) / 2f : Mathf.Clamp(value, min, max);
    }

    private static int NearestZoomStepIndex(float zoom)
    {
        var best = 0;
        var bestDistance = MathF.Abs(CameraZoomSteps[0] - zoom);
        for (var i = 1; i < CameraZoomSteps.Length; i++)
        {
            var distance = MathF.Abs(CameraZoomSteps[i] - zoom);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void RecenterCamera()
    {
        if (_state is not { } state || _mapInputLocked)
        {
            return;
        }

        var target = SelectedCameraCoord(state) ?? PlayerCityCoord(state);
        if (target is not null)
        {
            SetCameraPositionPixelSnapped(HexToPixel(target.Value));
        }
    }

    private HexCoord? SelectedCameraCoord(GameState state)
    {
        if (_selectedStackId is { } stackId && state.Stacks.TryGetValue(stackId, out var stack))
        {
            return stack.Coord;
        }

        if (_selectedAgentId is { } agentId && state.Agents.TryGetValue(agentId, out var agent))
        {
            return agent.Coord;
        }

        return _inspectedTileCoord;
    }

    private HexCoord? PlayerCityCoord(GameState state)
    {
        return state.Cities.Values
            .Where(c => c.FactionId == state.PlayerFaction.Id)
            .OrderBy(c => c.Id)
            .Select(c => (HexCoord?)c.Coord)
            .FirstOrDefault();
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
