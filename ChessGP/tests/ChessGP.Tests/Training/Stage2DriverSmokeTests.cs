using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ChessGP.Core.Config;
using ChessGP.Core.Training;
using Xunit;
using ConfigGeneSpec = ChessGP.Core.Config.GeneSpec;

namespace ChessGP.Tests.Training;

public sealed class Stage2DriverSmokeTests
{
    [Fact]
    public void Run_RoundRobin_WritesMetricsAndCheckpoint_AndHasExpectedFitnessOnAllDrawOpenings()
    {
        const string drawFen = "8/8/8/8/8/8/8/K6k w - - 0 1";
        const double expectedBestFitness = 0.5;

        var rootDirectory = Path.Combine(Path.GetTempPath(), "chessgp_stage2_" + Guid.NewGuid());

        Directory.CreateDirectory(rootDirectory);

        try
        {
            var openingPath = Path.Combine(rootDirectory, "openings.txt");

            File.WriteAllText(openingPath, drawFen + Environment.NewLine);

            var options = CreateStage2Options();

            Stage2Driver.Run(options, configBaseDir: rootDirectory);

            var metricsPath = Path.Combine(rootDirectory, "artifacts", "metrics.csv");
            var checkpointsDirectory = Path.Combine(rootDirectory, "artifacts", "checkpoints");

            Assert.True(File.Exists(metricsPath), "metrics.csv was not created.");
            Assert.True(Directory.Exists(checkpointsDirectory), "checkpoints directory was not created.");

            var lines = File.ReadAllLines(metricsPath);

            Assert.True(lines.Length >= 2, "metrics.csv should contain at least header + one row.");

            var parts = lines[^1].Split(',');

            Assert.True(parts.Length >= 9, "metrics row has unexpected format.");

            Assert.Equal("stage2", parts[1]);
            Assert.Equal("0", parts[2]);

            var bestFitness = double.Parse(parts[3], CultureInfo.InvariantCulture);

            Assert.Equal(expectedBestFitness, bestFitness, precision: 9);

            var checkpointFiles = Directory.EnumerateFiles(checkpointsDirectory, "*.json").ToList();

            Assert.True(checkpointFiles.Count > 0, "No checkpoint JSON files were created.");

            var checkpointJson = File.ReadAllText(checkpointFiles[0]);

            Assert.Contains("\"Stage\": \"stage2\"", checkpointJson);
        }
        finally
        {
            TryDeleteDirectory(rootDirectory);
        }
    }

    private static AppOptions CreateStage2Options()
    {
        const int population = 4;
        const int gamesPerPair = 4;
        const int generations = 1;

        return new AppOptions(
            Data: new DataOptions(
                path: "",
                format: "TSV",
                filters: null,
                labeling: null),
            Stage1: new Stage1Options(
                population: 1,
                crossoverRate: 0.0,
                mutationRate: 0.0,
                generations: 1,
                batchSize: 1,
                ply: 1),
            Stage2: new Stage2Options(
                population: population,
                crossoverRate: 0.0,
                mutationRate: 0.0,
                generations: generations,
                gamesPerPair: gamesPerPair,
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
            Rng: new RngOptions(seed: 123),
            Output: new OutputOptions(
                checkpointsDir: "artifacts/checkpoints",
                metricsFile: "artifacts/metrics.csv"));
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
}
