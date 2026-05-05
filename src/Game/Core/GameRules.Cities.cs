namespace StrategyGame.Core;

public static partial class GameRules
{
    public static bool TryUpgradeCityBuilding(GameState state, int cityId)
    {
        // City upgrades currently have no cost or production queue. This rule
        // only advances the city's single building-chain level if one exists.
        if (!state.Cities.TryGetValue(cityId, out var city))
        {
            return false;
        }

        var currentBuildingId = city.BuildingIds.LastOrDefault();
        if (currentBuildingId is null)
        {
            return false;
        }

        var currentBuilding = state.Database.Buildings[currentBuildingId];
        if (currentBuilding.UpgradesTo is null)
        {
            return false;
        }

        // This is a building level chain: Campsite becomes Shelter, Shelter
        // becomes Encampment, and so on. The higher level replaces the old level
        // instead of sitting beside it in the city list.
        city.BuildingIds[city.BuildingIds.Count - 1] = currentBuilding.UpgradesTo;
        state.AddLog($"{city.Name} upgraded to {state.Database.Buildings[currentBuilding.UpgradesTo].Name}.");
        return true;
    }
}
