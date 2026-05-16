namespace StrategyGame.Core;

// GameState is the full live simulation container. Core rules mutate this type
// directly, while presentation reads it for drawing and panel text.
public sealed class GameState
{
    // Database is shared authored data. Map and the dictionaries below are the
    // mutable runtime board state created by MapGenerator or GameStateSerializer.
    public required GameDatabase Database { get; init; }
    public required HexMap Map { get; init; }
    public WorldGenerationSettings WorldGeneration { get; set; } = new();
    public List<FactionState> Factions { get; } = [];
    public Dictionary<int, RegionState> Regions { get; } = [];
    public Dictionary<int, GroupState> Groups { get; } = [];
    public Dictionary<int, CityState> Cities { get; } = [];
    public List<GameLogEntry> Log { get; } = [];
    public int CurrentFactionIndex { get; set; }
    public int Turn { get; set; } = 1;
    public bool FogOfWarEnabled { get; set; }

    // Turn order is list-based and follows data/factions.json. The player helper
    // assumes exactly one faction definition is marked IsPlayer.
    public FactionState CurrentFaction => Factions[CurrentFactionIndex];
    public FactionState PlayerFaction => Factions.First(f => f.IsPlayer);

    // These helpers keep rule code readable and centralize faction filtering.
    public IEnumerable<GroupState> GroupsForFaction(string factionId) => Groups.Values.Where(g => g.FactionId == factionId);
    public FactionState GetFaction(string id) => Factions.First(f => f.Id == id);

    // Logs are part of state so save/load preserves the player's recent history.
    public void AddLog(string text) => Log.Add(new GameLogEntry { Turn = Turn, Text = text });

    // Incremented whenever tile terrain changes (elevation, moisture,
    // features). Presentation uses this to know when cached movement ranges are stale.
    public int MapVersion { get; private set; }
    public void IncrementMapVersion() => MapVersion++;
}
