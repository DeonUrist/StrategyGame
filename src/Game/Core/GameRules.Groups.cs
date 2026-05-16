namespace StrategyGame.Core;

public static partial class GameRules
{
    public static bool TryMoveGroup(GameState state, int groupId, HexCoord destination)
    {
        if (!state.Groups.TryGetValue(groupId, out var group)
            || group.StationedCityId is not null
            || !state.Map.TryGet(destination, out var tile))
        {
            return false;
        }

        var range = MovementRange(state, group.Coord, group.MovementLeft);
        if (!range.TryGetValue(destination, out var cost) || cost == 0)
        {
            return false;
        }

        var origin = state.Map.Get(group.Coord);
        origin.GroupIds.Remove(group.Id);
        tile.GroupIds.Add(group.Id);
        group.Coord = destination;
        group.MovementLeft = Math.Max(0.0, group.MovementLeft - cost);

        foreach (var enemy in tile.GroupIds
            .Where(id => id != group.Id)
            .Select(id => state.Groups[id])
            .Where(g => g.FactionId != group.FactionId && g.StationedCityId is null)
            .ToList())
        {
            CombatResolver.Resolve(state, group, enemy);
            if (!state.Groups.ContainsKey(group.Id))
            {
                break;
            }
        }

        return true;
    }

    public static bool TryMergeGroups(GameState state, int sourceGroupId, int targetGroupId)
    {
        if (!state.Groups.TryGetValue(sourceGroupId, out var source))
        {
            return false;
        }

        return TryTransferUnits(state, sourceGroupId, targetGroupId, source.Units.Select(unit => unit.Id).ToList());
    }

    public static bool TryTransferUnits(GameState state, int sourceGroupId, int targetGroupId, IReadOnlyCollection<int> unitIds)
    {
        if (sourceGroupId == targetGroupId
            || !state.Groups.TryGetValue(sourceGroupId, out var source)
            || !state.Groups.TryGetValue(targetGroupId, out var target)
            || source.FactionId != target.FactionId
            || unitIds.Count == 0)
        {
            return false;
        }

        if (!CanTransferBetween(source, target))
        {
            return false;
        }

        var requested = unitIds.ToHashSet();
        var moving = source.Units.Where(unit => requested.Contains(unit.Id)).ToList();
        if (moving.Count != requested.Count)
        {
            return false;
        }

        foreach (var unit in moving)
        {
            source.Units.Remove(unit);
        }

        target.Units.AddRange(moving);
        target.MovementLeft = Math.Min(Math.Min(target.MovementLeft, source.MovementLeft), MaxGroupMovement(state, target));
        if (source.Units.Count == 0)
        {
            RemoveGroup(state, source);
        }
        else
        {
            source.MovementLeft = Math.Min(source.MovementLeft, MaxGroupMovement(state, source));
        }

        state.AddLog($"{moving.Count} unit{(moving.Count == 1 ? "" : "s")} moved into {GroupDisplayName(state, target)}.");
        return true;
    }

    public static bool TryRenameGroup(GameState state, int groupId, string name)
    {
        if (!state.Groups.TryGetValue(groupId, out var group))
        {
            return false;
        }

        var trimmed = name.Trim();
        group.Name = trimmed.Length > 40 ? trimmed[..40] : trimmed;
        state.AddLog($"Group renamed to {GroupDisplayName(state, group)}.");
        return true;
    }

    public static int? TrySplitGroup(GameState state, int groupId, IReadOnlyCollection<int> unitIds)
    {
        if (!state.Groups.TryGetValue(groupId, out var source) || unitIds.Count == 0 || unitIds.Count >= source.Units.Count)
        {
            return null;
        }

        var requested = unitIds.ToHashSet();
        var moving = source.Units.Where(unit => requested.Contains(unit.Id)).ToList();
        if (moving.Count != requested.Count)
        {
            return null;
        }

        foreach (var unit in moving)
        {
            source.Units.Remove(unit);
        }

        var created = new GroupState
        {
            Id = NextGroupId(state),
            FactionId = source.FactionId,
            Coord = source.Coord,
            StationedCityId = source.StationedCityId,
            MovementLeft = Math.Min(source.MovementLeft, MaxUnitMovement(state, moving))
        };
        created.Units.AddRange(moving);
        state.Groups[created.Id] = created;

        source.MovementLeft = Math.Min(source.MovementLeft, MaxGroupMovement(state, source));
        if (created.StationedCityId is { } cityId)
        {
            state.Cities[cityId].StationedGroupIds.Add(created.Id);
        }
        else
        {
            state.Map.Get(created.Coord).GroupIds.Add(created.Id);
        }

        state.AddLog($"{GroupDisplayName(state, created)} split from {GroupDisplayName(state, source)}.");
        return created.Id;
    }

    public static bool TryStationGroup(GameState state, int groupId, int cityId)
    {
        if (!state.Groups.TryGetValue(groupId, out var group)
            || group.StationedCityId is not null
            || !state.Cities.TryGetValue(cityId, out var city)
            || city.FactionId != group.FactionId
            || city.Coord != group.Coord)
        {
            return false;
        }

        if (GetCityGarrison(state, cityId) is { } garrison)
        {
            return TryTransferUnits(state, group.Id, garrison.Id, group.Units.Select(unit => unit.Id).ToList());
        }

        state.Map.Get(group.Coord).GroupIds.Remove(group.Id);
        group.StationedCityId = city.Id;
        city.StationedGroupIds.Add(group.Id);
        state.AddLog($"{GroupDisplayName(state, group)} stationed at {city.Name}.");
        return true;
    }

    public static bool TryStationUnits(GameState state, int groupId, int cityId, IReadOnlyCollection<int> unitIds)
    {
        if (!state.Groups.TryGetValue(groupId, out var group)
            || group.StationedCityId is not null
            || !state.Cities.TryGetValue(cityId, out var city)
            || city.FactionId != group.FactionId
            || city.Coord != group.Coord
            || GetCityGarrison(state, cityId) is not { } garrison)
        {
            return false;
        }

        return TryTransferUnits(state, group.Id, garrison.Id, unitIds);
    }

    public static bool TryDeployGroup(GameState state, int groupId)
    {
        if (!state.Groups.TryGetValue(groupId, out var group)
            || group.StationedCityId is not { } cityId
            || !state.Cities.TryGetValue(cityId, out var city)
            || !state.Map.TryGet(city.Coord, out var tile)
            || !TerrainResolver.Resolve(state, tile).Passable)
        {
            return false;
        }

        city.StationedGroupIds.Remove(group.Id);
        group.StationedCityId = null;
        group.Coord = city.Coord;
        tile.GroupIds.Add(group.Id);
        state.AddLog($"{GroupDisplayName(state, group)} deployed from {city.Name}.");
        return true;
    }

    public static int? TryDeployUnits(GameState state, int cityId, IReadOnlyCollection<int> unitIds)
    {
        if (!state.Cities.TryGetValue(cityId, out var city)
            || GetCityGarrison(state, cityId) is not { } garrison
            || !state.Map.TryGet(city.Coord, out var tile)
            || !TerrainResolver.Resolve(state, tile).Passable
            || unitIds.Count == 0
            || unitIds.Count > garrison.Units.Count)
        {
            return null;
        }

        if (unitIds.Count == garrison.Units.Count)
        {
            return TryDeployGroup(state, garrison.Id) ? garrison.Id : null;
        }

        var createdId = TrySplitGroup(state, garrison.Id, unitIds);
        if (createdId is not { } groupId)
        {
            return null;
        }

        return TryDeployGroup(state, groupId) ? groupId : null;
    }

    public static GroupState? GetCityGarrison(GameState state, int cityId)
    {
        return state.Cities.TryGetValue(cityId, out var city)
            ? city.StationedGroupIds
                .Where(state.Groups.ContainsKey)
                .Select(id => state.Groups[id])
                .FirstOrDefault()
            : null;
    }

    public static bool IsSingleAgentGroup(GameState state, GroupState group)
    {
        return group.Units.Count == 1 && IsAgentUnit(state, group.Units[0]);
    }

    public static bool IsAgentUnit(GameState state, UnitInstance unit)
    {
        return state.Database.Units[unit.TypeId].Role.Equals("agent", StringComparison.OrdinalIgnoreCase);
    }

    public static double MaxGroupMovement(GameState state, GroupState group)
    {
        return group.Units.Count == 0 ? 0.0 : MaxUnitMovement(state, group.Units);
    }

    private static double MaxUnitMovement(GameState state, IEnumerable<UnitInstance> units)
    {
        return units.Min(unit => state.Database.Units[unit.TypeId].Movement);
    }

    private static int NextGroupId(GameState state)
    {
        return state.Groups.Count == 0 ? 1 : state.Groups.Keys.Max() + 1;
    }

    private static void RemoveGroup(GameState state, GroupState group)
    {
        if (group.StationedCityId is { } cityId && state.Cities.TryGetValue(cityId, out var city))
        {
            city.StationedGroupIds.Remove(group.Id);
        }
        else if (state.Map.TryGet(group.Coord, out var tile))
        {
            tile.GroupIds.Remove(group.Id);
        }

        state.Groups.Remove(group.Id);
    }

    public static string GroupDisplayName(GameState state, GroupState group)
    {
        return string.IsNullOrWhiteSpace(group.Name)
            ? FactionAdjective(group.FactionId)
            : group.Name;
    }

    public static string FactionAdjective(string factionId)
    {
        return factionId.ToLowerInvariant() switch
        {
            "dwarves" => "Dwarven",
            "elves" => "Elven",
            "humans" => "Human",
            "orcs" => "Orcish",
            "ratmen" => "Ratman",
            "undead" => "Undead",
            var id => char.ToUpperInvariant(id[0]) + id[1..]
        };
    }

    private static bool CanTransferBetween(GroupState source, GroupState target)
    {
        if (source.StationedCityId is not null || target.StationedCityId is not null)
        {
            return source.StationedCityId == target.StationedCityId
                   || (source.StationedCityId is null && target.StationedCityId is not null && source.Coord == target.Coord)
                   || (source.StationedCityId is not null && target.StationedCityId is null && source.Coord == target.Coord);
        }

        return source.Coord == target.Coord;
    }
}
