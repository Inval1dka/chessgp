using ChessGP.Core.Engine;
using Xunit;

namespace ChessGP.Tests.Engine;

public sealed class ThreefoldRepetitionTests
{
    [Fact]
    public void ChessDotNetBoardAdapter_Detects_Threefold_Repetition()
    {
        var board = new ChessDotNetBoardAdapter();

        board.LoadStartpos();

        Assert.False(board.IsThreefold());

        RepeatKnightCycle(board);

        Assert.False(board.IsThreefold());

        RepeatKnightCycle(board);

        Assert.True(board.IsThreefold());
    }

    private static void RepeatKnightCycle(ChessDotNetBoardAdapter board)
    {
        board.Make(UciMove.Parse("g1f3"));
        board.Make(UciMove.Parse("g8f6"));
        board.Make(UciMove.Parse("f3g1"));
        board.Make(UciMove.Parse("f6g8"));
    }
}
