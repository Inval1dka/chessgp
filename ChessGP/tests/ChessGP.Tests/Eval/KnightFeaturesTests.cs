using ChessGP.Core.Engine;
using ChessGP.Core.Eval;
using Xunit;

namespace ChessGP.Tests.Eval;

public sealed class KnightFeaturesTests
{
    [Fact]
    public void KnightCentrality_CenterScoresHigherThanCorner()
    {
        const string centerFen = "4k3/8/8/8/3N4/8/8/4K3 w - - 0 1";
        const string cornerFen = "4k3/8/8/8/8/8/N7/4K3 w - - 0 1";

        var parameters = CreateKnightOnlyParams(
            knightSquareMult: 10,
            knightOutpostBonus: 0);

        var evaluator = new ParametricEvaluator(parameters);
        var board = new ChessDotNetBoardAdapter();

        board.LoadFen(centerFen);
        var centerScore = evaluator.Evaluate(board);

        board.LoadFen(cornerFen);
        var cornerScore = evaluator.Evaluate(board);

        Assert.True(centerScore > cornerScore);
    }

    [Fact]
    public void KnightOutpost_SupportedAndUnattackable_GetsBonus()
    {
        const string outpostFen = "4k3/8/8/4N3/3P4/8/8/4K3 w - - 0 1";
        const string noSupportFen = "4k3/8/8/4N3/8/8/8/4K3 w - - 0 1";

        var parameters = CreateKnightOnlyParams(
            knightSquareMult: 0,
            knightOutpostBonus: 50);

        var evaluator = new ParametricEvaluator(parameters);
        var board = new ChessDotNetBoardAdapter();

        board.LoadFen(outpostFen);
        var outpostScore = evaluator.Evaluate(board);

        board.LoadFen(noSupportFen);
        var noSupportScore = evaluator.Evaluate(board);

        Assert.True(outpostScore > noSupportScore);
    }

    [Fact]
    public void KnightOutpost_FuturePawnPushPossible_IsNotOutpost()
    {
        const string futureChasePossibleFen = "4k3/3p4/3r4/4N3/3P4/8/8/4K3 w - - 0 1";
        const string pawnBlocksFuturePushFen = "4k3/3p4/3P4/4N3/3P4/8/8/4K3 w - - 0 1";

        var parameters = CreateKnightOnlyParams(
            knightSquareMult: 0,
            knightOutpostBonus: 50);

        var evaluator = new ParametricEvaluator(parameters);
        var board = new ChessDotNetBoardAdapter();

        board.LoadFen(pawnBlocksFuturePushFen);
        var outpostScore = evaluator.Evaluate(board);

        board.LoadFen(futureChasePossibleFen);
        var notOutpostScore = evaluator.Evaluate(board);

        Assert.True(outpostScore > notOutpostScore);
    }

    private static DecodedParams CreateKnightOnlyParams(
        int knightSquareMult,
        int knightOutpostBonus)
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
            ConnectedPassedBonus = 0,

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

            KnightSquareMult = knightSquareMult,
            KnightOutpostBonus = knightOutpostBonus
        };
    }
}
