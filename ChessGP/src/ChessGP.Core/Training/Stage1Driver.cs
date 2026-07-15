using System.Diagnostics;
using System.Globalization;
using System.Text;
using ChessGP.Core.Config;
using ChessGP.Core.Data;
using ChessGP.Core.GA;
using ChessGP.Core.Genome;
using ChessGP.Core.Persistence;

namespace ChessGP.Core.Training;

/// <summary>
/// Runs Stage 1 evolution against labeled training samples.
/// </summary>
public static class Stage1Driver
{
    /// <summary>
    /// Runs a complete Stage 1 training session using the supplied application options.
    /// </summary>
    public static void Run(AppOptions options, string configBaseDir)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(configBaseDir))
        {
            configBaseDir = AppContext.BaseDirectory;
        }

        var runId = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
        const string stage = "stage1";

        string ResolvePath(string path) => Path.GetFullPath(path, configBaseDir);

        var dataPath = ResolvePath(options.Data.path);
        var checkpointsDir = ResolvePath(options.Output.checkpointsDir);
        var metricsPath = ResolvePath(options.Output.metricsFile);
        var testMetricsPath = Path.Combine(
            Path.GetDirectoryName(metricsPath) ?? "",
            Path.GetFileNameWithoutExtension(metricsPath) + "_stage1_test.csv");

        var checkpointer = new Checkpointer(checkpointsDir);
        var metrics = new MetricsWriter(metricsPath);
        var schema = GeneSchemaBuilder.FromConfig(options.Genome);

        var evaluationSampleCount = options.Stage1.batchSize;
        if (evaluationSampleCount <= 0)
        {
            throw new ArgumentException("Stage1.batchSize must be > 0.");
        }

        var (trainTsv, testTsv) = EnsureStage1Datasets(
            options,
            dataPath,
            options.Rng.seed,
            evaluationSampleCount);

        var trainSamples = LoadSamplesFromTsv(trainTsv, max: evaluationSampleCount);
        var testSamples = LoadSamplesFromTsv(testTsv, max: evaluationSampleCount);

        var trainer = new Stage1Trainer();

        var initRng = new Random(options.Rng.seed);
        var population = GaEngine.CreateRandomPopulation(options.Stage1.population, schema, initRng);

        var warmStartCheckpoint = checkpointer.LoadLatest(stage: stage);
        if (warmStartCheckpoint is not null &&
            string.Equals(warmStartCheckpoint.SchemaId, options.Genome.schemaId, StringComparison.OrdinalIgnoreCase) &&
            warmStartCheckpoint.GenomeBits.Length == schema.TotalBits)
        {
            population[0] = new Individual(warmStartCheckpoint.GenomeBits, Fitness: 0);
        }

        Console.WriteLine("=== RunStage1 ===");
        Console.WriteLine($"runId:     {runId}");
        Console.WriteLine($"train:     {trainTsv} (N={trainSamples.Count:n0})");
        Console.WriteLine($"test:      {testTsv} (N={testSamples.Count:n0})");
        Console.WriteLine($"pop:       {options.Stage1.population}");
        Console.WriteLine($"gens:      {options.Stage1.generations}");
        Console.WriteLine($"seed:      {options.Rng.seed}");
        Console.WriteLine($"schemaId:  {options.Genome.schemaId} (bits={schema.TotalBits})");
        Console.WriteLine($"metrics:   {metricsPath}");
        Console.WriteLine($"ckptDir:   {checkpointsDir}");
        Console.WriteLine("=================");

        for (var generation = 0; generation < options.Stage1.generations; generation++)
        {
            var generationStopwatch = Stopwatch.StartNew();
            var generationSeed = MixSeed(options.Rng.seed, generation);

            var evaluated = new List<Individual>(capacity: population.Count);
            long nodesTotal = 0;

            foreach (var individual in population)
            {
                var result = trainer.Evaluate(
                    individual.GenomeBits,
                    schema,
                    trainSamples,
                    options.Stage1);

                nodesTotal += result.NodesEvaluated;
                evaluated.Add(new Individual(individual.GenomeBits, result.Fitness));
            }

            var best = evaluated.OrderByDescending(individual => individual.Fitness).First();
            var bestFitness = (double)best.Fitness;

            var fitnessValues = evaluated
                .Select(individual => (double)individual.Fitness)
                .OrderBy(fitness => fitness)
                .ToArray();

            var meanFitness = fitnessValues.Average();
            var medianFitness = Median(fitnessValues);

            var testNote = "";
            if (testSamples.Count > 0)
            {
                var testResult = trainer.Evaluate(best.GenomeBits, schema, testSamples, options.Stage1);
                var testAccuracy = testResult.Total > 0
                    ? (double)testResult.Correct / testResult.Total
                    : 0.0;

                testNote = $" | testAcc={testAccuracy:0.000} ({testResult.Correct}/{testResult.Total})";

                AppendStage1TestMetrics(
                    testMetricsPath,
                    runId,
                    generation,
                    testResult.Correct,
                    testResult.Total,
                    testAccuracy);
            }

            var checkpointPath = checkpointer.SaveBest(
                runId,
                stage,
                generation,
                options.Genome.schemaId,
                best.GenomeBits);

            generationStopwatch.Stop();

            var gaSeed = MixSeed(generationSeed, 0x9E3779B);
            var gaRng = new Random(gaSeed);

            metrics.Append(new GenerationMetricsRow(
                RunId: runId,
                Stage: stage,
                Generation: generation,
                BestFitness: bestFitness,
                MeanFitness: meanFitness,
                MedianFitness: medianFitness,
                NodesEvaluated: nodesTotal,
                Seed: gaSeed,
                ElapsedMs: generationStopwatch.ElapsedMilliseconds,
                BestCheckpointPath: checkpointPath));

            Console.WriteLine(
                $"gen {generation:D4} | " +
                $"best={bestFitness:0} " +
                $"mean={meanFitness:0.00} " +
                $"median={medianFitness:0.00} " +
                $"nodes={nodesTotal:n0} " +
                $"seed={generationSeed}" +
                testNote);

            population = GaEngine.NextGeneration(
                current: evaluated,
                populationSize: options.Stage1.population,
                schema: schema,
                stage1: options.Stage1,
                ga: options.GA,
                rng: gaRng);
        }

        Console.WriteLine("Stage1 run complete.");
    }

    private static (string trainTsv, string testTsv) EnsureStage1Datasets(
        AppOptions options,
        string resolvedDataPath,
        int baseSeed,
        int evaluationSampleCount)
    {
        if (string.Equals(options.Data.format, "TSV", StringComparison.OrdinalIgnoreCase))
        {
            var trainTsv = resolvedDataPath;
            var testTsv = DeriveTestPath(trainTsv);

            if (!File.Exists(trainTsv))
            {
                throw new FileNotFoundException($"Train TSV not found: {trainTsv}");
            }

            if (!File.Exists(testTsv))
            {
                throw new FileNotFoundException(
                    $"Test TSV not found: {testTsv}\n" +
                    "For Data.format=TSV you must provide both train and test files.\n" +
                    "Tip: name files like '*.train.uci.tsv' and '*.test.uci.tsv'.");
            }

            var trainLines = CountLinesUpTo(trainTsv, evaluationSampleCount);
            var testLines = CountLinesUpTo(testTsv, evaluationSampleCount);

            if (trainLines < evaluationSampleCount || testLines < evaluationSampleCount)
            {
                throw new InvalidOperationException(
                    $"Train/test TSV too short for N={evaluationSampleCount}.\n" +
                    $"Train lines: {trainLines}, Test lines: {testLines}\n" +
                    "Either lower Stage1.batchSize or provide larger TSV files.");
            }

            Console.WriteLine(
                $"Reusing TSV datasets: trainLines>={evaluationSampleCount}, " +
                $"testLines>={evaluationSampleCount}. Using N={evaluationSampleCount}.");

            return (trainTsv, testTsv);
        }

        if (string.Equals(options.Data.format, "PGN", StringComparison.OrdinalIgnoreCase))
        {
            var directory = Path.GetDirectoryName(resolvedDataPath) ?? "";
            var baseName = Path.GetFileNameWithoutExtension(resolvedDataPath);

            var trainTsv = Path.Combine(directory, baseName + ".train.uci.tsv");
            var testTsv = Path.Combine(directory, baseName + ".test.uci.tsv");

            if (File.Exists(trainTsv) && File.Exists(testTsv))
            {
                var trainLines = CountLinesUpTo(trainTsv, evaluationSampleCount);
                var testLines = CountLinesUpTo(testTsv, evaluationSampleCount);

                if (trainLines >= evaluationSampleCount && testLines >= evaluationSampleCount)
                {
                    Console.WriteLine($"Reusing existing paper datasets. Using N={evaluationSampleCount}.");
                    return (trainTsv, testTsv);
                }

                Console.WriteLine(
                    $"Existing datasets are too short for N={evaluationSampleCount} " +
                    $"(train={trainLines}, test={testLines}). Rebuilding...");
            }

            Console.WriteLine($"Building paper-style train/test TSV from PGN: {resolvedDataPath}");
            Console.WriteLine($"Target: train={evaluationSampleCount}, test={evaluationSampleCount}");

            var ingestionOptions = new PgnIngestorOptions
            {
                Filters = options.Data.filters,
                SkipInvalidGames = true
            };

            var datasetSeed = MixSeed(baseSeed, 0xC0FFEE);

            var stats = PgnIngestor.BuildTrainTestFromPgn(
                inputPgnPath: resolvedDataPath,
                trainTsvPath: trainTsv,
                testTsvPath: testTsv,
                trainCount: evaluationSampleCount,
                testCount: evaluationSampleCount,
                seed: datasetSeed,
                options: ingestionOptions);

            if (stats.TrainWritten < evaluationSampleCount || stats.TestWritten < evaluationSampleCount)
            {
                throw new InvalidOperationException(
                    $"Not enough eligible decisive games to build dataset for N={evaluationSampleCount}.\n" +
                    $"Got train={stats.TrainWritten}, test={stats.TestWritten}.\n" +
                    "Use a larger PGN database or lower Stage1.batchSize.");
            }

            Console.WriteLine(
                $"Dataset built: collected={stats.SamplesCollected:n0}, " +
                $"train={stats.TrainWritten:n0}, test={stats.TestWritten:n0}");

            Console.WriteLine(
                $"Skipped: draw={stats.SkippedDraws:n0} " +
                $"unknown={stats.SkippedUnknown:n0} " +
                $"tooShort={stats.SkippedTooShort:n0} " +
                $"invalid={stats.SkippedInvalid:n0}");

            return (trainTsv, testTsv);
        }

        throw new NotSupportedException($"Unsupported Data.format: {options.Data.format}. Use PGN or TSV.");
    }

    private static void AppendStage1TestMetrics(
        string csvPath,
        string runId,
        int generation,
        int correct,
        int total,
        double accuracy)
    {
        var directory = Path.GetDirectoryName(csvPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var writeHeader = !File.Exists(csvPath);

        using var writer = new StreamWriter(
            csvPath,
            append: true,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (writeHeader)
        {
            writer.WriteLine("runId,generation,testCorrect,testTotal,testAccuracy");
        }

        writer.Write(runId);
        writer.Write(',');
        writer.Write(generation);
        writer.Write(',');
        writer.Write(correct);
        writer.Write(',');
        writer.Write(total);
        writer.Write(',');
        writer.WriteLine(accuracy.ToString(CultureInfo.InvariantCulture));
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

    private static int CountLinesUpTo(string path, int limit)
    {
        var count = 0;

        foreach (var _ in File.ReadLines(path))
        {
            count++;

            if (count >= limit)
            {
                break;
            }
        }

        return count;
    }

    private static int MixSeed(int baseSeed, int value)
    {
        unchecked
        {
            var hash = baseSeed;

            hash = hash * 397 ^ value;
            hash ^= hash << 13;
            hash ^= hash >> 17;
            hash ^= hash << 5;

            return hash;
        }
    }

    private static double Median(double[] sorted)
    {
        if (sorted.Length == 0)
        {
            return 0.0;
        }

        var middle = sorted.Length / 2;

        if ((sorted.Length & 1) == 1)
        {
            return sorted[middle];
        }

        return (sorted[middle - 1] + sorted[middle]) / 2.0;
    }
}
