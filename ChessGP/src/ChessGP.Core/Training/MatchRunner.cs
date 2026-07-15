using ChessGP.Core.Config;
using ChessGP.Core.Engine;

namespace ChessGP.Core.Training;

/// <summary>
/// Aggregated result of a match from the first evaluator's perspective.
/// </summary>
public readonly record struct MatchScore(
    int Wins,
    int Draws,
    int Losses,
    long NodesEvaluated,
    long SearchElapsedMs = 0,
    int Searches = 0,
    int CompletedDepthTotal = 0,
    int TimeLosses = 0)
{
    /// <summary>
    /// Match score using two points for a win and one point for a draw.
    /// </summary>
    public long Points => (long)Wins * 2 + Draws;
}

/// <summary>
/// Runs head-to-head self-play matches between two evaluators.
/// </summary>
public static class MatchRunner
{
    /// <summary>
    /// Plays a configured number of games between two evaluators, alternating colors.
    /// </summary>
    public static MatchScore PlayMatch(
        IReadOnlyList<string> openingFens,
        IEvaluator firstEvaluator,
        IEvaluator secondEvaluator,
        Stage2Options config,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(openingFens);
        ArgumentNullException.ThrowIfNull(firstEvaluator);
        ArgumentNullException.ThrowIfNull(secondEvaluator);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(rng);

        if (openingFens.Count == 0)
        {
            throw new ArgumentException("Opening list is empty.", nameof(openingFens));
        }

        var wins = 0;
        var draws = 0;
        var losses = 0;

        long nodesEvaluated = 0;
        long searchElapsedMs = 0;

        var searches = 0;
        var completedDepthTotal = 0;
        var timeLosses = 0;

        var gamesToPlay = Math.Max(1, config.gamesPerPair);

        for (var gameIndex = 0; gameIndex < gamesToPlay; gameIndex++)
        {
            var startFen = OpeningBook.PickRandom(openingFens, rng);
            var firstEvaluatorIsWhite = gameIndex % 2 == 0;

            var result = GameRunner.PlayGame(
                startFen: startFen,
                whiteEval: firstEvaluatorIsWhite ? firstEvaluator : secondEvaluator,
                blackEval: firstEvaluatorIsWhite ? secondEvaluator : firstEvaluator,
                config: config,
                rng: rng);

            nodesEvaluated += result.NodesEvaluated;
            searchElapsedMs += result.SearchElapsedMs;
            searches += result.Searches;
            completedDepthTotal += result.CompletedDepthTotal;
            timeLosses += result.WhiteTimeLosses + result.BlackTimeLosses;

            switch (result.Result)
            {
                case GameResult.Draw:
                    draws++;
                    break;

                case GameResult.WhiteWin:
                    if (firstEvaluatorIsWhite)
                    {
                        wins++;
                    }
                    else
                    {
                        losses++;
                    }

                    break;

                case GameResult.BlackWin:
                    if (firstEvaluatorIsWhite)
                    {
                        losses++;
                    }
                    else
                    {
                        wins++;
                    }

                    break;

                default:
                    draws++;
                    break;
            }
        }

        return new MatchScore(
            wins,
            draws,
            losses,
            nodesEvaluated,
            searchElapsedMs,
            searches,
            completedDepthTotal,
            timeLosses);
    }
}
