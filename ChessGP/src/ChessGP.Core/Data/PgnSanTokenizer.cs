namespace ChessGP.Core.Data;

/// <summary>
/// Converts simple PGN move text into SAN move tokens.
/// </summary>
public static class PgnSanTokenizer
{
    private static readonly HashSet<string> ResultTokens =
    [
        "1-0",
        "0-1",
        "1/2-1/2",
        "*"
    ];

    /// <summary>
    /// Tokenizes PGN move text into SAN move strings.
    /// </summary>
    /// <param name="moveText">Raw PGN move text.</param>
    public static IReadOnlyList<string> Tokenize(string moveText)
    {
        ArgumentNullException.ThrowIfNull(moveText);

        var rawTokens = moveText.Split(
            separator: null as char[],
            options: StringSplitOptions.RemoveEmptyEntries);

        var moves = new List<string>(capacity: 256);

        foreach (var rawToken in rawTokens)
        {
            var token = rawToken.Trim();

            if (token.Length == 0)
            {
                continue;
            }

            if (ResultTokens.Contains(token))
            {
                break;
            }

            token = StripMoveNumberPrefix(token);

            if (token.Length == 0)
            {
                continue;
            }

            moves.Add(token);
        }

        return moves;
    }

    private static string StripMoveNumberPrefix(string token)
    {
        var index = 0;

        while (index < token.Length && char.IsDigit(token[index]))
        {
            index++;
        }

        if (index == 0)
        {
            return token;
        }

        if (index >= token.Length || token[index] != '.')
        {
            return token;
        }

        while (index < token.Length && token[index] == '.')
        {
            index++;
        }

        return index >= token.Length
            ? string.Empty
            : token[index..];
    }
}
