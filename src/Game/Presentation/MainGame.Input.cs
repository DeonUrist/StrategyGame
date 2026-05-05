using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
    public override void _UnhandledInput(InputEvent @event)
    {
        // Godot sends raw input events here after UI controls have had a chance
        // to consume them. That lets buttons work without also clicking the map.
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            var coord = PixelToHex(GetGlobalMousePosition());
            HandleHexLeftClick(coord);
        }

        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
        {
            var coord = PixelToHex(GetGlobalMousePosition());
            HandleHexRightClick(coord);
        }

        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp })
        {
            ZoomCamera(1.1f);
        }

        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown })
        {
            ZoomCamera(0.9f);
        }

        if (@event is InputEventMouseMotion motion && (((MouseButtonMask)motion.ButtonMask) & MouseButtonMask.Middle) != 0)
        {
            _camera.Position -= motion.Relative / _camera.Zoom.X;
        }
    }

    private void HandleHexLeftClick(HexCoord coord)
    {
        // Left click is for selection and tile inspection only. Clicking a
        // different tile clears any current selection instead of moving.
        if (_state is not { } state || !state.Map.TryGet(coord, out var tile))
        {
            return;
        }

        _actionMenuPanel.Visible = false;
        _gearMenuPanel.Visible = false;

        var selectableStacks = tile.StackIds
            .Select(id => state.Stacks[id])
            .OrderBy(s => s.Id)
            .ToList();
        var selectableAgents = tile.AgentIds
            .Select(id => state.Agents[id])
            .Where(a => a.JoinedStackId is null)
            .OrderBy(a => a.Id)
            .ToList();

        if (selectableStacks.Count + selectableAgents.Count > 0)
        {
            SelectNextPieceOnTile(state, tile, selectableStacks, selectableAgents);
            UpdatePanel(tile);
            QueueRedraw();
            return;
        }

        ClearSelection();
        UpdatePanel(tile);
        QueueRedraw();
    }

    private void HandleHexRightClick(HexCoord coord)
    {
        // Right click is only for commands, so enemy selections and empty state
        // become inspect-only.
        if (_state is not { } state || !state.CurrentFaction.IsPlayer || !state.Map.TryGet(coord, out var tile))
        {
            return;
        }

        _actionMenuPanel.Visible = false;

        if (_selectedStackId is { } stackId
            && state.Stacks.TryGetValue(stackId, out var selectedStack)
            && selectedStack.FactionId == state.PlayerFaction.Id
            && _selectedRange.ContainsKey(coord)
            && selectedStack.Coord != coord)
        {
            GameRules.TryMoveStack(state, stackId, coord);
            SelectStack(state, selectedStack);
            _gearMenuPanel.Visible = false;
            QueueRedraw();
            UpdatePanel(tile);
            return;
        }

        if (_selectedAgentId is { } agentId
            && state.Agents.TryGetValue(agentId, out var selectedAgent)
            && selectedAgent.FactionId == state.PlayerFaction.Id
            && _selectedRange.ContainsKey(coord)
            && selectedAgent.Coord != coord)
        {
            GameRules.TryMoveAgent(state, agentId, coord);
            SelectAgent(state, selectedAgent);
            _gearMenuPanel.Visible = false;
            QueueRedraw();
            UpdatePanel(tile);
        }
    }

    private void SelectNextPieceOnTile(GameState state, HexTile tile, List<StackState> stacks, List<AgentState> agents)
    {
        var selectedIndex = -1;
        for (var i = 0; i < stacks.Count; i++)
        {
            if (_selectedStackId == stacks[i].Id)
            {
                selectedIndex = i;
                break;
            }
        }

        if (selectedIndex < 0)
        {
            for (var i = 0; i < agents.Count; i++)
            {
                if (_selectedAgentId == agents[i].Id)
                {
                    selectedIndex = stacks.Count + i;
                    break;
                }
            }
        }

        var nextIndex = (selectedIndex + 1) % (stacks.Count + agents.Count);
        if (nextIndex < stacks.Count)
        {
            SelectStack(state, stacks[nextIndex]);
            return;
        }

        SelectAgent(state, agents[nextIndex - stacks.Count]);
    }

    private void SelectStack(GameState state, StackState stack)
    {
        _selectedStackId = stack.Id;
        _selectedAgentId = null;
        _selectedRange = stack.FactionId == state.PlayerFaction.Id && state.CurrentFaction.IsPlayer
            ? GameRules.MovementRange(state, stack.Coord, stack.MovementLeft)
            : [];
    }

    private void SelectAgent(GameState state, AgentState agent)
    {
        _selectedAgentId = agent.Id;
        _selectedStackId = null;
        _selectedRange = agent.FactionId == state.PlayerFaction.Id && state.CurrentFaction.IsPlayer
            ? GameRules.MovementRange(state, agent.Coord, agent.MovementLeft)
            : [];
    }
}
