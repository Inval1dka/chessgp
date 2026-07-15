using System.Collections.Generic;
using ChessGP.Core.Config;
using ChessGP.Core.Data;
using ChessGP.Core.Genome;
using ChessGP.Core.Training;
using Xunit;
using GenomeGeneSpec = ChessGP.Core.Genome.GeneSpec;

namespace ChessGP.Tests.Training;

public sealed class Stage1TrainerTests
{
    [Fact]
    public void Evaluate_ReturnsCorrectSquaredFitness_OnClearCapture()
    {
        const string fen = "7k/4r3/8/8/8/8/4Q3/K7 w - - 0 1";

        var batch = new List<TrainingSample>
        {
            new(fen, "e2e7", PlyIndex: 0),
            new(fen, "e2e7", PlyIndex: 1)
        };

        var schema = new GeneSchema(new[]
        {
            new GenomeGeneSpec("Dummy", 1, 0, 1, null)
        });

        var config = new Stage1Options(
            population: 1,
            crossoverRate: 0,
            mutationRate: 0,
            generations: 1,
            batchSize: 2,
            ply: 1);

        var trainer = new Stage1Trainer();

        var result = trainer.Evaluate("0", schema, batch, config);

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Correct);
        Assert.Equal(4, result.Fitness);
        Assert.True(result.NodesEvaluated > 0);
    }
}
