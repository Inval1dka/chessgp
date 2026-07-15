using System.Text;

namespace ChessGP.Core.Data;

/// <summary>
/// Reads PGN files and yields individual games with their header lines and move text.
/// </summary>
public static class PgnGameReader
{
    /// <summary>
    /// Reads all games from the specified PGN file.
    /// </summary>
    /// <param name="pgnPath">Path to the PGN file.</param>
    public static IEnumerable<PgnGame> ReadGames(string pgnPath)
    {
        using var reader = new StreamReader(pgnPath);

        var headers = new List<string>(16);
        var moveText = new StringBuilder(4096);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmedLine = line.Trim();

            if (headers.Count == 0 && (trimmedLine.Length == 0 || trimmedLine[0] != '['))
            {
                continue;
            }

            if (trimmedLine.Length == 0)
            {
                if (moveText.Length > 0)
                {
                    moveText.Append(' ');
                }

                continue;
            }

            if (trimmedLine[0] == '[')
            {
                if (headers.Count > 0 && moveText.Length > 0)
                {
                    yield return new PgnGame(headers.ToArray(), moveText.ToString().Trim());
                    headers.Clear();
                    moveText.Clear();
                }

                headers.Add(trimmedLine);
                continue;
            }

            if (headers.Count > 0)
            {
                moveText.Append(trimmedLine);
                moveText.Append(' ');
            }
        }

        if (headers.Count > 0 && moveText.Length > 0)
        {
            yield return new PgnGame(headers.ToArray(), moveText.ToString().Trim());
        }
    }
}
