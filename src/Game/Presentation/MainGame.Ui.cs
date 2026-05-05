using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
    private void BuildUi()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);

        _menuRoot = BuildMainMenu();
        canvas.AddChild(_menuRoot);

        _gameRoot = BuildGameHud();
        canvas.AddChild(_gameRoot);
    }

    private Control BuildMainMenu()
    {
        var panel = new PanelContainer
        {
            Position = new Vector2(24, 24),
            CustomMinimumSize = new Vector2(260, 190)
        };

        var box = new VBoxContainer();
        panel.AddChild(box);

        box.AddChild(new Label
        {
            Text = "StrategyGame",
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var newGame = new Button { Text = "New Game" };
        newGame.Pressed += NewGame;
        box.AddChild(newGame);

        _loadGameButton = new Button { Text = "Load Game" };
        _loadGameButton.Pressed += LoadGameFromMenu;
        box.AddChild(_loadGameButton);

        var exit = new Button { Text = "Exit" };
        exit.Pressed += () => GetTree().Quit();
        box.AddChild(exit);

        return panel;
    }

    private Control BuildGameHud()
    {
        var panel = new PanelContainer
        {
            Position = new Vector2(12, 12),
            CustomMinimumSize = new Vector2(340, 290)
        };

        var box = new VBoxContainer();
        panel.AddChild(box);

        _infoLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        box.AddChild(_infoLabel);

        var endTurn = new Button { Text = "End Turn" };
        endTurn.Pressed += EndPlayerTurn;
        box.AddChild(endTurn);

        var saveGame = new Button { Text = "Save" };
        saveGame.Pressed += SaveGame;
        box.AddChild(saveGame);

        var saveAndExit = new Button { Text = "Save and Exit" };
        saveAndExit.Pressed += SaveAndExitToMenu;
        box.AddChild(saveAndExit);

        _detachLeaderButton = new Button { Text = "Detach Leader" };
        _detachLeaderButton.Pressed += DetachSelectedLeader;
        box.AddChild(_detachLeaderButton);

        _logLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        box.AddChild(_logLabel);

        return panel;
    }

    private void ShowMainMenu()
    {
        _state = null;
        ClearSelection();
        _menuRoot.Visible = true;
        _gameRoot.Visible = false;
        _loadGameButton.Disabled = !File.Exists(_savePath);
        QueueRedraw();
    }

    private void ShowGame()
    {
        _menuRoot.Visible = false;
        _gameRoot.Visible = true;
        ClearSelection();
        UpdatePanel();
        QueueRedraw();
    }

    private void NewGame()
    {
        var seed = Random.Shared.Next();
        _state = MapGenerator.CreateSandbox(_database, seed);
        _state.AddLog($"New world seed {seed}.");
        ShowGame();
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

    private void SaveAndExitToMenu()
    {
        SaveGame();
        ShowMainMenu();
    }

    private void LoadGameFromMenu()
    {
        if (!File.Exists(_savePath))
        {
            _loadGameButton.Disabled = true;
            return;
        }

        _state = GameStateSerializer.LoadFromFile(_database, _savePath);
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
        GameRules.AdvanceTurn(state);
        while (!state.CurrentFaction.IsPlayer)
        {
            _director.TakeTurn(state, state.CurrentFaction.Id);
            GameRules.AdvanceTurn(state);
        }

        UpdatePanel();
        QueueRedraw();
    }

    private void DetachSelectedLeader()
    {
        if (_state is not { } state || _selectedStackId is not { } stackId)
        {
            return;
        }

        GameRules.TryDetachLeader(state, stackId);
        UpdatePanel(state.Stacks.TryGetValue(stackId, out var stack) ? state.Map.Get(stack.Coord) : null);
        QueueRedraw();
    }

    private void UpdatePanel(HexTile? tile = null)
    {
        if (_state is not { } state)
        {
            return;
        }

        var selected = _selectedStackId is { } stackId && state.Stacks.TryGetValue(stackId, out var stack)
            ? $"Selected army {stack.Id}, move {stack.MovementLeft}, strength {CombatResolver.StackStrength(state, stack)}"
              + (stack.LeaderAgentId is null ? "" : $", led by {state.Agents[stack.LeaderAgentId.Value].Name}")
            : _selectedAgentId is { } agentId && state.Agents.TryGetValue(agentId, out var agent)
                ? $"Selected agent {agent.Name}, move {agent.MovementLeft}"
                : "Select a player army or agent.";

        var tileText = tile is null ? "" : TilePanelText(tile);

        _infoLabel.Text = $"Turn {state.Turn}: {state.CurrentFaction.Name}\n{selected}{tileText}";
        _detachLeaderButton.Disabled = _selectedStackId is not { } selectedStackId
                                      || !state.Stacks.TryGetValue(selectedStackId, out var selectedStack)
                                      || selectedStack.LeaderAgentId is null;
        _logLabel.Text = string.Join('\n', state.Log.TakeLast(8).Select(e => $"T{e.Turn}: {e.Text}"));
    }

    private string TilePanelText(HexTile tile)
    {
        var state = _state ?? throw new InvalidOperationException("No active game.");
        var terrain = TerrainResolver.Resolve(state, tile);
        var featureText = tile.FeatureIds.Count == 0 ? "" : $", features: {string.Join(", ", tile.FeatureIds)}";
        var resourceText = tile.ResourceId is null ? "" : $", {state.Database.Resources[tile.ResourceId].Name}";
        var cityText = tile.CityId is null
            ? ""
            : $"\nCity: {state.Cities[tile.CityId.Value].Name}, buildings: {string.Join(", ", state.Cities[tile.CityId.Value].BuildingIds.Select(id => state.Database.Buildings[id].Name))}";

        return $"\nTile {tile.Coord.Q},{tile.Coord.R}: {terrain.Name}"
             + $"\nClimate: {tile.Climate}, rainfall: {tile.Rainfall}, elevation: {tile.Elevation}, vegetation: {tile.Vegetation}"
             + featureText
             + resourceText
             + cityText;
    }
}
