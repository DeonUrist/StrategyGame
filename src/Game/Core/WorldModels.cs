namespace StrategyGame.Core;

public sealed class HexTile
{
    public required HexCoord Coord { get; init; }
    public required string TerrainId { get; set; }
    public string? FeatureId { get; set; }
    public string? ResourceId { get; set; }
    public int? CityId { get; set; }
    public List<int> StackIds { get; } = [];
    public List<int> AgentIds { get; } = [];
}

public sealed class HexMap
{
    private readonly Dictionary<HexCoord, HexTile> _tiles = [];

    public IEnumerable<HexTile> Tiles => _tiles.Values;
    public int Count => _tiles.Count;

    public void Add(HexTile tile) => _tiles[tile.Coord] = tile;
    public bool TryGet(HexCoord coord, out HexTile tile) => _tiles.TryGetValue(coord, out tile!);
    public HexTile Get(HexCoord coord) => _tiles[coord];
    public IEnumerable<HexTile> Neighbors(HexCoord coord) => coord.Neighbors().Where(_tiles.ContainsKey).Select(Get);
}

public sealed class FactionState
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Color { get; init; }
    public required bool IsPlayer { get; init; }
}

public sealed class UnitInstance
{
    public required string TypeId { get; init; }
    public int Count { get; set; }
}

public sealed class StackState
{
    public int Id { get; init; }
    public required string FactionId { get; init; }
    public required HexCoord Coord { get; set; }
    public List<UnitInstance> Units { get; } = [];
    public int MovementLeft { get; set; }
    public int? LeaderAgentId { get; set; }
}

public sealed class AgentState
{
    public int Id { get; init; }
    public required string FactionId { get; init; }
    public required string TypeId { get; init; }
    public required string Name { get; init; }
    public HexCoord Coord { get; set; }
    public int MovementLeft { get; set; }
    public int? JoinedStackId { get; set; }
}

public sealed class CityState
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string FactionId { get; set; }
    public required HexCoord Coord { get; init; }
    public List<string> BuildingIds { get; } = ["shelter"];
}

public sealed class GameLogEntry
{
    public int Turn { get; init; }
    public required string Text { get; init; }
}
