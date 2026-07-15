using System;

namespace ChessGP.Core.Genome;

/// <summary>
/// Optional constraints that can be attached to genome parameters.
/// </summary>
[Flags]
public enum ConstraintFlags
{
    None = 0,

    MonotonicNonDecreasing = 1 << 0,
    MonotonicNonIncreasing = 1 << 1,
    NonNegative = 1 << 2
}
