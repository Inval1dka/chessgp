using System.Text;
using Chess;
using ChessGP.Core.Config;
using ChessGP.Core.Engine;

namespace ChessGP.Core.Data;

/// <summary>
/// Options used when building the Stage 1 dataset from PGN games.
/// </summary>
public sealed class PgnIngestorOptions
{
    /// <summary>
    /// Optional filters applied while selecting candidate positions.
    /// </summary>
    public Filters? Filters { get; init; } = null;

    /// <summary>
    /// If true, games with replay or move-resolution errors are skipped instead of failing the whole import.
    /// </summary>
    public bool SkipInvalidGames { get; init; } = true;

    /// <summary>
    /// Optional limit on how many games are scanned from the PGN file.
    /// </summary>
    public int? MaxGamesToScan { get; init; } = null;
}

/// <summary>
/// Statistics collected while building the Stage 1 dataset.
/// </summary>
public readonly record struct PaperDatasetStats(
    int TotalGames,
    int EligibleGames,
    int SamplesCollected,
    int TrainWritten,
    int TestWritten,
    int SkippedDraws,
    int SkippedUnknown,
    int SkippedTooShort,
    int SkippedInvalid);

/// <summary>
/// Builds Stage 1 training and test datasets from PGN games.
/// </summary>
public static class PgnIngestor
{
    /// <summary>
    /// Creates shuffled train and test TSV files containing one sampled winning move per eligible game.
    /// </summary>
    /// <param name="inputPgnPath">Path to the source PGN file.</param>
    /// <param name="trainTsvPath">Path where the training TSV file will be written.</param>
    /// <param name="testTsvPath">Path where the test TSV file will be written.</param>
    /// <param name="trainCount">Requested number of training samples.</param>
    /// <param name="testCount">Requested number of test samples.</param>
    /// <param name="seed">Random seed used for sampling positions and shuffling the dataset.</param>
    /// <param name="options">Optional ingestion settings.</param>
    public static PaperDatasetStats BuildTrainTestFromPgn(
        string inputPgnPath,
        string trainTsvPath,
        string testTsvPath,
        int trainCount,
        int testCount,
        int seed,
        PgnIngestorOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(inputPgnPath))
        {
            throw new ArgumentException("Input PGN path is required.", nameof(inputPgnPath));
        }

        if (string.IsNullOrWhiteSpace(trainTsvPath))
        {
            throw new ArgumentException("Train TSV path is required.", nameof(trainTsvPath));
        }

        if (string.IsNullOrWhiteSpace(testTsvPath))
        {
            throw new ArgumentException("Test TSV path is required.", nameof(testTsvPath));
        }

        if (trainCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(trainCount));
        }

        if (testCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(testCount));
        }

        options ??= new PgnIngestorOptions();

        if (!File.Exists(inputPgnPath))
        {
            throw new FileNotFoundException($"PGN file not found: {inputPgnPath}");
        }

        CreateOutputDirectory(trainTsvPath);
        CreateOutputDirectory(testTsvPath);

        var targetTotal = checked(trainCount + testCount);
        var rng = new Random(seed);

        var totalGames = 0;
        var eligibleGames = 0;
        var skippedDraws = 0;
        var skippedUnknown = 0;
        var skippedTooShort = 0;
        var skippedInvalid = 0;

        var samples = new List<TrainingSample>(capacity: targetTotal);

        foreach (var game in PgnGameReader.ReadGames(inputPgnPath))
        {
            totalGames++;

            if (options.MaxGamesToScan.HasValue && totalGames > options.MaxGamesToScan.Value)
            {
                break;
            }

            if (samples.Count >= targetTotal)
            {
                break;
            }

            if (game.Result == PgnResult.Unknown)
            {
                skippedUnknown++;
                continue;
            }

            if (game.Result == PgnResult.Draw)
            {
                skippedDraws++;
                continue;
            }

            IReadOnlyList<string> moves;
            try
            {
                moves = PgnSanTokenizer.Tokenize(game.MoveText);
            }
            catch
            {
                skippedInvalid++;
                if (!options.SkipInvalidGames)
                {
                    throw;
                }

                continue;
            }

            if (options.Filters?.minLegalMoves is int minPlies && moves.Count < minPlies)
            {
                skippedTooShort++;
                continue;
            }

            var winnerIsWhite = game.Result == PgnResult.WhiteWin;
            var candidates = BuildCandidatePlies(moves.Count, winnerIsWhite, options.Filters?.phase);

            if (candidates.Count == 0)
            {
                skippedTooShort++;
                continue;
            }

            var chosenPly = candidates[rng.Next(candidates.Count)];

            if (!TryGetFenBeforeAfter(moves, chosenPly, out var fenBefore, out var fenAfter))
            {
                skippedInvalid++;
                if (!options.SkipInvalidGames)
                {
                    throw new InvalidOperationException("Failed to replay game moves.");
                }

                continue;
            }

            if (!TryResolveUciByFenDiff(fenBefore, fenAfter, out var uci))
            {
                skippedInvalid++;
                if (!options.SkipInvalidGames)
                {
                    throw new InvalidOperationException("Failed to resolve UCI uniquely.");
                }

                continue;
            }

            samples.Add(new TrainingSample(fenBefore, uci.ToString(), chosenPly));
            eligibleGames++;
        }

        Shuffle(samples, rng);

        var trainWritten = Math.Min(trainCount, samples.Count);
        var testWritten = Math.Min(testCount, Math.Max(0, samples.Count - trainWritten));

        WriteTsv(trainTsvPath, samples, start: 0, count: trainWritten);
        WriteTsv(testTsvPath, samples, start: trainWritten, count: testWritten);

        return new PaperDatasetStats(
            TotalGames: totalGames,
            EligibleGames: eligibleGames,
            SamplesCollected: samples.Count,
            TrainWritten: trainWritten,
            TestWritten: testWritten,
            SkippedDraws: skippedDraws,
            SkippedUnknown: skippedUnknown,
            SkippedTooShort: skippedTooShort,
            SkippedInvalid: skippedInvalid);
    }

    private static List<int> BuildCandidatePlies(int moveCount, bool winnerIsWhite, string? phaseRaw)
    {
        var phase = (phaseRaw ?? "Any").Trim().ToLowerInvariant();
        var result = new List<int>(moveCount / 2 + 1);

        var startPly = winnerIsWhite ? 0 : 1;
        for (var ply = startPly; ply < moveCount; ply += 2)
        {
            if (phase == "opening" && ply >= 20)
            {
                continue;
            }

            if (phase == "middlegame" && (ply < 20 || ply >= 60))
            {
                continue;
            }

            if (phase == "endgame" && ply < 60)
            {
                continue;
            }

            result.Add(ply);
        }

        return result;
    }

    private static bool TryGetFenBeforeAfter(
        IReadOnlyList<string> moves,
        int chosenPly,
        out string fenBefore,
        out string fenAfter)
    {
        fenBefore = "";
        fenAfter = "";

        try
        {
            var board = new ChessBoard();

            for (var ply = 0; ply < moves.Count; ply++)
            {
                if (ply == chosenPly)
                {
                    fenBefore = GeraFen.GetFen(board);

                    board.Move(moves[ply]);

                    fenAfter = GeraFen.GetFen(board);
                    return true;
                }

                board.Move(moves[ply]);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolveUciByFenDiff(string fenBefore, string fenAfter, out UciMove uci)
    {
        uci = default;

        var targetKey = FenKey(fenAfter);
        var board = new ChessDotNetBoardAdapter();

        try
        {
            board.LoadFen(fenBefore);
        }
        catch
        {
            return false;
        }

        var found = default(UciMove);
        var matches = 0;

        foreach (var move in board.GenerateLegalMoves())
        {
            string undoToken;

            try
            {
                undoToken = board.Make(move);
            }
            catch
            {
                continue;
            }

            var candidateKey = FenKey(board.ToFen());
            board.Unmake(undoToken);

            if (candidateKey != targetKey)
            {
                continue;
            }

            found = move;
            matches++;

            if (matches > 1)
            {
                break;
            }
        }

        if (matches != 1)
        {
            return false;
        }

        uci = found;
        return true;
    }

    private static string FenKey(string fen)
    {
        var parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 4)
        {
            return fen.Trim();
        }

        return $"{parts[0]} {parts[1]} {parts[2]} {parts[3]}";
    }

    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static void WriteTsv(string path, List<TrainingSample> samples, int start, int count)
    {
        using var writer = new StreamWriter(path, append: false, encoding: new UTF8Encoding(false));

        for (var i = 0; i < count; i++)
        {
            var sample = samples[start + i];

            writer.Write(sample.Fen);
            writer.Write('\t');
            writer.WriteLine(sample.MoveUci);
        }
    }

    private static void CreateOutputDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
