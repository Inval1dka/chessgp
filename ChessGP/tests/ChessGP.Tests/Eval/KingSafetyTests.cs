using ChessGP.Core.Engine;
using ChessGP.Core.Eval;
using Xunit;

namespace ChessGP.Tests.Eval;

public sealed class KingSafetyTests
{
    private static IBoardAdapter CreateBoard()
    {
        return new ChessDotNetBoardAdapter();
    }

    [Fact]
    public void KingSafety_PawnShield_Matters()
    {
        var parameters = CreateKingSafetyOnlyParams() with
        {
            PawnShieldBonus = 20
        };

        var evaluator = new ParametricEvaluator(parameters);

        var withShield = CreateBoard();
        withShield.LoadFen("7k/8/8/8/8/8/5PPP/6K1 w - - 0 1");

        var withoutShield = CreateBoard();
        withoutShield.LoadFen("7k/8/8/8/8/8/5PP1/6K1 w - - 0 1");

        var withShieldScore = evaluator.Evaluate(withShield);
        var withoutShieldScore = evaluator.Evaluate(withoutShield);

        Assert.True(
            withShieldScore > withoutShieldScore,
            $"Expected better score with pawn shield. with={withShieldScore}, without={withoutShieldScore}");
    }

    [Fact]
    public void KingSafety_OpenFile_To_King_Is_Penalized()
    {
        var parameters = CreateKingSafetyOnlyParams() with
        {
            OpenFileToKingPenalty = 40
        };

        var evaluator = new ParametricEvaluator(parameters);

        var openFile = CreateBoard();
        openFile.LoadFen("7k/8/8/8/8/8/8/4K3 w - - 0 1");

        var closedFile = CreateBoard();
        closedFile.LoadFen("7k/8/8/8/8/8/4P3/4K3 w - - 0 1");

        var openFileScore = evaluator.Evaluate(openFile);
        var closedFileScore = evaluator.Evaluate(closedFile);

        Assert.True(
            closedFileScore > openFileScore,
            $"Expected open king file to be worse. open={openFileScore}, closed={closedFileScore}");
    }

    private static DecodedParams CreateKingSafetyOnlyParams()
    {
        return DecodedParams.ReasonableDefaults with
        {
            PawnShieldBonus = 0,
            OpenFileToKingPenalty = 0,
            SemiOpenFileToKingPenalty = 0,
            KingAttackUnit = 0,

            DoubledPawnPenalty = 0,
            IsolatedPawnPenalty = 0,
            BackwardPawnPenalty = 0,
            PassedPawnBonus = 0,
            ConnectedPassedBonus = 0
        };
    }
}
