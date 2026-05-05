using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame : Node2D
{
    // This partial class is split by responsibility: startup/shared fields here,
    // input in MainGame.Input, UI construction/flow in MainGame.Ui/Flow, drawing
    // in MainGame.Drawing, and coordinate math in MainGame.HexMath.
    private const float HexSize = 28f;

    private readonly FactionDirector _director = new();
    private GameDatabase _database = null!;
    private GameState? _state;
    private Camera2D _camera = null!;
    private Control _menuRoot = null!;
    private Control _newGameRoot = null!;
    private Control _gameRoot = null!;
    private RichTextLabel _selectionInfoLabel = null!;
    private RichTextLabel _tileInfoLabel = null!;
    private RichTextLabel _logLabel = null!;
    private Label _logHeaderLabel = null!;
    private VBoxContainer _actionMenuButtons = null!;
    private Button _loadGameButton = null!;
    private Button _attachToArmyButton = null!;
    private Button _detachLeaderButton = null!;
    private Button _endTurnButton = null!;
    private Button _gearButton = null!;
    private Button _logToggleButton = null!;
    private Control _actionMenuPanel = null!;
    private Control _gearMenuPanel = null!;
    private Control _logPanel = null!;
    private Control _exitConfirmOverlay = null!;
    private Label _mapSizeValueLabel = null!;
    private Label _wetnessValueLabel = null!;
    private Label _vegetationValueLabel = null!;
    private Label _elevationValueLabel = null!;
    private Label _maxSeaValueLabel = null!;
    private Label _climateBiasValueLabel = null!;
    private HSlider _mapSizeSlider = null!;
    private HSlider _wetnessSlider = null!;
    private HSlider _vegetationSlider = null!;
    private HSlider _elevationSlider = null!;
    private HSlider _maxSeaSlider = null!;
    private HSlider _climateBiasSlider = null!;
    private string _savePath = "";

    // Selection stores either a stack id or an agent id. _selectedRange is the
    // precomputed movement map used both for highlighting and validating clicks.
    private int? _selectedStackId;
    private int? _selectedAgentId;
    private HexCoord? _inspectedTileCoord;
    private bool _isLogCollapsed;
    private Dictionary<HexCoord, double> _selectedRange = [];

    // Range cache: reuse the last Dijkstra result when the unit and map are unchanged.
    // IsStack distinguishes stack vs agent IDs (both int, different namespaces).
    private record struct RangeCacheKey(bool IsStack, int UnitId, HexCoord Coord, double MovementLeft, int MapVersion);
    private RangeCacheKey? _rangeCacheKey;
    private Dictionary<HexCoord, double> _cachedRange = [];

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
