namespace ChessGP.Core.Genome;

/// <summary>
/// Defines one gene in the genome and the numeric range it decodes to.
/// </summary>
public sealed record GeneSpec(
    string Name,
    int BitWidth,
    double Min,
    double Max,
    IReadOnlyList<string>? ConstraintFlagNames = null)
{
    /// <summary>
    /// Constraint flag names configured for this gene.
    /// </summary>
    public IReadOnlyList<string> ConstraintFlagsList => ConstraintFlagNames ?? Array.Empty<string>();

    /// <summary>
    /// Validates the gene definition.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Gene name must be non-empty.", nameof(Name));
        }

        if (BitWidth <= 0 || BitWidth > 63)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BitWidth),
                "BitWidth must be in [1..63].");
        }

        if (Max <= Min)
        {
            throw new ArgumentException($"Gene '{Name}' has invalid range: Max must be > Min.");
        }
    }

    /// <summary>
    /// Parses configured constraint flag names into a combined flag value.
    /// </summary>
    public ConstraintFlags ParsedFlags()
    {
        var flags = ConstraintFlags.None;

        foreach (var flagName in ConstraintFlagsList)
        {
            if (string.IsNullOrWhiteSpace(flagName))
            {
                continue;
            }

            if (!Enum.TryParse<ConstraintFlags>(flagName, ignoreCase: true, out var parsed))
            {
                var validFlags = string.Join(", ", Enum.GetNames(typeof(ConstraintFlags)));

                throw new ArgumentException(
                    $"Unknown constraint flag '{flagName}' in gene '{Name}'. Valid: {validFlags}");
            }

            flags |= parsed;
        }

        return flags;
    }
}
