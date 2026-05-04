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
    private string _savePath = "";
    private int? _selectedStackId;
    private int? _selectedAgentId;
    private Dictionary<HexCoord, int> _selectedRange = [];

    public override void _Ready()
    {
        // Godot calls _Ready once when the scene starts.
        // We load authored JSON data, generate the starting sandbox map, then
        // build a small UI over the immediate-mode map drawing.
        var dataPath = ProjectSettings.GlobalizePath("res://data");
        var database = GameDatabase.LoadFromDirectory(dataPath);
        _state = MapGenerator.CreateSandbox(database, 42);
        _savePath = ProjectSettings.GlobalizePath("user://strategy-save.json");

        _camera = new Camera2D { Enabled = true, Position = new Vector2(470, 340), Zoom = new Vector2(0.9f, 0.9f) };
        AddChild(_camera);

        BuildUi();
        UpdatePanel();
    }

    private void ClearSelection()
    {
        _selectedStackId = null;
        _selectedAgentId = null;
        _selectedRange = [];
    }
}
