using ChessGP.Core.Engine;
using ChessGP.Core.Eval;
using Xunit;

namespace ChessGP.Tests.Eval;

public sealed class WeakSquareTests
{
    [Fact]
    public void WeakSquares_AdvancedWhitePawns_CreateMoreWeakSquares_AndArePenalized()
    {
        const string safeWhitePawnsFen = "7k/8/8/8/8/8/2P1P3/7K w - - 0 1";
        const string weakWhiteSquaresFen = "7k/8/8/2P1P3/8/8/8/7K w - - 0 1";

        var parameters = CreateWeakSquareOnlyParams(weakSquarePenalty: 20);

        var safeScore = Score(safeWhitePawnsFen, parameters);
        var weakSquaresScore = Score(weakWhiteSquaresFen, parameters);

        Assert.True(safeScore > weakSquaresScore);
    }

    [Fact]
    public void WeakSquares_AdvancedBlackPawns_CreateMoreWeakSquares_AndBenefitWhite()
    {
        const string safeBlackPawnsFen = "k7/2p1p3/8/8/8/8/8/7K w - - 0 1";
        const string weakBlackSquaresFen = "k7/8/8/8/2p1p3/8/8/7K w - - 0 1";

        var parameters = CreateWeakSquareOnlyParams(weakSquarePenalty: 20);

        var safeScore = Score(safeBlackPawnsFen, parameters);
        var weakSquaresScore = Score(weakBlackSquaresFen, parameters);

        Assert.True(weakSquaresScore > safeScore);
    }

    private static int Score(string fen, DecodedParams parameters)
    {
        var evaluator = new ParametricEvaluator(parameters);
        var board = new ChessDotNetBoardAdapter();

        board.LoadFen(fen);

        return evaluator.Evaluate(board);
    }

    private static DecodedParams CreateWeakSquareOnlyParams(int weakSquarePenalty)
    {
        return DecodedParams.ReasonableDefaults with
        {
            KnightValue = 0,
            BishopValue = 0,
            RookValue = 0,
            QueenValue = 0,
            BishopPairBonus = 0,
            TempoBonus = 0,

            DoubledPawnPenalty = 0,
            IsolatedPawnPenalty = 0,
            BackwardPawnPenalty = 0,
            PassedPawnBonus = 0,
            ProtectedPassedPawnBonus = 0,
            ConnectedPassedBonus = 0,
            PassedPawnEnemyKingDistBonus = 0,

            PawnShieldBonus = 0,
            OpenFileToKingPenalty = 0,
            SemiOpenFileToKingPenalty = 0,
            KingAttackUnit = 0,

            BishopMobility = 0,
            RookMobility = 0,
            QueenMobility = 0,

            RookOpenFileBonus = 0,
            RookSemiOpenFileBonus = 0,
            RookSeventhRankBonus = 0,
            RookConnectedBonus = 0,
            RookBehindPassedPawnBonus = 0,
            RookAttackKingFileBonus = 0,
            RookAttackKingAdjFileBonus = 0,
            RookAttackWeakPawnOpenFileBonus = 0,

            KnightSquareMult = 0,
            KnightOutpostBonus = 0,

            WeakSquarePenalty = weakSquarePenalty
        };
    }
}
