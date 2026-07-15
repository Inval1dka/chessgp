using ChessGP.Core.Config;

namespace ChessGP.Core.Genome;

/// <summary>
/// Builds runtime genome schemas from application configuration.
/// </summary>
public static class GeneSchemaBuilder
{
    /// <summary>
    /// Creates a validated gene schema from configured genome options.
    /// </summary>
    public static GeneSchema FromConfig(GenomeOptions config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.genes is null || config.genes.Count == 0)
        {
            throw new ArgumentException(
                "GenomeOptions.genes must contain at least one gene.",
                nameof(config));
        }

        var genes = new List<GeneSpec>(capacity: config.genes.Count);

        foreach (var configuredGene in config.genes)
        {
            genes.Add(new GeneSpec(
                Name: configuredGene.name,
                BitWidth: configuredGene.bitWidth,
                Min: configuredGene.min,
                Max: configuredGene.max,
                ConstraintFlagNames: configuredGene.constraintFlags));
        }

        return new GeneSchema(genes);
    }
}
