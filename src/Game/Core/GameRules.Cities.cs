namespace StrategyGame.Core;

public static partial class GameRules
{
    public static int? TryCreateLocation(GameState state, LocationKind kind, string factionId, HexCoord coord, string name, int population = 0)
    {
        if (!state.Factions.Any(faction => faction.Id == factionId)
            || !state.Map.TryGet(coord, out var tile)
            || tile.LocationId is not null
            || !TerrainResolver.Resolve(state, tile).Passable
            || population < 0)
        {
            return null;
        }

        var id = state.Cities.Count == 0 ? 1 : state.Cities.Keys.Max() + 1;
        var location = new LocationState
        {
            Id = id,
            Kind = kind,
            Name = name.Trim(),
            FactionId = factionId,
            Coord = coord,
            Population = population
        };

        state.Cities[id] = location;
        tile.LocationId = id;
        state.AddLog($"{location.Name} established.");
        return id;
    }

    public static bool TryUpgradeCityBuilding(GameState state, int cityId)
    {
        // City upgrades currently have no cost or production queue. This rule
        // only advances the hardcoded TownCenter settlement level if one exists.
        if (!state.Cities.TryGetValue(cityId, out var city) || city.Kind != LocationKind.Settlement)
        {
            return false;
        }

        var maxLevel = SettlementProgression.MaxTownCenterLevel(state);
        if (city.TownCenterLevel >= maxLevel)
        {
            return false;
        }

        var previousName = SettlementProgression.DisplayName(state, city);
        city.TownCenterLevel++;
        var nextLevel = SettlementProgression.CurrentTownCenter(state, city);
        state.AddLog($"{previousName} upgraded to {nextLevel.Name}.");
        return true;
    }
}
