using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
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

    private void HandleHexClick(HexCoord coord)
    {
        if (_state is not { } state || !state.Map.TryGet(coord, out var tile) || !state.CurrentFaction.IsPlayer)
        {
            return;
        }

        // If something is already selected, clicking a highlighted reachable hex
        // spends movement and moves the selected piece.
        if (_selectedStackId is { } stackId && _selectedRange.ContainsKey(coord))
        {
            GameRules.TryMoveStack(state, stackId, coord);
            ClearSelection();
            QueueRedraw();
            UpdatePanel(tile);
            return;
        }

        if (_selectedAgentId is { } agentId && _selectedRange.ContainsKey(coord))
        {
            GameRules.TryMoveAgent(state, agentId, coord);
            ClearSelection();
            QueueRedraw();
            UpdatePanel(tile);
            return;
        }

        var playerStack = tile.StackIds.Select(id => state.Stacks[id]).FirstOrDefault(s => s.FactionId == state.PlayerFaction.Id);
        if (playerStack is not null)
        {
            _selectedStackId = playerStack.Id;
            _selectedAgentId = null;
            _selectedRange = GameRules.MovementRange(state, playerStack.Coord, playerStack.MovementLeft);
            UpdatePanel(tile);
            QueueRedraw();
            return;
        }

        var playerAgent = tile.AgentIds.Select(id => state.Agents[id]).FirstOrDefault(a => a.FactionId == state.PlayerFaction.Id);
        if (playerAgent is not null)
        {
            _selectedAgentId = playerAgent.Id;
            _selectedStackId = null;
            _selectedRange = GameRules.MovementRange(state, playerAgent.Coord, playerAgent.MovementLeft);
            UpdatePanel(tile);
            QueueRedraw();
            return;
        }

        ClearSelection();
        UpdatePanel(tile);
        QueueRedraw();
    }
}
