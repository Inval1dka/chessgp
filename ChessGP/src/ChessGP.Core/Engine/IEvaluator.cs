namespace ChessGP.Core.Engine;

/// <summary>
/// Evaluates a chess position from White's perspective in centipawns.
/// </summary>
public interface IEvaluator
{
    /// <summary>
    /// Returns a deterministic evaluation score for the current board.
    /// </summary>
    int Evaluate(IBoardAdapter board);
}
