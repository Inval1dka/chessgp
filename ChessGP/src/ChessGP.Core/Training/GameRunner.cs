using System;
using System.Diagnostics;
using ChessGP.Core.Config;
using ChessGP.Core.Engine;

namespace ChessGP.Core.Training;

/// <summary>
/// Result of one self-play game.
/// </summary>
public readonly record struct GamePlayResult(
    GameResult Result,
    int Plies,
    long NodesEvaluated,
    long SearchElapsedMs = 0,
    int Searches = 0,
    int CompletedDepthTotal = 0,
    int WhiteTimeLosses = 0,
    int BlackTimeLosses = 0);

/// <summary>
/// Plays self-play games between two evaluators using the Stage 2 search configuration.
/// </summary>
public static class GameRunner
{
    /// <summary>
    /// Plays one game from the supplied start position until a terminal result, draw rule, or ply limit.
    /// </summary>
    public static GamePlayResult PlayGame(
        string startFen,
        IEvaluator whiteEval,
        IEvaluator blackEval,
        Stage2Options config,
        Random rng,
        EngineCore? engine = null,
        Func<IBoardAdapter>? boardFactory = null,
        int maxPlies = 400)
    {
        ArgumentNullException.ThrowIfNull(whiteEval);
        ArgumentNullException.ThrowIfNull(blackEval);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(rng);

        engine ??= new EngineCore();
        boardFactory ??= () => new ChessDotNetBoardAdapter();

        var board = boardFactory();

        if (string.IsNullOrWhiteSpace(startFen))
        {
            board.LoadStartpos();
        }
        else
        {
            board.LoadFen(startFen);
        }

        long nodes = 0;
        long searchElapsedMs = 0;
        var searches = 0;
        var completedDepthTotal = 0;
        var plies = 0;

        var whiteClockMs = Math.Max(0, config.initialTimeMs);
        var blackClockMs = Math.Max(0, config.initialTimeMs);

        if (board.IsTerminal(out var terminalResult))
        {
            return new GamePlayResult(
                terminalResult,
                plies,
                nodes,
                searchElapsedMs,
                searches,
                completedDepthTotal);
        }

        while (plies < maxPlies)
        {
            if (board.HalfmoveClock >= 100)
            {
                return new GamePlayResult(
                    GameResult.Draw,
                    plies,
                    nodes,
                    searchElapsedMs,
                    searches,
                    completedDepthTotal);
            }

            var sideToMove = SideToMove(board);
            var evaluator = sideToMove == 'w' ? whiteEval : blackEval;
            var limits = BuildSearchLimits(config, sideToMove, plies, whiteClockMs, blackClockMs);

            if (IsTimePerGame(config))
            {
                if (sideToMove == 'w' && whiteClockMs <= 0)
                {
                    return new GamePlayResult(
                        GameResult.BlackWin,
                        plies,
                        nodes,
                        searchElapsedMs,
                        searches,
                        completedDepthTotal,
                        WhiteTimeLosses: 1);
                }

                if (sideToMove == 'b' && blackClockMs <= 0)
                {
                    return new GamePlayResult(
                        GameResult.WhiteWin,
                        plies,
                        nodes,
                        searchElapsedMs,
                        searches,
                        completedDepthTotal,
                        BlackTimeLosses: 1);
                }
            }

            var stopwatch = Stopwatch.StartNew();
            var bestMove = engine.SearchAlphaBeta(board, evaluator, limits);
            stopwatch.Stop();

            var elapsedMs = bestMove.ElapsedMs > 0
                ? bestMove.ElapsedMs
                : stopwatch.ElapsedMilliseconds;

            nodes += bestMove.Nodes;
            searchElapsedMs += elapsedMs;
            searches++;
            completedDepthTotal += Math.Max(0, bestMove.CompletedDepth);

            if (IsTimePerGame(config))
            {
                var elapsedInt = elapsedMs >= int.MaxValue
                    ? int.MaxValue
                    : (int)Math.Max(0, elapsedMs);

                if (sideToMove == 'w')
                {
                    whiteClockMs -= elapsedInt;

                    if (whiteClockMs <= 0)
                    {
                        return new GamePlayResult(
                            GameResult.BlackWin,
                            plies,
                            nodes,
                            searchElapsedMs,
                            searches,
                            completedDepthTotal,
                            WhiteTimeLosses: 1);
                    }

                    whiteClockMs += Math.Max(0, config.incrementMs);
                }
                else
                {
                    blackClockMs -= elapsedInt;

                    if (blackClockMs <= 0)
                    {
                        return new GamePlayResult(
                            GameResult.WhiteWin,
                            plies,
                            nodes,
                            searchElapsedMs,
                            searches,
                            completedDepthTotal,
                            BlackTimeLosses: 1);
                    }

                    blackClockMs += Math.Max(0, config.incrementMs);
                }
            }

            if (bestMove.Move.Equals(default(UciMove)))
            {
                if (board.IsTerminal(out var result))
                {
                    return new GamePlayResult(
                        result,
                        plies,
                        nodes,
                        searchElapsedMs,
                        searches,
                        completedDepthTotal);
                }

                return new GamePlayResult(
                    GameResult.Draw,
                    plies,
                    nodes,
                    searchElapsedMs,
                    searches,
                    completedDepthTotal);
            }

            board.Make(bestMove.Move);
            plies++;

            if (board.IsThreefold())
            {
                return new GamePlayResult(
                    GameResult.Draw,
                    plies,
                    nodes,
                    searchElapsedMs,
                    searches,
                    completedDepthTotal);
            }

            if (board.IsTerminal(out var resultAfterMove))
            {
                return new GamePlayResult(
                    resultAfterMove,
                    plies,
                    nodes,
                    searchElapsedMs,
                    searches,
                    completedDepthTotal);
            }
        }

        return new GamePlayResult(
            GameResult.Draw,
            plies,
            nodes,
            searchElapsedMs,
            searches,
            completedDepthTotal);
    }

    private static SearchLimits BuildSearchLimits(
        Stage2Options config,
        char sideToMove,
        int ply,
        int whiteClockMs,
        int blackClockMs)
    {
        var maxDepth = GetMaxDepth(config);
        var nodeCap = config.perMoveNodeCap > 0 ? config.perMoveNodeCap : (int?)null;
        var moveOverheadMs = Math.Max(0, config.moveOverheadMs);

        if (IsFixedDepth(config) || IsNodeCap(config))
        {
            return new SearchLimits(
                MaxDepth: maxDepth,
                NodeCap: nodeCap,
                MoveOverheadMs: moveOverheadMs,
                DrawContemptCp: DrawContemptCp(config),
                AvoidDrawWhenAheadCp: AvoidDrawWhenAheadCp(config));
        }

        if (IsTimePerMove(config))
        {
            var timePerMoveMs = Math.Max(1, config.timePerMoveMs);

            return new SearchLimits(
                MaxDepth: maxDepth,
                NodeCap: nodeCap,
                MaxTimeMs: timePerMoveMs,
                MoveOverheadMs: moveOverheadMs,
                DrawContemptCp: DrawContemptCp(config),
                AvoidDrawWhenAheadCp: AvoidDrawWhenAheadCp(config));
        }

        if (IsTimePerGame(config))
        {
            var remainingMs = sideToMove == 'w' ? whiteClockMs : blackClockMs;
            var allocatedMs = AllocateMoveTimeMs(config, remainingMs, ply);

            return new SearchLimits(
                MaxDepth: maxDepth,
                NodeCap: nodeCap,
                MaxTimeMs: allocatedMs,
                MoveOverheadMs: moveOverheadMs,
                DrawContemptCp: DrawContemptCp(config),
                AvoidDrawWhenAheadCp: AvoidDrawWhenAheadCp(config));
        }

        throw new NotSupportedException($"Unsupported Stage2.searchMode: {config.searchMode}");
    }

    private static int AllocateMoveTimeMs(Stage2Options config, int remainingMs, int ply)
    {
        var moveOverheadMs = Math.Max(0, config.moveOverheadMs);
        var safeRemainingMs = Math.Max(1, remainingMs - moveOverheadMs);
        var configuredMovesToGo = config.estimatedMovesToGo > 0
            ? config.estimatedMovesToGo
            : 30;

        var dynamicMovesToGo = Math.Max(10, configuredMovesToGo - ply / 2);

        var budgetMs = safeRemainingMs / dynamicMovesToGo;
        budgetMs += (int)Math.Round(
            Math.Max(0, config.incrementMs) * 0.75,
            MidpointRounding.AwayFromZero);

        var maxForMoveMs = Math.Max(1, safeRemainingMs / 3);

        if (budgetMs > maxForMoveMs)
        {
            budgetMs = maxForMoveMs;
        }

        if (budgetMs < 1)
        {
            budgetMs = 1;
        }

        return budgetMs;
    }

    private static int GetMaxDepth(Stage2Options config)
    {
        if (config.maxSearchDepth > 0)
        {
            return config.maxSearchDepth;
        }

        return Math.Max(1, config.searchDepth);
    }

    private static bool IsFixedDepth(Stage2Options config)
    {
        return SearchMode(config).Equals("FixedDepth", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNodeCap(Stage2Options config)
    {
        return SearchMode(config).Equals("NodeCap", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTimePerMove(Stage2Options config)
    {
        var searchMode = SearchMode(config);

        return searchMode.Equals("TimePerMove", StringComparison.OrdinalIgnoreCase) ||
               searchMode.Equals("FixedTimePerMove", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTimePerGame(Stage2Options config)
    {
        return SearchMode(config).Equals("TimePerGame", StringComparison.OrdinalIgnoreCase);
    }

    private static int DrawContemptCp(Stage2Options config)
    {
        return config.drawContemptCp > 0 ? config.drawContemptCp : 30;
    }

    private static int AvoidDrawWhenAheadCp(Stage2Options config)
    {
        return config.avoidDrawWhenAheadCp > 0 ? config.avoidDrawWhenAheadCp : 200;
    }

    private static string SearchMode(Stage2Options config)
    {
        return string.IsNullOrWhiteSpace(config.searchMode)
            ? "FixedDepth"
            : config.searchMode;
    }

    private static char SideToMove(IBoardAdapter board)
    {
        var fen = board.ToFen();
        var parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            return 'w';
        }

        return parts[1].Length > 0 ? parts[1][0] : 'w';
    }
}
