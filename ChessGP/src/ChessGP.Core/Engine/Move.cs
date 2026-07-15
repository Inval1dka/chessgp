namespace ChessGP.Core.Engine;

/// <summary>
/// Represents a move in UCI notation, such as e2e4 or e7e8q.
/// </summary>
public readonly struct UciMove
{
    /// <summary>
    /// Source square, for example e2.
    /// </summary>
    public string From { get; }

    /// <summary>
    /// Target square, for example e4.
    /// </summary>
    public string To { get; }

    /// <summary>
    /// Promotion piece, or '\0' when the move is not a promotion.
    /// </summary>
    public char Promotion { get; }

    public UciMove(string from, string to, char promotion = '\0')
    {
        From = from;
        To = to;
        Promotion = promotion;
    }

    /// <summary>
    /// Returns the move in UCI notation.
    /// </summary>
    public override string ToString()
    {
        return Promotion == '\0'
            ? $"{From}{To}"
            : $"{From}{To}{Promotion}";
    }

    /// <summary>
    /// Parses a UCI move string.
    /// </summary>
    public static UciMove Parse(string uci)
    {
        if (uci is null || (uci.Length != 4 && uci.Length != 5))
        {
            throw new ArgumentException("UCI must be 4 or 5 chars.", nameof(uci));
        }

        var from = uci[..2];
        var to = uci.Substring(2, 2);
        var promotion = uci.Length == 5
            ? char.ToLowerInvariant(uci[4])
            : '\0';

        return new UciMove(from, to, promotion);
    }
}
