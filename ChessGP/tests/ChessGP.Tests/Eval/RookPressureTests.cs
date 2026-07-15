using ChessGP.Core.Engine;
using ChessGP.Core.Eval;
using Xunit;

namespace ChessGP.Tests.Eval;

public sealed class RookPressureTests
{
    [Fact]
    public void RookAttackKingAdjacentFile_RequiresOpenOrSemiOpenFile()
    {
        const string openAdjacentFileFen = "8/8/8/8/3Rk3/8/2P5/K7 w - - 0 1";
        const string blockedAdjacentFileFen = "8/8/8/8/3Rk3/8/3P4/K7 w - - 0 1";

        var parameters = CreateRookPressureOnlyParams(
            kingAdjacentFileBonus: 50,
            weakPawnOpenFileBonus: 0);

        var openAdjacentFileScore = Score(openAdjacentFileFen, parameters);
        var blockedAdjacentFileScore = Score(blockedAdjacentFileFen, parameters);

        Assert.True(openAdjacentFileScore > blockedAdjacentFileScore);
    }

    [Fact]
    public void RookAttacksWeakPawnOnOpenOrSemiOpenFile_GetsBonus()
    {
        const string attackingWeakPawnFen = "4k3/8/8/3p4/8/8/2P5/K2R4 w - - 0 1";
        const string blockedWeakPawnFen = "4k3/8/8/3p4/8/8/3P4/K2R4 w - - 0 1";

        var parameters = CreateRookPressureOnlyParams(
            kingAdjacentFileBonus: 0,
            weakPawnOpenFileBonus: 60);

        var attackingWeakPawnScore = Score(attackingWeakPawnFen, parameters);
        var blockedWeakPawnScore = Score(blockedWeakPawnFen, parameters);

        Assert.True(attackingWeakPawnScore > blockedWeakPawnScore);
    }

    private static int Score(string fen, DecodedParams parameters)
    {
        var evaluator = new ParametricEvaluator(parameters);
        var board = new ChessDotNetBoardAdapter();

        board.LoadFen(fen);

        return evaluator.Evaluate(board);
    }

    private static DecodedParams CreateRookPressureOnlyParams(
        int kingAdjacentFileBonus,
        int weakPawnOpenFileBonus)
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
            WeakSquarePenalty = 0,
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

            RookAttackKingAdjFileBonus = kingAdjacentFileBonus,
            RookAttackWeakPawnOpenFileBonus = weakPawnOpenFileBonus,

            KnightSquareMult = 0,
            KnightOutpostBonus = 0
        };
    }
}
