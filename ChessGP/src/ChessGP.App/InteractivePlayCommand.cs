using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ChessGP.Core.Config;
using ChessGP.Core.Engine;
using ChessGP.Core.Eval;
using ChessGP.Core.Genome;
using ChessGP.Core.Persistence;

/// <summary>
/// Command-line helper for playing manually against a saved genome checkpoint.
/// </summary>
internal static class InteractivePlayCommand
{
    /// <summary>
    /// Starts an interactive console game using the supplied configuration and command arguments.
    /// </summary>
    public static void Run(AppOptions options, string configBaseDir, string[] args)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length < 1)
        {
            PrintUsage();
            return;
        }

        var checkpointPath = ResolveExistingFile(args[0], configBaseDir);

        var humanIsWhite = true;
        var depth = Math.Max(1, options.Stage2.searchDepth);
        var nodeCap = options.Stage2.perMoveNodeCap > 0
            ? options.Stage2.perMoveNodeCap
            : (int?)null;

        string? startFen = null;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];

            if (argument.Equals("--human", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                humanIsWhite = ParseColor(args[++index]);
                continue;
            }

            if (argument.Equals("--depth", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                depth = Math.Max(1, int.Parse(args[++index], CultureInfo.InvariantCulture));
                continue;
            }

            if (argument.Equals("--node-cap", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                var value = int.Parse(args[++index], CultureInfo.InvariantCulture);
                nodeCap = value > 0 ? value : null;
                continue;
            }

            if (argument.Equals("--fen", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                startFen = args[++index];
                continue;
            }

            throw new ArgumentException($"Unknown or incomplete argument: {argument}");
        }

        var checkpoint = LoadCheckpoint(checkpointPath);
        var schema = GeneSchemaBuilder.FromConfig(options.Genome);

        if (!checkpoint.SchemaId.Equals(options.Genome.schemaId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Checkpoint schema '{checkpoint.SchemaId}' does not match config schema '{options.Genome.schemaId}'.");
        }

        var parameters = GenomeToParamsDecoder.Decode(checkpoint.GenomeBits, schema);
        var evaluator = new ParametricEvaluator(parameters);
        var engine = new EngineCore();

        var board = new ChessDotNetBoardAdapter();

        if (string.IsNullOrWhiteSpace(startFen))
        {
            board.LoadStartpos();
        }
        else
        {
            board.LoadFen(startFen);
        }

        Console.WriteLine("=== ChessGP interactive play ===");
        Console.WriteLine($"Checkpoint: {checkpointPath}");
        Console.WriteLine($"Checkpoint stage: {checkpoint.Stage}, generation: {checkpoint.Generation}");
        Console.WriteLine($"Human plays: {(humanIsWhite ? "White" : "Black")}");
        Console.WriteLine($"Engine depth: {depth}");
        Console.WriteLine($"Node cap: {(nodeCap.HasValue ? nodeCap.Value.ToString(CultureInfo.InvariantCulture) : "none")}");
        Console.WriteLine();
        Console.WriteLine("Enter moves in UCI format, for example: e2e4, g1f3, e7e8q.");
        Console.WriteLine("Commands: help, board, fen, moves, quit");
        Console.WriteLine();

        PrintBoard(board);

        while (true)
        {
            if (PrintTerminalIfGameOver(board))
            {
                return;
            }

            var whiteToMove = IsWhiteToMove(board);
            var humanToMove = whiteToMove == humanIsWhite;

            if (humanToMove)
            {
                if (!ReadAndPlayHumanMove(board))
                {
                    return;
                }

                PrintBoard(board);
            }
            else
            {
                if (!PlayEngineMove(board, evaluator, engine, depth, nodeCap))
                {
                    return;
                }

                PrintBoard(board);
            }
        }
    }

    private static bool ReadAndPlayHumanMove(ChessDotNetBoardAdapter board)
    {
        while (true)
        {
            Console.Write("Your move> ");

            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (input.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Game ended by user.");
                return false;
            }

            if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Use UCI moves: e2e4, g1f3, e7e8q.");
                Console.WriteLine("Commands: help, board, fen, moves, quit");
                continue;
            }

            if (input.Equals("board", StringComparison.OrdinalIgnoreCase))
            {
                PrintBoard(board);
                continue;
            }

            if (input.Equals("fen", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(board.ToFen());
                continue;
            }

            if (input.Equals("moves", StringComparison.OrdinalIgnoreCase))
            {
                PrintLegalMoves(board);
                continue;
            }

            UciMove move;

            try
            {
                move = UciMove.Parse(input.ToLowerInvariant());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Invalid UCI move: {ex.Message}");
                continue;
            }

            var isLegal = board.GenerateLegalMoves().Any(legalMove =>
                legalMove.ToString().Equals(move.ToString(), StringComparison.OrdinalIgnoreCase));

            if (!isLegal)
            {
                Console.WriteLine("Illegal move in this position. Type 'moves' to show legal moves.");
                continue;
            }

            board.Make(move);
            return true;
        }
    }

    private static bool PlayEngineMove(
        ChessDotNetBoardAdapter board,
        ParametricEvaluator evaluator,
        EngineCore engine,
        int depth,
        int? nodeCap)
    {
        Console.WriteLine("Engine is thinking...");

        var stopwatch = Stopwatch.StartNew();

        var result = depth <= 1
            ? engine.SelectBestMove1Ply(board, evaluator)
            : engine.SearchAlphaBeta(board, evaluator, depth, nodeCap);

        stopwatch.Stop();

        if (string.IsNullOrWhiteSpace(result.Move.From) ||
            string.IsNullOrWhiteSpace(result.Move.To))
        {
            Console.WriteLine("Engine found no move.");
            return false;
        }

        Console.WriteLine(
            $"Engine plays: {result.Move}  " +
            $"score={result.Score}  " +
            $"nodes={result.Nodes}  " +
            $"time={stopwatch.Elapsed.TotalSeconds:F2}s");

        board.Make(result.Move);
        return true;
    }

    private static void PrintBoard(ChessDotNetBoardAdapter board)
    {
        var fen = board.ToFen();
        var parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var placement = parts[0];
        var ranks = placement.Split('/');

        Console.WriteLine();
        Console.WriteLine("    a b c d e f g h");
        Console.WriteLine("  +-----------------+");

        for (var rankIndex = 0; rankIndex < 8; rankIndex++)
        {
            var rankNumber = 8 - rankIndex;

            Console.Write($"{rankNumber} | ");

            foreach (var character in ranks[rankIndex])
            {
                if (char.IsDigit(character))
                {
                    var emptySquares = character - '0';

                    for (var index = 0; index < emptySquares; index++)
                    {
                        Console.Write(". ");
                    }
                }
                else
                {
                    Console.Write($"{character} ");
                }
            }

            Console.WriteLine($"| {rankNumber}");
        }

        Console.WriteLine("  +-----------------+");
        Console.WriteLine("    a b c d e f g h");
        Console.WriteLine($"Side to move: {(IsWhiteToMove(board) ? "White" : "Black")}");
        Console.WriteLine();
    }

    private static void PrintLegalMoves(ChessDotNetBoardAdapter board)
    {
        var moves = board.GenerateLegalMoves()
            .Select(move => move.ToString())
            .OrderBy(move => move, StringComparer.Ordinal)
            .ToList();

        Console.WriteLine($"Legal moves ({moves.Count}):");

        for (var index = 0; index < moves.Count; index++)
        {
            Console.Write(moves[index].PadRight(7));

            if ((index + 1) % 10 == 0)
            {
                Console.WriteLine();
            }
        }

        Console.WriteLine();
    }

    private static bool PrintTerminalIfGameOver(ChessDotNetBoardAdapter board)
    {
        if (!board.IsTerminal(out var result))
        {
            return false;
        }

        Console.WriteLine(result switch
        {
            GameResult.WhiteWin => "Game over: White wins.",
            GameResult.BlackWin => "Game over: Black wins.",
            GameResult.Draw => "Game over: Draw.",
            _ => "Game over."
        });

        return true;
    }

    private static bool IsWhiteToMove(ChessDotNetBoardAdapter board)
    {
        var parts = board.ToFen().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && parts[1] == "w";
    }

    private static bool ParseColor(string value)
    {
        if (value.Equals("white", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("w", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Equals("black", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("b", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new ArgumentException("Color must be 'white' or 'black'.");
    }

    private static BestGenomeCheckpoint LoadCheckpoint(string path)
    {
        var json = File.ReadAllText(path);
        var checkpoint = JsonSerializer.Deserialize<BestGenomeCheckpoint>(json);

        if (checkpoint is null)
        {
            throw new InvalidOperationException($"Could not read checkpoint: {path}");
        }

        return checkpoint;
    }

    private static string ResolveExistingFile(string path, string configBaseDir)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty.", nameof(path));
        }

        var currentDirectoryPath = Path.GetFullPath(path);
        if (File.Exists(currentDirectoryPath))
        {
            return currentDirectoryPath;
        }

        var configRelativePath = Path.GetFullPath(Path.Combine(configBaseDir, path));
        if (File.Exists(configRelativePath))
        {
            return configRelativePath;
        }

        throw new FileNotFoundException($"File not found: {path}");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  ChessGP.App play <config.json> <checkpoint.json> [--human white|black] [--depth N] [--node-cap N] [--fen FEN]");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  ChessGP.App play configs/stage2_real_001.json checkpoints/best.json --human white --depth 2 --node-cap 20000");
    }
}
