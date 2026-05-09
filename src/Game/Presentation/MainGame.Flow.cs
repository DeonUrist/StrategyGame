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
        _isLogCollapsed = false;
        _menuRoot.Visible = true;
        _newGameRoot.Visible = false;
        _gameRoot.Visible = false;
        _actionMenuPanel.Visible = false;
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
        _menuRoot.Visible = false;
        _newGameRoot.Visible = true;
        _gameRoot.Visible = false;
        _actionMenuPanel.Visible = false;
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
        _gearMenuPanel.Visible = false;
        _actionMenuPanel.Visible = false;
        _exitConfirmOverlay.Visible = false;
        SetLogCollapsed(_isLogCollapsed);
        UpdatePanel();
        RequestFullRedraw();
    }

    private void CreateNewGameFromSetup()
    {
        var settings = new WorldGenerationSettings
        {
            MapSize = (int)_mapSizeSlider.Value,
            Wetness = (int)_wetnessSlider.Value,
            GrasslandShrublandBias = (int)_grasslandShrublandSlider.Value,
            DesertBadlandsBias = (int)_desertBadlandsSlider.Value,
            ConiferBroadleafForestBias = (int)_coniferBroadleafSlider.Value,
            ElevationVariance = (int)_elevationSlider.Value,
            MaxSeaNumber = (int)_maxSeaSlider.Value,
            ClimateBias = SliderClimateBias()
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

    private void EndPlayerTurn()
    {
        if (_state is not { } state || !state.CurrentFaction.IsPlayer)
        {
            return;
        }

        // The human player clicks once to end their turn.
        // Every AI faction then takes a full turn immediately until control comes
        // back to the player faction.
        ClearSelection();
        _gearMenuPanel.Visible = false;
        _actionMenuPanel.Visible = false;
        GameRules.AdvanceTurn(state);
        while (!state.CurrentFaction.IsPlayer)
        {
            _director.TakeTurn(state, state.CurrentFaction.Id);
            GameRules.AdvanceTurn(state);
        }

        UpdatePanel();
        SyncDynamicObjects();
    }

    private void DetachSelectedLeader()
    {
        if (_state is not { } state || _selectedStackId is not { } stackId)
        {
            return;
        }

        _gearMenuPanel.Visible = false;

        if (!state.Stacks.TryGetValue(stackId, out var stack) || stack.JoinedAgentIds.Count == 0)
        {
            return;
        }

        if (stack.JoinedAgentIds.Count == 1)
        {
            GameRules.TryDetachAgentFromStack(state, stack.JoinedAgentIds[0]);
            _actionMenuPanel.Visible = false;
        }
        else
        {
            ShowActionMenu(
                stack.JoinedAgentIds
                    .Where(state.Agents.ContainsKey)
                    .Select(id => (Label: DetachMenuLabel(state, state.Agents[id]), Action: (Action)(() =>
                    {
                        GameRules.TryDetachAgentFromStack(state, id);
                        _actionMenuPanel.Visible = false;
                        UpdatePanel(state.Map.Get(stack.Coord));
                        SyncDynamicObjects();
                    })))
                    .ToList());
            return;
        }

        UpdatePanel(state.Stacks.TryGetValue(stackId, out var updatedStack) ? state.Map.Get(updatedStack.Coord) : null);
        SyncDynamicObjects();
    }

    private void AttachSelectedAgentToArmy()
    {
        if (_state is not { } state
            || _selectedAgentId is not { } agentId
            || !state.Agents.TryGetValue(agentId, out var agent)
            || agent.FactionId != state.PlayerFaction.Id
            || agent.JoinedStackId is not null)
        {
            return;
        }

        _gearMenuPanel.Visible = false;
        var stacks = state.Map.Get(agent.Coord).StackIds
            .Select(id => state.Stacks[id])
            .Where(stack => stack.FactionId == agent.FactionId)
            .OrderBy(stack => stack.Id)
            .ToList();

        if (stacks.Count == 0)
        {
            return;
        }

        if (stacks.Count == 1)
        {
            GameRules.TryJoinAgentToStack(state, agent.Id, stacks[0].Id);
            SelectStack(state, stacks[0]);
            _actionMenuPanel.Visible = false;
            UpdatePanel(state.Map.Get(stacks[0].Coord));
            ComputeSelectedRange(state);
            SyncDynamicObjects();
            return;
        }

        ShowActionMenu(stacks.Select(stack =>
            (Label: $"Army #{stack.Id}",
             Action: (Action)(() =>
             {
                 GameRules.TryJoinAgentToStack(state, agent.Id, stack.Id);
                 SelectStack(state, stack);
                 _actionMenuPanel.Visible = false;
                 UpdatePanel(state.Map.Get(stack.Coord));
                 ComputeSelectedRange(state);
                 SyncDynamicObjects();
             }))).ToList());
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
    }

    private void ToggleGrid()
    {
        _gridVisible = !_gridVisible;
        SetGridVisible(_gridVisible);
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

    private static string DetachMenuLabel(GameState state, AgentState agent)
    {
        var unit = state.Database.Units[agent.TypeId];
        return $"{agent.Name} ({unit.Name.ToLowerInvariant()})";
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
