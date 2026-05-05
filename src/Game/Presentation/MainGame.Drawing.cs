using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
    public override void _Draw()
    {
        // Godot calls _Draw after QueueRedraw. Draw order matters: base terrain
        // first, then cities, then mobile pieces so units are visible on top.
        if (_state is not { } state)
        {
            return;
        }

        foreach (var tile in state.Map.Tiles)
        {
            DrawTile(tile);
        }

        foreach (var city in state.Cities.Values)
        {
            DrawCity(city);
        }

        foreach (var tile in state.Map.Tiles)
        {
            DrawStacksOnTile(tile);
            DrawAgentsOnTile(tile);
        }
    }

    private void DrawTile(HexTile tile)
    {
        // Tiles are immediate-mode hex polygons. The resolved terrain supplies
        // the base color, while selected movement range lightly highlights tiles.
        var center = HexToPixel(tile.Coord);
        var terrain = TerrainResolver.Resolve(_state!, tile);
        var color = new Color(terrain.Color);

        if (_selectedRange.ContainsKey(tile.Coord))
        {
            color = color.Lightened(0.25f);
        }

        var corners = HexCorners(center);
        DrawColoredPolygon(corners, color);
        DrawPolyline(corners.Append(corners[0]).ToArray(), new Color(0, 0, 0, 0.32f), 1.0f);

        DrawElevation(center, tile.Elevation);
        DrawVegetation(center, tile.Vegetation);

        if (tile.FeatureIds.Contains("volcano", StringComparer.OrdinalIgnoreCase))
        {
            DrawVolcano(center, tile.Elevation);
        }

        if (tile.ResourceId is not null)
        {
            DrawResource(center + new Vector2(10, -9), tile.ResourceId);
        }
    }

    private void DrawCity(CityState city)
    {
        var center = HexToPixel(city.Coord);
        var factionColor = new Color(_state!.Factions.First(f => f.Id == city.FactionId).Color);
        var mainBuildingId = city.BuildingIds.LastOrDefault() ?? "campsite";
        DrawCityMarker(center, factionColor, mainBuildingId);
    }

    private void DrawStacksOnTile(HexTile tile)
    {
        var stacks = tile.StackIds
            .Where(id => _state!.Stacks.ContainsKey(id))
            .Select(id => _state!.Stacks[id])
            .OrderBy(s => s.Id)
            .ToList();

        for (var i = 0; i < stacks.Count; i++)
        {
            DrawStack(stacks[i], PieceOffset(i, stacks.Count, new Vector2(-12, 10)));
        }
    }

    private void DrawAgentsOnTile(HexTile tile)
    {
        var agents = tile.AgentIds
            .Where(id => _state!.Agents.ContainsKey(id))
            .Select(id => _state!.Agents[id])
            .Where(a => a.JoinedStackId is null)
            .OrderBy(a => a.Id)
            .ToList();

        for (var i = 0; i < agents.Count; i++)
        {
            DrawAgent(agents[i], PieceOffset(i, agents.Count, new Vector2(12, 10)));
        }
    }

    private static Vector2 PieceOffset(int index, int count, Vector2 baseOffset)
    {
        // Multiple armies or agents can occupy one hex. Spread same-kind markers
        // horizontally around their normal anchor so each remains clickable by
        // repeated tile selection and visible to the player.
        return baseOffset + new Vector2((index - (count - 1) / 2f) * 13f, 0);
    }

    private void DrawStack(StackState stack, Vector2 offset)
    {
        // Army stacks are circles offset down-left from the hex center. The
        // number is total unit count, not combat strength.
        var center = HexToPixel(stack.Coord) + offset;
        DrawCircle(center, 8, new Color(_state!.Factions.First(f => f.Id == stack.FactionId).Color));
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-5, 5), stack.Units.Sum(u => u.Count).ToString(), HorizontalAlignment.Left, -1, 12, Colors.White);
    }

    private void DrawAgent(AgentState agent, Vector2 offset)
    {
        // Loose agents are smaller circles offset down-right. Joined agents are
        // skipped by _Draw because they are represented as stack leaders.
        var center = HexToPixel(agent.Coord) + offset;
        DrawCircle(center, 6, new Color(_state!.Factions.First(f => f.Id == agent.FactionId).Color).Lightened(0.35f));
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-4, 4), "A", HorizontalAlignment.Left, -1, 11, Colors.Black);
    }

    private void DrawCityMarker(Vector2 center, Color factionColor, string mainBuildingId)
    {
        switch (mainBuildingId)
        {
            case "campsite":
                DrawCampsite(center, factionColor);
                break;
            case "shelter":
                DrawTent(center, factionColor, 1);
                break;
            case "encampment":
                DrawTent(center + new Vector2(-6, 1), factionColor, 0.92f);
                DrawTent(center + new Vector2(0, -1), factionColor.Lightened(0.08f), 1.0f);
                DrawTent(center + new Vector2(7, 2), factionColor.Darkened(0.08f), 0.88f);
                break;
            case "villagesquare":
                DrawHouse(center, factionColor, 1.0f, hasWindow: true);
                break;
            case "townsquare":
                DrawHouse(center + new Vector2(-8, 2), factionColor.Lightened(0.08f), 0.84f, hasWindow: true);
                DrawHouse(center + new Vector2(0, -2), factionColor, 1.0f, hasWindow: true);
                DrawHouse(center + new Vector2(9, 3), factionColor.Darkened(0.08f), 0.8f, hasWindow: true);
                break;
            case "citysquare":
                DrawCastleTown(center, factionColor);
                break;
            default:
                DrawHouse(center, factionColor, 1.0f, hasWindow: true);
                break;
        }
    }

    private void DrawCampsite(Vector2 center, Color factionColor)
    {
        var stickColor = factionColor.Darkened(0.6f);
        DrawLine(center + new Vector2(-7, 10), center + new Vector2(-1, 3), stickColor, 1.8f);
        DrawLine(center + new Vector2(7, 10), center + new Vector2(1, 3), stickColor, 1.8f);
        DrawLine(center + new Vector2(-3, 11), center + new Vector2(4, 2), stickColor, 1.6f);

        var outerFlame = new[]
        {
            center + new Vector2(0, -7),
            center + new Vector2(-5, 1),
            center + new Vector2(-2, 8),
            center + new Vector2(0, 4),
            center + new Vector2(2, 8),
            center + new Vector2(5, 1)
        };
        DrawColoredPolygon(outerFlame, new Color("#ef8b2c"));
        var innerFlame = new[]
        {
            center + new Vector2(0, -3),
            center + new Vector2(-2, 2),
            center + new Vector2(0, 6),
            center + new Vector2(2, 2)
        };
        DrawColoredPolygon(innerFlame, new Color("#ffe7a3"));
    }

    private void DrawTent(Vector2 center, Color factionColor, float scale)
    {
        var tent = new[]
        {
            center + new Vector2(0, -10 * scale),
            center + new Vector2(-10 * scale, 8 * scale),
            center + new Vector2(10 * scale, 8 * scale)
        };
        DrawColoredPolygon(tent, factionColor);
        DrawPolyline(tent.Append(tent[0]).ToArray(), new Color(0, 0, 0, 0.35f), 1.0f);
        DrawLine(center + new Vector2(0, -10 * scale), center + new Vector2(0, 8 * scale), new Color(1, 1, 1, 0.28f), 1.0f);
    }

    private void DrawHouse(Vector2 center, Color factionColor, float scale, bool hasWindow = false)
    {
        var baseRect = new Rect2(center + new Vector2(-8 * scale, -1 * scale), new Vector2(16 * scale, 13 * scale));
        DrawRect(baseRect, factionColor);
        DrawRect(baseRect, new Color(0, 0, 0, 0.28f), false, 1.0f);

        var roof = new[]
        {
            center + new Vector2(0, -11 * scale),
            center + new Vector2(-10 * scale, -1 * scale),
            center + new Vector2(10 * scale, -1 * scale)
        };
        DrawColoredPolygon(roof, factionColor.Darkened(0.22f));
        DrawPolyline(roof.Append(roof[0]).ToArray(), new Color(0, 0, 0, 0.28f), 1.0f);

        if (hasWindow)
        {
            DrawRect(new Rect2(center + new Vector2(-5 * scale, 3 * scale), new Vector2(3 * scale, 3 * scale)), Colors.LightGoldenrod);
            DrawRect(new Rect2(center + new Vector2(2 * scale, 3 * scale), new Vector2(3 * scale, 3 * scale)), Colors.LightGoldenrod);
        }
    }

    private void DrawCastleTown(Vector2 center, Color factionColor)
    {
        DrawRect(new Rect2(center + new Vector2(-14, 2), new Vector2(28, 11)), factionColor);
        DrawRect(new Rect2(center + new Vector2(-8, -14), new Vector2(16, 27)), factionColor.Lightened(0.06f));
        DrawRect(new Rect2(center + new Vector2(-3, 4), new Vector2(6, 9)), new Color("#5c3c2f"));

        for (var i = -8; i <= 4; i += 4)
        {
            DrawRect(new Rect2(center + new Vector2(i, -18), new Vector2(3, 5)), factionColor.Lightened(0.14f));
        }
    }
}
