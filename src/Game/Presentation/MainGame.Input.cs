using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
    public override void _UnhandledInput(InputEvent @event)
    {
        // Godot sends raw input events here after UI controls have had a chance
        // to consume them. That lets buttons work without also clicking the map.
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
        if (_mapInputLocked)
        {
            return;
        }

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
            ComputeSelectedRange(state);
            UpdateSelectionHighlights();
            return;
        }

        var hadSelection = _selectedStackId is not null || _selectedAgentId is not null || _selectedRange.Count > 0;
        ClearSelection();
        UpdatePanel(tile);
        if (hadSelection)
        {
            UpdateSelectionHighlights();
        }
    }

    private void HandleHexRightClick(HexCoord coord)
    {
        if (_mapInputLocked)
        {
            return;
        }

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
            var origin = selectedStack.Coord;
            if (!GameRules.TryMoveStack(state, stackId, coord))
            {
                return;
            }
            if (state.Stacks.ContainsKey(stackId))
            {
                SelectStack(state, selectedStack);
                ComputeSelectedRange(state);
            }
            else
            {
                ClearSelection();
            }
            UpdatePanel(tile);
            _gearMenuPanel.Visible = false;
            AnimateMovedStack(stackId, origin);
            return;
        }

        if (_selectedAgentId is { } agentId
            && state.Agents.TryGetValue(agentId, out var selectedAgent)
            && selectedAgent.FactionId == state.PlayerFaction.Id
            && _selectedRange.ContainsKey(coord)
            && selectedAgent.Coord != coord)
        {
            var origin = selectedAgent.Coord;
            if (!GameRules.TryMoveAgent(state, agentId, coord))
            {
                return;
            }
            SelectAgent(state, selectedAgent);
            UpdatePanel(tile);
            ComputeSelectedRange(state);
            _gearMenuPanel.Visible = false;
            AnimateMovedAgent(agentId, origin);
        }
    }

    private void AnimateMovedStack(int stackId, HexCoord origin)
    {
        SyncUnitObjects(movingStackId: stackId);
        UpdateSelectionHighlights();
        if (_state is not { } state || !state.Stacks.TryGetValue(stackId, out var stack) || !_stackViews.TryGetValue(stackId, out var view))
        {
            SyncDynamicObjects();
            return;
        }

        AnimateUnitView(view, HexContentToPixel(origin) + new Vector2(-7, 4), HexContentToPixel(stack.Coord) + new Vector2(-7, 4));
    }

    private void AnimateMovedAgent(int agentId, HexCoord origin)
    {
        SyncUnitObjects(movingAgentId: agentId);
        UpdateSelectionHighlights();
        if (_state is not { } state || !state.Agents.TryGetValue(agentId, out var agent) || !_agentViews.TryGetValue(agentId, out var view))
        {
            SyncDynamicObjects();
            return;
        }

        AnimateUnitView(view, HexContentToPixel(origin) + new Vector2(7, 4), HexContentToPixel(agent.Coord) + new Vector2(7, 4));
    }

    private void AnimateUnitView(Node2D view, Vector2 from, Vector2 to)
    {
        _mapInputLocked = true;
        view.Position = from;
        var tween = CreateTween();
        tween.TweenProperty(view, "position", to, 0.22).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.Finished += () =>
        {
            _mapInputLocked = false;
            SyncDynamicObjects();
        };
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
        _selectedRange = [];
    }

    private void SelectAgent(GameState state, AgentState agent)
    {
        _selectedAgentId = agent.Id;
        _selectedStackId = null;
        _selectedRange = [];
    }

    private void ComputeSelectedRange(GameState state)
    {
        RangeCacheKey key;

        if (_selectedStackId is { } stackId && state.Stacks.TryGetValue(stackId, out var stack))
        {
            if (!(stack.FactionId == state.PlayerFaction.Id && state.CurrentFaction.IsPlayer))
            {
                _selectedRange = [];
                return;
            }

            key = new RangeCacheKey(true, stackId, stack.Coord, stack.MovementLeft, state.MapVersion);
            if (key == _rangeCacheKey)
            {
                _selectedRange = _cachedRange;
                return;
            }

            _selectedRange = GameRules.MovementRange(state, stack.Coord, stack.MovementLeft);
        }
        else if (_selectedAgentId is { } agentId && state.Agents.TryGetValue(agentId, out var agent))
        {
            if (!(agent.FactionId == state.PlayerFaction.Id && state.CurrentFaction.IsPlayer))
            {
                _selectedRange = [];
                return;
            }

            key = new RangeCacheKey(false, agentId, agent.Coord, agent.MovementLeft, state.MapVersion);
            if (key == _rangeCacheKey)
            {
                _selectedRange = _cachedRange;
                return;
            }

            _selectedRange = GameRules.MovementRange(state, agent.Coord, agent.MovementLeft);
        }
        else
        {
            return;
        }

        _cachedRange = _selectedRange;
        _rangeCacheKey = key;
    }
}
