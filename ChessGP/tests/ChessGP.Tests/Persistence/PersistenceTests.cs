using System;
using System.IO;
using ChessGP.Core.Persistence;
using Xunit;

namespace ChessGP.Tests.Persistence;

public sealed class PersistenceTests
{
    [Fact]
    public void Checkpointer_Save_And_LoadLatest_RoundTrips()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ChessGP_ckpt_" + Guid.NewGuid());

        Directory.CreateDirectory(directory);

        try
        {
            var checkpointer = new Checkpointer(directory);

            var path = checkpointer.SaveBest(
                runId: "run-test",
                stage: "Stage1",
                generation: 1,
                schemaId: "paper-v1",
                genomeBits: "010101");

            Assert.True(File.Exists(path));

            var loaded = checkpointer.LoadLatest(runId: "run-test", stage: "Stage1");

            Assert.NotNull(loaded);
            Assert.Equal("paper-v1", loaded!.SchemaId);
            Assert.Equal("010101", loaded.GenomeBits);
            Assert.Equal(1, loaded.Generation);
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public void MetricsWriter_Writes_Header_Once_And_Appends()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ChessGP_metrics_" + Guid.NewGuid());

        Directory.CreateDirectory(directory);

        var csvPath = Path.Combine(directory, "metrics.csv");

        try
        {
            var writer = new MetricsWriter(csvPath);

            writer.Append(new GenerationMetricsRow(
                RunId: "run-test",
                Stage: "Stage1",
                Generation: 1,
                BestFitness: 10,
                MeanFitness: 5,
                MedianFitness: 4,
                NodesEvaluated: 123,
                Seed: 42,
                ElapsedMs: 1000,
                BestCheckpointPath: "path1.json"));

            writer.Append(new GenerationMetricsRow(
                RunId: "run-test",
                Stage: "Stage1",
                Generation: 2,
                BestFitness: 12,
                MeanFitness: 6,
                MedianFitness: 5,
                NodesEvaluated: 456,
                Seed: 43,
                ElapsedMs: 1100,
                BestCheckpointPath: "path2.json"));

            var lines = File.ReadAllLines(csvPath);

            Assert.True(lines.Length >= 3);
            Assert.Equal(GenerationMetricsRow.Header, lines[0]);
        }
        finally
        {
            TryDeleteDirectory(directory);
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
