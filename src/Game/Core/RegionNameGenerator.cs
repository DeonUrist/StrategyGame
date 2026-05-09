namespace StrategyGame.Core;

// Generates a deterministic dark-fantasy place name for a region based on its
// resolved biome and climate. Style target: grim, archaic, evocative —
// names that feel earned by geography, not picked from a random-word table.
public static class RegionNameGenerator
{
    // Prefixes are grouped by the emotional tone of the terrain family.
    // Each biome family picks from its own prefix pool so wet regions feel
    // different from arid ones even when both draw "Pale" or "Hollow".
    private static readonly string[] AridPrefixes =
        ["Ash", "Cinder", "Bone", "Salt", "Gaunt", "Pale", "Grey", "Sere"];

    private static readonly string[] ColdPrefixes =
        ["Hoar", "Iron", "Pale", "Bleak", "Frost", "Gaunt", "Sable", "Dusk"];

    private static readonly string[] WetPrefixes =
        ["Brackish", "Sunken", "Silt", "Dusk", "Hollow", "Rot", "Grey", "Murk"];

    private static readonly string[] ForestPrefixes =
        ["Withered", "Sable", "Grim", "Pale", "Hollow", "Briar", "Bleak", "Veil"];

    private static readonly string[] PlainPrefixes =
        ["Waning", "Gaunt", "Hollow", "Pale", "Ashen", "Bleak", "Dusk", "Sere"];

    private static readonly string[] DefaultPrefixes =
        ["Pale", "Gaunt", "Grey", "Bleak", "Hollow", "Ashen", "Dusk", "Sable"];

    // Suffixes describe what kind of terrain feature dominates the region.
    private static readonly string[] AridSuffixes =
        ["Wastes", "Expanse", "Drift", "Reach", "Scorch", "Basin", "Flats"];

    private static readonly string[] ColdSuffixes =
        ["Drift", "Strand", "Fell", "Expanse", "Reach", "Barrow", "Waste"];

    private static readonly string[] WetSuffixes =
        ["Mire", "Fen", "Bog", "Hollow", "Mere", "Slough", "Murk"];

    private static readonly string[] JungleSuffixes =
        ["Tangle", "Veil", "Canopy", "Shroud", "Weald", "Brake"];

    private static readonly string[] ForestSuffixes =
        ["Weald", "Hollow", "Brake", "Fell", "Thicket", "Umber"];

    private static readonly string[] ScrubSuffixes =
        ["Heath", "Moor", "Down", "Scrub", "Fell", "Reach"];

    private static readonly string[] GrasslandSuffixes =
        ["Down", "Sward", "Vale", "Reach", "Fell", "Heath", "Wold"];

    private static readonly string[] DefaultSuffixes =
        ["Reach", "Hollow", "Expanse", "Fell", "Vale", "Strand"];

    public static string Generate(string biomeName, TemperatureBand temperature, int seed)
    {
        // Use a fast mixing step so nearby region IDs don't produce sequential-
        // sounding names. The unchecked block lets the multiplication overflow
        // safely — only the low bits matter for the array indices.
        unchecked
        {
            seed = (int)(seed * 2654435761u);
        }

        var prefixes = PickPrefixes(biomeName, temperature);
        var suffixes = PickSuffixes(biomeName);

        var prefix = prefixes[((seed >> 3) & 0x7fffffff) % prefixes.Length];
        var suffix = suffixes[((seed >> 11) & 0x7fffffff) % suffixes.Length];

        // Avoid prefix/suffix that are the same word (e.g. "Mire Mire" or
        // "Hollow Hollow" could occur if the same word appears in both tables).
        if (string.Equals(prefix, suffix, StringComparison.OrdinalIgnoreCase))
        {
            suffix = suffixes[(((seed >> 11) & 0x7fffffff) + 1) % suffixes.Length];
        }

        return $"{prefix} {suffix}";
    }

    private static string[] PickPrefixes(string biomeName, TemperatureBand temperature)
    {
        // Cold climate overrides biome family so arctic regions always sound frozen.
        if (temperature is TemperatureBand.Arctic or TemperatureBand.Subarctic)
        {
            return ColdPrefixes;
        }

        return biomeName switch
        {
            "Desert" or "Badlands" => AridPrefixes,
            "Swamp" => WetPrefixes,
            "Jungle" or "Conifer Forest" or "Broadleaf Forest" or "Taiga" => ForestPrefixes,
            "Shrubland" => PlainPrefixes,
            "Grassland" or "Prairie" => PlainPrefixes,
            _ => DefaultPrefixes
        };
    }

    private static string[] PickSuffixes(string biomeName)
    {
        return biomeName switch
        {
            "Desert" or "Badlands" => AridSuffixes,
            "Tundra" or "Ice Sheet" or "Ocean Ice Sheet" => ColdSuffixes,
            "Swamp" => WetSuffixes,
            "Jungle" => JungleSuffixes,
            "Conifer Forest" or "Broadleaf Forest" or "Taiga" => ForestSuffixes,
            "Shrubland" => ScrubSuffixes,
            "Grassland" or "Prairie" => GrasslandSuffixes,
            _ => DefaultSuffixes
        };
    }
}
