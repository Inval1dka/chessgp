using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessGP.Core.Genome;

/// <summary>
/// Describes how a genome bitstring is partitioned into named genes.
/// </summary>
public sealed class GeneSchema
{
    private readonly List<GeneSpec> _genes;
    private readonly Dictionary<string, int> _indexByName;

    public GeneSchema(IReadOnlyList<GeneSpec> genes)
    {
        ArgumentNullException.ThrowIfNull(genes);

        if (genes.Count == 0)
        {
            throw new ArgumentException("Schema must contain at least one gene.", nameof(genes));
        }

        _genes = genes.ToList();
        _indexByName = new Dictionary<string, int>(StringComparer.Ordinal);

        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < _genes.Count; index++)
        {
            var gene = _genes[index];

            gene.Validate();

            if (!seenNames.Add(gene.Name))
            {
                throw new ArgumentException($"Duplicate gene name '{gene.Name}' in schema.");
            }

            _indexByName[gene.Name] = index;
        }

        TotalBits = _genes.Sum(gene => gene.BitWidth);
    }

    /// <summary>
    /// Ordered list of genes in the schema.
    /// </summary>
    public IReadOnlyList<GeneSpec> Genes => _genes;

    /// <summary>
    /// Total number of bits required by the schema.
    /// </summary>
    public int TotalBits { get; }

    /// <summary>
    /// Returns the index of a gene by name.
    /// </summary>
    public int GetGeneIndex(string name)
    {
        if (!_indexByName.TryGetValue(name, out var index))
        {
            throw new KeyNotFoundException($"Gene '{name}' not found.");
        }

        return index;
    }

    /// <summary>
    /// Returns the bit offset and width of a gene by index.
    /// </summary>
    public (int Offset, int Width) GetGeneLayout(int geneIndex)
    {
        if (geneIndex < 0 || geneIndex >= _genes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(geneIndex));
        }

        var offset = 0;

        for (var index = 0; index < geneIndex; index++)
        {
            offset += _genes[index].BitWidth;
        }

        return (offset, _genes[geneIndex].BitWidth);
    }

    /// <summary>
    /// Returns the bit offset and width of a gene by name.
    /// </summary>
    public (int Offset, int Width) GetGeneLayout(string name)
    {
        return GetGeneLayout(GetGeneIndex(name));
    }

    /// <summary>
    /// Enumerates genes together with their bit offsets and widths.
    /// </summary>
    public IEnumerable<(GeneSpec Gene, int Offset, int Width)> Enumerate()
    {
        var offset = 0;

        foreach (var gene in _genes)
        {
            yield return (gene, offset, gene.BitWidth);
            offset += gene.BitWidth;
        }
    }
}
