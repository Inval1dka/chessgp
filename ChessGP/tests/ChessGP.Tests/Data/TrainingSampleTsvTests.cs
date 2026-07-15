using System;
using System.IO;
using ChessGP.Core.Data;
using Xunit;

namespace ChessGP.Tests.Data;

public sealed class TrainingSampleTsvTests
{
    [Fact]
    public void Sample_IsDeterministic_ForSameSeed()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".tsv");

        try
        {
            using (var writer = new StreamWriter(path))
            {
                for (var index = 0; index < 50; index++)
                {
                    writer.WriteLine($"FEN{index}\te2e4");
                }
            }

            var firstSample = TrainingSampleTsv.Sample(path, sampleCount: 10, seed: 123);
            var secondSample = TrainingSampleTsv.Sample(path, sampleCount: 10, seed: 123);

            Assert.Equal(firstSample.Count, secondSample.Count);

            for (var index = 0; index < firstSample.Count; index++)
            {
                Assert.Equal(firstSample[index], secondSample[index]);
            }
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup for temporary test files.
        }
    }
}
