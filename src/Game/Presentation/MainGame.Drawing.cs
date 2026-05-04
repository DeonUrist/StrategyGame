using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
    public override void _Draw()
    {
        foreach (var tile in _state.Map.Tiles)
        {
            DrawTile(tile);
        }

        foreach (var city in _state.Cities.Values)
        {
            DrawCity(city);
        }

        foreach (var stack in _state.Stacks.Values)
        {
            DrawStack(stack);
        }

        foreach (var agent in _state.Agents.Values.Where(a => a.JoinedStackId is null))
        {
            DrawAgent(agent);
        }
    }

    private void DrawTile(HexTile tile)
    {
        var center = HexToPixel(tile.Coord);
        var terrain = _state.Database.Terrains[tile.TerrainId];
        var color = new Color(terrain.Color);

        if (_selectedRange.ContainsKey(tile.Coord))
        {
            color = color.Lightened(0.25f);
        }

        var corners = HexCorners(center);
        DrawColoredPolygon(corners, color);
        DrawPolyline(corners.Append(corners[0]).ToArray(), new Color(0, 0, 0, 0.32f), 1.0f);

        if (tile.FeatureId is not null)
        {
            DrawString(ThemeDB.FallbackFont, center + new Vector2(-8, -4), tile.FeatureId[..1].ToUpperInvariant(), HorizontalAlignment.Left, -1, 14, Colors.DarkGreen);
        }

        if (tile.ResourceId is not null)
        {
            DrawCircle(center + new Vector2(10, -9), 4, Colors.Gold);
        }
    }

    private void DrawCity(CityState city)
    {
        var center = HexToPixel(city.Coord);
        DrawRect(new Rect2(center - new Vector2(9, 9), new Vector2(18, 18)), Colors.White);
        DrawRect(new Rect2(center - new Vector2(7, 7), new Vector2(14, 14)), new Color(_state.Factions.First(f => f.Id == city.FactionId).Color));
    }

    private void DrawStack(StackState stack)
    {
        var center = HexToPixel(stack.Coord) + new Vector2(-10, 10);
        DrawCircle(center, 8, new Color(_state.Factions.First(f => f.Id == stack.FactionId).Color));
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-5, 5), stack.Units.Sum(u => u.Count).ToString(), HorizontalAlignment.Left, -1, 12, Colors.White);
    }

    private void DrawAgent(AgentState agent)
    {
        var center = HexToPixel(agent.Coord) + new Vector2(12, 10);
        DrawCircle(center, 6, new Color(_state.Factions.First(f => f.Id == agent.FactionId).Color).Lightened(0.35f));
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-4, 4), "A", HorizontalAlignment.Left, -1, 11, Colors.Black);
    }
}
