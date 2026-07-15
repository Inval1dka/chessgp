using System;

namespace ChessGP.Core.Persistence;

/// <summary>
/// Persisted checkpoint containing the best genome found at a given training generation.
/// </summary>
public sealed record BestGenomeCheckpoint(
    string RunId,
    string Stage,
    int Generation,
    string SchemaId,
    string GenomeBits,
    DateTimeOffset TimestampUtc);
