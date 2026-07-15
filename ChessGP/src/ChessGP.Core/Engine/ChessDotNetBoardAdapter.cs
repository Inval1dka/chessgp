using ChessDotNet;

namespace ChessGP.Core.Engine;

/// <summary>
/// Adapter that exposes ChessDotNet through the board interface used by the engine.
/// </summary>
public sealed class ChessDotNetBoardAdapter : IBoardAdapter
{
    private readonly Dictionary<string, int> _repetitionCounts = new(StringComparer.Ordinal);
    private readonly List<string> _repetitionStack = new(capacity: 512);

    private ChessGame _game = new();

    /// <summary>
    /// Loads the standard chess starting position.
    /// </summary>
    public void LoadStartpos()
    {
        _game = new ChessGame();
        ResetRepetition();
    }

    /// <summary>
    /// Loads a board position from FEN.
    /// </summary>
    /// <param name="fen">FEN string describing the position to load.</param>
    public void LoadFen(string fen)
    {
        _game = new ChessGame(fen);
        ResetRepetition();
    }

    /// <summary>
    /// Generates all legal moves in the current position.
    /// </summary>
    public IReadOnlyList<UciMove> GenerateLegalMoves()
    {
        var moves = new List<UciMove>(capacity: 64);
        var legalMoves = _game.GetValidMoves(_game.WhoseTurn);

        foreach (var move in legalMoves)
        {
            var from = ToSquareString(move.OriginalPosition);
            var to = ToSquareString(move.NewPosition);
            var promotion = move.Promotion.HasValue
                ? char.ToLowerInvariant(move.Promotion.Value)
                : '\0';

            moves.Add(new UciMove(from, to, promotion));
        }

        return moves;
    }

    /// <summary>
    /// Applies a legal move and returns an undo token that can be passed to <see cref="Unmake"/>.
    /// </summary>
    /// <param name="move">Move to apply in UCI notation.</param>
    public string Make(UciMove move)
    {
        var fenBeforeMove = _game.GetFen();

        var player = _game.WhoseTurn;
        var from = ParseSquare(move.From);
        var to = ParseSquare(move.To);
        char? promotion = move.Promotion == '\0'
            ? null
            : char.ToLowerInvariant(move.Promotion);

        var chessDotNetMove = new Move(from, to, player, promotion);
        var moveType = _game.MakeMove(chessDotNetMove, true);

        if (moveType == MoveType.Invalid)
        {
            throw new InvalidOperationException($"Illegal move for position: {move}");
        }

        PushCurrentRepetitionKey();

        return fenBeforeMove;
    }

    /// <summary>
    /// Restores the board to the position represented by an undo token returned from <see cref="Make"/>.
    /// </summary>
    /// <param name="token">Undo token returned by a previous call to <see cref="Make"/>.</param>
    public void Unmake(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Unmake token was empty.", nameof(token));
        }

        PopCurrentRepetitionKey();

        _game = new ChessGame(token);
    }

    /// <summary>
    /// Returns true when the current position has occurred at least three times in the current line.
    /// </summary>
    public bool IsThreefold()
    {
        var key = CurrentRepetitionKey();
        return _repetitionCounts.TryGetValue(key, out var count) && count >= 3;
    }

    /// <summary>
    /// Halfmove clock value parsed from the current FEN.
    /// </summary>
    public int HalfmoveClock
    {
        get
        {
            var fen = _game.GetFen();
            var parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return parts.Length >= 5 && int.TryParse(parts[4], out var halfmoveClock)
                ? halfmoveClock
                : 0;
        }
    }

    /// <summary>
    /// Checks whether the current position is a terminal win or draw.
    /// </summary>
    /// <param name="result">Terminal result, or ongoing when the game has not ended.</param>
    public bool IsTerminal(out GameResult result)
    {
        if (_game.IsCheckmated(Player.White))
        {
            result = GameResult.BlackWin;
            return true;
        }

        if (_game.IsCheckmated(Player.Black))
        {
            result = GameResult.WhiteWin;
            return true;
        }

        if (_game.IsStalemated(Player.White) ||
            _game.IsStalemated(Player.Black) ||
            _game.IsInsufficientMaterial() ||
            _game.IsDraw())
        {
            result = GameResult.Draw;
            return true;
        }

        result = GameResult.Ongoing;
        return false;
    }

    /// <summary>
    /// Returns the current position as FEN.
    /// </summary>
    public string ToFen() => _game.GetFen();

    private void ResetRepetition()
    {
        _repetitionCounts.Clear();
        _repetitionStack.Clear();

        PushCurrentRepetitionKey();
    }

    private void PushCurrentRepetitionKey()
    {
        var key = FenKey(_game.GetFen());

        _repetitionStack.Add(key);

        if (_repetitionCounts.TryGetValue(key, out var count))
        {
            _repetitionCounts[key] = count + 1;
        }
        else
        {
            _repetitionCounts[key] = 1;
        }
    }

    private void PopCurrentRepetitionKey()
    {
        if (_repetitionStack.Count == 0)
        {
            return;
        }

        var key = _repetitionStack[^1];
        _repetitionStack.RemoveAt(_repetitionStack.Count - 1);

        if (!_repetitionCounts.TryGetValue(key, out var count))
        {
            return;
        }

        count--;

        if (count <= 0)
        {
            _repetitionCounts.Remove(key);
        }
        else
        {
            _repetitionCounts[key] = count;
        }
    }

    private string CurrentRepetitionKey()
    {
        return _repetitionStack.Count > 0
            ? _repetitionStack[^1]
            : FenKey(_game.GetFen());
    }

    private static string FenKey(string fen)
    {
        var parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 4)
        {
            return fen.Trim();
        }

        return $"{parts[0]} {parts[1]} {parts[2]} {parts[3]}";
    }

    private static string ToSquareString(Position position)
    {
        return $"{(char)('a' + (int)position.File)}{(char)('0' + position.Rank)}";
    }

    private static Position ParseSquare(string uciSquare)
    {
        if (uciSquare is null || uciSquare.Length != 2)
        {
            throw new ArgumentException("Square must be like 'e4'.", nameof(uciSquare));
        }

        var fileIndex = char.ToLowerInvariant(uciSquare[0]) - 'a';
        var rankIndex = uciSquare[1] - '0';

        return new Position((ChessDotNet.File)fileIndex, rankIndex);
    }
}
