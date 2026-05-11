using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame : Node2D
{
    // This partial class is split by responsibility: startup/shared fields here,
    // input in MainGame.Input, UI construction/flow in MainGame.Ui/Flow, map
    // node sync in MainGame.Layers, and coordinate math in MainGame.HexMath.
    private const float HexSize = 28f;
    private const float DefaultCameraZoom = 1.5f;
    private static readonly float[] CameraZoomSteps = [1.0f, 1.5f, 2.0f, 2.5f, 3.0f, 4.0f, 5.0f];
    private const float MinCameraZoom = 1.0f;
    private const float MaxCameraZoom = 5.0f;

    private readonly FactionDirector _director = new();
    private GameDatabase _database = null!;
    private GameState? _state;
    private Camera2D _camera = null!;
    private Sprite2D? _backgroundSprite;
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
    private Control _optionsPanel = null!;
    private Button _openMenuSecondaryButton = null!;
    private Button _recenterPrimaryButton = null!;
    private Button _recenterSecondaryButton = null!;
    private Label _mapSizeValueLabel = null!;
    private Label _civilizationsValueLabel = null!;
    private Label _wetnessValueLabel = null!;
    private Label _grasslandShrublandValueLabel = null!;
    private Label _desertBadlandsValueLabel = null!;
    private Label _coniferBroadleafValueLabel = null!;
    private Label _elevationValueLabel = null!;
    private Label _maxSeaValueLabel = null!;
    private Label _climateBiasValueLabel = null!;
    private HSlider _mapSizeSlider = null!;
    private HSlider _civilizationsSlider = null!;
    private HSlider _wetnessSlider = null!;
    private HSlider _grasslandShrublandSlider = null!;
    private HSlider _desertBadlandsSlider = null!;
    private HSlider _coniferBroadleafSlider = null!;
    private HSlider _elevationSlider = null!;
    private HSlider _maxSeaSlider = null!;
    private HSlider _climateBiasSlider = null!;
    private string _savePath = "";
    private string _settingsPath = "";
    private PresentationSettings _settings = new();

    // Selection stores either a stack id or an agent id. _selectedRange is the
    // precomputed movement map used both for highlighting and validating clicks.
    private int? _selectedStackId;
    private int? _selectedAgentId;
    private HexCoord? _inspectedTileCoord;
    private bool _isLogCollapsed;
    private bool _gridVisible = true;
    private Dictionary<HexCoord, double> _selectedRange = [];
    private bool _mapInputLocked;
    private string? _capturingKeyBinding;

    // Range cache: reuse the last Dijkstra result when the unit and map are unchanged.
    // IsStack distinguishes stack vs agent IDs (both int, different namespaces).
    private record struct RangeCacheKey(bool IsStack, int UnitId, HexCoord Coord, double MovementLeft, int MapVersion);
    private RangeCacheKey? _rangeCacheKey;
    private Dictionary<HexCoord, double> _cachedRange = [];

    // Faction lookup rebuilt whenever _state changes; avoids O(n) First() in the draw hot path.
    private Dictionary<string, FactionState> _factionById = [];

    public override void _Ready()
    {
        // Godot calls _Ready once when the scene starts.
        // We load authored JSON data, build the menu/HUD, and wait for the player
        // to start a new game or load an existing save.
        var dataPath = ProjectSettings.GlobalizePath("res://data");
        _database = GameDatabase.LoadFromDirectory(dataPath);
        _savePath = ProjectSettings.GlobalizePath("user://strategy-save.json");
        _settingsPath = ProjectSettings.GlobalizePath("user://settings.json");
        _settings = PresentationSettings.Load(_settingsPath);
        _gridVisible = _settings.GridVisible;
        ApplyAudioSettings();

        _camera = new Camera2D { Enabled = true, Position = new Vector2(470, 340), Zoom = new Vector2(DefaultCameraZoom, DefaultCameraZoom) };
        AddChild(_camera);

        InitDrawLayers();
        BuildUi();
        ShowMainMenu();
    }

    private void ClearSelection()
    {
        _selectedStackId = null;
        _selectedAgentId = null;
        _selectedRange = [];
        UpdateSelectionHighlights();
    }
}
