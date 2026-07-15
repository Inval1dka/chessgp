using ChessGP.Core.Engine;
using Xunit;

namespace ChessGP.Tests.Engine;

public sealed class BoardAdapterTests
{
    private static IBoardAdapter CreateBoard()
    {
        return new ChessDotNetBoardAdapter();
    }

    [Fact]
    public void Startpos_HasLegalMoves_And_FEN_RoundTrip()
    {
        var board = CreateBoard();
        board.LoadStartpos();

        var initialFen = board.ToFen();
        var moves = board.GenerateLegalMoves();

        Assert.NotEmpty(moves);

        var undoToken = board.Make(moves[0]);
        var fenAfterMove = board.ToFen();

        board.Unmake(undoToken);

        var restoredFen = board.ToFen();

        Assert.NotEqual(initialFen, fenAfterMove);
        Assert.Equal(initialFen, restoredFen);
    }

    [Fact]
    public void Check_TerminalDetection_StalemateExample()
    {
        const string stalemateFen = "7k/5Q2/6K1/8/8/8/8/8 b - - 0 1";

        var board = CreateBoard();

        board.LoadFen(stalemateFen);

        Assert.True(board.IsTerminal(out var result));
        Assert.Equal(GameResult.Draw, result);
    }

    [Fact]
    public void Startpos_Has_Exactly_20_LegalMoves()
    {
        var board = CreateBoard();

        board.LoadStartpos();

        var moves = board.GenerateLegalMoves();

        Assert.Equal(20, moves.Count);
    }

    [Fact]
    public void MakeUnmake_MultipleMoves_RestoresFen()
    {
        var board = CreateBoard();

        board.LoadStartpos();

        var initialFen = board.ToFen();

        var firstMove = board.GenerateLegalMoves()[0];
        var firstUndoToken = board.Make(firstMove);

        var secondMove = board.GenerateLegalMoves()[0];
        var secondUndoToken = board.Make(secondMove);

        board.Unmake(secondUndoToken);
        board.Unmake(firstUndoToken);

        var restoredFen = board.ToFen();

        Assert.Equal(initialFen, restoredFen);
    }
}
