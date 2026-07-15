using System.Collections.Generic;

namespace ChessGP.Core.Engine;

/// <summary>
/// Terminal state of a chess game.
/// </summary>
public enum GameResult
{
    Ongoing,
    WhiteWin,
    BlackWin,
    Draw
}

/// <summary>
/// Minimal board abstraction used by the engine independently of the underlying chess library.
/// </summary>
public interface IBoardAdapter
{
    /// <summary>
    /// Loads the standard chess starting position.
    /// </summary>
    void LoadStartpos();

    /// <summary>
    /// Loads a position from FEN.
    /// </summary>
    void LoadFen(string fen);

    /// <summary>
    /// Generates all legal moves in the current position.
    /// </summary>
    IReadOnlyList<UciMove> GenerateLegalMoves();

    /// <summary>
    /// Applies a move and returns an opaque token that can be used to undo it.
    /// </summary>
    string Make(UciMove move);

    /// <summary>
    /// Restores the board to the state represented by a previous undo token.
    /// </summary>
    void Unmake(string token);

    /// <summary>
    /// Returns true when the current position has occurred at least three times.
    /// </summary>
    bool IsThreefold();

    /// <summary>
    /// Current halfmove clock value.
    /// </summary>
    int HalfmoveClock { get; }

    /// <summary>
    /// Checks whether the current position is terminal.
    /// </summary>
    bool IsTerminal(out GameResult result);

    /// <summary>
    /// Returns the current position as FEN.
    /// </summary>
    string ToFen();
}
