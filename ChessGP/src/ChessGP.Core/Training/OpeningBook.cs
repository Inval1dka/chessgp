namespace ChessGP.Core.Training;

/// <summary>
/// Loads and selects starting positions used for self-play games.
/// </summary>
public static class OpeningBook
{
    /// <summary>
    /// Standard chess starting position in FEN format.
    /// </summary>
    public const string StartposFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    /// <summary>
    /// Loads usable starting positions from a text file.
    /// </summary>
    /// <param name="path">Path to a text file containing FEN positions or the keyword startpos.</param>
    public static List<string> LoadFens(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Opening book path is required.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Opening book not found: {path}");
        }

        var fens = new List<string>();

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            if (IsCommentLine(line))
            {
                continue;
            }

            if (string.Equals(line, "startpos", StringComparison.OrdinalIgnoreCase))
            {
                fens.Add(StartposFen);
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
            {
                continue;
            }

            fens.Add(line);
        }

        if (fens.Count == 0)
        {
            throw new InvalidOperationException($"Opening book contained no usable FENs: {path}");
        }

        return fens;
    }

    /// <summary>
    /// Picks a random starting position from a loaded opening list.
    /// </summary>
    public static string PickRandom(IReadOnlyList<string> fens, Random rng)
    {
        ArgumentNullException.ThrowIfNull(fens);
        ArgumentNullException.ThrowIfNull(rng);

        if (fens.Count == 0)
        {
            throw new ArgumentException("Opening list is empty.", nameof(fens));
        }

        return fens[rng.Next(fens.Count)];
    }

    private static bool IsCommentLine(string line)
    {
        return line.StartsWith("#", StringComparison.Ordinal) ||
               line.StartsWith(";", StringComparison.Ordinal) ||
               line.StartsWith("//", StringComparison.Ordinal);
    }
}
