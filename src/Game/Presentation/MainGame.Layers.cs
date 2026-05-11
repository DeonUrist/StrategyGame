using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
    private Node2D _terrainRoot = null!;
    private Node2D _featuresRoot = null!;
    private Node2D _resourcesRoot = null!;
    private Node2D _locationsRoot = null!;
    private Node2D _unitsRoot = null!;

    private readonly Dictionary<HexCoord, TileView> _tileViews = [];
    private readonly Dictionary<int, Node2D> _stackViews = [];
    private readonly Dictionary<int, Node2D> _agentViews = [];
    private readonly Dictionary<string, Texture2D> _textureCache = [];
    private int? _renderedMapVersion;

    private void InitDrawLayers()
    {
        _terrainRoot = GetNodeOrCreateLayer("Terrain");
        _featuresRoot = GetNodeOrCreateLayer("Features");
        _resourcesRoot = GetNodeOrCreateLayer("Resources");
        _locationsRoot = GetNodeOrCreateLayer("Locations");
        _unitsRoot = GetNodeOrCreateLayer("Units");
    }

    private void RequestFullRedraw()
    {
        _mapInputLocked = false;
        if (_state is null)
        {
            ClearLayer(_terrainRoot);
            ClearLayer(_featuresRoot);
            ClearLayer(_resourcesRoot);
            ClearLayer(_locationsRoot);
            ClearLayer(_unitsRoot);
            _tileViews.Clear();
            _stackViews.Clear();
            _agentViews.Clear();
            _renderedMapVersion = null;
            return;
        }

        SyncTerrainObjects(force: true);
        SyncDynamicObjects();
    }

    private Node2D GetNodeOrCreateLayer(string name)
    {
        if (GetNodeOrNull<Node2D>(name) is { } existing)
        {
            return existing;
        }

        var layer = new Node2D { Name = name };
        AddChild(layer);
        MoveChild(layer, GetChildCount() - 1);
        return layer;
    }

    private static void ClearLayer(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            child.QueueFree();
        }
    }

    private void SyncTerrainObjects(bool force = false)
    {
        if (_state is not { } state)
        {
            return;
        }

        if (!force && _renderedMapVersion == state.MapVersion)
        {
            return;
        }

        ClearLayer(_terrainRoot);
        _tileViews.Clear();

        foreach (var tile in state.Map.Tiles.OrderBy(t => t.Coord.R).ThenBy(t => t.Coord.Q))
        {
            var view = new TileView
            {
                Name = $"Tile_{tile.Coord.Q}_{tile.Coord.R}",
                Coord = tile.Coord,
                Position = HexToPixel(tile.Coord)
            };
            view.Setup(
                LoadTexture(TerrainSpritePath(state, tile)),
                TileTextureHexCornerOffsets,
                _gridVisible,
                coord => HandleHexLeftClick(coord),
                coord => HandleHexRightClick(coord),
                () => _mapInputLocked);
            _terrainRoot.AddChild(view);
            _tileViews[tile.Coord] = view;
        }

        _renderedMapVersion = state.MapVersion;
        UpdateSelectionHighlights();
    }

    private void SyncDynamicObjects()
    {
        SyncTerrainObjects();
        SyncFeatureObjects();
        SyncResourceObjects();
        SyncLocationObjects();
        SyncUnitObjects();
        UpdateSelectionHighlights();
    }

    private void SyncFeatureObjects()
    {
        if (_state is not { } state)
        {
            return;
        }

        ClearLayer(_featuresRoot);
        foreach (var tile in state.Map.Tiles.Where(t => t.FeatureIds.Contains("volcano", StringComparer.OrdinalIgnoreCase)))
        {
            _featuresRoot.AddChild(CreateMapSprite(
                $"Feature_volcano_{tile.Coord.Q}_{tile.Coord.R}",
                "res://assets/image/features/volcano.png",
                HexContentToPixel(tile.Coord) + new Vector2(0, -10),
                0.78f));
        }
    }

    private void SyncResourceObjects()
    {
        if (_state is not { } state)
        {
            return;
        }

        ClearLayer(_resourcesRoot);
        foreach (var tile in state.Map.Tiles.Where(t => t.ResourceId is not null))
        {
            var resourceId = tile.ResourceId!;
            _resourcesRoot.AddChild(CreateMapSprite(
                $"Resource_{resourceId}_{tile.Coord.Q}_{tile.Coord.R}",
                $"res://assets/image/resources/{NormalizeAssetKey(resourceId)}.png",
                HexContentToPixel(tile.Coord) + new Vector2(10, -9),
                0.42f));
        }
    }

    private void SyncLocationObjects()
    {
        if (_state is not { } state)
        {
            return;
        }

        ClearLayer(_locationsRoot);
        foreach (var city in state.Cities.Values.OrderBy(c => c.Id))
        {
            var townCenter = SettlementProgression.CurrentTownCenter(state, city);
            _locationsRoot.AddChild(CreateTileAlignedSprite(
                $"Location_{city.Id}",
                $"res://assets/image/locations/{NormalizeAssetKey(townCenter.Sprite)}.png",
                HexToPixel(city.Coord)));
            _locationsRoot.AddChild(CreateSettlementLabel(state, city, HexContentToPixel(city.Coord) + new Vector2(0, 8)));
        }
    }

    private Node CreateSettlementLabel(GameState state, CityState city, Vector2 position)
    {
        var labelText = SettlementProgression.DisplayName(state, city);
        var faction = _factionById.TryGetValue(city.FactionId, out var foundFaction)
            ? foundFaction
            : state.GetFaction(city.FactionId);
        const float labelRenderScale = 2f;
        var width = Mathf.Clamp(labelText.Length * 5.5f + 6f, 24f, TileTextureWidth);
        var height = 12f;
        var snappedPosition = new Vector2(
            MathF.Round(position.X - width / 2f),
            MathF.Round(position.Y));
        var panel = new PanelContainer
        {
            Name = $"LocationLabel_{city.Id}",
            Position = snappedPosition,
            CustomMinimumSize = new Vector2(width * labelRenderScale, height * labelRenderScale),
            Scale = new Vector2(1f / labelRenderScale, 1f / labelRenderScale),
            ZIndex = 20
        };
        var style = new StyleBoxFlat
        {
            BgColor = Colors.White,
            BorderColor = new Color(faction.Color),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            ContentMarginLeft = 4,
            ContentMarginTop = 0,
            ContentMarginRight = 4,
            ContentMarginBottom = 0
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var label = new Label
        {
            Text = labelText,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ClipText = true,
            CustomMinimumSize = new Vector2(width * labelRenderScale, (height - 2) * labelRenderScale)
        };
        label.AddThemeColorOverride("font_color", Colors.Black);
        label.AddThemeFontSizeOverride("font_size", 18);
        label.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
        panel.AddChild(label);

        return panel;
    }

    private void SyncUnitObjects(int? movingStackId = null, int? movingAgentId = null)
    {
        if (_state is not { } state)
        {
            return;
        }

        var expectedStacks = new HashSet<int>(state.Stacks.Keys);
        foreach (var staleId in _stackViews.Keys.Where(id => !expectedStacks.Contains(id)).ToList())
        {
            _stackViews[staleId].QueueFree();
            _stackViews.Remove(staleId);
        }

        var looseAgentIds = state.Agents.Values.Where(a => a.JoinedStackId is null).Select(a => a.Id).ToHashSet();
        foreach (var staleId in _agentViews.Keys.Where(id => !looseAgentIds.Contains(id)).ToList())
        {
            _agentViews[staleId].QueueFree();
            _agentViews.Remove(staleId);
        }

        foreach (var tile in state.Map.Tiles)
        {
            var stacks = tile.StackIds
                .Where(state.Stacks.ContainsKey)
                .Select(id => state.Stacks[id])
                .OrderBy(s => s.Id)
                .ToList();
            for (var i = 0; i < stacks.Count; i++)
            {
                var stack = stacks[i];
                var view = GetOrCreateUnitView(_stackViews, stack.Id, $"Stack_{stack.Id}", "res://assets/image/units/army.png");
                if (movingStackId != stack.Id)
                {
                    view.Position = HexToPixel(stack.Coord) + PieceOffset(i, stacks.Count);
                }
            }

            var agents = tile.AgentIds
                .Where(state.Agents.ContainsKey)
                .Select(id => state.Agents[id])
                .Where(a => a.JoinedStackId is null)
                .OrderBy(a => a.Id)
                .ToList();
            for (var i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                var view = GetOrCreateUnitView(_agentViews, agent.Id, $"Agent_{agent.Id}", "res://assets/image/units/agent.png");
                if (movingAgentId != agent.Id)
                {
                    view.Position = HexToPixel(agent.Coord) + PieceOffset(i, agents.Count);
                }
            }
        }
    }

    private Node2D GetOrCreateUnitView(Dictionary<int, Node2D> views, int id, string nodeName, string path)
    {
        if (views.TryGetValue(id, out var existing))
        {
            return existing;
        }

        var view = CreateTileAlignedSprite(nodeName, path, Vector2.Zero);
        _unitsRoot.AddChild(view);
        views[id] = view;
        return view;
    }

    private Sprite2D CreateMapSprite(string nodeName, string texturePath, Vector2 position, float targetScale)
    {
        var sprite = new Sprite2D
        {
            Name = nodeName,
            Texture = LoadTexture(texturePath),
            Position = position,
            ZAsRelative = false
        };
        ApplySpriteScale(sprite, targetScale);
        return sprite;
    }

    private Sprite2D CreateTileAlignedSprite(string nodeName, string texturePath, Vector2 position)
    {
        var sprite = new Sprite2D
        {
            Name = nodeName,
            Texture = LoadTexture(texturePath),
            Position = position,
            ZAsRelative = false
        };
        ApplyTileSpriteScale(sprite);
        return sprite;
    }

    private Texture2D LoadTexture(string path)
    {
        if (_textureCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var texture = ResourceLoader.Exists(path)
            ? ResourceLoader.Load<Texture2D>(path)
            : ResourceLoader.Load<Texture2D>("res://assets/image/terrain/tile.png");
        _textureCache[path] = texture;
        return texture;
    }

    private static void ApplySpriteScale(Sprite2D sprite, float hexWidthFactor)
    {
        if (sprite.Texture is null)
        {
            return;
        }

        var size = sprite.Texture.GetSize();
        var longest = MathF.Max(size.X, size.Y);
        if (longest <= 0)
        {
            return;
        }

        var scale = HexSize * 2f * hexWidthFactor / longest;
        sprite.Scale = new Vector2(scale, scale);
    }

    private static void ApplyTileSpriteScale(Sprite2D sprite)
    {
        if (sprite.Texture is null)
        {
            return;
        }

        var size = sprite.Texture.GetSize();
        var longest = MathF.Max(size.X, size.Y);
        if (longest <= 0)
        {
            return;
        }

        var scale = TileTextureWidth / longest;
        sprite.Scale = new Vector2(scale, scale);
    }

    private void UpdateSelectionHighlights()
    {
        if (_tileViews.Count == 0)
        {
            return;
        }

        foreach (var (coord, view) in _tileViews)
        {
            view.SetHighlighted(_selectedRange.ContainsKey(coord));
        }
    }

    private void SetGridVisible(bool visible)
    {
        foreach (var view in _tileViews.Values)
        {
            view.SetGridVisible(visible);
        }
    }

    private static Vector2 PieceOffset(int index, int count)
    {
        return count <= 1 ? Vector2.Zero : new Vector2((index - (count - 1) / 2f) * 5f, 0);
    }

    private static string TerrainSpritePath(GameState state, HexTile tile)
    {
        var key = TerrainSpriteKey(state, tile);
        var path = $"res://assets/image/terrain/{key}.png";
        return ResourceLoader.Exists(path) ? path : "res://assets/image/terrain/tile.png";
    }

    private static string TerrainSpriteKey(GameState state, HexTile tile)
    {
        if (tile.Elevation == Elevation.Coast)
        {
            return "coast";
        }

        if (tile.Elevation == Elevation.DeepIce)
        {
            return "ocean_ice_sheet";
        }

        if (tile.Elevation == Elevation.Ocean)
        {
            return tile.WaterBodyKind switch
            {
                WaterBodyKind.Lake => "lake",
                WaterBodyKind.Sea => "sea",
                _ => "ocean"
            };
        }

        var biome = TerrainAssetKey(TerrainResolver.Resolve(state, tile).Name);
        return tile.Elevation switch
        {
            Elevation.Hills => $"{biome}_hills",
            Elevation.Mountains => $"{biome}_mountains",
            Elevation.Peaks => $"{biome}_peaks",
            _ => biome
        };
    }

    private static string NormalizeAssetKey(string value)
    {
        return value.Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_");
    }

    private static string TerrainAssetKey(string terrainName)
    {
        return NormalizeAssetKey(terrainName);
    }
}

internal sealed partial class TileView : Area2D
{
    private Line2D _border = null!;
    private Polygon2D _highlight = null!;
    private Action<HexCoord>? _leftClick;
    private Action<HexCoord>? _rightClick;
    private Func<bool>? _inputLocked;

    public HexCoord Coord { get; set; }

    public void Setup(Texture2D texture, Vector2[] corners, bool gridVisible, Action<HexCoord> leftClick, Action<HexCoord> rightClick, Func<bool> inputLocked)
    {
        _leftClick = leftClick;
        _rightClick = rightClick;
        _inputLocked = inputLocked;
        InputPickable = true;

        var sprite = new Sprite2D
        {
            Texture = texture,
            TextureFilter = TextureFilterEnum.Nearest,
            ZIndex = 0
        };
        if (texture is not null)
        {
            var size = texture.GetSize();
            var longest = MathF.Max(size.X, size.Y);
            if (longest > 0)
            {
                var scale = MainGame.TileTextureWidth / longest;
                sprite.Scale = new Vector2(scale, scale);
            }
        }
        AddChild(sprite);

        _border = new Line2D
        {
            Points = corners.Concat([corners[0]]).ToArray(),
            Width = 1.0f,
            DefaultColor = new Color(0, 0, 0, 0.32f),
            ZIndex = 1,
            Visible = gridVisible
        };
        AddChild(_border);

        _highlight = new Polygon2D
        {
            Polygon = corners,
            Color = new Color(1f, 1f, 1f, 0.25f),
            Visible = false,
            ZIndex = 2
        };
        AddChild(_highlight);

        AddChild(new CollisionPolygon2D { Polygon = corners });
    }

    public void SetHighlighted(bool highlighted)
    {
        if (_highlight is not null)
        {
            _highlight.Visible = highlighted;
        }
    }

    public void SetGridVisible(bool visible)
    {
        if (_border is not null)
        {
            _border.Visible = visible;
        }
    }

    public override void _InputEvent(Viewport viewport, InputEvent @event, int shapeIdx)
    {
        if (_inputLocked?.Invoke() == true)
        {
            viewport.SetInputAsHandled();
            return;
        }

        if (@event is not InputEventMouseButton { Pressed: true } mouseButton)
        {
            return;
        }

        if (mouseButton.ButtonIndex == MouseButton.Left)
        {
            _leftClick?.Invoke(Coord);
            viewport.SetInputAsHandled();
        }
        else if (mouseButton.ButtonIndex == MouseButton.Right)
        {
            _rightClick?.Invoke(Coord);
            viewport.SetInputAsHandled();
        }
    }
}
