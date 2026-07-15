using ChessGP.Core.Engine;
using ChessGP.Core.Eval;
using Xunit;

namespace ChessGP.Tests.Eval;

public sealed class PawnStructureTests
{
    [Fact]
    public void PassedPawnBonus_IncreasesWithAdvancement()
    {
        const string lowPassedPawnFen = "4k3/8/8/8/8/4P3/8/4K3 w - - 0 1";
        const string highPassedPawnFen = "4k3/8/4P3/8/8/8/8/4K3 w - - 0 1";

        var parameters = CreatePawnOnlyParams(passedPawnBonus: 60);

        var lowScore = Score(lowPassedPawnFen, parameters);
        var highScore = Score(highPassedPawnFen, parameters);

        Assert.True(highScore > lowScore);
    }

    [Fact]
    public void ProtectedPassedPawn_GetsExtraBonus()
    {
        const string protectedPassedPawnFen = "7k/8/8/2p1P3/3P4/8/8/7K w - - 0 1";
        const string unprotectedPassedPawnFen = "7k/8/8/2p1P3/8/8/P7/7K w - - 0 1";

        var parameters = CreatePawnOnlyParams(protectedPassedPawnBonus: 50);

        var protectedScore = Score(protectedPassedPawnFen, parameters);
        var unprotectedScore = Score(unprotectedPassedPawnFen, parameters);

        Assert.True(protectedScore > unprotectedScore);
    }

    [Fact]
    public void ConnectedPassedPawn_RequiresAdjacentFileAndCloseRank()
    {
        const string connectedPassedPawnsFen = "7k/8/3P4/2P5/8/8/8/7K w - - 0 1";
        const string notConnectedPassedPawnsFen = "7k/3P4/8/8/8/8/2P5/7K w - - 0 1";

        var parameters = CreatePawnOnlyParams(connectedPassedBonus: 30);

        var connectedScore = Score(connectedPassedPawnsFen, parameters);
        var notConnectedScore = Score(notConnectedPassedPawnsFen, parameters);

        Assert.True(connectedScore > notConnectedScore);
    }

    [Fact]
    public void PassedPawnEnemyKingDistance_FarKingScoresHigher()
    {
        const string nearEnemyKingFen = "8/4k3/4P3/8/8/8/8/7K w - - 0 1";
        const string farEnemyKingFen = "7K/8/4P3/8/8/8/8/k7 w - - 0 1";

        var parameters = CreatePawnOnlyParams(passedPawnEnemyKingDistanceBonus: 70);

        var nearScore = Score(nearEnemyKingFen, parameters);
        var farScore = Score(farEnemyKingFen, parameters);

        Assert.True(farScore > nearScore);
    }

    [Fact]
    public void BackwardPawn_PawnPushSupportPossibleLater_AvoidsPenalty()
    {
        const string backwardPawnFen = "7k/2p5/2P1P3/3P4/8/8/8/7K w - - 0 1";
        const string notBackwardPawnFen = "7k/2p5/8/3P4/2P1P3/8/8/7K w - - 0 1";

        var parameters = CreatePawnOnlyParams(backwardPawnPenalty: 50);

        var backwardScore = Score(backwardPawnFen, parameters);
        var notBackwardScore = Score(notBackwardPawnFen, parameters);

        Assert.True(notBackwardScore > backwardScore);
    }

    private static int Score(string fen, DecodedParams parameters)
    {
        var evaluator = new ParametricEvaluator(parameters);
        var board = new ChessDotNetBoardAdapter();

        board.LoadFen(fen);

        return evaluator.Evaluate(board);
    }

    private static DecodedParams CreatePawnOnlyParams(
        int doubledPawnPenalty = 0,
        int isolatedPawnPenalty = 0,
        int backwardPawnPenalty = 0,
        int passedPawnBonus = 0,
        int protectedPassedPawnBonus = 0,
        int connectedPassedBonus = 0,
        int passedPawnEnemyKingDistanceBonus = 0)
    {
        return DecodedParams.ReasonableDefaults with
        {
            KnightValue = 0,
            BishopValue = 0,
            RookValue = 0,
            QueenValue = 0,
            BishopPairBonus = 0,
            TempoBonus = 0,

            DoubledPawnPenalty = doubledPawnPenalty,
            IsolatedPawnPenalty = isolatedPawnPenalty,
            BackwardPawnPenalty = backwardPawnPenalty,
            PassedPawnBonus = passedPawnBonus,
            ProtectedPassedPawnBonus = protectedPassedPawnBonus,
            ConnectedPassedBonus = connectedPassedBonus,
            PassedPawnEnemyKingDistBonus = passedPawnEnemyKingDistanceBonus,

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

            KnightSquareMult = 0,
            KnightOutpostBonus = 0
        };
    }
}
