using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ChessGP.Core.Engine;

/// <summary>
/// Search configuration used by fixed-depth, node-limited, and time-limited searches.
/// </summary>
public sealed record SearchLimits(
    int MaxDepth,
    int? NodeCap = null,
    int? MaxTimeMs = null,
    int MoveOverheadMs = 10,
    int DrawContemptCp = 30,
    int AvoidDrawWhenAheadCp = 200)
{
    public int SafeMaxDepth => Math.Max(1, MaxDepth);

    public int? SafeMaxTimeMs =>
        MaxTimeMs.HasValue && MaxTimeMs.Value > 0 ? MaxTimeMs.Value : null;

    public int SafeMoveOverheadMs => Math.Max(0, MoveOverheadMs);

    public int SafeDrawContemptCp => Math.Max(0, DrawContemptCp);

    public int SafeAvoidDrawWhenAheadCp => Math.Max(0, AvoidDrawWhenAheadCp);
}

/// <summary>
/// Implements move selection using one-ply search, alpha-beta search, and time-limited iterative deepening.
/// </summary>
public sealed class EngineCore
{
    private const int INF = 1_000_000_000;
    private const int MATE = 30_000;

    /// <summary>
    /// Result returned by a completed or partially completed search.
    /// </summary>
    public readonly record struct BestMoveResult(
        UciMove Move,
        int Score,
        long Nodes,
        int CompletedDepth = 0,
        long ElapsedMs = 0,
        bool StoppedEarly = false);

    /// <summary>
    /// Selects the best move using static one-ply evaluation.
    /// </summary>
    public BestMoveResult SelectBestMove1Ply(IBoardAdapter board, IEvaluator evaluator)
    {
        return SelectBestMove1Ply(
            board,
            evaluator,
            new SearchLimits(MaxDepth: 1, DrawContemptCp: 0, AvoidDrawWhenAheadCp: 0));
    }

    /// <summary>
    /// Searches the current position using alpha-beta with optional node limit.
    /// </summary>
    public BestMoveResult SearchAlphaBeta(
        IBoardAdapter board,
        IEvaluator evaluator,
        int depth,
        int? nodeCap = null)
    {
        return SearchAlphaBeta(board, evaluator, new SearchLimits(MaxDepth: depth, NodeCap: nodeCap));
    }

    /// <summary>
    /// Searches the current position using the supplied depth, node, time, and draw-handling limits.
    /// </summary>
    public BestMoveResult SearchAlphaBeta(
        IBoardAdapter board,
        IEvaluator evaluator,
        SearchLimits limits)
    {
        if (limits is null)
        {
            throw new ArgumentNullException(nameof(limits));
        }

        var maxDepth = limits.SafeMaxDepth;
        var nodeCap = limits.NodeCap;
        var maxTimeMs = limits.SafeMaxTimeMs;
        var moveOverheadMs = limits.SafeMoveOverheadMs;
        var drawContemptCp = limits.SafeDrawContemptCp;
        var avoidDrawWhenAheadCp = limits.SafeAvoidDrawWhenAheadCp;

        var rootColor = SideToMoveColor(board);
        var rootStaticScore = rootColor * evaluator.Evaluate(board);

        if (IsTerminalOrThreefold(board, out var rootResult))
        {
            var score = TerminalScoreForSide(
                board,
                evaluator,
                rootResult,
                rootColor,
                ply: 0,
                drawContemptCp,
                avoidDrawWhenAheadCp);

            return new BestMoveResult(
                Move: default,
                Score: score,
                Nodes: 0,
                CompletedDepth: 0,
                ElapsedMs: 0,
                StoppedEarly: false);
        }

        if (maxDepth <= 1 && !maxTimeMs.HasValue)
        {
            return SelectBestMove1Ply(board, evaluator, limits);
        }

        var legalRootMoves = board.GenerateLegalMoves();
        if (legalRootMoves.Count == 0)
        {
            return new BestMoveResult(default, 0, 0);
        }

        var stopwatch = Stopwatch.StartNew();

        long nodes = 0;
        var stoppedEarly = false;

        var bestMoveAtRoot = legalRootMoves[0];
        var lastScore = 0;
        var completedDepth = 0;

        for (var depth = 1; depth <= maxDepth; depth++)
        {
            if (ShouldStop())
            {
                stoppedEarly = true;
                break;
            }

            var cancelledThisDepth = false;
            var alpha = -INF;
            var beta = INF;
            var principalVariationMove = default(UciMove);

            var score = SearchRoot(depth);

            if (cancelledThisDepth)
            {
                stoppedEarly = true;
                break;
            }

            bestMoveAtRoot = principalVariationMove.Equals(default(UciMove))
                ? bestMoveAtRoot
                : principalVariationMove;

            lastScore = score;
            completedDepth = depth;

            bool ShouldStop()
            {
                if (nodeCap.HasValue && nodes >= nodeCap.Value)
                {
                    return true;
                }

                if (!maxTimeMs.HasValue)
                {
                    return false;
                }

                var hardTimeLimit = Math.Max(1, maxTimeMs.Value - moveOverheadMs);
                return stopwatch.ElapsedMilliseconds >= hardTimeLimit;
            }

            int SearchRoot(int depthLeft)
            {
                if (IsTerminalOrThreefold(board, out var result))
                {
                    return TerminalScoreForSide(
                        board,
                        evaluator,
                        result,
                        rootColor,
                        ply: 0,
                        drawContemptCp,
                        avoidDrawWhenAheadCp);
                }

                if (depthLeft == 0)
                {
                    nodes++;

                    if (ShouldStop())
                    {
                        cancelledThisDepth = true;
                    }

                    return rootColor * evaluator.Evaluate(board);
                }

                var moves = board.GenerateLegalMoves();
                if (moves.Count == 0)
                {
                    return 0;
                }

                var bestScore = -INF;
                var bestMove = moves[0];

                foreach (var move in moves)
                {
                    if (ShouldStop())
                    {
                        cancelledThisDepth = true;
                        return alpha;
                    }

                    var undoToken = board.Make(move);
                    int childScore;

                    if (IsTerminalOrThreefold(board, out var childResult) &&
                        childResult == GameResult.Draw &&
                        IsClearlyAhead(rootStaticScore, avoidDrawWhenAheadCp) &&
                        drawContemptCp > 0)
                    {
                        childScore = RootDrawScore(
                            rootStaticScore,
                            drawContemptCp,
                            avoidDrawWhenAheadCp);
                    }
                    else
                    {
                        childScore = -SearchChild(
                            depthLeft - 1,
                            -beta,
                            -alpha,
                            ply: 1,
                            sideColor: -rootColor);
                    }

                    board.Unmake(undoToken);

                    if (cancelledThisDepth)
                    {
                        return alpha;
                    }

                    if (childScore > bestScore)
                    {
                        bestScore = childScore;
                        bestMove = move;
                    }

                    if (bestScore > alpha)
                    {
                        alpha = bestScore;
                    }

                    if (alpha >= beta)
                    {
                        break;
                    }
                }

                principalVariationMove = bestMove;
                return bestScore;
            }

            int SearchChild(int depthLeft, int alpha, int beta, int ply, int sideColor)
            {
                if (IsTerminalOrThreefold(board, out var result))
                {
                    return TerminalScoreForSide(
                        board,
                        evaluator,
                        result,
                        sideColor,
                        ply,
                        drawContemptCp,
                        avoidDrawWhenAheadCp);
                }

                if (depthLeft == 0)
                {
                    nodes++;

                    if (ShouldStop())
                    {
                        cancelledThisDepth = true;
                    }

                    return sideColor * evaluator.Evaluate(board);
                }

                var moves = board.GenerateLegalMoves();
                if (moves.Count == 0)
                {
                    return 0;
                }

                var bestScore = -INF;

                foreach (var move in moves)
                {
                    if (ShouldStop())
                    {
                        cancelledThisDepth = true;
                        return alpha;
                    }

                    var undoToken = board.Make(move);
                    var childScore = -SearchChild(depthLeft - 1, -beta, -alpha, ply + 1, -sideColor);
                    board.Unmake(undoToken);

                    if (cancelledThisDepth)
                    {
                        return alpha;
                    }

                    if (childScore > bestScore)
                    {
                        bestScore = childScore;
                    }

                    if (bestScore > alpha)
                    {
                        alpha = bestScore;
                    }

                    if (alpha >= beta)
                    {
                        break;
                    }
                }

                return bestScore;
            }
        }

        stopwatch.Stop();

        return new BestMoveResult(
            bestMoveAtRoot,
            lastScore,
            nodes,
            completedDepth,
            stopwatch.ElapsedMilliseconds,
            stoppedEarly);
    }

    private BestMoveResult SelectBestMove1Ply(
        IBoardAdapter board,
        IEvaluator evaluator,
        SearchLimits limits)
    {
        var moves = board.GenerateLegalMoves();
        if (moves.Count == 0)
        {
            return new BestMoveResult(default, 0, 0);
        }

        var rootColor = SideToMoveColor(board);
        var rootStaticScore = rootColor * evaluator.Evaluate(board);
        var drawContemptCp = limits.SafeDrawContemptCp;
        var avoidDrawWhenAheadCp = limits.SafeAvoidDrawWhenAheadCp;

        var bestMove = moves[0];
        var bestScore = int.MinValue;
        long nodes = 0;

        foreach (var move in moves)
        {
            var undoToken = board.Make(move);
            nodes++;

            int score;

            if (IsTerminalOrThreefold(board, out var result))
            {
                score = result == GameResult.Draw
                    ? RootDrawScore(rootStaticScore, drawContemptCp, avoidDrawWhenAheadCp)
                    : TerminalScoreForSide(
                        board,
                        evaluator,
                        result,
                        rootColor,
                        ply: 0,
                        drawContemptCp,
                        avoidDrawWhenAheadCp);
            }
            else
            {
                score = rootColor * evaluator.Evaluate(board);
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }

            board.Unmake(undoToken);
        }

        return new BestMoveResult(bestMove, bestScore, nodes, CompletedDepth: 1);
    }

    private static int SideToMoveColor(IBoardAdapter board)
    {
        var parts = board.ToFen().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length >= 2 && parts[1].Equals("b", StringComparison.OrdinalIgnoreCase)
            ? -1
            : +1;
    }

    private static bool IsTerminalOrThreefold(IBoardAdapter board, out GameResult result)
    {
        if (board.IsTerminal(out result))
        {
            return true;
        }

        if (board.IsThreefold())
        {
            result = GameResult.Draw;
            return true;
        }

        result = GameResult.Ongoing;
        return false;
    }

    private static int TerminalScoreForSide(
        IBoardAdapter board,
        IEvaluator evaluator,
        GameResult result,
        int sideColor,
        int ply,
        int drawContemptCp,
        int avoidDrawWhenAheadCp)
    {
        var whitePerspectiveScore = result switch
        {
            GameResult.WhiteWin => MATE - ply,
            GameResult.BlackWin => -MATE + ply,
            GameResult.Draw => int.MinValue,
            _ => 0
        };

        if (result != GameResult.Draw)
        {
            return sideColor * whitePerspectiveScore;
        }

        return DrawScoreForSide(
            board,
            evaluator,
            sideColor,
            drawContemptCp,
            avoidDrawWhenAheadCp);
    }

    private static int DrawScoreForSide(
        IBoardAdapter board,
        IEvaluator evaluator,
        int sideColor,
        int drawContemptCp,
        int avoidDrawWhenAheadCp)
    {
        if (drawContemptCp <= 0 || avoidDrawWhenAheadCp <= 0)
        {
            return 0;
        }

        var sideStaticScore = sideColor * evaluator.Evaluate(board);

        if (sideStaticScore >= avoidDrawWhenAheadCp)
        {
            return -drawContemptCp;
        }

        if (sideStaticScore <= -avoidDrawWhenAheadCp)
        {
            return drawContemptCp;
        }

        return 0;
    }

    private static int RootDrawScore(
        int rootStaticScore,
        int drawContemptCp,
        int avoidDrawWhenAheadCp)
    {
        if (drawContemptCp <= 0 || avoidDrawWhenAheadCp <= 0)
        {
            return 0;
        }

        if (rootStaticScore >= avoidDrawWhenAheadCp)
        {
            return -Math.Max(drawContemptCp, avoidDrawWhenAheadCp);
        }

        if (rootStaticScore <= -avoidDrawWhenAheadCp)
        {
            return drawContemptCp;
        }

        return 0;
    }

    private static bool IsClearlyAhead(int rootStaticScore, int avoidDrawWhenAheadCp)
    {
        return avoidDrawWhenAheadCp > 0 && rootStaticScore >= avoidDrawWhenAheadCp;
    }
}
