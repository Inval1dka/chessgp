using System.Globalization;
using System.Text.Json;
using ChessGP.Core.Config;
using ChessGP.Core.Engine;
using ChessGP.Core.Eval;
using ChessGP.Core.Genome;
using ChessGP.Core.Persistence;

/// <summary>
/// Runs the engine as a UCI-compatible chess engine.
/// </summary>
internal static class UciCommand
{
    /// <summary>
    /// Starts a UCI session using the supplied configuration, checkpoint, and command-line overrides.
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

        var depth = Math.Max(1, options.Stage2.searchDepth);
        var nodeCap = options.Stage2.perMoveNodeCap > 0
            ? options.Stage2.perMoveNodeCap
            : (int?)null;

        var maxSearchDepth = options.Stage2.maxSearchDepth > 0
            ? options.Stage2.maxSearchDepth
            : 32;

        var moveOverheadMs = Math.Max(0, options.Stage2.moveOverheadMs);
        var estimatedMovesToGo = options.Stage2.estimatedMovesToGo > 0
            ? options.Stage2.estimatedMovesToGo
            : 30;

        var minimumMoveTimeMs = 20;
        var drawContemptCp = options.Stage2.drawContemptCp > 0
            ? options.Stage2.drawContemptCp
            : 30;

        var avoidDrawWhenAheadCp = options.Stage2.avoidDrawWhenAheadCp > 0
            ? options.Stage2.avoidDrawWhenAheadCp
            : 200;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];

            if (argument.Equals("--depth", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                depth = Math.Max(1, ParseInt(args[++index]));
                continue;
            }

            if (argument.Equals("--node-cap", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                var value = ParseInt(args[++index]);
                nodeCap = value > 0 ? value : null;
                continue;
            }

            if (argument.Equals("--max-depth", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                maxSearchDepth = Math.Max(1, ParseInt(args[++index]));
                continue;
            }

            if (argument.Equals("--move-overhead-ms", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                moveOverheadMs = Math.Max(0, ParseInt(args[++index]));
                continue;
            }

            if (argument.Equals("--estimated-moves-to-go", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                estimatedMovesToGo = Math.Max(1, ParseInt(args[++index]));
                continue;
            }

            if (argument.Equals("--minimum-move-time-ms", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                minimumMoveTimeMs = Math.Max(1, ParseInt(args[++index]));
                continue;
            }

            if (argument.Equals("--draw-contempt-cp", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                drawContemptCp = Math.Max(0, ParseInt(args[++index]));
                continue;
            }

            if (argument.Equals("--avoid-draw-when-ahead-cp", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                avoidDrawWhenAheadCp = Math.Max(0, ParseInt(args[++index]));
                continue;
            }

            throw new ArgumentException($"Unknown or incomplete argument: {argument}");
        }

        maxSearchDepth = Math.Max(depth, maxSearchDepth);

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
        board.LoadStartpos();

        var session = new UciSession(
            board,
            evaluator,
            engine,
            depth,
            nodeCap,
            maxSearchDepth,
            moveOverheadMs,
            estimatedMovesToGo,
            minimumMoveTimeMs,
            drawContemptCp,
            avoidDrawWhenAheadCp);

        session.RunLoop();
    }

    private sealed class UciSession
    {
        private readonly ChessDotNetBoardAdapter _board;
        private readonly ParametricEvaluator _evaluator;
        private readonly EngineCore _engine;

        private int _defaultDepth;
        private int? _defaultNodeCap;
        private int _maxSearchDepth;
        private int _moveOverheadMs;
        private int _estimatedMovesToGo;
        private int _minimumMoveTimeMs;
        private int _drawContemptCp;
        private int _avoidDrawWhenAheadCp;
        private bool _debug;

        public UciSession(
            ChessDotNetBoardAdapter board,
            ParametricEvaluator evaluator,
            EngineCore engine,
            int defaultDepth,
            int? defaultNodeCap,
            int maxSearchDepth,
            int moveOverheadMs,
            int estimatedMovesToGo,
            int minimumMoveTimeMs,
            int drawContemptCp,
            int avoidDrawWhenAheadCp)
        {
            _board = board;
            _evaluator = evaluator;
            _engine = engine;
            _defaultDepth = Math.Max(1, defaultDepth);
            _defaultNodeCap = defaultNodeCap;
            _maxSearchDepth = Math.Max(_defaultDepth, maxSearchDepth);
            _moveOverheadMs = Math.Max(0, moveOverheadMs);
            _estimatedMovesToGo = Math.Max(1, estimatedMovesToGo);
            _minimumMoveTimeMs = Math.Max(1, minimumMoveTimeMs);
            _drawContemptCp = Math.Max(0, drawContemptCp);
            _avoidDrawWhenAheadCp = Math.Max(0, avoidDrawWhenAheadCp);
        }

        public void RunLoop()
        {
            string? line;

            while ((line = Console.ReadLine()) is not null)
            {
                line = line.Trim();

                if (line.Length == 0)
                {
                    continue;
                }

                try
                {
                    if (line.Equals("uci", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleUci();
                        continue;
                    }

                    if (line.Equals("isready", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteLine("readyok");
                        continue;
                    }

                    if (line.Equals("ucinewgame", StringComparison.OrdinalIgnoreCase))
                    {
                        _board.LoadStartpos();
                        continue;
                    }

                    if (line.StartsWith("debug ", StringComparison.OrdinalIgnoreCase))
                    {
                        _debug = line.EndsWith(" on", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    if (line.StartsWith("setoption ", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleSetOption(line);
                        continue;
                    }

                    if (line.StartsWith("position ", StringComparison.OrdinalIgnoreCase))
                    {
                        HandlePosition(line);
                        continue;
                    }

                    if (line.Equals("go", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("go ", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleGo(line);
                        continue;
                    }

                    if (line.Equals("stop", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (line.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    if (_debug)
                    {
                        WriteError($"Ignored unknown UCI command: {line}");
                    }
                }
                catch (Exception ex)
                {
                    WriteError($"UCI command failed: {line}");
                    WriteError(ex.ToString());
                }
            }
        }

        private void HandleUci()
        {
            WriteLine("id name ChessGP");
            WriteLine("id author inv_");
            WriteLine($"option name SearchDepth type spin default {_defaultDepth} min 1 max 64");
            WriteLine($"option name NodeCap type spin default {_defaultNodeCap.GetValueOrDefault(0)} min 0 max 100000000");
            WriteLine($"option name MaxSearchDepth type spin default {_maxSearchDepth} min 1 max 64");
            WriteLine($"option name MoveOverheadMs type spin default {_moveOverheadMs} min 0 max 5000");
            WriteLine($"option name EstimatedMovesToGo type spin default {_estimatedMovesToGo} min 1 max 200");
            WriteLine($"option name MinimumMoveTimeMs type spin default {_minimumMoveTimeMs} min 1 max 10000");
            WriteLine($"option name DrawContemptCp type spin default {_drawContemptCp} min 0 max 2000");
            WriteLine($"option name AvoidDrawWhenAheadCp type spin default {_avoidDrawWhenAheadCp} min 0 max 5000");
            WriteLine("uciok");
        }

        private void HandleSetOption(string line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var nameIndex = Array.FindIndex(parts, part => part.Equals("name", StringComparison.OrdinalIgnoreCase));
            var valueIndex = Array.FindIndex(parts, part => part.Equals("value", StringComparison.OrdinalIgnoreCase));

            if (nameIndex < 0 ||
                valueIndex < 0 ||
                valueIndex <= nameIndex + 1 ||
                valueIndex + 1 >= parts.Length)
            {
                return;
            }

            var name = string.Join(' ', parts.Skip(nameIndex + 1).Take(valueIndex - nameIndex - 1));
            var value = string.Join(' ', parts.Skip(valueIndex + 1));

            if (name.Equals("SearchDepth", StringComparison.OrdinalIgnoreCase))
            {
                _defaultDepth = Math.Max(1, ParseInt(value));
                _maxSearchDepth = Math.Max(_defaultDepth, _maxSearchDepth);
                return;
            }

            if (name.Equals("NodeCap", StringComparison.OrdinalIgnoreCase))
            {
                var parsed = ParseInt(value);
                _defaultNodeCap = parsed > 0 ? parsed : null;
                return;
            }

            if (name.Equals("MaxSearchDepth", StringComparison.OrdinalIgnoreCase))
            {
                _maxSearchDepth = Math.Max(_defaultDepth, ParseInt(value));
                return;
            }

            if (name.Equals("MoveOverheadMs", StringComparison.OrdinalIgnoreCase))
            {
                _moveOverheadMs = Math.Max(0, ParseInt(value));
                return;
            }

            if (name.Equals("EstimatedMovesToGo", StringComparison.OrdinalIgnoreCase))
            {
                _estimatedMovesToGo = Math.Max(1, ParseInt(value));
                return;
            }

            if (name.Equals("MinimumMoveTimeMs", StringComparison.OrdinalIgnoreCase))
            {
                _minimumMoveTimeMs = Math.Max(1, ParseInt(value));
                return;
            }

            if (name.Equals("DrawContemptCp", StringComparison.OrdinalIgnoreCase))
            {
                _drawContemptCp = Math.Max(0, ParseInt(value));
                return;
            }

            if (name.Equals("AvoidDrawWhenAheadCp", StringComparison.OrdinalIgnoreCase))
            {
                _avoidDrawWhenAheadCp = Math.Max(0, ParseInt(value));
            }
        }

        private void HandlePosition(string line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                throw new ArgumentException("Incomplete position command.");
            }

            var moveStartIndex = Array.FindIndex(parts, part => part.Equals("moves", StringComparison.OrdinalIgnoreCase));

            if (parts[1].Equals("startpos", StringComparison.OrdinalIgnoreCase))
            {
                _board.LoadStartpos();

                if (moveStartIndex < 0)
                {
                    return;
                }
            }
            else if (parts[1].Equals("fen", StringComparison.OrdinalIgnoreCase))
            {
                var fenEnd = moveStartIndex >= 0 ? moveStartIndex : parts.Length;
                var fenParts = parts.Skip(2).Take(fenEnd - 2).ToArray();

                if (fenParts.Length < 4)
                {
                    throw new ArgumentException("FEN position must contain at least 4 fields.");
                }

                _board.LoadFen(string.Join(' ', fenParts));
            }
            else
            {
                throw new ArgumentException($"Unsupported position command: {line}");
            }

            if (moveStartIndex >= 0)
            {
                foreach (var moveText in parts.Skip(moveStartIndex + 1))
                {
                    PlayMove(moveText);
                }
            }
        }

        private void HandleGo(string line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var goOptions = ParseGo(parts);

            if (_board.IsTerminal(out _))
            {
                WriteLine("bestmove 0000");
                return;
            }

            var legalMoves = _board.GenerateLegalMoves();
            if (legalMoves.Count == 0)
            {
                WriteLine("bestmove 0000");
                return;
            }

            var limits = BuildSearchLimits(goOptions);
            var result = _engine.SearchAlphaBeta(_board, _evaluator, limits);

            var bestMove = string.IsNullOrWhiteSpace(result.Move.From) ||
                           string.IsNullOrWhiteSpace(result.Move.To)
                ? legalMoves[0]
                : result.Move;

            var reportedDepth = result.CompletedDepth > 0
                ? result.CompletedDepth
                : limits.SafeMaxDepth;

            WriteLine($"info depth {reportedDepth} score cp {result.Score} nodes {result.Nodes} time {result.ElapsedMs}");
            WriteLine($"bestmove {bestMove}");
        }

        private SearchLimits BuildSearchLimits(GoOptions goOptions)
        {
            var requestedDepth = goOptions.Depth.HasValue
                ? Math.Max(1, goOptions.Depth.Value)
                : _defaultDepth;

            var nodeCap = goOptions.NodeCap ?? _defaultNodeCap;

            if (goOptions.MoveTimeMs.HasValue)
            {
                var maxDepth = goOptions.Depth.HasValue ? requestedDepth : _maxSearchDepth;

                return new SearchLimits(
                    MaxDepth: maxDepth,
                    NodeCap: nodeCap,
                    MaxTimeMs: Math.Max(_minimumMoveTimeMs, goOptions.MoveTimeMs.Value),
                    MoveOverheadMs: _moveOverheadMs,
                    DrawContemptCp: _drawContemptCp,
                    AvoidDrawWhenAheadCp: _avoidDrawWhenAheadCp);
            }

            var clockBudget = AllocateClockBudget(goOptions);
            if (clockBudget.HasValue)
            {
                var maxDepth = goOptions.Depth.HasValue ? requestedDepth : _maxSearchDepth;

                return new SearchLimits(
                    MaxDepth: maxDepth,
                    NodeCap: nodeCap,
                    MaxTimeMs: clockBudget.Value,
                    MoveOverheadMs: _moveOverheadMs,
                    DrawContemptCp: _drawContemptCp,
                    AvoidDrawWhenAheadCp: _avoidDrawWhenAheadCp);
            }

            if (goOptions.NodeCap.HasValue && !goOptions.Depth.HasValue)
            {
                return new SearchLimits(
                    MaxDepth: _maxSearchDepth,
                    NodeCap: nodeCap,
                    MoveOverheadMs: _moveOverheadMs,
                    DrawContemptCp: _drawContemptCp,
                    AvoidDrawWhenAheadCp: _avoidDrawWhenAheadCp);
            }

            return new SearchLimits(
                MaxDepth: requestedDepth,
                NodeCap: nodeCap,
                MoveOverheadMs: _moveOverheadMs,
                DrawContemptCp: _drawContemptCp,
                AvoidDrawWhenAheadCp: _avoidDrawWhenAheadCp);
        }

        private int? AllocateClockBudget(GoOptions goOptions)
        {
            var sideToMove = SideToMove();
            var remainingMs = sideToMove == 'w'
                ? goOptions.WhiteTimeMs
                : goOptions.BlackTimeMs;

            if (!remainingMs.HasValue || remainingMs.Value <= 0)
            {
                return null;
            }

            var incrementMs = sideToMove == 'w'
                ? goOptions.WhiteIncrementMs
                : goOptions.BlackIncrementMs;

            var overheadMs = Math.Max(0, _moveOverheadMs);
            var safeRemainingMs = Math.Max(1, remainingMs.Value - overheadMs);

            int movesToGo;

            if (goOptions.MovesToGo.HasValue && goOptions.MovesToGo.Value > 0)
            {
                movesToGo = goOptions.MovesToGo.Value;
            }
            else
            {
                var playedFullMoves = FullMovesPlayed();
                movesToGo = Math.Max(10, _estimatedMovesToGo - playedFullMoves);
            }

            var budgetMs = safeRemainingMs / Math.Max(1, movesToGo);
            budgetMs += (int)Math.Round(
                Math.Max(0, incrementMs) * 0.75,
                MidpointRounding.AwayFromZero);

            var maxForMoveMs = Math.Max(1, safeRemainingMs / 3);

            budgetMs = Math.Min(budgetMs, maxForMoveMs);
            budgetMs = Math.Max(budgetMs, _minimumMoveTimeMs);
            budgetMs = Math.Min(budgetMs, safeRemainingMs);

            return Math.Max(1, budgetMs);
        }

        private GoOptions ParseGo(string[] parts)
        {
            var result = new GoOptions();

            for (var index = 1; index < parts.Length; index++)
            {
                var token = parts[index];

                if (token.Equals("searchmoves", StringComparison.OrdinalIgnoreCase))
                {
                    while (index + 1 < parts.Length && !IsGoKeyword(parts[index + 1]))
                    {
                        index++;
                    }

                    continue;
                }

                if (token.Equals("ponder", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("infinite", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (token.Equals("depth", StringComparison.OrdinalIgnoreCase) &&
                    index + 1 < parts.Length)
                {
                    result.Depth = Math.Max(1, ParseInt(parts[++index]));
                    continue;
                }

                if (token.Equals("nodes", StringComparison.OrdinalIgnoreCase) &&
                    index + 1 < parts.Length)
                {
                    var parsed = ParseInt(parts[++index]);
                    result.NodeCap = parsed > 0 ? parsed : null;
                    continue;
                }

                if (token.Equals("movetime", StringComparison.OrdinalIgnoreCase) &&
                    index + 1 < parts.Length)
                {
                    var parsed = ParseInt(parts[++index]);
                    result.MoveTimeMs = parsed > 0 ? parsed : null;
                    continue;
                }

                if (token.Equals("wtime", StringComparison.OrdinalIgnoreCase) &&
                    index + 1 < parts.Length)
                {
                    result.WhiteTimeMs = Math.Max(0, ParseInt(parts[++index]));
                    continue;
                }

                if (token.Equals("btime", StringComparison.OrdinalIgnoreCase) &&
                    index + 1 < parts.Length)
                {
                    result.BlackTimeMs = Math.Max(0, ParseInt(parts[++index]));
                    continue;
                }

                if (token.Equals("winc", StringComparison.OrdinalIgnoreCase) &&
                    index + 1 < parts.Length)
                {
                    result.WhiteIncrementMs = Math.Max(0, ParseInt(parts[++index]));
                    continue;
                }

                if (token.Equals("binc", StringComparison.OrdinalIgnoreCase) &&
                    index + 1 < parts.Length)
                {
                    result.BlackIncrementMs = Math.Max(0, ParseInt(parts[++index]));
                    continue;
                }

                if (token.Equals("movestogo", StringComparison.OrdinalIgnoreCase) &&
                    index + 1 < parts.Length)
                {
                    var parsed = ParseInt(parts[++index]);
                    result.MovesToGo = parsed > 0 ? parsed : null;
                    continue;
                }

                if (token.Equals("mate", StringComparison.OrdinalIgnoreCase) &&
                    index + 1 < parts.Length)
                {
                    index++;
                }
            }

            return result;
        }

        private static bool IsGoKeyword(string token)
        {
            return token.Equals("searchmoves", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("ponder", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("wtime", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("btime", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("winc", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("binc", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("movestogo", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("depth", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("nodes", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("mate", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("movetime", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("infinite", StringComparison.OrdinalIgnoreCase);
        }

        private char SideToMove()
        {
            var parts = _board.ToFen().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2 || parts[1].Length == 0)
            {
                return 'w';
            }

            return parts[1][0] == 'b' ? 'b' : 'w';
        }

        private int FullMovesPlayed()
        {
            var parts = _board.ToFen().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 6 || !int.TryParse(parts[5], out var fullMoveNumber))
            {
                return 0;
            }

            return Math.Max(0, fullMoveNumber - 1);
        }

        private void PlayMove(string moveText)
        {
            if (moveText.Equals("0000", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var requestedMove = UciMove.Parse(moveText.ToLowerInvariant());
            var legalMove = _board.GenerateLegalMoves()
                .FirstOrDefault(move =>
                    move.ToString().Equals(requestedMove.ToString(), StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(legalMove.From) ||
                string.IsNullOrWhiteSpace(legalMove.To))
            {
                throw new InvalidOperationException($"Illegal move in position command: {moveText}");
            }

            _board.Make(legalMove);
        }

        private static void WriteLine(string text)
        {
            Console.WriteLine(text);
            Console.Out.Flush();
        }

        private static void WriteError(string text)
        {
            Console.Error.WriteLine(text);
            Console.Error.Flush();
        }

        private sealed class GoOptions
        {
            public int? Depth { get; set; }
            public int? NodeCap { get; set; }
            public int? MoveTimeMs { get; set; }
            public int? WhiteTimeMs { get; set; }
            public int? BlackTimeMs { get; set; }
            public int WhiteIncrementMs { get; set; }
            public int BlackIncrementMs { get; set; }
            public int? MovesToGo { get; set; }
        }
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

    private static int ParseInt(string value)
    {
        return int.Parse(value, CultureInfo.InvariantCulture);
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  ChessGP.App uci <config.json> <checkpoint.json> [--depth N] [--node-cap N] [--max-depth N]");
        Console.Error.WriteLine("                  [--move-overhead-ms N] [--estimated-moves-to-go N] [--minimum-move-time-ms N]");
        Console.Error.WriteLine("                  [--draw-contempt-cp N] [--avoid-draw-when-ahead-cp N]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Example:");
        Console.Error.WriteLine("  ChessGP.App uci configs/stage2_real_001.json checkpoints/best.json --depth 4 --node-cap 500000 --max-depth 32");
    }
}
