using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
    // Translucent white over the terrain produces the same channel arithmetic as
    // the previous Color.Lightened(0.25f) on a fully opaque base, so the move
    // from "recolor terrain" to "overlay polygon" is visually identical for the
    // base hex. Decorations on the terrain layer get tinted slightly (acceptable).
    private static readonly Color SelectionHighlightColor = new(1f, 1f, 1f, 0.25f);

    private void DrawTerrain(CanvasItem canvas)
    {
        // Called only when state.MapVersion changes (or state is set/cleared).
        // Click-time redraws skip this entirely.
        if (_state is not { } state)
        {
            return;
        }

        foreach (var tile in state.Map.Tiles)
        {
            DrawTile(canvas, tile);
        }
    }

    private void DrawDynamic(CanvasItem canvas)
    {
        // Cities, stacks, agents, and the selection-range overlay all live on
        // the dynamic layer so a click only re-issues these primitives.
        if (_state is not { } state)
        {
            return;
        }

        DrawSelectionOverlay(canvas);

        foreach (var city in state.Cities.Values)
        {
            DrawCity(canvas, city);
        }

        foreach (var tile in state.Map.Tiles)
        {
            DrawStacksOnTile(canvas, tile);
            DrawAgentsOnTile(canvas, tile);
        }
    }

    private void DrawSelectionOverlay(CanvasItem canvas)
    {
        if (_selectedRange.Count == 0)
        {
            return;
        }

        foreach (var coord in _selectedRange.Keys)
        {
            var corners = HexCorners(HexToPixel(coord));
            canvas.DrawColoredPolygon(corners, SelectionHighlightColor);
        }
    }

    private void DrawCity(CanvasItem canvas, CityState city)
    {
        var center = HexToPixel(city.Coord);
        var factionColor = new Color(_factionById[city.FactionId].Color);
        var mainBuildingId = city.BuildingIds.LastOrDefault() ?? "campsite";
        DrawCityMarker(canvas, center, factionColor, mainBuildingId);
    }

    private void DrawStacksOnTile(CanvasItem canvas, HexTile tile)
    {
        var stacks = tile.StackIds
            .Where(id => _state!.Stacks.ContainsKey(id))
            .Select(id => _state!.Stacks[id])
            .OrderBy(s => s.Id)
            .ToList();

        for (var i = 0; i < stacks.Count; i++)
        {
            DrawStack(canvas, stacks[i], PieceOffset(i, stacks.Count, new Vector2(-12, 10)));
        }
    }

    private void DrawAgentsOnTile(CanvasItem canvas, HexTile tile)
    {
        var agents = tile.AgentIds
            .Where(id => _state!.Agents.ContainsKey(id))
            .Select(id => _state!.Agents[id])
            .Where(a => a.JoinedStackId is null)
            .OrderBy(a => a.Id)
            .ToList();

        for (var i = 0; i < agents.Count; i++)
        {
            DrawAgent(canvas, agents[i], PieceOffset(i, agents.Count, new Vector2(12, 10)));
        }
    }

    private static Vector2 PieceOffset(int index, int count, Vector2 baseOffset)
    {
        // Multiple armies or agents can occupy one hex. Spread same-kind markers
        // horizontally around their normal anchor so each remains clickable by
        // repeated tile selection and visible to the player.
        return baseOffset + new Vector2((index - (count - 1) / 2f) * 13f, 0);
    }

    private void DrawStack(CanvasItem canvas, StackState stack, Vector2 offset)
    {
        // Army stacks are circles offset down-left from the hex center. The
        // number is total unit count, not combat strength.
        var center = HexToPixel(stack.Coord) + offset;
        canvas.DrawCircle(center, 8, new Color(_factionById[stack.FactionId].Color));
        canvas.DrawString(ThemeDB.FallbackFont, center + new Vector2(-5, 5), stack.Units.Sum(u => u.Count).ToString(), HorizontalAlignment.Left, -1, 12, Colors.White);
    }

    private void DrawAgent(CanvasItem canvas, AgentState agent, Vector2 offset)
    {
        // Loose agents are smaller circles offset down-right. Joined agents are
        // skipped by _Draw because they are represented as stack leaders.
        var center = HexToPixel(agent.Coord) + offset;
        canvas.DrawCircle(center, 6, new Color(_factionById[agent.FactionId].Color).Lightened(0.35f));
        canvas.DrawString(ThemeDB.FallbackFont, center + new Vector2(-4, 4), "A", HorizontalAlignment.Left, -1, 11, Colors.Black);
    }

    private void DrawCityMarker(CanvasItem canvas, Vector2 center, Color factionColor, string mainBuildingId)
    {
        switch (mainBuildingId)
        {
            case "campsite":
                DrawCampsite(canvas, center, factionColor);
                break;
            case "shelter":
                DrawTent(canvas, center, factionColor, 1);
                break;
            case "encampment":
                DrawTent(canvas, center + new Vector2(-6, 1), factionColor, 0.92f);
                DrawTent(canvas, center + new Vector2(0, -1), factionColor.Lightened(0.08f), 1.0f);
                DrawTent(canvas, center + new Vector2(7, 2), factionColor.Darkened(0.08f), 0.88f);
                break;
            case "villagesquare":
                DrawHouse(canvas, center, factionColor, 1.0f, hasWindow: true);
                break;
            case "townsquare":
                DrawHouse(canvas, center + new Vector2(-8, 2), factionColor.Lightened(0.08f), 0.84f, hasWindow: true);
                DrawHouse(canvas, center + new Vector2(0, -2), factionColor, 1.0f, hasWindow: true);
                DrawHouse(canvas, center + new Vector2(9, 3), factionColor.Darkened(0.08f), 0.8f, hasWindow: true);
                break;
            case "citysquare":
                DrawCastleTown(canvas, center, factionColor);
                break;
            default:
                DrawHouse(canvas, center, factionColor, 1.0f, hasWindow: true);
                break;
        }
    }

    private void DrawCampsite(CanvasItem canvas, Vector2 center, Color factionColor)
    {
        var stickColor = factionColor.Darkened(0.6f);
        canvas.DrawLine(center + new Vector2(-7, 10), center + new Vector2(-1, 3), stickColor, 1.8f);
        canvas.DrawLine(center + new Vector2(7, 10), center + new Vector2(1, 3), stickColor, 1.8f);
        canvas.DrawLine(center + new Vector2(-3, 11), center + new Vector2(4, 2), stickColor, 1.6f);

        var outerFlame = new[]
        {
            center + new Vector2(0, -7),
            center + new Vector2(-5, 1),
            center + new Vector2(-2, 8),
            center + new Vector2(0, 4),
            center + new Vector2(2, 8),
            center + new Vector2(5, 1)
        };
        canvas.DrawColoredPolygon(outerFlame, new Color("#ef8b2c"));
        var innerFlame = new[]
        {
            center + new Vector2(0, -3),
            center + new Vector2(-2, 2),
            center + new Vector2(0, 6),
            center + new Vector2(2, 2)
        };
        canvas.DrawColoredPolygon(innerFlame, new Color("#ffe7a3"));
    }

    private void DrawTent(CanvasItem canvas, Vector2 center, Color factionColor, float scale)
    {
        var tent = new[]
        {
            center + new Vector2(0, -10 * scale),
            center + new Vector2(-10 * scale, 8 * scale),
            center + new Vector2(10 * scale, 8 * scale)
        };
        canvas.DrawColoredPolygon(tent, factionColor);
        canvas.DrawPolyline(TriBorder(tent), new Color(0, 0, 0, 0.35f), 1.0f);
        canvas.DrawLine(center + new Vector2(0, -10 * scale), center + new Vector2(0, 8 * scale), new Color(1, 1, 1, 0.28f), 1.0f);
    }

    private void DrawHouse(CanvasItem canvas, Vector2 center, Color factionColor, float scale, bool hasWindow = false)
    {
        var baseRect = new Rect2(center + new Vector2(-8 * scale, -1 * scale), new Vector2(16 * scale, 13 * scale));
        canvas.DrawRect(baseRect, factionColor);
        canvas.DrawRect(baseRect, new Color(0, 0, 0, 0.28f), false, 1.0f);

        var roof = new[]
        {
            center + new Vector2(0, -11 * scale),
            center + new Vector2(-10 * scale, -1 * scale),
            center + new Vector2(10 * scale, -1 * scale)
        };
        canvas.DrawColoredPolygon(roof, factionColor.Darkened(0.22f));
        canvas.DrawPolyline(TriBorder(roof), new Color(0, 0, 0, 0.28f), 1.0f);

        if (hasWindow)
        {
            canvas.DrawRect(new Rect2(center + new Vector2(-5 * scale, 3 * scale), new Vector2(3 * scale, 3 * scale)), Colors.LightGoldenrod);
            canvas.DrawRect(new Rect2(center + new Vector2(2 * scale, 3 * scale), new Vector2(3 * scale, 3 * scale)), Colors.LightGoldenrod);
        }
    }

    private void DrawCastleTown(CanvasItem canvas, Vector2 center, Color factionColor)
    {
        canvas.DrawRect(new Rect2(center + new Vector2(-14, 2), new Vector2(28, 11)), factionColor);
        canvas.DrawRect(new Rect2(center + new Vector2(-8, -14), new Vector2(16, 27)), factionColor.Lightened(0.06f));
        canvas.DrawRect(new Rect2(center + new Vector2(-3, 4), new Vector2(6, 9)), new Color("#5c3c2f"));

        for (var i = -8; i <= 4; i += 4)
        {
            canvas.DrawRect(new Rect2(center + new Vector2(i, -18), new Vector2(3, 5)), factionColor.Lightened(0.14f));
        }
    }
}
