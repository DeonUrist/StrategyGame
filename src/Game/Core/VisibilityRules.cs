namespace StrategyGame.Core;

public static class VisibilityRules
{
    private const int VisionRange = 5;

    public static bool IsVisibleToPlayer(GameState state, HexCoord coord)
    {
        if (!state.FogOfWarEnabled)
        {
            return true;
        }

        var playerId = state.PlayerFaction.Id;
        return state.Cities.Values.Any(city => city.FactionId == playerId && city.Coord.DistanceTo(coord) <= VisionRange)
               || state.Groups.Values.Any(group => group.FactionId == playerId && group.StationedCityId is null && group.Coord.DistanceTo(coord) <= VisionRange);
    }

    public static bool IsMoveVisibleToPlayer(GameState state, HexCoord origin, HexCoord destination)
    {
        return IsVisibleToPlayer(state, origin) || IsVisibleToPlayer(state, destination);
    }
}
