namespace StrategyGame.Core;

public enum AiTurnStepKind
{
    GroupMove,
    CityUpgrade
}

public sealed record AiTurnStep(
    AiTurnStepKind Kind,
    int PieceId,
    string FactionId,
    HexCoord? Origin,
    HexCoord? Destination,
    bool PieceSurvived);
