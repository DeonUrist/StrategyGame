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

        // Prefer selected stack text, then selected agent text, then a prompt.
        // This mirrors the input priority where stack selection wins on a shared
        // tile.
        if (tile is not null)
        {
            _inspectedTileCoord = tile.Coord;
        }

        var selected = _selectedStackId is { } stackId && state.Stacks.TryGetValue(stackId, out var stack)
            ? StackPanelText(state, stack)
            : _selectedAgentId is { } agentId && state.Agents.TryGetValue(agentId, out var agent)
                ? AgentPanelText(state, agent)
                : "Select an army or agent.";

        var inspectedTile = _inspectedTileCoord is { } inspectedCoord && state.Map.TryGet(inspectedCoord, out var resolvedTile)
            ? resolvedTile
            : null;

        _selectionInfoLabel.Text = selected;
        _attachToArmyButton.Visible = CanAttachSelectedAgent(state);
        var canDetachLeader = _selectedStackId is { } selectedStackId
                              && state.Stacks.TryGetValue(selectedStackId, out var selectedStack)
                              && selectedStack.FactionId == state.PlayerFaction.Id
                              && selectedStack.JoinedAgentIds.Count > 0;
        _detachLeaderButton.Visible = canDetachLeader;
        _detachLeaderButton.Disabled = !canDetachLeader;
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
        var cityText = tile.CityId is null
            ? ""
            : CityPanelText(state, state.Cities[tile.CityId.Value]);

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

    private string StackPanelText(GameState state, StackState stack)
    {
        var faction = _factionById[stack.FactionId];
        var armyName = $"#{stack.Id}";
        var units = string.Join(", ", stack.Units.Select(unit => $"{unit.Count} {Escape(state.Database.Units[unit.TypeId].Name)}"));
        var leaderText = stack.JoinedAgentIds.Count == 0
            ? ""
            : $"\nAttached: {string.Join(", ", stack.JoinedAgentIds.Where(state.Agents.ContainsKey).Select(id => AttachedAgentLabel(state, state.Agents[id])))}";

        return $"[b]Army {Escape(armyName)}[/b]"
             + $"\nMoves: {FormatMoves(stack.MovementLeft)}/{FormatMoves(MaxStackMovement(state, stack))}"
             + $"\nFaction: {ColorText(faction.Name, faction.Color)}"
             + $"\nUnits: {units} | strength: {CombatResolver.StackStrength(state, stack)}"
             + leaderText;
    }

    private string AgentPanelText(GameState state, AgentState agent)
    {
        var faction = _factionById[agent.FactionId];
        var unit = state.Database.Units[agent.TypeId];

        return $"[b]Agent:[/b] {ColorText(agent.Name, faction.Color)}"
             + $"\nMoves: {FormatMoves(agent.MovementLeft)}/{FormatMoves(MaxAgentMovement(state, agent))}"
             + $"\nFaction: {ColorText(faction.Name, faction.Color)}"
             + $"\nRole: {Escape(unit.Name)}"
             + (agent.JoinedStackId is { } joinedStackId ? $"\nJoined to army {joinedStackId}" : "");
    }

    private string CityPanelText(GameState state, CityState city)
    {
        var faction = _factionById[city.FactionId];
        return $"\nCity: {Escape(city.Name)}"
             + $"\nFaction: {ColorText(faction.Name, faction.Color)}"
             + $"\nBuildings: {string.Join(", ", city.BuildingIds.Select(id => ColorText(state.Database.Buildings[id].Name, BuildingColor(state.Database.Buildings[id].Level))))}";
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

    private static string AttachedAgentLabel(GameState state, AgentState agent)
    {
        var role = state.Database.Units[agent.TypeId].Name.ToLowerInvariant();
        return $"{Escape(agent.Name)} ({Escape(role)})";
    }

    private static string BuildingColor(int level)
    {
        return level switch
        {
            1 => "#b88f6a",
            2 => "#c9ba82",
            3 => "#8fb26a",
            4 => "#73a2d9",
            5 => "#d9b86a",
            6 => "#b8a0e0",
            _ => "#ffffff"
        };
    }

    private bool CanAttachSelectedAgent(GameState state)
    {
        if (_selectedAgentId is not { } agentId || !state.Agents.TryGetValue(agentId, out var agent))
        {
            return false;
        }

        return agent.FactionId == state.PlayerFaction.Id
               && agent.JoinedStackId is null
               && state.Map.Get(agent.Coord).StackIds.Select(id => state.Stacks[id]).Any(stack => stack.FactionId == agent.FactionId);
    }

    private static string FormatMoves(double movement)
    {
        return movement % 1 == 0 ? movement.ToString("0.0").Replace(".0", "") : movement.ToString("0.0");
    }

    private static double MaxStackMovement(GameState state, StackState stack)
    {
        return stack.Units.Count == 0 ? 0.0 : stack.Units.Min(unit => state.Database.Units[unit.TypeId].Movement);
    }

    private static double MaxAgentMovement(GameState state, AgentState agent)
    {
        return state.Database.Units[agent.TypeId].Movement;
    }
}
