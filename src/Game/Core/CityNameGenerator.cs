namespace StrategyGame.Core;

// Picks deterministic settlement names from the owning faction's editable name
// pool. The data remains in JSON so names can be tuned without code changes.
public static class CityNameGenerator
{
    public static string Generate(FactionDefinition faction, int seed)
    {
        if (faction.CityNames.Count == 0)
        {
            return $"{faction.Name} Hold";
        }

        unchecked
        {
            seed = (int)(seed * 2654435761u);
        }

        return faction.CityNames[(seed & 0x7fffffff) % faction.CityNames.Count];
    }
}
