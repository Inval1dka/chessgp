using ChessGP.Core.Config;
using ChessGP.Core.Data;
using ChessGP.Core.Engine;
using ChessGP.Core.Eval;
using ChessGP.Core.Genome;

namespace ChessGP.Core.Training;

/// <summary>
/// Result of evaluating one genome on a Stage 1 training batch.
/// </summary>
public readonly record struct Stage1EvalResult(
    long Fitness,
    int Correct,
    int Total,
    long NodesEvaluated);

/// <summary>
/// Evaluates genomes against labeled training samples for Stage 1.
/// </summary>
public sealed class Stage1Trainer
{
    private readonly EngineCore _engine;
    private readonly Func<IBoardAdapter> _boardFactory;

    public Stage1Trainer(EngineCore? engine = null, Func<IBoardAdapter>? boardFactory = null)
    {
        _engine = engine ?? new EngineCore();
        _boardFactory = boardFactory ?? (() => new ChessDotNetBoardAdapter());
    }

    /// <summary>
    /// Evaluates a genome by comparing one-ply engine choices with labeled target moves.
    /// </summary>
    public Stage1EvalResult Evaluate(
        string genomeBits,
        GeneSchema schema,
        IReadOnlyList<TrainingSample> batch,
        Stage1Options config)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(batch);

        if (config.ply != 1)
        {
            throw new ArgumentException(
                "Stage1Options.ply must be 1 for Stage 1 training.",
                nameof(config));
        }

        var parameters = GenomeToParamsDecoder.Decode(genomeBits, schema);
        var evaluator = new ParametricEvaluator(parameters);

        var correct = 0;
        var total = 0;
        long nodesEvaluated = 0;

        var board = _boardFactory();

        foreach (var sample in batch)
        {
            var targetMove = UciMove.Parse(sample.MoveUci);

            board.LoadFen(sample.Fen);

            var result = _engine.SelectBestMove1Ply(board, evaluator);
            nodesEvaluated += result.Nodes;

            total++;

            if (SameMove(result.Move, targetMove))
            {
                correct++;
            }
        }

        var fitness = (long)correct * correct;

        return new Stage1EvalResult(
            Fitness: fitness,
            Correct: correct,
            Total: total,
            NodesEvaluated: nodesEvaluated);
    }

    private static bool SameMove(UciMove first, UciMove second)
    {
        return string.Equals(first.From, second.From, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(first.To, second.To, StringComparison.OrdinalIgnoreCase) &&
               char.ToLowerInvariant(first.Promotion) == char.ToLowerInvariant(second.Promotion);
    }
}
