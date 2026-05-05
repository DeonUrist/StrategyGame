namespace StrategyGame.Core;

// A map tile stores generated world properties plus lightweight indexes for
// pieces currently on the tile. The indexes duplicate each piece's Coord, but
// make drawing and click lookup direct instead of scanning every stack/agent.
public sealed class HexTile
{
    public required HexCoord Coord { get; init; }
    public Elevation Elevation { get; set; }
    // Moisture and Vegetation begin as copies of the owning region's values, then
    // elevation can locally reduce them.
    public MoistureLevel Moisture { get; set; }
    public Vegetation Vegetation { get; set; }
    public int? RegionId { get; set; }
    public List<string> FeatureIds { get; } = [];
    public string? ResourceId { get; set; }
    public int? CityId { get; set; }
    public List<int> StackIds { get; } = [];
    public List<int> AgentIds { get; } = [];
}

// RegionState is the source of truth for land biome identity. Every land tile
// points to a region; the region owns moisture, water retention, temperature,
// vegetation, and the final resolved biome name that future population systems
// can use.
public sealed class RegionState
{
    public int Id { get; init; }
    public List<HexCoord> TileCoords { get; } = [];
    public MoistureLevel Moisture { get; set; }
    public WaterRetention WaterRetention { get; set; }
    public TemperatureBand Temperature { get; set; }
    public BaseBiome BaseBiome { get; set; }
    public Vegetation Vegetation { get; set; }
    public required string FinalBiomeName { get; set; }
}

// HexMap owns the coordinate dictionary and provides the small set of spatial
// queries the rest of the rules need. It deliberately stays simple: pathfinding
// and terrain logic live in GameRules.Movement and TerrainResolver.
public sealed class HexMap
{
    private readonly Dictionary<HexCoord, HexTile> _tiles = [];

    public IEnumerable<HexTile> Tiles => _tiles.Values;
    public int Count => _tiles.Count;

    public void Add(HexTile tile) => _tiles[tile.Coord] = tile;
    public bool TryGet(HexCoord coord, out HexTile tile) => _tiles.TryGetValue(coord, out tile!);
    public HexTile Get(HexCoord coord) => _tiles[coord];
    public IEnumerable<HexTile> Neighbors(HexCoord coord) => coord.Neighbors().Where(_tiles.ContainsKey).Select(Get);
    public bool IsCoastline(HexTile tile) => tile.Elevation == Elevation.Coast && IsOuterWaterBody(tile);

    public bool IsOuterWaterBody(HexTile tile)
    {
        if (!tile.Elevation.IsLiquidWater())
        {
            return false;
        }

        var visited = new HashSet<HexCoord>();
        var queue = new Queue<HexCoord>();
        var maxRow = _tiles.Keys.Max(c => c.R);
        var maxCol = _tiles.Keys.Max(c => c.Q + c.R / 2);
        visited.Add(tile.Coord);
        queue.Enqueue(tile.Coord);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var col = current.Q + current.R / 2;
            if (col == 0 || current.R == 0 || col == maxCol || current.R == maxRow)
            {
                return true;
            }

            foreach (var neighbor in Neighbors(current))
            {
                if (!neighbor.Elevation.IsLiquidWater() || !visited.Add(neighbor.Coord))
                {
                    continue;
                }

                queue.Enqueue(neighbor.Coord);
            }
        }

        return false;
    }
}

// FactionState is copied from faction definitions into the saveable game state.
// Keeping it in state means a saved game remains self-contained for current
// prototype needs, even if the catalog changes later.
public sealed class FactionState
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Color { get; init; }
    public required bool IsPlayer { get; init; }
}

// UnitInstance is one line in an army stack, such as "8 militia". Unit stats are
// looked up by TypeId in GameDatabase.Units so the instance only needs a count.
public sealed class UnitInstance
{
    public required string TypeId { get; init; }
    public int Count { get; set; }
}

// StackState is an army on the map. A stack can contain several unit rows and
// may have one or more joined AgentState leaders/scouts attached to it.
public sealed class StackState
{
    public int Id { get; init; }
    public required string FactionId { get; init; }
    public required HexCoord Coord { get; set; }
    public List<UnitInstance> Units { get; } = [];
    public double MovementLeft { get; set; }
    public List<int> JoinedAgentIds { get; } = [];
}

// AgentState represents a hero/scout-style piece. Agents can move independently
// while JoinedStackId is null, or attach to a friendly stack as its leader.
public sealed class AgentState
{
    public int Id { get; init; }
    public required string FactionId { get; init; }
    public required string TypeId { get; init; }
    public required string Name { get; init; }
    public HexCoord Coord { get; set; }
    public double MovementLeft { get; set; }
    public int? JoinedStackId { get; set; }
}

// CityState is the prototype settlement model. BuildingIds currently represents
// one upgrade chain level, starting at campsite and replacing the previous level.
public sealed class CityState
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string FactionId { get; set; }
    public required HexCoord Coord { get; init; }
    public List<string> BuildingIds { get; } = ["campsite"];
}

// GameLogEntry is intentionally tiny: all systems write human-readable text
// tagged with the current turn for display and save/load round trips.
public sealed class GameLogEntry
{
    public int Turn { get; init; }
    public required string Text { get; init; }
}

// WorldGenerationSettings records the global knobs that shaped this world.
// Saving them makes later world-info screens and deterministic tests able to
// explain why a generated map looks the way it does.
public sealed class WorldGenerationSettings
{
    public const int MinMapSize = 32;
    public const int DefaultMapSize = 64;
    public const int MaxMapSize = 96;

    public int MapSize { get; init; } = DefaultMapSize;
    public int Wetness { get; init; } = 50;
    public int Vegetation { get; init; } = 65;
    public int ElevationVariance { get; init; } = 50;
    public int MaxSeaNumber { get; init; } = 2;
    public ClimateBias ClimateBias { get; init; } = ClimateBias.Normal;
}
