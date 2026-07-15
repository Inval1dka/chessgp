using System.Collections.Generic;

namespace ChessGP.Core.Config;

/// <summary>
/// Controls how training labels are derived from chess games.
/// </summary>
public record LabelingOptions(
    bool onlyDecisive = true,
    bool winnerMovesOnly = true,
    bool labelAllMovesInDraws = false);

/// <summary>
/// Describes the input dataset used for training or evaluation.
/// </summary>
public record DataOptions(
    string path,
    string format,
    Filters? filters,
    LabelingOptions? labeling = null);

/// <summary>
/// Optional filters applied when selecting training samples.
/// </summary>
public record Filters(
    int? minLegalMoves,
    string? phase);

/// <summary>
/// Configuration for supervised Stage 1 evolution against labeled move data.
/// </summary>
public record Stage1Options(
    int population,
    double crossoverRate,
    double mutationRate,
    int generations,
    int batchSize,
    int ply);

/// <summary>
/// Configuration for Stage 2 self-play evolution.
/// </summary>
public record Stage2Options(
    int population,
    double crossoverRate,
    double mutationRate,
    int generations,
    int gamesPerPair,
    string openingBookPath,
    bool useHallOfFame,
    int hallOfFameSize,
    int searchDepth,
    string searchMode,
    int timePerMoveMs,
    int perMoveNodeCap,
    int maxSearchDepth = 0,
    int initialTimeMs = 0,
    int incrementMs = 0,
    int moveOverheadMs = 20,
    int estimatedMovesToGo = 30,
    int drawContemptCp = 30,
    int avoidDrawWhenAheadCp = 200);

/// <summary>
/// Configuration of the genetic algorithm operators.
/// </summary>
public record GaOptions(
    string selectionType,
    int tournamentK,
    double bitFlipRate,
    string crossoverType,
    int elitism);

/// <summary>
/// Describes one tunable evaluation parameter encoded in the genome.
/// </summary>
public record GeneSpec(
    string name,
    int bitWidth,
    double min,
    double max,
    string[]? constraintFlags);

/// <summary>
/// Defines the genome schema used by the evolutionary algorithm.
/// </summary>
public record GenomeOptions(
    string schemaId,
    List<GeneSpec> genes);

/// <summary>
/// Configuration of deterministic random number generation.
/// </summary>
public record RngOptions(int seed);

/// <summary>
/// Output paths used for metrics and checkpoints.
/// </summary>
public record OutputOptions(
    string checkpointsDir,
    string metricsFile);

/// <summary>
/// Root configuration object loaded from the application JSON config file.
/// </summary>
public record AppOptions(
    DataOptions Data,
    Stage1Options Stage1,
    Stage2Options Stage2,
    GaOptions GA,
    GenomeOptions Genome,
    RngOptions Rng,
    OutputOptions Output);
