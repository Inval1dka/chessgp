using System;
using ChessGP.Core.Config;
using ChessGP.Core.GA;
using ChessGP.Core.Genome;
using Xunit;
using GeneSpec = ChessGP.Core.Genome.GeneSpec;

namespace ChessGP.Tests.GA;

public sealed class GaEngineTests
{
    [Fact]
    public void BitFlipMutate_FlipsAllBits_WhenRateIsOne()
    {
        var rng = new Random(1);

        var mutated = GaEngine.BitFlipMutate("0101", bitFlipRate: 1.0, rng);

        Assert.Equal("1010", mutated);
    }

    [Fact]
    public void UniformPerGeneCrossover_CopiesWholeGeneSegments()
    {
        var schema = new GeneSchema(new[]
        {
            new GeneSpec("G1", 2, 0, 3, null),
            new GeneSpec("G2", 2, 0, 3, null)
        });

        var rng = new Random(123);

        const string firstParent = "0000";
        const string secondParent = "1111";

        var child = GaEngine.UniformPerGeneCrossover(firstParent, secondParent, schema, rng);

        var firstSegment = child.Substring(0, 2);
        var secondSegment = child.Substring(2, 2);

        Assert.True(firstSegment is "00" or "11");
        Assert.True(secondSegment is "00" or "11");
    }

    [Fact]
    public void NextGeneration_RespectsElitism()
    {
        var schema = new GeneSchema(new[]
        {
            new GeneSpec("G1", 4, 0, 15, null)
        });

        var rng = new Random(42);

        var current = new[]
        {
            new Individual("0000", 1),
            new Individual("1111", 10),
            new Individual("0011", 3)
        };

        var stage1 = new Stage1Options(
            population: 3,
            crossoverRate: 0.0,
            mutationRate: 0.0,
            generations: 1,
            batchSize: 1,
            ply: 1);

        var ga = new GaOptions(
            selectionType: "tournament",
            tournamentK: 2,
            bitFlipRate: 0.0,
            crossoverType: "uniform-per-gene",
            elitism: 1);

        var next = GaEngine.NextGeneration(current, 3, schema, stage1, ga, rng);

        Assert.Contains(next, individual => individual.GenomeBits == "1111");
        Assert.Equal(3, next.Count);
        Assert.All(next, individual => Assert.Equal(0, individual.Fitness));
    }
}
