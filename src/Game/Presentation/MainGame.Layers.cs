using Godot;
using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
    private const string BackgroundTexturePath = "res://assets/image/background.png";

    private Node2D _backgroundRoot = null!;
    private Node2D _terrainBaseRoot = null!;
    private Node2D _gridRoot = null!;
    private Node2D _selectionBorderRoot = null!;
    private Node2D _terrainRoot = null!;
    private Node2D _featuresRoot = null!;
    private Node2D _resourcesRoot = null!;
    private Node2D _selectionRoot = null!;
    private Node2D _locationsRoot = null!;
    private Node2D _unitsRoot = null!;

    private readonly Dictionary<HexCoord, TileView> _tileViews = [];
    private readonly Dictionary<HexCoord, Polygon2D> _selectionViews = [];
    private readonly Dictionary<int, Node2D> _groupViews = [];
    private readonly Dictionary<string, Texture2D> _textureCache = [];
    private int? _renderedMapVersion;

    private void InitDrawLayers()
    {
        _backgroundRoot = GetNodeOrCreateLayer("Background", 0);
        _terrainBaseRoot = GetNodeOrCreateLayer("TerrainBase", 1);
        _gridRoot = GetNodeOrCreateLayer("Grid", 2);
        _selectionBorderRoot = GetNodeOrCreateLayer("SelectionBorder", 3);
        _terrainRoot = GetNodeOrCreateLayer("Terrain", 4);
        _featuresRoot = GetNodeOrCreateLayer("Features", 5);
        _resourcesRoot = GetNodeOrCreateLayer("Resources", 6);
        _resourcesRoot.Visible = _resourceIconsVisible;
        _selectionRoot = GetNodeOrCreateLayer("Selection", 7);
        _locationsRoot = GetNodeOrCreateLayer("Locations", 8);
        _unitsRoot = GetNodeOrCreateLayer("Units", 9);
        SyncBackgroundObject();
    }

    private void RequestFullRedraw()
    {
        _mapInputLocked = false;
        if (_state is null)
        {
            ClearLayer(_terrainBaseRoot);
            ClearLayer(_gridRoot);
            ClearLayer(_selectionBorderRoot);
            ClearLayer(_terrainRoot);
            ClearLayer(_featuresRoot);
            ClearLayer(_resourcesRoot);
            ClearLayer(_selectionRoot);
            ClearLayer(_locationsRoot);
            ClearLayer(_unitsRoot);
            SyncBackgroundObject();
            _tileViews.Clear();
            _selectionViews.Clear();
            _groupViews.Clear();
            _renderedMapVersion = null;
            return;
        }

        SyncTerrainObjects(force: true);
        SyncDynamicObjects();
    }

    private Node2D GetNodeOrCreateLayer(string name, int? childIndex = null)
    {
        if (GetNodeOrNull<Node2D>(name) is { } existing)
        {
            if (childIndex is { } existingIndex)
            {
                MoveChild(existing, existingIndex);
            }

            return existing;
        }

        var layer = new Node2D { Name = name };
        AddChild(layer);
        MoveChild(layer, childIndex ?? GetChildCount() - 1);
        return layer;
    }

    private void SyncBackgroundObject()
    {
        if (!ResourceLoader.Exists(BackgroundTexturePath))
        {
            ClearLayer(_backgroundRoot);
            _backgroundSprite = null;
            return;
        }

        if (_backgroundSprite is null || !_backgroundRoot.GetChildren().Contains(_backgroundSprite))
        {
            ClearLayer(_backgroundRoot);
            _backgroundSprite = new Sprite2D
            {
                Name = "Background",
                Texture = LoadTexture(BackgroundTexturePath),
                Centered = true,
                ZIndex = -1000,
                ZAsRelative = false,
                TextureFilter = CanvasItem.TextureFilterEnum.Linear
            };
            _backgroundRoot.AddChild(_backgroundSprite);
        }

        UpdateBackgroundTransform();
    }

    private void UpdateBackgroundTransform()
    {
        if (_backgroundSprite is not { Texture: { } texture } || _camera is null)
        {
            return;
        }

        var viewportSize = GetViewportRect().Size;
        var worldSize = viewportSize / _camera.Zoom.X;
        var textureSize = texture.GetSize();
        var scale = MathF.Max(worldSize.X / textureSize.X, worldSize.Y / textureSize.Y);
        _backgroundSprite.Position = _camera.Position;
        _backgroundSprite.Scale = new Vector2(scale, scale);
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

        ClearLayer(_terrainBaseRoot);
        ClearLayer(_gridRoot);
        ClearLayer(_selectionBorderRoot);
        ClearLayer(_terrainRoot);
        ClearLayer(_selectionRoot);
        _tileViews.Clear();
        _selectionViews.Clear();

        foreach (var tile in state.Map.Tiles.OrderBy(t => t.Coord.R).ThenBy(t => t.Coord.Q))
        {
            var tilePosition = HexToPixel(tile.Coord);
            _terrainBaseRoot.AddChild(CreateTerrainTileSprite(
                $"TerrainBase_{tile.Coord.Q}_{tile.Coord.R}",
                TerrainBaseSpritePath(state, tile),
                tilePosition));

            var view = new TileView
            {
                Name = $"Grid_{tile.Coord.Q}_{tile.Coord.R}",
                Coord = tile.Coord,
                Position = tilePosition
            };
            view.Setup(
                TileTextureHexCornerOffsets,
                _gridVisible,
                coord => HandleHexLeftClick(coord),
                coord => HandleHexRightClick(coord),
                () => _mapInputLocked);
            _gridRoot.AddChild(view);
            _tileViews[tile.Coord] = view;

            _terrainRoot.AddChild(CreateTerrainTileSprite(
                $"Terrain_{tile.Coord.Q}_{tile.Coord.R}",
                TerrainSpritePath(state, tile),
                tilePosition));

            var highlight = new Polygon2D
            {
                Name = $"Selection_{tile.Coord.Q}_{tile.Coord.R}",
                Position = tilePosition,
                Polygon = TileTextureHexCornerOffsets,
                Color = new Color(1f, 1f, 1f, 0.25f),
                Visible = false
            };
            _selectionRoot.AddChild(highlight);
            _selectionViews[tile.Coord] = highlight;
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
            _featuresRoot.AddChild(CreateTileAlignedSprite(
                $"Feature_volcano_{tile.Coord.Q}_{tile.Coord.R}",
                "res://assets/image/features/volcano.png",
                HexToPixel(tile.Coord)));
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
                LocationSpritePath(state, city, townCenter),
                HexToPixel(city.Coord)));
            _locationsRoot.AddChild(CreateSettlementLabel(state, city, HexContentToPixel(city.Coord) + new Vector2(0, 8)));
        }
    }

    private Node CreateSettlementLabel(GameState state, LocationState city, Vector2 position)
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

    private void SyncUnitObjects(int? movingGroupId = null)
    {
        if (_state is not { } state)
        {
            return;
        }

        var deployedGroups = state.Groups.Values.Where(g => g.StationedCityId is null).Select(g => g.Id).ToHashSet();
        foreach (var staleId in _groupViews.Keys.Where(id => !deployedGroups.Contains(id)).ToList())
        {
            _groupViews[staleId].QueueFree();
            _groupViews.Remove(staleId);
        }

        foreach (var tile in state.Map.Tiles)
        {
            var groups = tile.GroupIds
                .Where(state.Groups.ContainsKey)
                .Select(id => state.Groups[id])
                .Where(g => g.StationedCityId is null)
                .OrderBy(g => g.Id)
                .ToList();
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var view = GetOrCreateUnitView(_groupViews, group.Id, $"Group_{group.Id}", GroupSpritePath(state, group));
                if (movingGroupId != group.Id)
                {
                    view.Position = HexToPixel(group.Coord) + PieceOffset(i, groups.Count);
                }
            }
        }
    }

    private Node2D GetOrCreateUnitView(Dictionary<int, Node2D> views, int id, string nodeName, string path)
    {
        if (views.TryGetValue(id, out var existing))
        {
            if (existing is Sprite2D sprite)
            {
                sprite.Texture = LoadTexture(path);
            }

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
            Position = position
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
            Position = position
        };
        ApplyTileSpriteScale(sprite);
        return sprite;
    }

    private Sprite2D CreateTerrainTileSprite(string nodeName, string texturePath, Vector2 position)
    {
        var sprite = CreateTileAlignedSprite(nodeName, texturePath, position);
        sprite.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
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
        SyncSelectionBorderObjects();

        if (_selectionViews.Count == 0)
        {
            return;
        }

        foreach (var (coord, view) in _selectionViews)
        {
            view.Visible = _selectedRange.ContainsKey(coord);
        }
    }

    private void SetGridVisible(bool visible)
    {
        foreach (var view in _tileViews.Values)
        {
            view.SetGridVisible(visible);
        }
    }

    private void SetResourceIconsVisible(bool visible)
    {
        if (_resourcesRoot is not null)
        {
            _resourcesRoot.Visible = visible;
        }
    }

    private void SyncSelectionBorderObjects()
    {
        ClearLayer(_selectionBorderRoot);

        if (_state is not { } state || _selectedRegionId is not { } selectedRegionId)
        {
            return;
        }

        foreach (var tile in state.Map.Tiles
                     .Where(t => t.RegionId == selectedRegionId)
                     .OrderBy(t => t.Coord.R)
                     .ThenBy(t => t.Coord.Q))
        {
            var coord = tile.Coord;
            var center = HexToPixel(coord);
            for (var directionIndex = 0; directionIndex < HexCoord.Directions.Length; directionIndex++)
            {
                var direction = HexCoord.Directions[directionIndex];
                var neighbor = new HexCoord(coord.Q + direction.Q, coord.R + direction.R);
                if (state.Map.TryGet(neighbor, out var neighborTile) && neighborTile.RegionId == selectedRegionId)
                {
                    continue;
                }

                var (startIndex, endIndex) = SelectionBorderEdgeCornerIndexes(directionIndex);
                _selectionBorderRoot.AddChild(new Line2D
                {
                    Name = $"SelectionBorder_{coord.Q}_{coord.R}_{directionIndex}",
                    Points =
                    [
                        center + TileTextureHexCornerOffsets[startIndex],
                        center + TileTextureHexCornerOffsets[endIndex]
                    ],
                    Width = 2.0f,
                    DefaultColor = Colors.White
                });
            }
        }
    }

    private static (int Start, int End) SelectionBorderEdgeCornerIndexes(int directionIndex)
    {
        return directionIndex switch
        {
            0 => (2, 3),
            1 => (1, 2),
            2 => (0, 1),
            3 => (5, 0),
            4 => (4, 5),
            _ => (3, 4)
        };
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

    private static string TerrainBaseSpritePath(GameState state, HexTile tile)
    {
        var key = TerrainBaseSpriteKey(state, tile);
        var path = $"res://assets/image/terrain/{key}_base.png";
        return ResourceLoader.Exists(path) ? path : "res://assets/image/terrain/tile.png";
    }

    private static string TerrainBaseSpriteKey(GameState state, HexTile tile)
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

        return TerrainAssetKey(TerrainResolver.Resolve(state, tile).Name);
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

    private static string LocationSpritePath(GameState state, LocationState location, BuildingLevelDefinition townCenter)
    {
        var key = location.Kind == LocationKind.Settlement
            ? NormalizeAssetKey(townCenter.Sprite)
            : NormalizeAssetKey(location.Kind.ToString());
        var raceId = NormalizeAssetKey(state.GetFaction(location.FactionId).RaceId);
        var racePath = $"res://assets/image/locations/{key}_{raceId}.png";
        return ResourceLoader.Exists(racePath) ? racePath : $"res://assets/image/locations/{key}.png";
    }

    private static string GroupSpritePath(GameState state, GroupState group)
    {
        var groupType = GameRules.IsCivilianOnlyGroup(state, group) ? "civilians" : "squad";
        var size = GroupSizeSpriteKey(group.Units.Count);
        var raceId = NormalizeAssetKey(state.GetFaction(group.FactionId).RaceId);
        var racePath = $"res://assets/image/units/{groupType}_{size}_{raceId}.png";
        return ResourceLoader.Exists(racePath) ? racePath : $"res://assets/image/units/{groupType}_{size}.png";
    }

    private static string GroupSizeSpriteKey(int unitCount)
    {
        return unitCount switch
        {
            <= 1 => "one",
            <= 10 => "few",
            <= 25 => "medium",
            _ => "many"
        };
    }

    private static string TerrainAssetKey(string terrainName)
    {
        return NormalizeAssetKey(terrainName);
    }
}

internal sealed partial class TileView : Area2D
{
    private Line2D _border = null!;
    private Action<HexCoord>? _leftClick;
    private Action<HexCoord>? _rightClick;
    private Func<bool>? _inputLocked;

    public HexCoord Coord { get; set; }

    public void Setup(Vector2[] corners, bool gridVisible, Action<HexCoord> leftClick, Action<HexCoord> rightClick, Func<bool> inputLocked)
    {
        _leftClick = leftClick;
        _rightClick = rightClick;
        _inputLocked = inputLocked;
        InputPickable = true;

        _border = new Line2D
        {
            Points = corners.Concat([corners[0]]).ToArray(),
            Width = 1.0f,
            DefaultColor = new Color(0, 0, 0, 0.32f),
            Visible = gridVisible
        };
        AddChild(_border);

        AddChild(new CollisionPolygon2D { Polygon = corners });
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
