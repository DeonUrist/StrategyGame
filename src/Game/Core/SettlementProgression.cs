namespace StrategyGame.Core;

public static class SettlementProgression
{
    public const string TownCenterId = "TownCenter";

    public static BuildingLevelDefinition CurrentTownCenter(GameState state, LocationState city)
    {
        var townCenter = state.Database.Buildings[TownCenterId];
        return townCenter.Levels.First(level => level.Level == city.TownCenterLevel);
    }

    public static int MaxTownCenterLevel(GameState state)
    {
        return state.Database.Buildings[TownCenterId].Levels.Max(level => level.Level);
    }

    public static string DisplayName(GameState state, LocationState city)
    {
        var level = CurrentTownCenter(state, city);
        return city.TownCenterLevel <= 1 ? level.Name : city.Name;
    }
}
