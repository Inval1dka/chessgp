using System;
using System.Collections.Generic;
using ChessGP.Core.Config;
using ChessGP.Core.Eval;
using ChessGP.Core.Training;
using Xunit;

namespace ChessGP.Tests.Training;

public sealed class MatchRunnerTests
{
    [Fact]
    public void PlayMatch_ReturnsConsistentCounts_ForTerminalOpenings()
    {
        var openings = new List<string>
        {
            "7k/5Q2/7K/8/8/8/8/8 b - - 0 1"
        };

        var firstEvaluator = new ParametricEvaluator(DecodedParams.ReasonableDefaults);
        var secondEvaluator = new ParametricEvaluator(DecodedParams.ReasonableDefaults);

        var config = CreateFixedDepthConfig(gamesPerPair: 4);
        var rng = new Random(123);

        var score = MatchRunner.PlayMatch(
            openingFens: openings,
            firstEvaluator: firstEvaluator,
            secondEvaluator: secondEvaluator,
            config: config,
            rng: rng);

        Assert.Equal(config.gamesPerPair, score.Wins + score.Draws + score.Losses);
        Assert.True(score.NodesEvaluated >= 0);
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
