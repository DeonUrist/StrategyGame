using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
    private void ShowMainMenu()
    {
        // Returning to the menu clears active game state so _Draw stops rendering
        // the map and the Load button can reflect whether a save file exists.
        _state = null;
        _factionById = new Dictionary<string, FactionState>();
        ClearSelection();
        _inspectedTileCoord = null;
        _selectedRegionId = null;
        _isLogCollapsed = false;
        _menuRoot.Visible = true;
        _newGameRoot.Visible = false;
        _gameRoot.Visible = false;
        _actionMenuPanel.Visible = false;
        _optionsPanel.Visible = false;
        _loadGameButton.Disabled = !File.Exists(_savePath);
        RequestFullRedraw();
    }

    private void ShowNewGameSetup()
    {
        // New game setup is separate from generation so players can tune the
        // world before the map and starting pieces are created.
        _state = null;
        ClearSelection();
        _inspectedTileCoord = null;
        _selectedRegionId = null;
        _menuRoot.Visible = false;
        _newGameRoot.Visible = true;
        _gameRoot.Visible = false;
        _actionMenuPanel.Visible = false;
        _optionsPanel.Visible = false;
        RequestFullRedraw();
    }

    private void ShowGame()
    {
        // Entering game mode hides the menu, resets selection, then populates the
        // HUD with the current faction and default selection hint.
        _menuRoot.Visible = false;
        _newGameRoot.Visible = false;
        _gameRoot.Visible = true;
        _factionById = _state?.Factions.ToDictionary(f => f.Id) ?? [];
        ClearSelection();
        _rangeCacheKey = null;
        _cachedRange = [];
        _inspectedTileCoord = null;
        _selectedRegionId = null;
        _gearMenuPanel.Visible = false;
        _actionMenuPanel.Visible = false;
        _exitConfirmOverlay.Visible = false;
        _optionsPanel.Visible = false;
        SetLogCollapsed(_isLogCollapsed);
        UpdatePanel();
        RequestFullRedraw();
        CenterCameraOnPlayerTown();
    }

    private void CreateNewGameFromSetup()
    {
        var settings = new WorldGenerationSettings
        {
            MapSize = (int)_mapSizeSlider.Value,
            Civilizations = (int)_civilizationsSlider.Value,
            Wetness = (int)_wetnessSlider.Value,
            GrasslandShrublandBias = (int)_grasslandShrublandSlider.Value,
            DesertBadlandsBias = (int)_desertBadlandsSlider.Value,
            ConiferBroadleafForestBias = (int)_coniferBroadleafSlider.Value,
            ElevationVariance = (int)_elevationSlider.Value,
            MaxSeaNumber = (int)_maxSeaSlider.Value,
            ClimateBias = SliderClimateBias(),
            AllowedFactionIds = AllowedFactionIdsFromSetup()
        };
        var seed = Random.Shared.Next();
        _state = MapGenerator.CreateSandbox(_database, seed, settings);
        _state.AddLog($"New world seed {seed}.");
        ShowGame();
    }

    private ClimateBias SliderClimateBias()
    {
        return (int)_climateBiasSlider.Value switch
        {
            < 0 => ClimateBias.Cold,
            > 0 => ClimateBias.Hot,
            _ => ClimateBias.Normal
        };
    }

    private List<string> AllowedFactionIdsFromSetup()
    {
        return _raceAllowedChecks
            .Where(kv => kv.Value.ButtonPressed)
            .Select(kv => kv.Key)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void RefreshCivilizationLimit()
    {
        if (_civilizationsSlider is null)
        {
            return;
        }

        var allowedCount = _raceAllowedChecks.Values.Count(check => check.ButtonPressed);
        if (allowedCount <= 0 && _raceAllowedChecks.TryGetValue("humans", out var humans))
        {
            humans.ButtonPressed = true;
            allowedCount = 1;
        }

        var max = Math.Max(WorldGenerationSettings.MinCivilizations, allowedCount);
        _civilizationsSlider.MaxValue = max;
        if (_civilizationsSlider.Value > max)
        {
            _civilizationsSlider.Value = max;
        }

        _civilizationsValueLabel.Text = $"{(int)_civilizationsSlider.Value}";
    }

    private void SaveGame()
    {
        if (_state is null)
        {
            return;
        }

        _state.AddLog("Game saved.");
        GameStateSerializer.SaveToFile(_state, _savePath);
        UpdatePanel();
    }

    private void SaveGameFromMenu()
    {
        SaveGame();
        _gearMenuPanel.Visible = false;
        _actionMenuPanel.Visible = false;
    }

    private void SaveAndExitToMenu()
    {
        // SaveGame already handles null state; after saving, drop back to the
        // menu and clear the in-memory game.
        SaveGame();
        ShowMainMenu();
    }

    private void SaveAndExitFromMenu()
    {
        _gearMenuPanel.Visible = false;
        SaveAndExitToMenu();
    }

    private void LoadGameFromMenu()
    {
        // The menu disables Load when no file exists, but this guard also handles
        // stale UI state or a save being deleted while the menu is open.
        if (!File.Exists(_savePath))
        {
            _loadGameButton.Disabled = true;
            return;
        }

        try
        {
            _state = GameStateSerializer.LoadFromFile(_database, _savePath);
        }
        catch
        {
            // Corrupted or version-mismatched save — disable Load and stay on the menu.
            _loadGameButton.Disabled = true;
            return;
        }

        _state.AddLog("Game loaded.");
        ShowGame();
    }

    private async void EndPlayerTurn()
    {
        if (_state is not { } state || !state.CurrentFaction.IsPlayer || _mapInputLocked)
        {
            return;
        }

        // The human player clicks once to end their turn.
        // Every AI faction then takes a full turn immediately until control comes
        // back to the player faction.
        ClearSelection();
        _gearMenuPanel.Visible = false;
        _actionMenuPanel.Visible = false;
        _optionsPanel.Visible = false;
        _endTurnButton.Disabled = true;
        _mapInputLocked = true;
        GameRules.AdvanceTurn(state);

        try
        {
            while (!state.CurrentFaction.IsPlayer)
            {
                UpdatePanel();
                SyncDynamicObjects();
                await PlayAiTurnAsync(state, state.CurrentFaction.Id);
                GameRules.AdvanceTurn(state);
            }
        }
        finally
        {
            _mapInputLocked = false;
            _endTurnButton.Disabled = false;
        }

        UpdatePanel();
        SyncDynamicObjects();
    }

    private async Task PlayAiTurnAsync(GameState state, string factionId)
    {
        foreach (var step in _director.TakeTurnSteps(state, factionId))
        {
            UpdatePanel();
            switch (step.Kind)
            {
                case AiTurnStepKind.GroupMove:
                    await AnimateAiMoveAsync(state, step);
                    break;
                case AiTurnStepKind.CityUpgrade:
                    SyncDynamicObjects();
                    await WaitAnimationPauseAsync();
                    break;
            }
        }
    }

    private async Task AnimateAiMoveAsync(GameState state, AiTurnStep step)
    {
        if (step.Origin is not { } origin || step.Destination is not { } destination)
        {
            SyncDynamicObjects();
            return;
        }

        var visible = _settings.AnimationSpeed != AnimationSpeedSetting.Immediate
                      && VisibilityRules.IsMoveVisibleToPlayer(state, origin, destination);
        if (visible)
        {
            await AnimateCameraToAsync(HexToPixel(destination));
        }

        Task unitTask = Task.CompletedTask;

        if (step.Kind == AiTurnStepKind.GroupMove)
        {
            if (step.PieceSurvived)
            {
                SyncUnitObjects(movingGroupId: step.PieceId);
            }

            if (_groupViews.TryGetValue(step.PieceId, out var groupView))
            {
                unitTask = AnimateUnitViewAsync(groupView, HexToPixel(origin), HexToPixel(destination));
            }
        }

        await unitTask;
        SyncDynamicObjects();
        await WaitAnimationPauseAsync();
    }

    private void StationSelectedGroup()
    {
        if (_state is not { } state || _selectedGroupId is not { } groupId || !state.Groups.TryGetValue(groupId, out var group))
        {
            return;
        }

        var tile = state.Map.Get(group.Coord);
        if (tile.CityId is not { } cityId)
        {
            return;
        }

        ShowUnitSelectionMenu(group.Units, "Station", selectedUnitIds =>
        {
            var stationed = GameRules.GetCityGarrison(state, cityId) is not null
                ? GameRules.TryStationUnits(state, group.Id, cityId, selectedUnitIds)
                : StationIntoEmptyGarrison(state, group, cityId, selectedUnitIds);
            if (!stationed)
            {
                return;
            }

            if (!state.Groups.ContainsKey(group.Id))
            {
                ClearSelection();
            }

            _actionMenuPanel.Visible = false;
            UpdatePanel(tile);
            SyncDynamicObjects();
        });
    }

    private static bool StationIntoEmptyGarrison(GameState state, GroupState source, int cityId, IReadOnlyCollection<int> selectedUnitIds)
    {
        if (selectedUnitIds.Count == source.Units.Count)
        {
            return GameRules.TryStationGroup(state, source.Id, cityId);
        }

        var splitId = GameRules.TrySplitGroup(state, source.Id, selectedUnitIds);
        return splitId is { } groupId && GameRules.TryStationGroup(state, groupId, cityId);
    }

    private void DeployStationedGroup()
    {
        if (_state is not { } state || _inspectedTileCoord is not { } coord || !state.Map.TryGet(coord, out var tile) || tile.CityId is not { } cityId)
        {
            return;
        }

        if (GameRules.GetCityGarrison(state, cityId) is not { } garrison)
        {
            return;
        }

        ShowUnitSelectionMenu(garrison.Units, "Deploy", selectedUnitIds =>
        {
            var deployedId = GameRules.TryDeployUnits(state, cityId, selectedUnitIds);
            if (deployedId is { } groupId && state.Groups.TryGetValue(groupId, out var deployed))
            {
                SelectGroup(state, deployed);
                ComputeSelectedRange(state);
            }

            _actionMenuPanel.Visible = false;
            UpdatePanel(tile);
            SyncDynamicObjects();
        });
    }

    private void TransferUnitsToSelectedGroup()
    {
        if (_state is not { } state || _selectedGroupId is not { } groupId || !state.Groups.TryGetValue(groupId, out var selected))
        {
            return;
        }

        var groups = state.Map.Get(selected.Coord).GroupIds
            .Where(id => id != selected.Id)
            .Select(id => state.Groups[id])
            .Where(group => group.FactionId == selected.FactionId && group.StationedCityId is null)
            .OrderBy(group => group.Id)
            .ToList();

        ShowActionMenu(groups.Select(group =>
            (Label: GroupMenuLabel(state, group),
             Action: (Action)(() => ShowTwoWayTransferMenu(state, selected, group)))).ToList());
    }

    private void ShowTwoWayTransferMenu(GameState state, GroupState leftGroup, GroupState rightGroup)
    {
        foreach (var child in _actionMenuButtons.GetChildren())
        {
            child.QueueFree();
        }

        var header = new HBoxContainer();
        _actionMenuButtons.AddChild(header);
        header.AddChild(new Label
        {
            Text = GroupMenuLabel(state, leftGroup),
            CustomMinimumSize = new Vector2(170, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        });
        header.AddChild(new Label
        {
            Text = "",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        });
        header.AddChild(new Label
        {
            Text = GroupMenuLabel(state, rightGroup),
            CustomMinimumSize = new Vector2(170, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        });
        _actionMenuButtons.AddChild(new HSeparator());

        var sliders = new Dictionary<string, HSlider>();
        var leftCounts = UnitCounts(leftGroup.Units);
        var rightCounts = UnitCounts(rightGroup.Units);
        var typeIds = leftCounts.Keys
            .Concat(rightCounts.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(typeId => state.Database.Units[typeId].Name)
            .ToList();

        foreach (var typeId in typeIds)
        {
            var leftCount = leftCounts.GetValueOrDefault(typeId);
            var rightCount = rightCounts.GetValueOrDefault(typeId);
            var totalCount = leftCount + rightCount;
            var unitName = state.Database.Units[typeId].Name;
            var row = new HBoxContainer();
            _actionMenuButtons.AddChild(row);

            var leftLabel = new Label
            {
                Text = $"{unitName} {leftCount}",
                CustomMinimumSize = new Vector2(125, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            row.AddChild(leftLabel);

            var slider = new HSlider
            {
                MinValue = 0,
                MaxValue = totalCount,
                Step = 1,
                Value = rightCount,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            var rightLabel = new Label
            {
                Text = $"{rightCount} {unitName}",
                CustomMinimumSize = new Vector2(125, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            slider.ValueChanged += value =>
            {
                var nextLeftCount = totalCount - (int)value;
                leftLabel.Text = $"{unitName} {nextLeftCount}";
                rightLabel.Text = $"{totalCount - nextLeftCount} {unitName}";
            };
            row.AddChild(slider);
            row.AddChild(rightLabel);
            sliders[typeId] = slider;
        }

        var buttons = new HBoxContainer();
        _actionMenuButtons.AddChild(buttons);

        var transfer = new Button { Text = "Transfer" };
        ApplyButtonChrome(transfer);
        transfer.Pressed += () =>
        {
            var rightToLeft = SelectAllocationTransferUnits(rightGroup.Units, sliders, leftCounts, toLeft: true);
            var leftToRight = SelectAllocationTransferUnits(leftGroup.Units, sliders, leftCounts, toLeft: false);
            var changed = false;
            if (rightToLeft.Count > 0)
            {
                changed |= GameRules.TryTransferUnits(state, rightGroup.Id, leftGroup.Id, rightToLeft);
            }

            if (leftToRight.Count > 0 && state.Groups.ContainsKey(leftGroup.Id) && state.Groups.ContainsKey(rightGroup.Id))
            {
                changed |= GameRules.TryTransferUnits(state, leftGroup.Id, rightGroup.Id, leftToRight);
            }

            if (!changed)
            {
                return;
            }

            if (state.Groups.ContainsKey(leftGroup.Id))
            {
                SelectGroup(state, leftGroup);
            }
            else if (state.Groups.ContainsKey(rightGroup.Id))
            {
                SelectGroup(state, rightGroup);
            }
            else
            {
                ClearSelection();
            }

            _actionMenuPanel.Visible = false;
            UpdatePanel(state.Map.Get(rightGroup.Coord));
            ComputeSelectedRange(state);
            SyncDynamicObjects();
        };
        buttons.AddChild(transfer);

        var cancel = new Button { Text = "Cancel" };
        ApplyButtonChrome(cancel);
        cancel.Pressed += () => _actionMenuPanel.Visible = false;
        buttons.AddChild(cancel);

        _actionMenuPanel.Visible = typeIds.Count > 0;
    }

    private void SplitSelectedGroup()
    {
        if (_state is not { } state || _selectedGroupId is not { } groupId || !state.Groups.TryGetValue(groupId, out var selected))
        {
            return;
        }

        ShowUnitSelectionMenu(selected.Units, "Split", selectedUnitIds =>
        {
            var createdId = GameRules.TrySplitGroup(state, selected.Id, selectedUnitIds);
            if (createdId is { } newGroupId && state.Groups.TryGetValue(newGroupId, out var created))
            {
                SelectGroup(state, created);
            }

            _actionMenuPanel.Visible = false;
            UpdatePanel(state.Map.Get(selected.Coord));
            ComputeSelectedRange(state);
            SyncDynamicObjects();
        });
    }

    private void RenameSelectedGroup()
    {
        if (_state is not { } state || _selectedGroupId is not { } groupId || !state.Groups.TryGetValue(groupId, out var group))
        {
            return;
        }

        foreach (var child in _actionMenuButtons.GetChildren())
        {
            child.QueueFree();
        }

        var input = new LineEdit
        {
            Text = group.Name,
            PlaceholderText = "Group name",
            CustomMinimumSize = new Vector2(260, 32)
        };
        _actionMenuButtons.AddChild(input);

        var buttons = new HBoxContainer();
        _actionMenuButtons.AddChild(buttons);

        var save = new Button { Text = "Rename" };
        ApplyButtonChrome(save);
        save.Pressed += () =>
        {
            GameRules.TryRenameGroup(state, group.Id, input.Text);
            _actionMenuPanel.Visible = false;
            UpdatePanel(state.Map.Get(group.Coord));
            SyncDynamicObjects();
        };
        buttons.AddChild(save);

        var cancel = new Button { Text = "Cancel" };
        ApplyButtonChrome(cancel);
        cancel.Pressed += () => _actionMenuPanel.Visible = false;
        buttons.AddChild(cancel);

        _actionMenuPanel.Visible = true;
        input.GrabFocus();
        input.SelectAll();
    }

    private void ToggleGearMenu()
    {
        if (_state is null)
        {
            return;
        }

        _gearMenuPanel.Visible = !_gearMenuPanel.Visible;
        _actionMenuPanel.Visible = false;
        _exitConfirmOverlay.Visible = false;
        _optionsPanel.Visible = false;
    }

    private void SavePresentationSettings()
    {
        ApplyAudioSettings();
        _settings.Save(_settingsPath);
    }

    private void ToggleGridVisibilityPreference()
    {
        SetGridVisibilityPreference(!_gridVisible);
    }

    private void SetGridVisibilityPreference(bool visible)
    {
        _settings.GridVisible = visible;
        _gridVisible = visible;
        if (_gridVisibleCheckBox is not null && _gridVisibleCheckBox.ButtonPressed != visible)
        {
            _gridVisibleCheckBox.ButtonPressed = visible;
        }

        SavePresentationSettings();
        SetGridVisible(visible);
    }

    private void ToggleResourceIconsVisibilityPreference()
    {
        SetResourceIconsVisibilityPreference(!_resourceIconsVisible);
    }

    private void SetResourceIconsVisibilityPreference(bool visible)
    {
        _settings.ResourceIconsVisible = visible;
        _resourceIconsVisible = visible;
        if (_resourceIconsVisibleCheckBox is not null && _resourceIconsVisibleCheckBox.ButtonPressed != visible)
        {
            _resourceIconsVisibleCheckBox.ButtonPressed = visible;
        }

        SavePresentationSettings();
        SetResourceIconsVisible(visible);
    }

    private void ApplyAudioSettings()
    {
        ApplyAudioBusVolume("Music", _settings.MusicVolume);
        ApplyAudioBusVolume("Effects", _settings.EffectsVolume);
    }

    private static void ApplyAudioBusVolume(string busName, int volume)
    {
        var bus = AudioServer.GetBusIndex(busName);
        if (bus < 0)
        {
            return;
        }

        var linear = Mathf.Clamp(volume / 100f, 0.0001f, 1f);
        AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(linear));
        AudioServer.SetBusMute(bus, volume <= 0);
    }

    private void ShowOptionsPanel()
    {
        _gearMenuPanel.Visible = false;
        _actionMenuPanel.Visible = false;
        _exitConfirmOverlay.Visible = false;
        _optionsPanel.Visible = true;
        _capturingKeyBinding = null;
        RefreshControlBindingButtons();
    }

    private void HideOptionsPanel()
    {
        _capturingKeyBinding = null;
        RefreshControlBindingButtons();
        _optionsPanel.Visible = false;
    }

    private void PromptExitWithoutSave()
    {
        if (_state is null)
        {
            return;
        }

        _gearMenuPanel.Visible = false;
        _actionMenuPanel.Visible = false;
        _exitConfirmOverlay.Visible = true;
    }

    private void ConfirmExitWithoutSave()
    {
        _exitConfirmOverlay.Visible = false;
        _actionMenuPanel.Visible = false;
        ShowMainMenu();
    }

    private void CancelExitWithoutSave()
    {
        _exitConfirmOverlay.Visible = false;
    }

    private void ShowActionMenu(List<(string Label, Action Action)> actions)
    {
        foreach (var child in _actionMenuButtons.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var item in actions)
        {
            var button = new Button { Text = item.Label };
            ApplyButtonChrome(button);
            button.Pressed += item.Action;
            _actionMenuButtons.AddChild(button);
        }

        _actionMenuPanel.Visible = actions.Count > 0;
    }

    private void ShowUnitSelectionMenu(
        IReadOnlyCollection<UnitInstance> units,
        string confirmText,
        Action<IReadOnlyCollection<int>> onConfirm,
        IReadOnlyCollection<UnitInstance>? destinationUnits = null)
    {
        foreach (var child in _actionMenuButtons.GetChildren())
        {
            child.QueueFree();
        }

        var sliders = new Dictionary<string, HSlider>();
        var destinationCounts = destinationUnits?
            .GroupBy(unit => unit.TypeId)
            .ToDictionary(group => group.Key, group => group.Count())
            ?? new Dictionary<string, int>();
        var typeIds = units
            .Select(unit => unit.TypeId)
            .Concat(destinationCounts.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(stateUnitName)
            .ToList();

        foreach (var typeId in typeIds)
        {
            var max = units.Count(unit => string.Equals(unit.TypeId, typeId, StringComparison.OrdinalIgnoreCase));
            var row = new HBoxContainer();
            _actionMenuButtons.AddChild(row);

            row.AddChild(new Label
            {
                Text = stateUnitName(typeId),
                CustomMinimumSize = new Vector2(86, 0)
            });

            var destinationLabel = new Label
            {
                Text = destinationCounts.TryGetValue(typeId, out var destinationCount) ? $"{destinationCount}" : "0",
                CustomMinimumSize = new Vector2(28, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                TooltipText = "Current group"
            };
            row.AddChild(destinationLabel);

            var valueLabel = new Label
            {
                Text = $"0/{max}",
                CustomMinimumSize = new Vector2(42, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var slider = new HSlider
            {
                MinValue = 0,
                MaxValue = max,
                Step = 1,
                Value = 0,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            slider.Editable = max > 0;
            slider.ValueChanged += value => valueLabel.Text = $"{(int)value}/{max}";
            row.AddChild(slider);
            row.AddChild(valueLabel);
            sliders[typeId] = slider;
        }

        var buttons = new HBoxContainer();
        _actionMenuButtons.AddChild(buttons);

        var confirm = new Button { Text = confirmText };
        ApplyButtonChrome(confirm);
        confirm.Pressed += () =>
        {
            var selected = SelectUnitsByCounts(units, sliders.ToDictionary(kv => kv.Key, kv => (int)kv.Value.Value));
            if (selected.Count == 0)
            {
                return;
            }

            onConfirm(selected);
        };
        buttons.AddChild(confirm);

        var cancel = new Button { Text = "Cancel" };
        ApplyButtonChrome(cancel);
        cancel.Pressed += () => _actionMenuPanel.Visible = false;
        buttons.AddChild(cancel);

        _actionMenuPanel.Visible = typeIds.Count > 0;

        string stateUnitName(string typeId)
        {
            return _state?.Database.Units[typeId].Name ?? typeId;
        }
    }

    private static List<int> SelectUnitsByCounts(IEnumerable<UnitInstance> units, Dictionary<string, int> countsByType)
    {
        var selected = new List<int>();
        foreach (var group in units.GroupBy(unit => unit.TypeId))
        {
            var count = countsByType.GetValueOrDefault(group.Key);
            selected.AddRange(group.Take(count).Select(unit => unit.Id));
        }

        return selected;
    }

    private static Dictionary<string, int> UnitCounts(IEnumerable<UnitInstance> units)
    {
        return units
            .GroupBy(unit => unit.TypeId)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private static List<int> SelectAllocationTransferUnits(
        IEnumerable<UnitInstance> units,
        Dictionary<string, HSlider> sliders,
        Dictionary<string, int> originalLeftCounts,
        bool toLeft)
    {
        var selected = new List<int>();
        foreach (var group in units.GroupBy(unit => unit.TypeId))
        {
            if (!sliders.TryGetValue(group.Key, out var slider))
            {
                continue;
            }

            var desiredLeftCount = (int)(slider.MaxValue - slider.Value);
            var originalLeftCount = originalLeftCounts.GetValueOrDefault(group.Key);
            var count = toLeft
                ? Math.Max(0, desiredLeftCount - originalLeftCount)
                : Math.Max(0, originalLeftCount - desiredLeftCount);
            selected.AddRange(group.Take(count).Select(unit => unit.Id));
        }

        return selected;
    }

    private static string UnitMenuLabel(GameState state, UnitInstance unit)
    {
        var definition = state.Database.Units[unit.TypeId];
        return unit.Name is null ? definition.Name : $"{unit.Name} ({definition.Name.ToLowerInvariant()})";
    }

    private static string GroupMenuLabel(GameState state, GroupState group)
    {
        return $"{GameRules.GroupDisplayName(state, group)} ({group.Units.Count} unit{(group.Units.Count == 1 ? "" : "s")})";
    }

    private void CollapseLog()
    {
        SetLogCollapsed(true);
    }

    private void ExpandLog()
    {
        SetLogCollapsed(false);
    }

    private void SetLogCollapsed(bool collapsed)
    {
        _isLogCollapsed = collapsed;
        if (_logPanel is not null)
        {
            _logPanel.Visible = !collapsed;
        }

        if (_logToggleButton is not null)
        {
            _logToggleButton.Visible = collapsed;
        }
    }
}
