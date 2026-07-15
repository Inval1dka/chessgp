namespace ChessGP.Core.Data;

/// <summary>
/// Represents one labeled training position and the target move played from it.
/// </summary>
public sealed record TrainingSample(
    string Fen,
    string MoveUci,
    int PlyIndex);
