namespace ChessGP.Core.Data;

/// <summary>
/// Loads training samples from TSV datasets.
/// </summary>
public static class TrainingSampleTsv
{
    /// <summary>
    /// Deterministically reservoir-samples up to the requested number of samples from a TSV dataset.
    /// </summary>
    /// <param name="tsvPath">Path to a TSV file containing FEN and UCI move columns.</param>
    /// <param name="sampleCount">Maximum number of samples to return.</param>
    /// <param name="seed">Random seed used for deterministic sampling.</param>
    public static List<TrainingSample> Sample(string tsvPath, int sampleCount, int seed)
    {
        if (string.IsNullOrWhiteSpace(tsvPath))
        {
            throw new ArgumentException("TSV path is required.", nameof(tsvPath));
        }

        if (!File.Exists(tsvPath))
        {
            throw new FileNotFoundException($"TSV dataset not found: {tsvPath}");
        }

        if (sampleCount <= 0)
        {
            return [];
        }

        var rng = new Random(seed);
        var reservoir = new List<TrainingSample>(capacity: sampleCount);

        long sampledAfterReservoirFilled = 0;
        var lineIndex = 0;

        foreach (var line in File.ReadLines(tsvPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                lineIndex++;
                continue;
            }

            var parts = line.Split('\t');
            if (parts.Length < 2)
            {
                lineIndex++;
                continue;
            }

            var fen = parts[0].Trim();
            var uci = parts[1].Trim();

            if (fen.Length == 0 || uci.Length == 0)
            {
                lineIndex++;
                continue;
            }

            var sample = new TrainingSample(fen, uci, lineIndex);
            lineIndex++;

            if (reservoir.Count < sampleCount)
            {
                reservoir.Add(sample);
                continue;
            }

            sampledAfterReservoirFilled++;

            var replacementIndex = NextLong(rng, sampledAfterReservoirFilled + sampleCount);
            if (replacementIndex < sampleCount)
            {
                reservoir[(int)replacementIndex] = sample;
            }
        }

        return reservoir;
    }

    private static long NextLong(Random rng, long maxExclusive)
    {
        if (maxExclusive <= int.MaxValue)
        {
            return rng.Next((int)maxExclusive);
        }

        return (long)(rng.NextDouble() * maxExclusive);
    }
}
