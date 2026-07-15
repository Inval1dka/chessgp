using System.Collections.Generic;
using ChessGP.Core.Config;
using ChessGP.Core.Genome;
using Xunit;
using ConfigGeneSpec = ChessGP.Core.Config.GeneSpec;

namespace ChessGP.Tests.Genome;

public sealed class GeneSchemaBuilderTests
{
    [Fact]
    public void FromConfig_BuildsSchema_WithCorrectGenesAndBitTotal()
    {
        var config = new GenomeOptions(
            schemaId: "test-schema",
            genes: new List<ConfigGeneSpec>
            {
                new("G1", 3, 0, 7, null),
                new("G2", 5, 0, 31, null)
            });

        var schema = GeneSchemaBuilder.FromConfig(config);

        Assert.Equal(2, schema.Genes.Count);
        Assert.Equal(8, schema.TotalBits);

        Assert.Equal("G1", schema.Genes[0].Name);
        Assert.Equal(3, schema.Genes[0].BitWidth);

        Assert.Equal("G2", schema.Genes[1].Name);
        Assert.Equal(5, schema.Genes[1].BitWidth);
    }
}
