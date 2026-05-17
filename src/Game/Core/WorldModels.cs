namespace StrategyGame.Core;

// A map tile stores generated world properties plus lightweight indexes for
// groups currently on the tile. The indexes duplicate each group's Coord, but
// make drawing and click lookup direct instead of scanning every group.
public sealed class HexTile
{
    private int? _locationId;

    public required HexCoord Coord { get; init; }
    public Elevation Elevation { get; set; }
    public MoistureLevel Moisture { get; set; }
    public WaterBodyKind WaterBodyKind { get; set; }
    public int? RegionId { get; set; }
    public List<string> FeatureIds { get; } = [];
    public string? ResourceId { get; set; }
    public int? LocationId { get => _locationId; set => _locationId = value; }
    public int? CityId { get => _locationId; set => _locationId = value; }
    public List<int> GroupIds { get; } = [];
}

// RegionState is the source of truth for land biome identity. Every land tile
// points to a region; the region owns moisture, temperature, and the final
// resolved biome name that future population systems can use.
public sealed class RegionState
{
    public int Id { get; init; }
    public required string Name { get; set; }
    public List<HexCoord> TileCoords { get; } = [];
    public MoistureLevel Moisture { get; set; }
    public TemperatureBand Temperature { get; set; }
    public BaseBiome BaseBiome { get; set; }
    public required string FinalBiomeName { get; set; }
}

// HexMap owns the coordinate dictionary and provides the small set of spatial
// queries the rest of the rules need. It deliberately stays simple: pathfinding
// and terrain logic live in GameRules.Movement and TerrainResolver.
public sealed class HexMap
{
    private readonly Dictionary<HexCoord, HexTile> _tiles = [];
    private int _maxRow;
    private int _maxCol;

    public IEnumerable<HexTile> Tiles => _tiles.Values;
    public int Count => _tiles.Count;

    public void Add(HexTile tile)
    {
        _tiles[tile.Coord] = tile;
        var col = tile.Coord.Q + tile.Coord.R / 2;
        if (tile.Coord.R > _maxRow) _maxRow = tile.Coord.R;
        if (col > _maxCol) _maxCol = col;
    }

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

        if (tile.WaterBodyKind != WaterBodyKind.None)
        {
            return tile.WaterBodyKind == WaterBodyKind.Outer;
        }

        var visited = new HashSet<HexCoord>();
        var queue = new Queue<HexCoord>();
        visited.Add(tile.Coord);
        queue.Enqueue(tile.Coord);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var col = current.Q + current.R / 2;
            if (col == 0 || current.R == 0 || col == _maxCol || current.R == _maxRow)
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
    public required string Type { get; init; }
    public required string Name { get; init; }
    public required string RaceId { get; init; }
    public required string Color { get; init; }
    public required string Description { get; init; }
    public required bool IsPlayer { get; init; }
}

// UnitInstance is one unit in a group. Repeated entries represent multiple units
// of the same type; authored stats are looked up by TypeId. Id preserves a
// specific unit's identity when groups are merged and split.
public sealed class UnitInstance
{
    public int Id { get; init; }
    public required string TypeId { get; init; }
    public string? Name { get; init; }
}

// GroupState is any movable set of units. Stationed military groups live in a
// settlement garrison instead of on the map.
public sealed class GroupState
{
    public int Id { get; init; }
    public string Name { get; set; } = "";
    public required string FactionId { get; init; }
    public required HexCoord Coord { get; set; }
    public List<UnitInstance> Units { get; } = [];
    public Dictionary<ResourceCategory, double> Inventory { get; } = [];
    public double MovementLeft { get; set; }
    public int? StationedCityId { get; set; }
}

// LocationState is the board model for established places. Settlement locations
// keep the existing town-center and garrison behavior; farms, camps, and mines
// are available for future creation and production rules.
public sealed class LocationState
{
    public int Id { get; init; }
    public LocationKind Kind { get; set; } = LocationKind.Settlement;
    public required string Name { get; init; }
    public required string FactionId { get; set; }
    public required HexCoord Coord { get; init; }
    public int TownCenterLevel { get; set; }
    public int Population { get; set; }
    public Dictionary<ResourceCategory, double> Inventory { get; } = [];
    public List<int> StationedGroupIds { get; } = [];
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
    public const int MinCivilizations = 1;
    public const int DefaultCivilizations = 3;
    public const int MaxCivilizations = 6;

    public int MapSize { get; init; } = DefaultMapSize;
    public int Civilizations { get; init; } = DefaultCivilizations;
    public int Wetness { get; init; } = 50;
    public int GrasslandShrublandBias { get; init; } = 35;
    public int DesertBadlandsBias { get; init; } = 25;
    public int ConiferBroadleafForestBias { get; init; } = 50;
    public int ElevationVariance { get; init; } = 50;
    public int MaxSeaNumber { get; init; } = 2;
    public ClimateBias ClimateBias { get; init; } = ClimateBias.Normal;
    public List<string> AllowedFactionIds { get; init; } = [];
}
