namespace StrategyGame.Core;

public sealed class FactionDirector
{
    private readonly Random _random = new(7781);

    public void TakeTurn(GameState state, string factionId)
    {
        var chosenAction = ChooseWeightedAction(state, factionId);
        var factionName = state.Factions.First(f => f.Id == factionId).Name;
        state.AddLog($"{factionName} director chose {state.Database.Events[chosenAction].Name}.");

        foreach (var stack in state.StacksForFaction(factionId).ToList())
        {
            if (!state.Stacks.ContainsKey(stack.Id))
            {
                continue;
            }

            var target = ChooseStackTarget(state, stack, chosenAction);
            if (target is not null)
            {
                GameRules.TryMoveStack(state, stack.Id, target.Value);
            }
        }

        foreach (var agent in state.AgentsForFaction(factionId).Where(a => a.JoinedStackId is null).ToList())
        {
            var target = ChooseScoutTarget(state, agent.Coord, agent.MovementLeft);
            if (target is not null)
            {
                GameRules.TryMoveAgent(state, agent.Id, target.Value);
            }
        }

        TryUpgradeCity(state, factionId);
    }

    private string ChooseWeightedAction(GameState state, string factionId)
    {
        var weights = state.Database.Events.Values
            .Select(e => new { e.Id, Weight = Math.Max(1, e.BaseWeight + StateModifier(state, factionId, e.Id)) })
            .ToList();
        var total = weights.Sum(w => w.Weight);
        var roll = _random.Next(total);

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

    private HexCoord? ChooseStackTarget(GameState state, StackState stack, string actionId)
    {
        var range = GameRules.MovementRange(state, stack.Coord, stack.MovementLeft);

        if (actionId is "attack_enemy" or "defend_city")
        {
            var enemies = state.Stacks.Values
                .Where(s => s.FactionId != stack.FactionId)
                .OrderBy(s => s.Coord.DistanceTo(stack.Coord))
                .ToList();

            foreach (var enemy in enemies)
            {
                return ClosestReachable(range, enemy.Coord);
            }
        }

        if (actionId == "claim_resource")
        {
            var resource = state.Map.Tiles
                .Where(t => t.ResourceId is not null)
                .OrderBy(t => t.Coord.DistanceTo(stack.Coord) + _random.Next(0, 3))
                .FirstOrDefault();

            return resource is null ? RandomReachable(range) : ClosestReachable(range, resource.Coord);
        }

        var ownedCity = state.Cities.Values
            .Where(c => c.FactionId == stack.FactionId)
            .OrderBy(c => c.Coord.DistanceTo(stack.Coord))
            .FirstOrDefault();

        return actionId == "upgrade_city" && ownedCity is not null
            ? ClosestReachable(range, ownedCity.Coord)
            : RandomReachable(range);
    }

    private HexCoord? ChooseScoutTarget(GameState state, HexCoord origin, int movement)
    {
        var range = GameRules.MovementRange(state, origin, movement);
        return RandomReachable(range);
    }

    private HexCoord? RandomReachable(Dictionary<HexCoord, int> range)
    {
        var options = range.Keys.Where(c => range[c] > 0).ToList();
        return options.Count == 0 ? null : options[_random.Next(options.Count)];
    }

    private static HexCoord? ClosestReachable(Dictionary<HexCoord, int> range, HexCoord target)
    {
        var options = range.Keys.Where(c => range[c] > 0).ToList();
        return options.Count == 0 ? null : options.OrderBy(c => c.DistanceTo(target)).First();
    }

    private void TryUpgradeCity(GameState state, string factionId)
    {
        var city = state.Cities.Values.FirstOrDefault(c => c.FactionId == factionId);
        var current = city?.BuildingIds.LastOrDefault();
        if (city is null || current is null || _random.NextDouble() > 0.35)
        {
            return;
        }

        var building = state.Database.Buildings[current];
        if (building.UpgradesTo is null)
        {
            return;
        }

        city.BuildingIds.Add(building.UpgradesTo);
        state.AddLog($"{city.Name} upgraded to {state.Database.Buildings[building.UpgradesTo].Name}.");
    }
}
