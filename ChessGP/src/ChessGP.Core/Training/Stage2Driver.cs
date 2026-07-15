using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChessGP.Core.Config;
using ChessGP.Core.Engine;
using ChessGP.Core.Eval;
using ChessGP.Core.GA;
using ChessGP.Core.Genome;
using ChessGP.Core.Persistence;

namespace ChessGP.Core.Training;

/// <summary>
/// Runs Stage 2 evolution through self-play matches.
/// </summary>
public static class Stage2Driver
{
    private const long FitnessScale = 1_000_000;
    private const int HallOfFameMatchesPerGeneration = 2;

    /// <summary>
    /// Runs a complete Stage 2 training session using the supplied application options.
    /// </summary>
    public static void Run(AppOptions options, string configBaseDir)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(configBaseDir))
        {
            configBaseDir = AppContext.BaseDirectory;
        }

        var runId = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
        const string stage = "stage2";

        string ResolvePath(string path) => Path.GetFullPath(path, configBaseDir);

        var checkpointsDir = ResolvePath(options.Output.checkpointsDir);
        var metricsPath = ResolvePath(options.Output.metricsFile);

        var checkpointer = new Checkpointer(checkpointsDir);
        var metrics = new MetricsWriter(metricsPath);

        var schema = GeneSchemaBuilder.FromConfig(options.Genome);
        var openings = LoadOpenings(options, ResolvePath);

        var initRng = new Random(options.Rng.seed);
        var population = GaEngine.CreateRandomPopulation(options.Stage2.population, schema, initRng);

        var stage1Seeds = LoadStage1Seeds(
            checkpointsDir,
            options.Genome.schemaId,
            schema.TotalBits,
            max: options.Stage2.population);

        for (var index = 0; index < stage1Seeds.Count && index < population.Count; index++)
        {
            population[index] = new Individual(stage1Seeds[index], Fitness: 0);
        }

        var warmStartCheckpoint = checkpointer.LoadLatest(stage: stage);
        if (warmStartCheckpoint is not null &&
            string.Equals(warmStartCheckpoint.SchemaId, options.Genome.schemaId, StringComparison.OrdinalIgnoreCase) &&
            warmStartCheckpoint.GenomeBits.Length == schema.TotalBits)
        {
            population[0] = new Individual(warmStartCheckpoint.GenomeBits, Fitness: 0);
        }

        Console.WriteLine("=== RunStage2 ===");
        Console.WriteLine($"runId:     {runId}");
        Console.WriteLine($"pop:       {options.Stage2.population}");
        Console.WriteLine($"gens:      {options.Stage2.generations}");
        Console.WriteLine($"gamesPair: {options.Stage2.gamesPerPair}");
        Console.WriteLine($"search:    {SearchDescription(options.Stage2)}");
        Console.WriteLine($"seed:      {options.Rng.seed}");
        Console.WriteLine($"schemaId:  {options.Genome.schemaId} (bits={schema.TotalBits})");
        Console.WriteLine($"openings:  {(string.IsNullOrWhiteSpace(options.Stage2.openingBookPath) ? "(startpos)" : options.Stage2.openingBookPath)}");
        Console.WriteLine($"metrics:   {metricsPath}");
        Console.WriteLine($"ckptDir:   {checkpointsDir}");
        Console.WriteLine("=================");

        var hallOfFame = new List<string>();

        for (var generation = 0; generation < options.Stage2.generations; generation++)
        {
            var stopwatch = Stopwatch.StartNew();
            var generationSeed = MixSeed(options.Rng.seed, generation);

            var evaluatorCache = new Dictionary<string, IEvaluator>(StringComparer.Ordinal);

            IEvaluator EvaluatorFor(string genomeBits)
            {
                if (evaluatorCache.TryGetValue(genomeBits, out var cachedEvaluator))
                {
                    return cachedEvaluator;
                }

                var parameters = GenomeToParamsDecoder.Decode(genomeBits, schema);
                var evaluator = new ParametricEvaluator(parameters);

                evaluatorCache[genomeBits] = evaluator;
                return evaluator;
            }

            var populationSize = population.Count;
            var points = new long[populationSize];

            long nodesTotal = 0;
            long searchElapsedTotal = 0;

            var searchesTotal = 0;
            var completedDepthTotal = 0;
            var timeLossesTotal = 0;

            for (var firstIndex = 0; firstIndex < populationSize - 1; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < populationSize; secondIndex++)
                {
                    var pairSeed = MixSeed(generationSeed, (firstIndex * 1_000_003) ^ secondIndex);
                    var pairRng = new Random(pairSeed);

                    var first = population[firstIndex];
                    var second = population[secondIndex];

                    var score = MatchRunner.PlayMatch(
                        openingFens: openings,
                        firstEvaluator: EvaluatorFor(first.GenomeBits),
                        secondEvaluator: EvaluatorFor(second.GenomeBits),
                        config: options.Stage2,
                        rng: pairRng);

                    nodesTotal += score.NodesEvaluated;
                    searchElapsedTotal += score.SearchElapsedMs;
                    searchesTotal += score.Searches;
                    completedDepthTotal += score.CompletedDepthTotal;
                    timeLossesTotal += score.TimeLosses;

                    var firstPoints = score.Points;
                    var secondPoints = (long)score.Losses * 2 + score.Draws;

                    points[firstIndex] += firstPoints;
                    points[secondIndex] += secondPoints;
                }
            }

            var hallOfFameActive =
                options.Stage2.useHallOfFame &&
                options.Stage2.hallOfFameSize > 0 &&
                hallOfFame.Count > 0;

            var hallOfFameMatches = hallOfFameActive
                ? Math.Min(HallOfFameMatchesPerGeneration, hallOfFame.Count)
                : 0;

            if (hallOfFameActive && hallOfFameMatches > 0)
            {
                for (var index = 0; index < populationSize; index++)
                {
                    var usedOpponents = new HashSet<string>(StringComparer.Ordinal);

                    for (var matchIndex = 0; matchIndex < hallOfFameMatches; matchIndex++)
                    {
                        var opponent = PickHallOfFameOpponent(
                            hallOfFame,
                            population[index].GenomeBits,
                            index,
                            generation,
                            matchIndex,
                            usedOpponents);

                        usedOpponents.Add(opponent);

                        var hallOfFameSeed = MixSeed(
                            generationSeed,
                            0x51ED270B ^ (index * 31 + matchIndex));

                        var hallOfFameRng = new Random(hallOfFameSeed);

                        var score = MatchRunner.PlayMatch(
                            openingFens: openings,
                            firstEvaluator: EvaluatorFor(population[index].GenomeBits),
                            secondEvaluator: EvaluatorFor(opponent),
                            config: options.Stage2,
                            rng: hallOfFameRng);

                        nodesTotal += score.NodesEvaluated;
                        searchElapsedTotal += score.SearchElapsedMs;
                        searchesTotal += score.Searches;
                        completedDepthTotal += score.CompletedDepthTotal;
                        timeLossesTotal += score.TimeLosses;

                        points[index] += score.Points;
                    }
                }
            }

            var extraMatches = hallOfFameActive ? hallOfFameMatches : 0;
            var possiblePoints = 2L * options.Stage2.gamesPerPair * (populationSize - 1 + extraMatches);

            if (possiblePoints <= 0)
            {
                possiblePoints = 1;
            }

            var evaluated = new List<Individual>(capacity: populationSize);

            for (var index = 0; index < populationSize; index++)
            {
                var relativeFitness = (double)points[index] / possiblePoints;
                relativeFitness = Math.Clamp(relativeFitness, 0, 1);

                var scaledFitness = (long)Math.Round(
                    relativeFitness * FitnessScale,
                    MidpointRounding.AwayFromZero);

                evaluated.Add(new Individual(population[index].GenomeBits, scaledFitness));
            }

            var best = evaluated.OrderByDescending(individual => individual.Fitness).First();

            var fitnessValues = evaluated
                .Select(individual => individual.Fitness / (double)FitnessScale)
                .OrderBy(fitness => fitness)
                .ToArray();

            var bestFitness = best.Fitness / (double)FitnessScale;
            var meanFitness = fitnessValues.Average();
            var medianFitness = Median(fitnessValues);

            var checkpointPath = checkpointer.SaveBest(
                runId,
                stage,
                generation,
                options.Genome.schemaId,
                best.GenomeBits);

            if (options.Stage2.useHallOfFame && options.Stage2.hallOfFameSize > 0)
            {
                AddToHallOfFame(hallOfFame, best.GenomeBits, options.Stage2.hallOfFameSize);
            }

            stopwatch.Stop();

            var averageCompletedDepth =
                searchesTotal > 0 ? (double)completedDepthTotal / searchesTotal : 0.0;

            var averageSearchMs =
                searchesTotal > 0 ? (double)searchElapsedTotal / searchesTotal : 0.0;

            metrics.Append(new GenerationMetricsRow(
                RunId: runId,
                Stage: stage,
                Generation: generation,
                BestFitness: bestFitness,
                MeanFitness: meanFitness,
                MedianFitness: medianFitness,
                NodesEvaluated: nodesTotal,
                Seed: generationSeed,
                ElapsedMs: stopwatch.ElapsedMilliseconds,
                BestCheckpointPath: checkpointPath,
                AverageCompletedDepth: averageCompletedDepth,
                AverageSearchMs: averageSearchMs,
                TimeLosses: timeLossesTotal));

            Console.WriteLine(
                $"gen {generation:D4} | " +
                $"best={bestFitness:0.000} " +
                $"mean={meanFitness:0.000} " +
                $"median={medianFitness:0.000} " +
                $"nodes={nodesTotal:n0} " +
                $"avgDepth={averageCompletedDepth:0.00} " +
                $"avgMs={averageSearchMs:0.0} " +
                $"flags={timeLossesTotal} " +
                $"seed={generationSeed}");

            var gaRng = new Random(MixSeed(generationSeed, 0x9E3779B));

            population = GaEngine.NextGenerationWithRates(
                current: evaluated,
                populationSize: options.Stage2.population,
                schema: schema,
                crossoverRate: options.Stage2.crossoverRate,
                mutationRate: options.Stage2.mutationRate,
                ga: options.GA,
                rng: gaRng);
        }

        Console.WriteLine("Stage2 run complete.");
    }

    private static List<string> LoadOpenings(AppOptions options, Func<string, string> resolvePath)
    {
        var openingBookPath = options.Stage2.openingBookPath ?? "";

        if (string.IsNullOrWhiteSpace(openingBookPath))
        {
            return new List<string> { OpeningBook.StartposFen };
        }

        var fullPath = resolvePath(openingBookPath);

        if (!File.Exists(fullPath))
        {
            return new List<string> { OpeningBook.StartposFen };
        }

        return OpeningBook.LoadFens(fullPath);
    }

    private static List<string> LoadStage1Seeds(
        string checkpointsDir,
        string schemaId,
        int totalBits,
        int max)
    {
        var seeds = new List<string>();

        if (!Directory.Exists(checkpointsDir))
        {
            return seeds;
        }

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var checkpoints = new List<BestGenomeCheckpoint>();

        foreach (var file in Directory.EnumerateFiles(checkpointsDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var checkpoint = JsonSerializer.Deserialize<BestGenomeCheckpoint>(json, jsonOptions);

                if (checkpoint is null)
                {
                    continue;
                }

                if (!string.Equals(checkpoint.Stage, "stage1", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.Equals(checkpoint.SchemaId, schemaId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (checkpoint.GenomeBits is null || checkpoint.GenomeBits.Length != totalBits)
                {
                    continue;
                }

                checkpoints.Add(checkpoint);
            }
            catch
            {
                // Ignore files that are not valid matching checkpoints.
            }
        }

        var latestPerRun = checkpoints
            .GroupBy(checkpoint => checkpoint.RunId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(checkpoint => checkpoint.Generation)
                .ThenByDescending(checkpoint => checkpoint.TimestampUtc)
                .First())
            .OrderByDescending(checkpoint => checkpoint.TimestampUtc)
            .Take(max);

        foreach (var checkpoint in latestPerRun)
        {
            seeds.Add(checkpoint.GenomeBits);
        }

        return seeds;
    }

    private static string PickHallOfFameOpponent(
        List<string> hallOfFame,
        string self,
        int index,
        int generation,
        int matchIndex,
        HashSet<string> usedOpponents)
    {
        var count = hallOfFame.Count;
        var start = (index + generation + matchIndex) % count;

        for (var offset = 0; offset < count; offset++)
        {
            var candidate = hallOfFame[(start + offset) % count];

            if (count > 1 && candidate == self)
            {
                continue;
            }

            if (usedOpponents.Contains(candidate))
            {
                continue;
            }

            return candidate;
        }

        return hallOfFame[start];
    }

    private static void AddToHallOfFame(List<string> hallOfFame, string genomeBits, int maxSize)
    {
        hallOfFame.RemoveAll(genome => genome == genomeBits);
        hallOfFame.Insert(0, genomeBits);

        if (hallOfFame.Count > maxSize)
        {
            hallOfFame.RemoveRange(maxSize, hallOfFame.Count - maxSize);
        }
    }

    private static string SearchDescription(Stage2Options options)
    {
        var drawContemptCp = options.drawContemptCp > 0 ? options.drawContemptCp : 30;
        var avoidDrawWhenAheadCp = options.avoidDrawWhenAheadCp > 0 ? options.avoidDrawWhenAheadCp : 200;

        return
            $"mode={options.searchMode} " +
            $"depth={options.searchDepth} " +
            $"maxDepth={options.maxSearchDepth} " +
            $"timeMoveMs={options.timePerMoveMs} " +
            $"initialMs={options.initialTimeMs} " +
            $"incMs={options.incrementMs} " +
            $"nodeCap={options.perMoveNodeCap} " +
            $"drawContempt={drawContemptCp} " +
            $"avoidDrawAhead={avoidDrawWhenAheadCp}";
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
