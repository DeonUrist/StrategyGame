namespace StrategyGame.Core;

public static partial class GameRules
{
    public static bool TryUpgradeCityBuilding(GameState state, int cityId)
    {
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

        // This is a building level chain: Shelter becomes Camp, Camp becomes
        // Townsquare, and so on. The higher level replaces the old level instead
        // of sitting beside it in the city list.
        city.BuildingIds[city.BuildingIds.Count - 1] = currentBuilding.UpgradesTo;
        state.AddLog($"{city.Name} upgraded to {state.Database.Buildings[currentBuilding.UpgradesTo].Name}.");
        return true;
    }
}
