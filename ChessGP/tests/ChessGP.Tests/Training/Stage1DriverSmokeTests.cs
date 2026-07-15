using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChessGP.Core.Config;
using ChessGP.Core.Training;
using Xunit;
using ConfigGeneSpec = ChessGP.Core.Config.GeneSpec;

namespace ChessGP.Tests.Training;

public sealed class Stage1DriverSmokeTests
{
    [Fact]
    public void Run_WritesMetricsAndCheckpoint_ForOneGeneration()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "chessgp_" + Guid.NewGuid());

        Directory.CreateDirectory(rootDirectory);

        try
        {
            var dataDirectory = Path.Combine(rootDirectory, "data");

            Directory.CreateDirectory(dataDirectory);

            var trainPath = Path.Combine(dataDirectory, "smoke.train.uci.tsv");
            var testPath = Path.Combine(dataDirectory, "smoke.test.uci.tsv");

            const string fen = "7k/4r3/8/8/8/8/4Q3/K7 w - - 0 1";

            var lines =
                $"{fen}\te2e7{Environment.NewLine}" +
                $"{fen}\te2e7{Environment.NewLine}";

            File.WriteAllText(trainPath, lines);
            File.WriteAllText(testPath, lines);

            const string checkpointsDirRelative = "artifacts/checkpoints";
            const string metricsRelative = "artifacts/metrics.csv";

            var options = new AppOptions(
                Data: new DataOptions(
                    path: Path.Combine("data", "smoke.train.uci.tsv"),
                    format: "TSV",
                    filters: null,
                    labeling: null),
                Stage1: new Stage1Options(
                    population: 1,
                    crossoverRate: 0.0,
                    mutationRate: 0.0,
                    generations: 1,
                    batchSize: 2,
                    ply: 1),
                Stage2: new Stage2Options(
                    population: 1,
                    crossoverRate: 0.0,
                    mutationRate: 0.0,
                    generations: 1,
                    gamesPerPair: 1,
                    openingBookPath: "",
                    useHallOfFame: false,
                    hallOfFameSize: 0,
                    searchDepth: 1,
                    searchMode: "FixedDepth",
                    timePerMoveMs: 0,
                    perMoveNodeCap: 1000),
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
                    checkpointsDir: checkpointsDirRelative,
                    metricsFile: metricsRelative));

            Stage1Driver.Run(options, configBaseDir: rootDirectory);

            var metricsPath = Path.Combine(
                rootDirectory,
                metricsRelative.Replace('/', Path.DirectorySeparatorChar));

            var checkpointsDirectory = Path.Combine(
                rootDirectory,
                checkpointsDirRelative.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(metricsPath));

            var metricsLines = File.ReadAllLines(metricsPath);

            Assert.True(metricsLines.Length >= 2);
            Assert.True(Directory.Exists(checkpointsDirectory));
            Assert.True(Directory.EnumerateFiles(checkpointsDirectory, "*.json").Any());
        }
        finally
        {
            TryDeleteDirectory(rootDirectory);
        }
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
