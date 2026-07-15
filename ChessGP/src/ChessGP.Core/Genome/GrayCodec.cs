namespace ChessGP.Core.Genome;

/// <summary>
/// Encodes and decodes gene values using Gray code.
/// </summary>
/// <remarks>
/// Bits are interpreted most-significant-bit first within each gene.
/// </remarks>
public static class GrayCodec
{
    /// <summary>
    /// Encodes a numeric value into Gray-coded bits according to a gene specification.
    /// </summary>
    public static bool[] Encode(double value, GeneSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        spec.Validate();

        var maxInteger = MaxInt(spec.BitWidth);
        var clampedValue = Math.Clamp(value, spec.Min, spec.Max);
        var normalized = (clampedValue - spec.Min) / (spec.Max - spec.Min);

        var integerValue = (ulong)Math.Round(
            normalized * maxInteger,
            MidpointRounding.AwayFromZero);

        if (integerValue > maxInteger)
        {
            integerValue = maxInteger;
        }

        var grayValue = BinaryToGray(integerValue);

        return UIntToBits(grayValue, spec.BitWidth);
    }

    /// <summary>
    /// Decodes Gray-coded bits into a numeric value according to a gene specification.
    /// </summary>
    public static double Decode(ReadOnlySpan<bool> bits, GeneSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        spec.Validate();

        if (bits.Length != spec.BitWidth)
        {
            throw new ArgumentException(
                $"Expected {spec.BitWidth} bits for gene '{spec.Name}', got {bits.Length}.");
        }

        var grayValue = BitsToUInt(bits);
        var binaryValue = GrayToBinary(grayValue);

        var maxInteger = MaxInt(spec.BitWidth);
        var normalized = (double)binaryValue / maxInteger;

        return spec.Min + normalized * (spec.Max - spec.Min);
    }

    /// <summary>
    /// Converts a binary integer to Gray code.
    /// </summary>
    public static ulong BinaryToGray(ulong binary)
    {
        return binary ^ (binary >> 1);
    }

    /// <summary>
    /// Converts a Gray-coded integer back to binary.
    /// </summary>
    public static ulong GrayToBinary(ulong gray)
    {
        ulong binary = 0;

        for (; gray != 0; gray >>= 1)
        {
            binary ^= gray;
        }

        return binary;
    }

    /// <summary>
    /// Counts differing bits between two unsigned integers.
    /// </summary>
    public static int HammingDistance(ulong first, ulong second)
    {
        var difference = first ^ second;
        var count = 0;

        while (difference != 0)
        {
            difference &= difference - 1;
            count++;
        }

        return count;
    }

    private static ulong MaxInt(int bitWidth)
    {
        return ((ulong)1 << bitWidth) - 1;
    }

    private static bool[] UIntToBits(ulong value, int bitWidth)
    {
        var bits = new bool[bitWidth];

        for (var index = 0; index < bitWidth; index++)
        {
            var shift = bitWidth - 1 - index;
            bits[index] = ((value >> shift) & 1UL) == 1UL;
        }

        return bits;
    }

    private static ulong BitsToUInt(ReadOnlySpan<bool> bits)
    {
        ulong value = 0;

        for (var index = 0; index < bits.Length; index++)
        {
            value <<= 1;

            if (bits[index])
            {
                value |= 1UL;
            }
        }

        return value;
    }
}
