using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame : Node2D
{
    private const float HexSize = 28f;

    private readonly FactionDirector _director = new();
    private GameState _state = null!;
    private Camera2D _camera = null!;
    private Label _infoLabel = null!;
    private Label _logLabel = null!;
    private Button _detachLeaderButton = null!;
    private int? _selectedStackId;
    private int? _selectedAgentId;
    private Dictionary<HexCoord, int> _selectedRange = [];

    public override void _Ready()
    {
        var dataPath = ProjectSettings.GlobalizePath("res://data");
        var database = GameDatabase.LoadFromDirectory(dataPath);
        _state = MapGenerator.CreateSandbox(database, 42);

        _camera = new Camera2D { Enabled = true, Position = new Vector2(470, 340), Zoom = new Vector2(0.9f, 0.9f) };
        AddChild(_camera);

        BuildUi();
        UpdatePanel();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            var coord = PixelToHex(GetGlobalMousePosition());
            HandleHexClick(coord);
        }

        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp })
        {
            ZoomCamera(1.1f);
        }

        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown })
        {
            ZoomCamera(0.9f);
        }

        if (@event is InputEventMouseMotion motion && (((MouseButtonMask)motion.ButtonMask) & (MouseButtonMask.Middle | MouseButtonMask.Right)) != 0)
        {
            _camera.Position -= motion.Relative / _camera.Zoom.X;
        }
    }

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

    private void BuildUi()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);

        var panel = new PanelContainer
        {
            Position = new Vector2(12, 12),
            CustomMinimumSize = new Vector2(340, 260)
        };
        canvas.AddChild(panel);

        var box = new VBoxContainer();
        panel.AddChild(box);

        _infoLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        box.AddChild(_infoLabel);

        var endTurn = new Button { Text = "End Turn" };
        endTurn.Pressed += EndPlayerTurn;
        box.AddChild(endTurn);

        _detachLeaderButton = new Button { Text = "Detach Leader" };
        _detachLeaderButton.Pressed += DetachSelectedLeader;
        box.AddChild(_detachLeaderButton);

        _logLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        box.AddChild(_logLabel);
    }

    private void HandleHexClick(HexCoord coord)
    {
        if (!_state.Map.TryGet(coord, out var tile) || !_state.CurrentFaction.IsPlayer)
        {
            return;
        }

        if (_selectedStackId is { } stackId && _selectedRange.ContainsKey(coord))
        {
            GameRules.TryMoveStack(_state, stackId, coord);
            ClearSelection();
            QueueRedraw();
            UpdatePanel(tile);
            return;
        }

        if (_selectedAgentId is { } agentId && _selectedRange.ContainsKey(coord))
        {
            GameRules.TryMoveAgent(_state, agentId, coord);
            ClearSelection();
            QueueRedraw();
            UpdatePanel(tile);
            return;
        }

        var playerStack = tile.StackIds.Select(id => _state.Stacks[id]).FirstOrDefault(s => s.FactionId == _state.PlayerFaction.Id);
        if (playerStack is not null)
        {
            _selectedStackId = playerStack.Id;
            _selectedAgentId = null;
            _selectedRange = GameRules.MovementRange(_state, playerStack.Coord, playerStack.MovementLeft);
            UpdatePanel(tile);
            QueueRedraw();
            return;
        }

        var playerAgent = tile.AgentIds.Select(id => _state.Agents[id]).FirstOrDefault(a => a.FactionId == _state.PlayerFaction.Id);
        if (playerAgent is not null)
        {
            _selectedAgentId = playerAgent.Id;
            _selectedStackId = null;
            _selectedRange = GameRules.MovementRange(_state, playerAgent.Coord, playerAgent.MovementLeft);
            UpdatePanel(tile);
            QueueRedraw();
            return;
        }

        ClearSelection();
        UpdatePanel(tile);
        QueueRedraw();
    }

    private void EndPlayerTurn()
    {
        if (!_state.CurrentFaction.IsPlayer)
        {
            return;
        }

        ClearSelection();
        GameRules.AdvanceTurn(_state);
        while (!_state.CurrentFaction.IsPlayer)
        {
            _director.TakeTurn(_state, _state.CurrentFaction.Id);
            GameRules.AdvanceTurn(_state);
        }

        UpdatePanel();
        QueueRedraw();
    }

    private void DetachSelectedLeader()
    {
        if (_selectedStackId is not { } stackId)
        {
            return;
        }

        GameRules.TryDetachLeader(_state, stackId);
        UpdatePanel(_state.Stacks.TryGetValue(stackId, out var stack) ? _state.Map.Get(stack.Coord) : null);
        QueueRedraw();
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

    private void UpdatePanel(HexTile? tile = null)
    {
        var selected = _selectedStackId is { } stackId && _state.Stacks.TryGetValue(stackId, out var stack)
            ? $"Selected army {stack.Id}, move {stack.MovementLeft}, strength {CombatResolver.StackStrength(_state, stack)}"
              + (stack.LeaderAgentId is null ? "" : $", led by {_state.Agents[stack.LeaderAgentId.Value].Name}")
            : _selectedAgentId is { } agentId && _state.Agents.TryGetValue(agentId, out var agent)
                ? $"Selected agent {agent.Name}, move {agent.MovementLeft}"
                : "Select a player army or agent.";

        var tileText = tile is null
            ? ""
            : $"\nTile {tile.Coord.Q},{tile.Coord.R}: {_state.Database.Terrains[tile.TerrainId].Name}"
              + (tile.FeatureId is null ? "" : $", {_state.Database.Features[tile.FeatureId].Name}")
              + (tile.ResourceId is null ? "" : $", {_state.Database.Resources[tile.ResourceId].Name}")
              + (tile.CityId is null ? "" : $"\nCity: {_state.Cities[tile.CityId.Value].Name}, buildings: {string.Join(", ", _state.Cities[tile.CityId.Value].BuildingIds.Select(id => _state.Database.Buildings[id].Name))}");

        _infoLabel.Text = $"Turn {_state.Turn}: {_state.CurrentFaction.Name}\n{selected}{tileText}";
        _detachLeaderButton.Disabled = _selectedStackId is not { } selectedStackId
                                      || !_state.Stacks.TryGetValue(selectedStackId, out var selectedStack)
                                      || selectedStack.LeaderAgentId is null;
        _logLabel.Text = string.Join('\n', _state.Log.TakeLast(8).Select(e => $"T{e.Turn}: {e.Text}"));
    }

    private void ClearSelection()
    {
        _selectedStackId = null;
        _selectedAgentId = null;
        _selectedRange = [];
    }

    private static Vector2 HexToPixel(HexCoord coord)
    {
        var x = HexSize * MathF.Sqrt(3f) * (coord.Q + coord.R / 2f) + 70f;
        var y = HexSize * 1.5f * coord.R + 70f;
        return new Vector2(x, y);
    }

    private void ZoomCamera(float factor)
    {
        var zoom = Mathf.Clamp(_camera.Zoom.X * factor, 0.45f, 2.0f);
        _camera.Zoom = new Vector2(zoom, zoom);
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
