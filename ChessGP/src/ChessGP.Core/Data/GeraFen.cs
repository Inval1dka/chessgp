using System.Reflection;
using Chess;

namespace ChessGP.Core.Data;

/// <summary>
/// Extracts FEN strings from Gera.Chess boards while tolerating small API differences between versions.
/// </summary>
internal static class GeraFen
{
    private static readonly string[] FenMethodNames =
    [
        "ToFen",
        "GetFen",
        "ToFEN",
        "GetFEN"
    ];

    private static readonly string[] FenPropertyNames =
    [
        "Fen",
        "FEN",
        "CurrentFen",
        "CurrentFEN"
    ];

    private const BindingFlags FenMemberFlags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.IgnoreCase;

    /// <summary>
    /// Returns the FEN representation of the supplied chess board.
    /// </summary>
    public static string GetFen(ChessBoard board)
    {
        ArgumentNullException.ThrowIfNull(board);

        var boardType = board.GetType();

        foreach (var methodName in FenMethodNames)
        {
            var method = boardType.GetMethod(
                methodName,
                FenMemberFlags,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            if (method?.ReturnType != typeof(string))
            {
                continue;
            }

            var value = (string?)method.Invoke(board, null);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        foreach (var propertyName in FenPropertyNames)
        {
            var property = boardType.GetProperty(propertyName, FenMemberFlags);

            if (property?.PropertyType != typeof(string))
            {
                continue;
            }

            var value = (string?)property.GetValue(board);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new InvalidOperationException(
            "Could not extract FEN from ChessBoard. Tried common FEN methods and properties.");
    }
}
