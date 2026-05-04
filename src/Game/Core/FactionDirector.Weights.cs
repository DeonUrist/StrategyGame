namespace StrategyGame.Core;

public sealed partial class FactionDirector
{
    private string ChooseWeightedAction(GameState state, string factionId, Random random)
    {
        // Every event starts with a base weight from JSON.
        // StateModifier adds extra weight when the board makes an event useful.
        var weights = state.Database.Events.Values
            .Select(e => new { e.Id, Weight = Math.Max(1, e.BaseWeight + StateModifier(state, factionId, e.Id)) })
            .ToList();
        var total = weights.Sum(w => w.Weight);
        var roll = random.Next(total);

        foreach (var item in weights)
        {
            roll -= item.Weight;
            if (roll < 0)
            {
                return item.Id;
            }
        }

        return weights[0].Id;
    }

    private static int StateModifier(GameState state, string factionId, string actionId)
    {
        return actionId switch
        {
            "defend_city" when EnemyNearOwnedCity(state, factionId) => 35,
            "claim_resource" when UnownedResourceExists(state, factionId) => 20,
            "attack_enemy" when WeakEnemyNearby(state, factionId) => 30,
            "scout" => 5,
            "upgrade_city" when CityCanUpgrade(state, factionId) => 15,
            _ => 0
        };
    }

    private static bool EnemyNearOwnedCity(GameState state, string factionId)
    {
        var cities = state.Cities.Values.Where(c => c.FactionId == factionId).ToList();
        return cities.Any(city => state.Stacks.Values.Any(stack => stack.FactionId != factionId && stack.Coord.DistanceTo(city.Coord) <= 4));
    }

    private static bool UnownedResourceExists(GameState state, string factionId)
    {
        var ownedCities = state.Cities.Values.Where(c => c.FactionId == factionId).Select(c => c.Coord).ToList();
        return state.Map.Tiles.Any(tile => tile.ResourceId is not null && ownedCities.All(city => city.DistanceTo(tile.Coord) > 2));
    }

    private static bool WeakEnemyNearby(GameState state, string factionId)
    {
        var ownStacks = state.StacksForFaction(factionId).ToList();
        var enemyStacks = state.Stacks.Values.Where(s => s.FactionId != factionId).ToList();
        return ownStacks.Any(own => enemyStacks.Any(enemy => own.Coord.DistanceTo(enemy.Coord) <= 6 && CombatResolver.StackStrength(state, own) >= CombatResolver.StackStrength(state, enemy)));
    }

    private static bool CityCanUpgrade(GameState state, string factionId)
    {
        return state.Cities.Values
            .Where(c => c.FactionId == factionId)
            .Select(c => c.BuildingIds.LastOrDefault())
            .Any(id => id is not null && state.Database.Buildings[id].UpgradesTo is not null);
    }
}
