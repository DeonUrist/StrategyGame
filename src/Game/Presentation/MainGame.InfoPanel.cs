using StrategyGame.Core;

namespace StrategyGame.Presentation;

public partial class MainGame
{
    private void UpdatePanel(HexTile? tile = null)
    {
        // The info panel is rebuilt from state whenever selection, tile clicks,
        // saves, loads, or turns change. It does not own gameplay state.
        if (_state is not { } state)
        {
            return;
        }

        // Prefer selected group text, then a prompt.
        if (tile is not null)
        {
            _inspectedTileCoord = tile.Coord;
        }

        var selected = _selectedGroupId is { } groupId && state.Groups.TryGetValue(groupId, out var group)
            ? GroupPanelText(state, group)
            : "Select a group.";

        var inspectedTile = _inspectedTileCoord is { } inspectedCoord && state.Map.TryGet(inspectedCoord, out var resolvedTile)
            ? resolvedTile
            : null;

        _selectionInfoLabel.Text = selected;
        var canStation = CanStationSelectedGroup(state);
        _stationGroupButton.Visible = canStation;
        _stationGroupButton.Disabled = !canStation;
        var canDeploy = CanDeployFromInspectedCity(state, inspectedTile);
        _deployGroupButton.Visible = canDeploy;
        _deployGroupButton.Disabled = !canDeploy;
        var canRelocate = CanRelocateFromInspectedLocation(state, inspectedTile);
        _relocateCiviliansButton.Visible = canRelocate;
        _relocateCiviliansButton.Disabled = !canRelocate;
        var canSettle = CanSettleSelectedCivilians(state);
        _settleCiviliansButton.Visible = canSettle;
        _settleCiviliansButton.Disabled = !canSettle;
        var canTransfer = CanTransferUnitsToSelectedGroup(state);
        _transferUnitsButton.Visible = canTransfer;
        _transferUnitsButton.Disabled = !canTransfer;
        var canSplit = CanSplitSelectedGroup(state);
        _splitGroupButton.Visible = canSplit;
        _splitGroupButton.Disabled = !canSplit;
        var canRename = CanRenameSelectedGroup(state);
        _renameGroupButton.Visible = canRename;
        _renameGroupButton.Disabled = !canRename;
        _tileInfoLabel.Text = inspectedTile is null
            ? "Inspect a tile to see terrain and city details."
            : TilePanelText(inspectedTile);
        _logHeaderLabel.Text = $"Turn {state.Turn}: {state.CurrentFaction.Name}";
        _logLabel.Text = string.Join('\n', state.Log.TakeLast(8).Select(LogPanelText));
    }

    private string TilePanelText(HexTile tile)
    {
        var state = _state ?? throw new InvalidOperationException("No active game.");
        var terrain = TerrainResolver.Resolve(state, tile);
        var terrainLabel = TerrainLabel(terrain, tile);
        var featureText = tile.FeatureIds.Count == 0
            ? ""
            : $"\nFeatures: {string.Join(", ", tile.FeatureIds.Select(feature => ColorText(feature, "#c88b4a")))}";
        var resourceText = tile.ResourceId is null
            ? ""
            : $"\nResources: {ColorText(state.Database.Resources[tile.ResourceId].Name, ResourceTextColor(tile.ResourceId))}";
        var regionText = tile.RegionId is null
            ? "\nRegion: none"
            : RegionPanelText(state.Regions[tile.RegionId.Value]);
        var cityText = tile.LocationId is null
            ? ""
            : CityPanelText(state, state.Cities[tile.LocationId.Value]);

        return $"Tile {tile.Coord.Q}/{tile.Coord.R}"
             + $"\n{Escape(terrainLabel)}"
             + regionText
             + resourceText
             + featureText
             + cityText;
    }

    private static string RegionPanelText(RegionState region)
    {
        return $"\nRegion {region.Id}: {Escape(region.Name)}";
    }

    private static string TerrainLabel(ResolvedTerrain terrain, HexTile tile)
    {
        return tile.Elevation switch
        {
            Elevation.Hills => $"{terrain.Name}, Hills",
            Elevation.Mountains => $"{terrain.Name}, Mountains",
            Elevation.Peaks => $"{terrain.Name}, Peaks",
            _ => terrain.Name
        };
    }

    private string GroupPanelText(GameState state, GroupState group)
    {
        var faction = _factionById[group.FactionId];
        var units = string.Join(", ", group.Units
            .GroupBy(unit => unit.TypeId)
            .Select(unitGroup =>
            {
                var name = Escape(state.Database.Unit(group.FactionId, unitGroup.Key).Name);
                return unitGroup.Count() == 1 ? name : $"{unitGroup.Count()} {name}";
            }));
        var stationedText = group.StationedCityId is { } cityId && state.Cities.TryGetValue(cityId, out var city)
            ? $"\nStationed: {Escape(city.Name)}"
            : "";

        return $"[b]{Escape(GameRules.GroupDisplayName(state, group))}[/b]"
             + $"\nMoves: {FormatMoves(group.MovementLeft)}/{FormatMoves(GameRules.MaxGroupMovement(state, group))}"
             + $"\nFaction: {ColorText(faction.Name, faction.Color)}"
             + $"\nUnits: {units} | strength: {CombatResolver.GroupStrength(state, group)}"
             + $"\nCarry: {FormatQuantity(GameRules.GroupInventoryWeight(group))}/{FormatQuantity(GameRules.GroupCarryCapacity(state, group))}"
             + GroupInventoryPanelText(group.Inventory)
             + stationedText;
    }

    private string CityPanelText(GameState state, LocationState city)
    {
        var faction = _factionById[city.FactionId];
        var townCenter = SettlementProgression.CurrentTownCenter(state, city);
        var title = city.Kind == LocationKind.Settlement
            ? $"Settlement: {Escape(SettlementProgression.DisplayName(state, city))}"
            : $"{city.Kind}: {Escape(city.Name)}";
        return $"\n{title}"
             + $"\nFaction: {ColorText(faction.Name, faction.Color)}"
             + $"\nPopulation: {city.Population}"
             + (city.Kind == LocationKind.Settlement ? $"\nTown Center: {ColorText(townCenter.Name, BuildingColor(townCenter.Level))}" : "")
             + LocationInventoryPanelText(city.Inventory)
             + GarrisonPanelText(state, city);
    }

    private static string LogPanelText(GameLogEntry entry)
    {
        return $"{ColorText($"Turn{entry.Turn}:", TurnColor(entry.Turn))} {Escape(entry.Text)}";
    }

    private static string ColorText(string text, string color)
    {
        // Strip ] so a crafted color value cannot break the BBCode tag structure.
        return $"[color={color.Replace("]", "")}]{Escape(text)}[/color]";
    }

    private static string Escape(string text)
    {
        return text.Replace("[", "[lb]").Replace("]", "[rb]");
    }

    private static string ResourceTextColor(string resourceId)
    {
        return resourceId switch
        {
            "gold" => "#d9b84a",
            "silver" => "#c9d3dc",
            "iron" => "#9ba3ad",
            "copper" => "#c9834b",
            "game" => "#78b36a",
            _ => "#ffffff"
        };
    }

    private static string TurnColor(int turn)
    {
        return turn % 2 == 0 ? "#88c0ff" : "#f0c86a";
    }

    private static string BuildingColor(int level)
    {
        return level switch
        {
            0 => "#b88f6a",
            1 => "#c9ba82",
            2 => "#8fb26a",
            3 => "#73a2d9",
            4 => "#d9b86a",
            5 => "#b8a0e0",
            _ => "#ffffff"
        };
    }

    private string GarrisonPanelText(GameState state, LocationState city)
    {
        if (GameRules.GetCityGarrison(state, city.Id) is not { } garrison || garrison.Units.Count == 0)
        {
            return "";
        }

        var units = string.Join(", ", garrison.Units
            .GroupBy(unit => unit.TypeId)
            .Select(unitGroup =>
            {
                var name = Escape(state.Database.Unit(garrison.FactionId, unitGroup.Key).Name);
                return unitGroup.Count() == 1 ? name : $"{unitGroup.Count()} {name}";
            }));

        return $"\nGarrison: {units}";
    }

    private bool CanStationSelectedGroup(GameState state)
    {
        return _selectedGroupId is { } groupId
               && state.Groups.TryGetValue(groupId, out var group)
               && group.FactionId == state.PlayerFaction.Id
               && group.StationedCityId is null
               && !GameRules.IsCivilianOnlyGroup(state, group)
               && state.Map.Get(group.Coord).LocationId is { } cityId
               && state.Cities[cityId].Kind == LocationKind.Settlement
               && state.Cities[cityId].FactionId == group.FactionId;
    }

    private bool CanDeployFromInspectedCity(GameState state, HexTile? tile)
    {
        return tile?.LocationId is { } cityId
               && state.Cities[cityId].FactionId == state.PlayerFaction.Id
               && state.Cities[cityId].Kind == LocationKind.Settlement
               && GameRules.GetCityGarrison(state, cityId) is { Units.Count: > 0 };
    }

    private bool CanTransferUnitsToSelectedGroup(GameState state)
    {
        return _selectedGroupId is { } groupId
               && state.Groups.TryGetValue(groupId, out var group)
               && group.FactionId == state.PlayerFaction.Id
               && group.StationedCityId is null
               && state.Map.Get(group.Coord).GroupIds
                   .Where(id => id != group.Id)
                   .Select(id => state.Groups[id])
                   .Any(other => other.FactionId == group.FactionId
                                 && other.StationedCityId is null
                                 && GameRules.HaveCompatibleUnitCategory(state, group, other));
    }

    private bool CanSplitSelectedGroup(GameState state)
    {
        return _selectedGroupId is { } groupId
               && state.Groups.TryGetValue(groupId, out var group)
               && group.FactionId == state.PlayerFaction.Id
               && group.Units.Count > 1;
    }

    private bool CanRenameSelectedGroup(GameState state)
    {
        return _selectedGroupId is { } groupId
               && state.Groups.TryGetValue(groupId, out var group)
               && group.FactionId == state.PlayerFaction.Id;
    }

    private static string FormatMoves(double movement)
    {
        return movement % 1 == 0 ? movement.ToString("0.0").Replace(".0", "") : movement.ToString("0.0");
    }

    private bool CanRelocateFromInspectedLocation(GameState state, HexTile? tile)
    {
        return tile?.LocationId is { } locationId
               && state.Cities.TryGetValue(locationId, out var location)
               && location.FactionId == state.PlayerFaction.Id
               && location.Population > 0;
    }

    private bool CanSettleSelectedCivilians(GameState state)
    {
        return _selectedGroupId is { } groupId
               && state.Groups.TryGetValue(groupId, out var group)
               && group.FactionId == state.PlayerFaction.Id
               && group.StationedCityId is null
               && GameRules.IsCivilianOnlyGroup(state, group)
               && state.Map.Get(group.Coord).LocationId is { } locationId
               && state.Cities[locationId].FactionId == group.FactionId;
    }

    private static string LocationInventoryPanelText(Dictionary<ResourceCategory, double> inventory)
    {
        return inventory.Count == 0
            ? "\nInventory: none"
            : $"\nInventory: {string.Join(", ", inventory.OrderBy(kv => kv.Key).Select(kv => $"{ResourceCategoryLabel(kv.Key)} {FormatQuantity(kv.Value)}"))}";
    }

    private static string GroupInventoryPanelText(Dictionary<ResourceCategory, double> inventory)
    {
        return inventory.Count == 0
            ? ""
            : $"\nInventory: {string.Join(", ", inventory.OrderBy(kv => kv.Key).Select(kv => $"{ResourceCategoryLabel(kv.Key)} {FormatQuantity(kv.Value)}"))}";
    }

    private static string ResourceCategoryLabel(ResourceCategory category)
    {
        return category switch
        {
            ResourceCategory.CommonGoods => "Common Goods",
            ResourceCategory.LuxuryGoods => "Luxury Goods",
            _ => category.ToString()
        };
    }

    private static string FormatQuantity(double quantity)
    {
        return quantity % 1 == 0 ? quantity.ToString("0") : quantity.ToString("0.0");
    }
}
