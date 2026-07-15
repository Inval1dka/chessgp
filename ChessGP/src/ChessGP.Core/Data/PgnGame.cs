namespace ChessGP.Core.Data;

/// <summary>
/// Result value parsed from the PGN Result tag.
/// </summary>
public enum PgnResult
{
    Unknown = 0,
    WhiteWin,
    BlackWin,
    Draw
}

/// <summary>
/// Represents one PGN game, including its header tags and move text.
/// </summary>
public sealed class PgnGame
{
    /// <summary>
    /// Raw PGN header lines, including the surrounding square brackets.
    /// </summary>
    public IReadOnlyList<string> HeaderLines { get; }

    /// <summary>
    /// Raw PGN move text for the game.
    /// </summary>
    public string MoveText { get; }

    public PgnGame(IReadOnlyList<string> headerLines, string moveText)
    {
        HeaderLines = headerLines ?? throw new ArgumentNullException(nameof(headerLines));
        MoveText = moveText ?? throw new ArgumentNullException(nameof(moveText));
    }

    /// <summary>
    /// Parsed game result from the PGN Result tag.
    /// </summary>
    public PgnResult Result
    {
        get
        {
            var result = GetTag("Result");

            return result switch
            {
                "1-0" => PgnResult.WhiteWin,
                "0-1" => PgnResult.BlackWin,
                "1/2-1/2" => PgnResult.Draw,
                _ => PgnResult.Unknown
            };
        }
    }

    /// <summary>
    /// True when the game ended with either a white or black win.
    /// </summary>
    public bool IsDecisive => Result is PgnResult.WhiteWin or PgnResult.BlackWin;

    /// <summary>
    /// Reconstructs the game as PGN text.
    /// </summary>
    public string ToPgnText()
    {
        var headers = string.Join(Environment.NewLine, HeaderLines);
        var moves = MoveText.Trim();

        return headers + Environment.NewLine + Environment.NewLine +
               moves + Environment.NewLine + Environment.NewLine;
    }

    private string? GetTag(string tagName)
    {
        var prefix = "[" + tagName + " ";

        foreach (var line in HeaderLines)
        {
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var firstQuote = line.IndexOf('"');
            if (firstQuote < 0)
            {
                return null;
            }

            var lastQuote = line.LastIndexOf('"');
            if (lastQuote <= firstQuote)
            {
                return null;
            }

            return line.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
        }

        return null;
    }
}
