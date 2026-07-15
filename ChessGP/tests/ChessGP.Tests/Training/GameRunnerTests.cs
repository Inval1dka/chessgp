using System;
using ChessGP.Core.Config;
using ChessGP.Core.Engine;
using ChessGP.Core.Eval;
using ChessGP.Core.Training;
using Xunit;

namespace ChessGP.Tests.Training;

public sealed class GameRunnerTests
{
    [Fact]
    public void PlayGame_ReturnsImmediately_WhenStartFenIsTerminal()
    {
        const string mateFen = "7k/5Q2/7K/8/8/8/8/8 b - - 0 1";

        var evaluator = new ParametricEvaluator(DecodedParams.ReasonableDefaults);
        var config = CreateFixedDepthConfig(gamesPerPair: 1);
        var rng = new Random(1);

        var result = GameRunner.PlayGame(
            startFen: mateFen,
            whiteEval: evaluator,
            blackEval: evaluator,
            config: config,
            rng: rng);

        Assert.Equal(0, result.Plies);
        Assert.True(result.Result is GameResult.WhiteWin or GameResult.BlackWin or GameResult.Draw);
    }

    private static Stage2Options CreateFixedDepthConfig(int gamesPerPair)
    {
        return new Stage2Options(
            population: 1,
            crossoverRate: 0.0,
            mutationRate: 0.0,
            generations: 1,
            gamesPerPair: gamesPerPair,
            openingBookPath: "",
            useHallOfFame: false,
            hallOfFameSize: 0,
            searchDepth: 2,
            searchMode: "FixedDepth",
            timePerMoveMs: 0,
            perMoveNodeCap: 0);
    }
}
