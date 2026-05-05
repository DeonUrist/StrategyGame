using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
    public override void _Draw()
    {
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

        foreach (var stack in state.Stacks.Values)
        {
            DrawStack(stack);
        }

        foreach (var agent in state.Agents.Values.Where(a => a.JoinedStackId is null))
        {
            DrawAgent(agent);
        }
    }

    private void DrawTile(HexTile tile)
    {
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
        DrawRect(new Rect2(center - new Vector2(9, 9), new Vector2(18, 18)), Colors.White);
        DrawRect(new Rect2(center - new Vector2(7, 7), new Vector2(14, 14)), new Color(_state!.Factions.First(f => f.Id == city.FactionId).Color));
    }

    private void DrawStack(StackState stack)
    {
        var center = HexToPixel(stack.Coord) + new Vector2(-10, 10);
        DrawCircle(center, 8, new Color(_state!.Factions.First(f => f.Id == stack.FactionId).Color));
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-5, 5), stack.Units.Sum(u => u.Count).ToString(), HorizontalAlignment.Left, -1, 12, Colors.White);
    }

    private void DrawAgent(AgentState agent)
    {
        var center = HexToPixel(agent.Coord) + new Vector2(12, 10);
        DrawCircle(center, 6, new Color(_state!.Factions.First(f => f.Id == agent.FactionId).Color).Lightened(0.35f));
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-4, 4), "A", HorizontalAlignment.Left, -1, 11, Colors.Black);
    }

    private void DrawVegetation(Vector2 center, Vegetation vegetation)
    {
        if (vegetation == Vegetation.None)
        {
            return;
        }

        if (vegetation == Vegetation.Sparse)
        {
            var sparseOffsets = new[]
            {
                new Vector2(-8, 7),
                new Vector2(0, 3),
                new Vector2(8, 7)
            };

            foreach (var offset in sparseOffsets)
            {
                DrawTree(center + offset, 0.68f, new Color("#3f8f44"));
            }

            return;
        }

        var forestOffsets = new[]
        {
            new Vector2(-11, 2),
            new Vector2(-5, -1),
            new Vector2(2, 1),
            new Vector2(9, 3),
            new Vector2(-9, 9),
            new Vector2(-2, 7),
            new Vector2(5, 9),
            new Vector2(11, 10)
        };

        foreach (var offset in forestOffsets)
        {
            DrawTree(center + offset, 0.62f, new Color("#1f6f35"));
        }
    }

    private void DrawTree(Vector2 basePosition, float scale, Color canopyColor)
    {
        var trunkTop = basePosition + new Vector2(0, -6 * scale);
        DrawLine(basePosition, trunkTop, new Color("#5b3a1f"), Math.Max(1.0f, 2.0f * scale));

        var canopy = new[]
        {
            trunkTop + new Vector2(0, -7 * scale),
            trunkTop + new Vector2(-5 * scale, 2 * scale),
            trunkTop + new Vector2(5 * scale, 2 * scale)
        };
        DrawColoredPolygon(canopy, canopyColor);
        DrawPolyline(canopy.Append(canopy[0]).ToArray(), new Color(0, 0, 0, 0.18f), 0.75f);
    }

    private void DrawElevation(Vector2 center, Elevation elevation)
    {
        switch (elevation)
        {
            case Elevation.Hills:
                DrawHill(center + new Vector2(0, -10));
                break;
            case Elevation.Mountains:
                DrawMountain(center + new Vector2(0, -9), hasSnowCap: false);
                break;
            case Elevation.Peaks:
                DrawMountain(center + new Vector2(0, -9), hasSnowCap: true);
                break;
        }
    }

    private void DrawHill(Vector2 center)
    {
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

        DrawPolyline(points, hillColor, 3.0f);
        DrawPolyline(points, outline, 0.8f);
    }

    private void DrawMountain(Vector2 center, bool hasSnowCap)
    {
        var mountain = new[]
        {
            center + new Vector2(-12, 8),
            center + new Vector2(0, -10),
            center + new Vector2(12, 8)
        };
        DrawColoredPolygon(mountain, new Color("#777d84"));
        DrawPolyline(mountain.Append(mountain[0]).ToArray(), new Color(0, 0, 0, 0.28f), 0.9f);

        var shadow = new[]
        {
            center + new Vector2(0, -10),
            center + new Vector2(12, 8),
            center + new Vector2(3, 8)
        };
        DrawColoredPolygon(shadow, new Color("#5f666d"));

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
        DrawColoredPolygon(snow, Colors.White);
    }

    private void DrawVolcano(Vector2 center, Elevation elevation)
    {
        var plumeBase = elevation == Elevation.Peaks
            ? center + new Vector2(0, -21)
            : center + new Vector2(0, -19);

        var lava = new[]
        {
            plumeBase + new Vector2(-4, 6),
            plumeBase + new Vector2(0, -3),
            plumeBase + new Vector2(4, 6)
        };
        DrawColoredPolygon(lava, new Color("#c73624"));

        DrawCircle(plumeBase + new Vector2(-3, -7), 3.2f, new Color(0.42f, 0.42f, 0.42f, 0.72f));
        DrawCircle(plumeBase + new Vector2(2, -10), 4.0f, new Color(0.55f, 0.55f, 0.55f, 0.62f));
        DrawCircle(plumeBase + new Vector2(7, -7), 2.8f, new Color(0.68f, 0.68f, 0.68f, 0.52f));
    }

    private void DrawResource(Vector2 position, string resourceId)
    {
        DrawCircle(position, 4, ResourceColor(resourceId));
        DrawCircle(position, 2, ResourceColor(resourceId).Lightened(0.25f));
    }

    private static Color ResourceColor(string resourceId)
    {
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
