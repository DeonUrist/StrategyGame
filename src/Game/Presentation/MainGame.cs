using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame : Node2D
{
    private const float HexSize = 28f;

    private readonly FactionDirector _director = new();
    private GameDatabase _database = null!;
    private GameState? _state;
    private Camera2D _camera = null!;
    private Control _menuRoot = null!;
    private Control _gameRoot = null!;
    private Label _infoLabel = null!;
    private Label _logLabel = null!;
    private Button _loadGameButton = null!;
    private Button _detachLeaderButton = null!;
    private string _savePath = "";
    private int? _selectedStackId;
    private int? _selectedAgentId;
    private Dictionary<HexCoord, int> _selectedRange = [];

    public override void _Ready()
    {
        // Godot calls _Ready once when the scene starts.
        // We load authored JSON data, build the menu/HUD, and wait for the player
        // to start a new game or load an existing save.
        var dataPath = ProjectSettings.GlobalizePath("res://data");
        _database = GameDatabase.LoadFromDirectory(dataPath);
        _savePath = ProjectSettings.GlobalizePath("user://strategy-save.json");

        _camera = new Camera2D { Enabled = true, Position = new Vector2(470, 340), Zoom = new Vector2(0.9f, 0.9f) };
        AddChild(_camera);

        BuildUi();
        ShowMainMenu();
    }

    private void ClearSelection()
    {
        _selectedStackId = null;
        _selectedAgentId = null;
        _selectedRange = [];
    }
}
