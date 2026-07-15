using ChessGP.Core.Config;
using ChessGP.Core.Genome;

namespace ChessGP.Core.GA;

/// <summary>
/// Provides genetic algorithm operators for bitstring genomes.
/// </summary>
public static class GaEngine
{
    // Population initialization

    /// <summary>
    /// Creates a random population of bitstring genomes compatible with the supplied schema.
    /// </summary>
    public static List<Individual> CreateRandomPopulation(
        int populationSize,
        GeneSchema schema,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(rng);

        if (populationSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(populationSize));
        }

        var population = new List<Individual>(capacity: populationSize);

        for (var index = 0; index < populationSize; index++)
        {
            var genome = RandomGenome(schema.TotalBits, rng);
            population.Add(new Individual(genome, Fitness: 0));
        }

        return population;
    }

    private static string RandomGenome(int bitCount, Random rng)
    {
        var characters = new char[bitCount];

        for (var index = 0; index < bitCount; index++)
        {
            characters[index] = rng.Next(2) == 0 ? '0' : '1';
        }

        return new string(characters);
    }

    // Next generation

    /// <summary>
    /// Creates the next population using Stage 1 crossover and mutation rates.
    /// </summary>
    public static List<Individual> NextGeneration(
        IReadOnlyList<Individual> current,
        int populationSize,
        GeneSchema schema,
        Stage1Options stage1,
        GaOptions ga,
        Random rng)
    {
        return NextGenerationWithRates(
            current,
            populationSize,
            schema,
            stage1.crossoverRate,
            stage1.mutationRate,
            ga,
            rng);
    }

    /// <summary>
    /// Creates the next population using selection, elitism, crossover, and mutation.
    /// </summary>
    public static List<Individual> NextGenerationWithRates(
        IReadOnlyList<Individual> current,
        int populationSize,
        GeneSchema schema,
        double crossoverRate,
        double mutationRate,
        GaOptions ga,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(ga);
        ArgumentNullException.ThrowIfNull(rng);

        if (populationSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(populationSize));
        }

        if (current.Count == 0)
        {
            throw new ArgumentException("Current population is empty.", nameof(current));
        }

        foreach (var individual in current)
        {
            if (individual.GenomeBits.Length != schema.TotalBits)
            {
                throw new ArgumentException(
                    $"Genome length {individual.GenomeBits.Length} != schema.TotalBits {schema.TotalBits}.");
            }
        }

        var elitism = Math.Clamp(ga.elitism, 0, populationSize);
        var next = new List<Individual>(capacity: populationSize);

        foreach (var elite in current.OrderByDescending(individual => individual.Fitness).Take(elitism))
        {
            next.Add(new Individual(elite.GenomeBits, Fitness: 0));
        }

        crossoverRate = Math.Clamp(crossoverRate, 0, 1);
        mutationRate = Math.Clamp(mutationRate, 0, 1);

        while (next.Count < populationSize)
        {
            var firstParent = SelectParent(current, ga, rng);
            var secondParent = SelectParent(current, ga, rng);

            var childGenome = firstParent.GenomeBits;

            if (rng.NextDouble() < crossoverRate)
            {
                childGenome = ga.crossoverType switch
                {
                    "uniform-per-gene" => UniformPerGeneCrossover(
                        firstParent.GenomeBits,
                        secondParent.GenomeBits,
                        schema,
                        rng),

                    "uniform-per-bit" => UniformPerBitCrossover(
                        firstParent.GenomeBits,
                        secondParent.GenomeBits,
                        rng),

                    _ => throw new NotSupportedException($"Unsupported crossoverType: {ga.crossoverType}")
                };
            }

            if (rng.NextDouble() < mutationRate)
            {
                childGenome = BitFlipMutate(childGenome, ga.bitFlipRate, rng);
            }

            next.Add(new Individual(childGenome, Fitness: 0));
        }

        return next;
    }

    // Selection

    private static Individual SelectParent(IReadOnlyList<Individual> population, GaOptions ga, Random rng)
    {
        return ga.selectionType switch
        {
            "tournament" => TournamentSelect(population, ga.tournamentK, rng),
            _ => throw new NotSupportedException($"Unsupported selectionType: {ga.selectionType}")
        };
    }

    /// <summary>
    /// Selects one parent using tournament selection.
    /// </summary>
    public static Individual TournamentSelect(
        IReadOnlyList<Individual> population,
        int tournamentSize,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(rng);

        if (population.Count == 0)
        {
            throw new ArgumentException("Population is empty.", nameof(population));
        }

        tournamentSize = Math.Clamp(tournamentSize, 1, population.Count);

        var best = population[rng.Next(population.Count)];

        for (var index = 1; index < tournamentSize; index++)
        {
            var candidate = population[rng.Next(population.Count)];

            if (candidate.Fitness > best.Fitness)
            {
                best = candidate;
            }
        }

        return best;
    }

    // Crossover

    /// <summary>
    /// Creates a child genome by choosing whole genes from either parent.
    /// </summary>
    public static string UniformPerGeneCrossover(
        string firstParent,
        string secondParent,
        GeneSchema schema,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(firstParent);
        ArgumentNullException.ThrowIfNull(secondParent);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(rng);

        if (firstParent.Length != secondParent.Length)
        {
            throw new ArgumentException("Parents have different genome lengths.");
        }

        if (firstParent.Length != schema.TotalBits)
        {
            throw new ArgumentException("Parent genome length != schema.TotalBits.");
        }

        var child = new char[firstParent.Length];

        foreach (var (_, offset, width) in schema.Enumerate())
        {
            var source = rng.Next(2) == 0 ? firstParent : secondParent;

            for (var index = 0; index < width; index++)
            {
                child[offset + index] = source[offset + index];
            }
        }

        return new string(child);
    }

    /// <summary>
    /// Creates a child genome by choosing each bit independently from either parent.
    /// </summary>
    public static string UniformPerBitCrossover(
        string firstParent,
        string secondParent,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(firstParent);
        ArgumentNullException.ThrowIfNull(secondParent);
        ArgumentNullException.ThrowIfNull(rng);

        if (firstParent.Length != secondParent.Length)
        {
            throw new ArgumentException("Parents have different genome lengths.");
        }

        var child = new char[firstParent.Length];

        for (var index = 0; index < firstParent.Length; index++)
        {
            child[index] = rng.Next(2) == 0
                ? firstParent[index]
                : secondParent[index];
        }

        return new string(child);
    }

    // Mutation

    /// <summary>
    /// Mutates a genome by independently flipping each bit with the supplied probability.
    /// </summary>
    public static string BitFlipMutate(string genome, double bitFlipRate, Random rng)
    {
        ArgumentNullException.ThrowIfNull(genome);
        ArgumentNullException.ThrowIfNull(rng);

        if (bitFlipRate <= 0)
        {
            return genome;
        }

        bitFlipRate = Math.Min(bitFlipRate, 1);

        var characters = genome.ToCharArray();

        for (var index = 0; index < characters.Length; index++)
        {
            if (rng.NextDouble() < bitFlipRate)
            {
                characters[index] = characters[index] == '0' ? '1' : '0';
            }
        }

        return new string(characters);
    }
}
