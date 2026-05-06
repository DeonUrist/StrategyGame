using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
    private static readonly Vector2[] SparseTreeOffsets =
    [
        new(-8, 7), new(0, 3), new(8, 7)
    ];

    private static readonly Vector2[] DenseTreeOffsets =
    [
        new(-11, 2), new(-5, -1), new(2, 1), new(9, 3),
        new(-9, 9),  new(-2, 7),  new(5, 9), new(11, 10)
    ];

    private void DrawTile(CanvasItem canvas, HexTile tile)
    {
        // Tiles are immediate-mode hex polygons. The resolved terrain supplies
        // the base color. Selection highlight is drawn separately on the dynamic
        // layer so click-time redraws do not invalidate the terrain layer.
        var center = HexToPixel(tile.Coord);
        var terrain = TerrainResolver.Resolve(_state!, tile);
        var color = new Color(terrain.Color);

        var corners = HexCorners(center);
        canvas.DrawColoredPolygon(corners, color);
        canvas.DrawPolyline(HexBorder(corners), new Color(0, 0, 0, 0.32f), 1.0f);

        DrawElevation(canvas, center, tile.Elevation);
        DrawVegetation(canvas, center, tile.Vegetation);

        if (tile.FeatureIds.Contains("volcano", StringComparer.OrdinalIgnoreCase))
        {
            DrawVolcano(canvas, center, tile.Elevation);
        }

        if (tile.ResourceId is not null)
        {
            DrawResource(canvas, center + new Vector2(10, -9), tile.ResourceId);
        }
    }

    private void DrawVegetation(CanvasItem canvas, Vector2 center, Vegetation vegetation)
    {
        if (vegetation == Vegetation.None)
        {
            return;
        }

        if (vegetation == Vegetation.Sparse)
        {
            foreach (var offset in SparseTreeOffsets)
                DrawTree(canvas, center + offset, 0.68f, new Color("#3f8f44"));
            return;
        }

        foreach (var offset in DenseTreeOffsets)
            DrawTree(canvas, center + offset, 0.62f, new Color("#1f6f35"));
    }

    private void DrawTree(CanvasItem canvas, Vector2 basePosition, float scale, Color canopyColor)
    {
        // Tree drawings are intentionally primitive so they stay readable at the
        // prototype zoom level and do not require imported art assets.
        var trunkTop = basePosition + new Vector2(0, -6 * scale);
        canvas.DrawLine(basePosition, trunkTop, new Color("#5b3a1f"), Math.Max(1.0f, 2.0f * scale));

        var canopy = new[]
        {
            trunkTop + new Vector2(0, -7 * scale),
            trunkTop + new Vector2(-5 * scale, 2 * scale),
            trunkTop + new Vector2(5 * scale, 2 * scale)
        };
        canvas.DrawColoredPolygon(canopy, canopyColor);
        canvas.DrawPolyline(TriBorder(canopy), new Color(0, 0, 0, 0.18f), 0.75f);
    }

    private void DrawElevation(CanvasItem canvas, Vector2 center, Elevation elevation)
    {
        // Elevation ornaments are layered over the terrain color. Water and flat
        // land draw no extra elevation symbol.
        switch (elevation)
        {
            case Elevation.Hills:
                DrawHill(canvas, center + new Vector2(0, -10));
                break;
            case Elevation.Mountains:
                DrawMountain(canvas, center + new Vector2(0, -9), hasSnowCap: false);
                break;
            case Elevation.Peaks:
                DrawMountain(canvas, center + new Vector2(0, -9), hasSnowCap: true);
                break;
        }
    }

    private void DrawHill(CanvasItem canvas, Vector2 center)
    {
        // Hills use a curved-looking polyline instead of a filled shape so the
        // terrain color still reads underneath.
        var hillColor = new Color("#6f7f55");
        var outline = new Color(0, 0, 0, 0.22f);
        var points = new[]
        {
            center + new Vector2(-12, 6),
            center + new Vector2(-7, -1),
            center + new Vector2(0, -5),
            center + new Vector2(7, -1),
            center + new Vector2(12, 6)
        };

        canvas.DrawPolyline(points, hillColor, 3.0f);
        canvas.DrawPolyline(points, outline, 0.8f);
    }

    private void DrawMountain(CanvasItem canvas, Vector2 center, bool hasSnowCap)
    {
        // Mountains are filled triangles with a darker side face. Peaks add a
        // small snowcap to distinguish the highest elevation tier.
        var mountain = new[]
        {
            center + new Vector2(-12, 8),
            center + new Vector2(0, -10),
            center + new Vector2(12, 8)
        };
        canvas.DrawColoredPolygon(mountain, new Color("#777d84"));
        canvas.DrawPolyline(TriBorder(mountain), new Color(0, 0, 0, 0.28f), 0.9f);

        var shadow = new[]
        {
            center + new Vector2(0, -10),
            center + new Vector2(12, 8),
            center + new Vector2(3, 8)
        };
        canvas.DrawColoredPolygon(shadow, new Color("#5f666d"));

        if (!hasSnowCap)
        {
            return;
        }

        var snow = new[]
        {
            center + new Vector2(0, -10),
            center + new Vector2(-4, -3),
            center + new Vector2(0, -5),
            center + new Vector2(4, -3)
        };
        canvas.DrawColoredPolygon(snow, Colors.White);
    }

    private void DrawVolcano(CanvasItem canvas, Vector2 center, Elevation elevation)
    {
        // Volcanoes are a feature overlay on mountains/peaks. The plume position
        // shifts slightly for peaks because peak mountains are taller visually.
        var plumeBase = elevation == Elevation.Peaks
            ? center + new Vector2(0, -21)
            : center + new Vector2(0, -19);

        var lava = new[]
        {
            plumeBase + new Vector2(-4, 6),
            plumeBase + new Vector2(0, -3),
            plumeBase + new Vector2(4, 6)
        };
        canvas.DrawColoredPolygon(lava, new Color("#c73624"));

        canvas.DrawCircle(plumeBase + new Vector2(-3, -7), 3.2f, new Color(0.42f, 0.42f, 0.42f, 0.72f));
        canvas.DrawCircle(plumeBase + new Vector2(2, -10), 4.0f, new Color(0.55f, 0.55f, 0.55f, 0.62f));
        canvas.DrawCircle(plumeBase + new Vector2(7, -7), 2.8f, new Color(0.68f, 0.68f, 0.68f, 0.52f));
    }

    private void DrawResource(CanvasItem canvas, Vector2 position, string resourceId)
    {
        // Resource markers are small colored dots offset from the center so they
        // do not cover city or unit markers.
        canvas.DrawCircle(position, 4, ResourceColor(resourceId));
        canvas.DrawCircle(position, 2, ResourceColor(resourceId).Lightened(0.25f));
    }

    private static Color ResourceColor(string resourceId)
    {
        // The colors mirror common material associations and are separate from
        // the ResourceDefinition because they are presentation-only.
        return resourceId switch
        {
            "copper" => new Color("#a6532f"),
            "iron" => new Color("#3f4145"),
            "gold" => Colors.Gold,
            "silver" => new Color("#aebfc6"),
            "game" => new Color("#b73535"),
            _ => Colors.Gold
        };
    }
}
