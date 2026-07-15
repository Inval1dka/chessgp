using ChessGP.Core.Engine;
using ChessGP.Core.Eval;
using Xunit;

namespace ChessGP.Tests.Eval;

public sealed class ParametricEvaluatorTests
{
    private static IBoardAdapter CreateBoard()
    {
        return new ChessDotNetBoardAdapter();
    }

    [Fact]
    public void Evaluate_IsDeterministic()
    {
        var board = CreateBoard();

        board.LoadStartpos();

        var evaluator = new ParametricEvaluator(DecodedParams.ReasonableDefaults);

        var firstScore = evaluator.Evaluate(board);
        var secondScore = evaluator.Evaluate(board);

        Assert.Equal(firstScore, secondScore);
    }

    [Fact]
    public void Tempo_DependsOnSideToMove()
    {
        var evaluator = new ParametricEvaluator(DecodedParams.ReasonableDefaults);

        var whiteToMove = CreateBoard();
        whiteToMove.LoadFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");

        var blackToMove = CreateBoard();
        blackToMove.LoadFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR b KQkq - 0 1");

        var whiteToMoveScore = evaluator.Evaluate(whiteToMove);
        var blackToMoveScore = evaluator.Evaluate(blackToMove);

        Assert.True(
            whiteToMoveScore > blackToMoveScore,
            $"Expected white-to-move to score higher. sW={whiteToMoveScore}, sB={blackToMoveScore}");
    }

    [Fact]
    public void BishopPair_AffectsScore()
    {
        var evaluator = new ParametricEvaluator(DecodedParams.ReasonableDefaults);

        var bishopPair = CreateBoard();
        bishopPair.LoadFen("8/8/8/8/8/8/2BB4/K6k w - - 0 1");

        var singleBishop = CreateBoard();
        singleBishop.LoadFen("8/8/8/8/8/8/2B5/K6k w - - 0 1");

        var bishopPairScore = evaluator.Evaluate(bishopPair);
        var singleBishopScore = evaluator.Evaluate(singleBishop);

        Assert.True(
            bishopPairScore > singleBishopScore,
            $"Expected bishop pair bonus. pair={bishopPairScore}, single={singleBishopScore}");
    }
}
