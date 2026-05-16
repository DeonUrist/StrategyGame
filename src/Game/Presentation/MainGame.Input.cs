using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            if (HandleKeyBindingCapture(keyEvent))
            {
                GetViewport().SetInputAsHandled();
                return;
            }

            if (IsToggleGridKey(keyEvent))
            {
                ToggleGridVisibilityPreference();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (IsToggleResourceIconsKey(keyEvent))
            {
                ToggleResourceIconsVisibilityPreference();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_optionsPanel.Visible)
            {
                if (IsOpenMenuKey(keyEvent))
                {
                    HandleOpenMenuKey();
                }

                GetViewport().SetInputAsHandled();
                return;
            }

            if (IsOpenMenuKey(keyEvent))
            {
                HandleOpenMenuKey();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (IsRecenterKey(keyEvent))
            {
                RecenterCamera();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event is InputEventMouseButton or InputEventMouseMotion)
        {
            UpdateBackgroundTransform();
        }

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
            SetCameraPositionPixelSnapped(_camera.Position - motion.Relative / _camera.Zoom.X);
        }
    }

    private bool HandleKeyBindingCapture(InputEventKey keyEvent)
    {
        if (_capturingKeyBinding is null)
        {
            return false;
        }

        var key = (int)keyEvent.Keycode;
        if (_capturingKeyBinding == "OpenMenuSecondary")
        {
            _settings.KeyBindings.OpenMenuSecondary = keyEvent.Keycode == Key.Escape ? 0 : key;
        }
        else if (_capturingKeyBinding == "RecenterSecondary")
        {
            _settings.KeyBindings.RecenterSecondary = keyEvent.Keycode == Key.Escape ? 0 : key;
        }
        else if (_capturingKeyBinding == "RecenterPrimary")
        {
            _settings.KeyBindings.RecenterPrimary = keyEvent.Keycode == Key.Escape ? 0 : key;
        }

        _capturingKeyBinding = null;
        RefreshControlBindingButtons();
        SavePresentationSettings();
        return true;
    }

    private bool IsOpenMenuKey(InputEventKey keyEvent)
    {
        return MatchesKey(keyEvent, _settings.KeyBindings.OpenMenuPrimary)
               || MatchesKey(keyEvent, _settings.KeyBindings.OpenMenuSecondary);
    }

    private bool IsRecenterKey(InputEventKey keyEvent)
    {
        return MatchesKey(keyEvent, _settings.KeyBindings.RecenterPrimary)
               || MatchesKey(keyEvent, _settings.KeyBindings.RecenterSecondary);
    }

    private static bool IsToggleGridKey(InputEventKey keyEvent)
    {
        return keyEvent.Keycode == Key.G;
    }

    private static bool IsToggleResourceIconsKey(InputEventKey keyEvent)
    {
        return keyEvent.Keycode == Key.R;
    }

    private static bool MatchesKey(InputEventKey keyEvent, int key)
    {
        return key != 0 && (int)keyEvent.Keycode == key;
    }

    private void HandleOpenMenuKey()
    {
        if (_optionsPanel.Visible)
        {
            HideOptionsPanel();
            return;
        }

        if (_state is not null)
        {
            ToggleGearMenu();
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

        _selectedRegionId = tile.RegionId;
        _actionMenuPanel.Visible = false;
        _gearMenuPanel.Visible = false;

        var selectableGroups = tile.GroupIds
            .Select(id => state.Groups[id])
            .Where(g => g.StationedCityId is null)
            .OrderBy(g => g.Id)
            .ToList();

        if (selectableGroups.Count > 0)
        {
            SelectNextGroupOnTile(state, selectableGroups);
            UpdatePanel(tile);
            ComputeSelectedRange(state);
            UpdateSelectionHighlights();
            return;
        }

        var hadSelection = _selectedGroupId is not null || _selectedRange.Count > 0;
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

        if (_selectedGroupId is { } groupId
            && state.Groups.TryGetValue(groupId, out var selectedGroup)
            && selectedGroup.FactionId == state.PlayerFaction.Id
            && selectedGroup.StationedCityId is null
            && _selectedRange.ContainsKey(coord)
            && selectedGroup.Coord != coord)
        {
            var origin = selectedGroup.Coord;
            if (!GameRules.TryMoveGroup(state, groupId, coord))
            {
                return;
            }
            if (state.Groups.ContainsKey(groupId))
            {
                SelectGroup(state, selectedGroup);
                ComputeSelectedRange(state);
            }
            else
            {
                ClearSelection();
            }
            UpdatePanel(tile);
            _gearMenuPanel.Visible = false;
            AnimateMovedGroup(groupId, origin);
            return;
        }
    }

    private void AnimateMovedGroup(int groupId, HexCoord origin)
    {
        SyncUnitObjects(movingGroupId: groupId);
        UpdateSelectionHighlights();
        if (_state is not { } state || !state.Groups.TryGetValue(groupId, out var group) || !_groupViews.TryGetValue(groupId, out var view))
        {
            SyncDynamicObjects();
            return;
        }

        AnimateUnitView(view, HexToPixel(origin), HexToPixel(group.Coord));
    }

    private void AnimateUnitView(Node2D view, Vector2 from, Vector2 to)
    {
        _ = AnimatePlayerUnitViewAsync(view, from, to);
    }

    private async Task AnimatePlayerUnitViewAsync(Node2D view, Vector2 from, Vector2 to)
    {
        _mapInputLocked = true;
        await AnimateUnitViewAsync(view, from, to);
        _mapInputLocked = false;
        SyncDynamicObjects();
    }

    private async Task AnimateUnitViewAsync(Node2D view, Vector2 from, Vector2 to)
    {
        view.Position = from;
        var (duration, _) = AnimationTiming();
        if (duration <= 0)
        {
            view.Position = to;
            return;
        }

        var tween = CreateTween();
        tween.TweenProperty(view, "position", to, duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private async Task AnimateCameraToAsync(Vector2 destination)
    {
        var (duration, _) = AnimationTiming();
        if (duration <= 0)
        {
            SetCameraPositionPixelSnapped(destination);
            return;
        }

        var tween = CreateTween();
        tween.TweenProperty(_camera, "position", ClampCameraPosition(SnapToCameraPixelGrid(destination)), duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);
        SnapCameraToPixelGrid();
    }

    private async Task WaitAnimationPauseAsync()
    {
        var (_, pause) = AnimationTiming();
        if (pause <= 0)
        {
            return;
        }

        await ToSignal(GetTree().CreateTimer(pause), Godot.Timer.SignalName.Timeout);
    }

    private (double Duration, double Pause) AnimationTiming()
    {
        return _settings.AnimationSpeed switch
        {
            AnimationSpeedSetting.Immediate => (0.0, 0.0),
            AnimationSpeedSetting.Slow => (0.60, 0.18),
            AnimationSpeedSetting.Medium => (0.35, 0.10),
            _ => (0.20, 0.05)
        };
    }

    private void SelectNextGroupOnTile(GameState state, List<GroupState> groups)
    {
        var selectedIndex = -1;
        for (var i = 0; i < groups.Count; i++)
        {
            if (_selectedGroupId == groups[i].Id)
            {
                selectedIndex = i;
                break;
            }
        }

        SelectGroup(state, groups[(selectedIndex + 1) % groups.Count]);
    }

    private void SelectGroup(GameState state, GroupState group)
    {
        _selectedGroupId = group.Id;
        _selectedRange = [];
    }

    private void ComputeSelectedRange(GameState state)
    {
        RangeCacheKey key;

        if (_selectedGroupId is { } groupId && state.Groups.TryGetValue(groupId, out var group))
        {
            if (!(group.FactionId == state.PlayerFaction.Id && state.CurrentFaction.IsPlayer) || group.StationedCityId is not null)
            {
                _selectedRange = [];
                return;
            }

            key = new RangeCacheKey(groupId, group.Coord, group.MovementLeft, state.MapVersion);
            if (key == _rangeCacheKey)
            {
                _selectedRange = _cachedRange;
                return;
            }

            _selectedRange = GameRules.MovementRange(state, group.Coord, group.MovementLeft);
        }
        else
        {
            return;
        }

        _cachedRange = _selectedRange;
        _rangeCacheKey = key;
    }
}
