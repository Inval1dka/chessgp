using System;
using System.IO;
using ChessGP.Core.Training;
using Xunit;

namespace ChessGP.Tests.Training;

public sealed class OpeningBookTests
{
    [Fact]
    public void LoadFens_ParsesStartposAndIgnoresComments()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");

        try
        {
            File.WriteAllText(
                path,
                """
                # comment
                ; comment2
                // comment3

                startpos
                7k/5Q2/7K/8/8/8/8/8 b - - 0 1

                """);

            var fens = OpeningBook.LoadFens(path);

            Assert.True(fens.Count >= 2);
            Assert.Contains(OpeningBook.StartposFen, fens);
            Assert.Contains("7k/5Q2/7K/8/8/8/8/8 b - - 0 1", fens);
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
