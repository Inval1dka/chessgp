using System.Globalization;
using System.Text.Json;
using ChessGP.Core.Config;
using ChessGP.Core.Data;
using ChessGP.Core.Engine;
using ChessGP.Core.Eval;
using ChessGP.Core.Genome;
using ChessGP.Core.Persistence;
using ChessGP.Core.Training;

/// <summary>
/// Command-line helper for evaluating a saved genome checkpoint on a labeled test dataset.
/// </summary>
internal static class EvaluateCheckpointCommand
{
    /// <summary>
    /// Runs checkpoint evaluation using the supplied application configuration and command arguments.
    /// </summary>
    public static void Run(AppOptions options, string configBaseDir, string[] args)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length < 1)
        {
            PrintUsage();
            return;
        }

        var checkpointPath = ResolveExistingFile(args[0], configBaseDir);

        string? testPath = null;
        var maxSamples = options.Stage1.batchSize;
        var depth = 1;
        int? nodeCap = null;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];

            if (argument.Equals("--test-path", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                testPath = args[++index];
                continue;
            }

            if (argument.Equals("--max", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                maxSamples = Math.Max(1, int.Parse(args[++index], CultureInfo.InvariantCulture));
                continue;
            }

            if (argument.Equals("--depth", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                depth = Math.Max(1, int.Parse(args[++index], CultureInfo.InvariantCulture));
                continue;
            }

            if (argument.Equals("--node-cap", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                var value = int.Parse(args[++index], CultureInfo.InvariantCulture);
                nodeCap = value > 0 ? value : null;
                continue;
            }

            throw new ArgumentException($"Unknown or incomplete argument: {argument}");
        }

        var resolvedTestPath = !string.IsNullOrWhiteSpace(testPath)
            ? ResolveExistingFile(testPath, configBaseDir)
            : DeriveDefaultTestPath(options, configBaseDir);

        var checkpoint = LoadCheckpoint(checkpointPath);
        var schema = GeneSchemaBuilder.FromConfig(options.Genome);

        if (!checkpoint.SchemaId.Equals(options.Genome.schemaId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Checkpoint schema '{checkpoint.SchemaId}' does not match config schema '{options.Genome.schemaId}'.");
        }

        if (checkpoint.GenomeBits.Length != schema.TotalBits)
        {
            throw new InvalidOperationException(
                $"Checkpoint genome length {checkpoint.GenomeBits.Length} does not match schema length {schema.TotalBits}.");
        }

        var samples = LoadSamplesFromTsv(resolvedTestPath, maxSamples);

        if (samples.Count == 0)
        {
            throw new InvalidOperationException($"No test samples loaded from {resolvedTestPath}.");
        }

        var result = depth <= 1
            ? EvaluateOnePly(checkpoint.GenomeBits, schema, samples, options.Stage1)
            : EvaluateSearch(checkpoint.GenomeBits, schema, samples, depth, nodeCap, options.Stage2);

        var accuracy = result.Total > 0
            ? (double)result.Correct / result.Total
            : 0.0;

        Console.WriteLine("=== Checkpoint evaluation ===");
        Console.WriteLine($"Checkpoint: {checkpointPath}");
        Console.WriteLine($"Stage:      {checkpoint.Stage}");
        Console.WriteLine($"Generation: {checkpoint.Generation}");
        Console.WriteLine($"Schema:     {checkpoint.SchemaId}");
        Console.WriteLine($"Test file:  {resolvedTestPath}");
        Console.WriteLine($"Samples:    {result.Total}");
        Console.WriteLine($"Depth:      {depth}");
        Console.WriteLine($"Correct:    {result.Correct}");
        Console.WriteLine($"Accuracy:   {accuracy.ToString("0.000000", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Accuracy %: {(100.0 * accuracy).ToString("0.00", CultureInfo.InvariantCulture)}%");
        Console.WriteLine($"Nodes:      {result.NodesEvaluated}");
    }

    private static EvalResult EvaluateOnePly(
        string genomeBits,
        GeneSchema schema,
        IReadOnlyList<TrainingSample> samples,
        Stage1Options stage1Options)
    {
        var trainer = new Stage1Trainer();
        var result = trainer.Evaluate(genomeBits, schema, samples, stage1Options);

        return new EvalResult(
            Correct: result.Correct,
            Total: result.Total,
            NodesEvaluated: result.NodesEvaluated);
    }

    private static EvalResult EvaluateSearch(
        string genomeBits,
        GeneSchema schema,
        IReadOnlyList<TrainingSample> samples,
        int depth,
        int? nodeCap,
        Stage2Options stage2Options)
    {
        var parameters = GenomeToParamsDecoder.Decode(genomeBits, schema);
        var evaluator = new ParametricEvaluator(parameters);

        var engine = new EngineCore();
        var board = new ChessDotNetBoardAdapter();

        var correct = 0;
        var total = 0;
        long nodesEvaluated = 0;

        var limits = new SearchLimits(
            MaxDepth: depth,
            NodeCap: nodeCap,
            DrawContemptCp: Math.Max(0, stage2Options.drawContemptCp),
            AvoidDrawWhenAheadCp: Math.Max(0, stage2Options.avoidDrawWhenAheadCp));

        foreach (var sample in samples)
        {
            var expectedMove = UciMove.Parse(sample.MoveUci);

            board.LoadFen(sample.Fen);

            var result = engine.SearchAlphaBeta(board, evaluator, limits);
            nodesEvaluated += result.Nodes;

            total++;

            if (SameMove(result.Move, expectedMove))
            {
                correct++;
            }
        }

        return new EvalResult(
            Correct: correct,
            Total: total,
            NodesEvaluated: nodesEvaluated);
    }

    private static List<TrainingSample> LoadSamplesFromTsv(string path, int max)
    {
        var samples = new List<TrainingSample>(capacity: max);

        foreach (var line in File.ReadLines(path))
        {
            if (samples.Count >= max)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split('\t');
            if (parts.Length < 2)
            {
                continue;
            }

            var fen = parts[0].Trim();
            var uci = parts[1].Trim();

            if (fen.Length == 0 || uci.Length == 0)
            {
                continue;
            }

            samples.Add(new TrainingSample(fen, uci, PlyIndex: 0));
        }

        return samples;
    }

    private static string DeriveDefaultTestPath(AppOptions options, string configBaseDir)
    {
        var trainPath = Path.GetFullPath(Path.Combine(configBaseDir, options.Data.path));
        var testPath = DeriveTestPath(trainPath);

        if (!File.Exists(testPath))
        {
            throw new FileNotFoundException(
                $"Default test TSV was not found: {testPath}\n" +
                "Use --test-path <path> to specify it explicitly.");
        }

        return testPath;
    }

    private static string DeriveTestPath(string trainPath)
    {
        var fileName = Path.GetFileName(trainPath);

        if (fileName.Contains(".train.", StringComparison.OrdinalIgnoreCase))
        {
            var replacedName = fileName.Replace(".train.", ".test.", StringComparison.OrdinalIgnoreCase);
            return Path.Combine(Path.GetDirectoryName(trainPath) ?? "", replacedName);
        }

        var directory = Path.GetDirectoryName(trainPath) ?? "";
        var baseName = Path.GetFileNameWithoutExtension(trainPath);
        var extension = Path.GetExtension(trainPath);

        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".tsv";
        }

        return Path.Combine(directory, baseName + ".test" + extension);
    }

    private static BestGenomeCheckpoint LoadCheckpoint(string path)
    {
        var json = File.ReadAllText(path);
        var checkpoint = JsonSerializer.Deserialize<BestGenomeCheckpoint>(json);

        if (checkpoint is null)
        {
            throw new InvalidOperationException($"Could not read checkpoint: {path}");
        }

        return checkpoint;
    }

    private static string ResolveExistingFile(string path, string configBaseDir)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty.", nameof(path));
        }

        var currentDirectoryPath = Path.GetFullPath(path);
        if (File.Exists(currentDirectoryPath))
        {
            return currentDirectoryPath;
        }

        var configRelativePath = Path.GetFullPath(Path.Combine(configBaseDir, path));
        if (File.Exists(configRelativePath))
        {
            return configRelativePath;
        }

        throw new FileNotFoundException($"File not found: {path}");
    }

    private static bool SameMove(UciMove first, UciMove second)
    {
        return string.Equals(first.From, second.From, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(first.To, second.To, StringComparison.OrdinalIgnoreCase) &&
               char.ToLowerInvariant(first.Promotion) == char.ToLowerInvariant(second.Promotion);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  ChessGP.App eval-checkpoint <config.json> <checkpoint.json> [--test-path PATH] [--max N] [--depth N] [--node-cap N]");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ChessGP.App eval-checkpoint configs/stage2_depth3.json checkpoints/best.json --test-path data/stage1.test.uci.tsv --max 4000");
        Console.WriteLine("  ChessGP.App eval-checkpoint configs/stage2_depth3.json checkpoints/best.json --test-path data/stage1.test.uci.tsv --max 4000 --depth 3");
    }

    private readonly record struct EvalResult(
        int Correct,
        int Total,
        long NodesEvaluated);
}
