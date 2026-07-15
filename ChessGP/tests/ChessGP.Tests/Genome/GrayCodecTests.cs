using System;
using ChessGP.Core.Genome;
using Xunit;

namespace ChessGP.Tests.Genome;

public sealed class GrayCodecTests
{
    [Fact]
    public void EncodeDecode_RoundTrips_Endpoints_And_Midpoint()
    {
        var spec = new GeneSpec(
            "Test",
            BitWidth: 6,
            Min: -100,
            Max: 100,
            ConstraintFlagNames: null);

        foreach (var value in new[] { -100.0, 0.0, 100.0, 37.0, -42.0 })
        {
            var bits = GrayCodec.Encode(value, spec);
            var decoded = GrayCodec.Decode(bits, spec);

            var steps = (1 << spec.BitWidth) - 1;
            var stepSize = (spec.Max - spec.Min) / steps;
            var clampedValue = Math.Clamp(value, spec.Min, spec.Max);

            Assert.True(
                Math.Abs(decoded - clampedValue) <= stepSize + 1e-9,
                $"v={value} decoded={decoded} step={stepSize}");
        }
    }

    [Fact]
    public void GrayCode_Adjacent_Integers_Differ_By_One_Bit()
    {
        const int bitWidth = 8;
        var maxValue = ((ulong)1 << bitWidth) - 1;

        for (ulong value = 0; value < maxValue; value++)
        {
            var firstGray = GrayCodec.BinaryToGray(value);
            var secondGray = GrayCodec.BinaryToGray(value + 1);

            var hammingDistance = GrayCodec.HammingDistance(firstGray, secondGray);

            Assert.Equal(1, hammingDistance);
        }
    }

    [Fact]
    public void GeneSchema_Computes_TotalBits_And_Offsets()
    {
        var schema = new GeneSchema(new[]
        {
            new GeneSpec("A", 3, 0, 1),
            new GeneSpec("B", 5, -10, 10),
            new GeneSpec("C", 6, 50, 150)
        });

        Assert.Equal(3 + 5 + 6, schema.TotalBits);

        var (aOffset, aWidth) = schema.GetGeneLayout("A");
        var (bOffset, bWidth) = schema.GetGeneLayout("B");
        var (cOffset, cWidth) = schema.GetGeneLayout("C");

        Assert.Equal((0, 3), (aOffset, aWidth));
        Assert.Equal((3, 5), (bOffset, bWidth));
        Assert.Equal((8, 6), (cOffset, cWidth));
    }
}
