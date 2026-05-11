namespace StrategyGame.Core;

public enum AiTurnStepKind
{
    StackMove,
    AgentMove,
    CityUpgrade
}

public sealed record AiTurnStep(
    AiTurnStepKind Kind,
    int PieceId,
    string FactionId,
    HexCoord? Origin,
    HexCoord? Destination,
    bool PieceSurvived);
