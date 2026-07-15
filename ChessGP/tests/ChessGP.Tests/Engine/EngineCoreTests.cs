using System;
using System.Collections.Generic;
using ChessGP.Core.Engine;
using Xunit;

namespace ChessGP.Tests.Engine;

public sealed class EngineCoreTests
{
    private static IBoardAdapter CreateBoard()
    {
        return new ChessDotNetBoardAdapter();
    }

    public sealed class MaterialEval : IEvaluator
    {
        public int Evaluate(IBoardAdapter board)
        {
            var boardPart = board.ToFen().Split(' ')[0];

            var whiteMaterial = 0;
            var blackMaterial = 0;

            foreach (var piece in boardPart)
            {
                switch (piece)
                {
                    case 'P':
                        whiteMaterial += 100;
                        break;

                    case 'N':
                        whiteMaterial += 320;
                        break;

                    case 'B':
                        whiteMaterial += 330;
                        break;

                    case 'R':
                        whiteMaterial += 500;
                        break;

                    case 'Q':
                        whiteMaterial += 900;
                        break;

                    case 'p':
                        blackMaterial += 100;
                        break;

                    case 'n':
                        blackMaterial += 320;
                        break;

                    case 'b':
                        blackMaterial += 330;
                        break;

                    case 'r':
                        blackMaterial += 500;
                        break;

                    case 'q':
                        blackMaterial += 900;
                        break;
                }
            }

            return whiteMaterial - blackMaterial;
        }
    }

    private sealed class ZeroEval : IEvaluator
    {
        public int Evaluate(IBoardAdapter board)
        {
            return 0;
        }
    }

    private sealed class ConstantWhiteAdvantageEval : IEvaluator
    {
        public int Evaluate(IBoardAdapter board)
        {
            return 500;
        }
    }

    private sealed class ImmediateRepetitionBoard : IBoardAdapter
    {
        private string _state = "root";

        public int HalfmoveClock => 0;

        public void LoadStartpos()
        {
            _state = "root";
        }

        public void LoadFen(string fen)
        {
            _state = "root";
        }

        public IReadOnlyList<UciMove> GenerateLegalMoves()
        {
            return _state == "root"
                ? new[] { new UciMove("a1", "a2"), new UciMove("b1", "b2") }
                : new[] { new UciMove("h1", "h2") };
        }

        public string Make(UciMove move)
        {
            var previousState = _state;

            _state = move.From == "a1" ? "draw" : "safe";

            return previousState;
        }

        public void Unmake(string token)
        {
            _state = token;
        }

        public bool IsThreefold()
        {
            return _state == "draw";
        }

        public bool IsTerminal(out GameResult result)
        {
            result = GameResult.Ongoing;
            return false;
        }

        public string ToFen()
        {
            return _state == "root"
                ? "8/8/8/8/8/8/8/K6k w - - 0 1"
                : "8/8/8/8/8/8/8/K6k b - - 1 1";
        }
    }

    [Fact]
    public void OnePly_Chooses_Capture_That_Gains_Material()
    {
        var board = CreateBoard();

        board.LoadFen("7k/4r3/8/8/8/8/4Q3/K7 w - - 0 1");

        var engine = new EngineCore();
        var evaluator = new MaterialEval();

        var result = engine.SelectBestMove1Ply(board, evaluator);

        Assert.Equal("e2", result.Move.From);
        Assert.Equal("e7", result.Move.To);
        Assert.True(result.Score >= 400, $"Expected a big material gain, got {result.Score} cp.");
        Assert.True(result.Nodes > 0);
    }

    [Fact]
    public void OnePly_Nodes_Equals_Children_Count()
    {
        var board = new ChessDotNetBoardAdapter();

        board.LoadStartpos();

        var evaluator = new MaterialEval();
        var engine = new EngineCore();

        var childCount = board.GenerateLegalMoves().Count;
        var result = engine.SelectBestMove1Ply(board, evaluator);

        Assert.Equal(childCount, (int)result.Nodes);
    }

    [Fact]
    public void AlphaBeta_Depth1_Equals_OnePly()
    {
        var board = new ChessDotNetBoardAdapter();

        board.LoadFen("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1");

        var engine = new EngineCore();
        var evaluator = new MaterialEval();

        var onePly = engine.SelectBestMove1Ply(board, evaluator);
        var alphaBeta = engine.SearchAlphaBeta(board, evaluator, depth: 1);

        Assert.Equal(onePly.Move.From, alphaBeta.Move.From);
        Assert.Equal(onePly.Move.To, alphaBeta.Move.To);
    }

    [Fact]
    public void AlphaBeta_Nodes_Increase_With_Depth()
    {
        var board = new ChessDotNetBoardAdapter();

        board.LoadStartpos();

        var engine = new EngineCore();
        var evaluator = new MaterialEval();

        var depthOne = engine.SearchAlphaBeta(board, evaluator, depth: 1);
        var depthTwo = engine.SearchAlphaBeta(board, evaluator, depth: 2);

        Assert.True(
            depthTwo.Nodes >= depthOne.Nodes,
            $"Expected nodes to grow: d1={depthOne.Nodes}, d2={depthTwo.Nodes}");
    }

    [Fact]
    public void AlphaBeta_Handles_Checkmate_With_Big_Negative_Score()
    {
        var board = new ChessDotNetBoardAdapter();

        board.LoadFen("7k/6Q1/6K1/8/8/8/8/8 b - - 0 1");

        Assert.True(board.IsTerminal(out var result));
        Assert.Equal(GameResult.WhiteWin, result);

        var engine = new EngineCore();
        var evaluator = new MaterialEval();

        var searchResult = engine.SearchAlphaBeta(board, evaluator, depth: 2);

        Assert.True(
            searchResult.Score < 0,
            $"Expected negative score for mated side-to-move, got {searchResult.Score}");

        Assert.True(
            Math.Abs(searchResult.Score) >= 10_000,
            $"Expected huge magnitude mate score, got {searchResult.Score}");
    }

    [Fact]
    public void OnePly_TieBreak_Is_FirstGeneratedMove()
    {
        var board = CreateBoard();

        board.LoadStartpos();

        var engine = new EngineCore();
        var evaluator = new ZeroEval();

        var moves = board.GenerateLegalMoves();
        var result = engine.SelectBestMove1Ply(board, evaluator);

        Assert.Equal(moves[0].From, result.Move.From);
        Assert.Equal(moves[0].To, result.Move.To);
    }

    [Fact]
    public void AlphaBeta_Avoids_Immediate_Repetition_Draw_When_Ahead()
    {
        var board = new ImmediateRepetitionBoard();
        var engine = new EngineCore();
        var evaluator = new ConstantWhiteAdvantageEval();

        var result = engine.SearchAlphaBeta(
            board,
            evaluator,
            new SearchLimits(
                MaxDepth: 1,
                DrawContemptCp: 30,
                AvoidDrawWhenAheadCp: 200));

        Assert.Equal("b1", result.Move.From);
        Assert.Equal("b2", result.Move.To);
    }
}
