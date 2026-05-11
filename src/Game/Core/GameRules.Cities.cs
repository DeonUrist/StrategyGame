namespace StrategyGame.Core;

public static partial class GameRules
{
    public static bool TryUpgradeCityBuilding(GameState state, int cityId)
    {
        // City upgrades currently have no cost or production queue. This rule
        // only advances the hardcoded TownCenter settlement level if one exists.
        if (!state.Cities.TryGetValue(cityId, out var city))
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
