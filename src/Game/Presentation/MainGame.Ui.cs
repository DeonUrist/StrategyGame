using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
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

        var saveGame = new Button { Text = "Save Game" };
        saveGame.Pressed += SaveGame;
        box.AddChild(saveGame);

        var loadGame = new Button { Text = "Load Game" };
        loadGame.Pressed += LoadGame;
        box.AddChild(loadGame);

        _detachLeaderButton = new Button { Text = "Detach Leader" };
        _detachLeaderButton.Pressed += DetachSelectedLeader;
        box.AddChild(_detachLeaderButton);

        _logLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        box.AddChild(_logLabel);
    }

    private void SaveGame()
    {
        _state.AddLog("Game saved.");
        GameStateSerializer.SaveToFile(_state, _savePath);
        UpdatePanel();
    }

    private void LoadGame()
    {
        if (!File.Exists(_savePath))
        {
            _state.AddLog("No saved game found.");
            UpdatePanel();
            return;
        }

        _state = GameStateSerializer.LoadFromFile(_state.Database, _savePath);
        _state.AddLog("Game loaded.");
        ClearSelection();
        UpdatePanel();
        QueueRedraw();
    }

    private void EndPlayerTurn()
    {
        if (!_state.CurrentFaction.IsPlayer)
        {
            return;
        }

        // The human player clicks once to end their turn.
        // Every AI faction then takes a full turn immediately until control comes
        // back to the player faction.
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
}
