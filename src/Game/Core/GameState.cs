namespace StrategyGame.Core;

public sealed class GameState
{
    public required GameDatabase Database { get; init; }
    public required HexMap Map { get; init; }
    public List<FactionState> Factions { get; } = [];
    public Dictionary<int, StackState> Stacks { get; } = [];
    public Dictionary<int, AgentState> Agents { get; } = [];
    public Dictionary<int, CityState> Cities { get; } = [];
    public List<GameLogEntry> Log { get; } = [];
    public int CurrentFactionIndex { get; set; }
    public int Turn { get; set; } = 1;

    public FactionState CurrentFaction => Factions[CurrentFactionIndex];
    public FactionState PlayerFaction => Factions.First(f => f.IsPlayer);

    public IEnumerable<StackState> StacksForFaction(string factionId) => Stacks.Values.Where(s => s.FactionId == factionId);
    public IEnumerable<AgentState> AgentsForFaction(string factionId) => Agents.Values.Where(a => a.FactionId == factionId);

    public void AddLog(string text) => Log.Add(new GameLogEntry { Turn = Turn, Text = text });
}
