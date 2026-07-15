namespace ChessGP.Core.GA;

/// <summary>
/// Represents one genetic algorithm individual with its genome and evaluated fitness.
/// </summary>
public sealed record Individual(
    string GenomeBits,
    long Fitness);
