using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChessGP.Core.Config;
using ChessGP.Core.Persistence;
using ChessGP.Core.Training;
using Xunit;
using ConfigGeneSpec = ChessGP.Core.Config.GeneSpec;

namespace ChessGP.Tests.Training;

public sealed class Stage2DeterminismTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Run_SameSeedAndOpenings_ProducesSameMetricsAndBestGenome()
    {
        const string drawFen = "8/8/8/8/8/8/8/K6k w - - 0 1";

        var firstRootDirectory = Path.Combine(Path.GetTempPath(), "chessgp_stage2_detA_" + Guid.NewGuid());
        var secondRootDirectory = Path.Combine(Path.GetTempPath(), "chessgp_stage2_detB_" + Guid.NewGuid());

        Directory.CreateDirectory(firstRootDirectory);
        Directory.CreateDirectory(secondRootDirectory);

        try
        {
            WriteOpeningBook(firstRootDirectory, drawFen);
            WriteOpeningBook(secondRootDirectory, drawFen);

            var options = CreateStage2Options(seed: 777);

            Stage2Driver.Run(options, configBaseDir: firstRootDirectory);
            Stage2Driver.Run(options, configBaseDir: secondRootDirectory);

            var firstMetrics = ReadLastMetricsRow(
                Path.Combine(firstRootDirectory, "artifacts", "metrics.csv"));

            var secondMetrics = ReadLastMetricsRow(
                Path.Combine(secondRootDirectory, "artifacts", "metrics.csv"));

            Assert.Equal(firstMetrics.Stage, secondMetrics.Stage);
            Assert.Equal(firstMetrics.Generation, secondMetrics.Generation);
            Assert.Equal(firstMetrics.BestFitness, secondMetrics.BestFitness, precision: 12);
            Assert.Equal(firstMetrics.MeanFitness, secondMetrics.MeanFitness, precision: 12);
            Assert.Equal(firstMetrics.MedianFitness, secondMetrics.MedianFitness, precision: 12);
            Assert.Equal(firstMetrics.NodesEvaluated, secondMetrics.NodesEvaluated);
            Assert.Equal(firstMetrics.Seed, secondMetrics.Seed);

            var firstCheckpoint = ReadAnyCheckpoint(
                Path.Combine(firstRootDirectory, "artifacts", "checkpoints"));

            var secondCheckpoint = ReadAnyCheckpoint(
                Path.Combine(secondRootDirectory, "artifacts", "checkpoints"));

            Assert.Equal("stage2", firstCheckpoint.Stage);
            Assert.Equal("stage2", secondCheckpoint.Stage);
            Assert.Equal(0, firstCheckpoint.Generation);
            Assert.Equal(0, secondCheckpoint.Generation);
            Assert.Equal(firstCheckpoint.SchemaId, secondCheckpoint.SchemaId);
            Assert.Equal(firstCheckpoint.GenomeBits, secondCheckpoint.GenomeBits);
        }
        finally
        {
            TryDeleteDirectory(firstRootDirectory);
            TryDeleteDirectory(secondRootDirectory);
        }
    }

    private static void WriteOpeningBook(string rootDirectory, string fen)
    {
        File.WriteAllText(
            Path.Combine(rootDirectory, "openings.txt"),
            fen + Environment.NewLine);
    }

    private static AppOptions CreateStage2Options(int seed)
    {
        return new AppOptions(
            Data: new DataOptions(
                path: "",
                format: "TSV",
                filters: null,
                labeling: null),
            Stage1: new Stage1Options(
                population: 1,
                crossoverRate: 0,
                mutationRate: 0,
                generations: 1,
                batchSize: 1,
                ply: 1),
            Stage2: new Stage2Options(
                population: 4,
                crossoverRate: 0.0,
                mutationRate: 0.0,
                generations: 1,
                gamesPerPair: 4,
                openingBookPath: "openings.txt",
                useHallOfFame: false,
                hallOfFameSize: 0,
                searchDepth: 1,
                searchMode: "FixedDepth",
                timePerMoveMs: 0,
                perMoveNodeCap: 0),
            GA: new GaOptions(
                selectionType: "tournament",
                tournamentK: 1,
                bitFlipRate: 0.0,
                crossoverType: "uniform-per-gene",
                elitism: 1),
            Genome: new GenomeOptions(
                schemaId: "test",
                genes: new List<ConfigGeneSpec>
                {
                    new("Dummy", 1, 0, 1, null)
                }),
            Rng: new RngOptions(seed: seed),
            Output: new OutputOptions(
                checkpointsDir: "artifacts/checkpoints",
                metricsFile: "artifacts/metrics.csv"));
    }

    private static MetricsSnapshot ReadLastMetricsRow(string csvPath)
    {
        Assert.True(File.Exists(csvPath), $"Missing metrics file: {csvPath}");

        var lines = File.ReadAllLines(csvPath);

        Assert.True(lines.Length >= 2, "metrics.csv should contain header + at least one row.");

        var parts = lines[^1].Split(',');

        Assert.True(parts.Length >= 9, "metrics row has unexpected format.");

        var invariantCulture = CultureInfo.InvariantCulture;

        return new MetricsSnapshot(
            Stage: parts[1],
            Generation: int.Parse(parts[2], invariantCulture),
            BestFitness: double.Parse(parts[3], invariantCulture),
            MeanFitness: double.Parse(parts[4], invariantCulture),
            MedianFitness: double.Parse(parts[5], invariantCulture),
            NodesEvaluated: long.Parse(parts[6], invariantCulture),
            Seed: int.Parse(parts[7], invariantCulture));
    }

    private static BestGenomeCheckpoint ReadAnyCheckpoint(string checkpointsDirectory)
    {
        Assert.True(Directory.Exists(checkpointsDirectory), $"Missing checkpoint dir: {checkpointsDirectory}");

        var file = Directory.EnumerateFiles(checkpointsDirectory, "*.json").FirstOrDefault();

        Assert.False(string.IsNullOrWhiteSpace(file), "No checkpoint files found.");

        var json = File.ReadAllText(file);
        var checkpoint = JsonSerializer.Deserialize<BestGenomeCheckpoint>(json, JsonOptions);

        Assert.NotNull(checkpoint);

        return checkpoint!;
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for temporary test directories.
        }
    }

    private readonly record struct MetricsSnapshot(
        string Stage,
        int Generation,
        double BestFitness,
        double MeanFitness,
        double MedianFitness,
        long NodesEvaluated,
        int Seed);
}
