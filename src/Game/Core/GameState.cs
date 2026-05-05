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
    public Dictionary<int, StackState> Stacks { get; } = [];
    public Dictionary<int, AgentState> Agents { get; } = [];
    public Dictionary<int, CityState> Cities { get; } = [];
    public List<GameLogEntry> Log { get; } = [];
    public int CurrentFactionIndex { get; set; }
    public int Turn { get; set; } = 1;

    // Turn order is list-based and follows data/factions.json. The player helper
    // assumes exactly one faction definition is marked IsPlayer.
    public FactionState CurrentFaction => Factions[CurrentFactionIndex];
    public FactionState PlayerFaction => Factions.First(f => f.IsPlayer);

    // These helpers keep rule code readable and centralize faction filtering.
    public IEnumerable<StackState> StacksForFaction(string factionId) => Stacks.Values.Where(s => s.FactionId == factionId);
    public IEnumerable<AgentState> AgentsForFaction(string factionId) => Agents.Values.Where(a => a.FactionId == factionId);

    // Logs are part of state so save/load preserves the player's recent history.
    public void AddLog(string text) => Log.Add(new GameLogEntry { Turn = Turn, Text = text });
}
