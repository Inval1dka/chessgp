using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ChessGP.Core.Data;
using Xunit;

namespace ChessGP.Tests.Data;

public sealed class PgnIngestorTests
{
    private static readonly Regex UciMoveRegex = new(
        "^[a-h][1-8][a-h][1-8][qrbn]?$",
        RegexOptions.IgnoreCase);

    [Fact]
    public void BuildTrainTestFromPgn_CreatesTrainAndTest_WithWinningSideToMove()
    {
        var pgn =
            """
            [Event "G1"]
            [Site "?"]
            [Result "1-0"]

            1. e4 e5 2. Qh5 Nc6 3. Bc4 Nf6 4. Qxf7# 1-0

            [Event "G2"]
            [Site "?"]
            [Result "0-1"]

            1. f3 e5 2. g4 Qh4# 0-1
            """;

        var inputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pgn");
        var trainPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".train.uci.tsv");
        var testPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".test.uci.tsv");

        File.WriteAllText(inputPath, pgn);

        try
        {
            var stats = PgnIngestor.BuildTrainTestFromPgn(
                inputPgnPath: inputPath,
                trainTsvPath: trainPath,
                testTsvPath: testPath,
                trainCount: 1,
                testCount: 1,
                seed: 123,
                options: new PgnIngestorOptions
                {
                    SkipInvalidGames = false,
                    Filters = null
                });

            Assert.True(File.Exists(trainPath));
            Assert.True(File.Exists(testPath));

            var trainLines = File.ReadAllLines(trainPath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            var testLines = File.ReadAllLines(testPath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            Assert.Equal(1, trainLines.Length);
            Assert.Equal(1, testLines.Length);

            var allLines = trainLines.Concat(testLines).ToArray();

            Assert.Equal(2, allLines.Length);

            var whiteToMoveCount = 0;
            var blackToMoveCount = 0;

            foreach (var line in allLines)
            {
                var parts = line.Split('\t');

                Assert.True(parts.Length >= 2);

                var fen = parts[0].Trim();
                var uci = parts[1].Trim();

                Assert.Matches(UciMoveRegex, uci);

                var fenParts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                Assert.True(fenParts.Length >= 2);

                var sideToMove = fenParts[1];

                if (sideToMove == "w")
                {
                    whiteToMoveCount++;
                }
                else if (sideToMove == "b")
                {
                    blackToMoveCount++;
                }
                else
                {
                    Assert.Fail("Side-to-move in FEN must be 'w' or 'b'.");
                }
            }

            Assert.Equal(1, whiteToMoveCount);
            Assert.Equal(1, blackToMoveCount);

            Assert.Equal(2, stats.SamplesCollected);
            Assert.Equal(1, stats.TrainWritten);
            Assert.Equal(1, stats.TestWritten);
        }
        finally
        {
            TryDelete(inputPath);
            TryDelete(trainPath);
            TryDelete(testPath);
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
