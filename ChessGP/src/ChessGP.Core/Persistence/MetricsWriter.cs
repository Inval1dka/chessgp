using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ChessGP.Core.Persistence;

/// <summary>
/// Appends generation metrics to a CSV file.
/// </summary>
public sealed class MetricsWriter
{
    private readonly string _csvPath;

    public MetricsWriter(string csvPath)
    {
        _csvPath = csvPath ?? throw new ArgumentNullException(nameof(csvPath));

        var directory = Path.GetDirectoryName(_csvPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Appends one generation metrics row and writes the header if the file does not exist yet.
    /// </summary>
    public void Append(GenerationMetricsRow row)
    {
        var needsHeader = !File.Exists(_csvPath);

        using var stream = new FileStream(_csvPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (needsHeader)
        {
            writer.WriteLine(GenerationMetricsRow.Header);
        }

        writer.WriteLine(row.ToCsvLine());
    }
}

/// <summary>
/// Metrics recorded for one evolutionary generation.
/// </summary>
public readonly record struct GenerationMetricsRow(
    string RunId,
    string Stage,
    int Generation,
    double BestFitness,
    double MeanFitness,
    double MedianFitness,
    long NodesEvaluated,
    int Seed,
    long ElapsedMs,
    string? BestCheckpointPath,
    double AverageCompletedDepth = 0,
    double AverageSearchMs = 0,
    int TimeLosses = 0)
{
    /// <summary>
    /// CSV header matching <see cref="ToCsvLine"/>.
    /// </summary>
    public static string Header =>
        "runId,stage,generation,bestFitness,meanFitness,medianFitness,nodesEvaluated,seed,elapsedMs,bestCheckpointPath,averageCompletedDepth,averageSearchMs,timeLosses";

    /// <summary>
    /// Serializes the row as one CSV line.
    /// </summary>
    public string ToCsvLine()
    {
        var invariantCulture = CultureInfo.InvariantCulture;

        return string.Join(
            ",",
            Csv(RunId),
            Csv(Stage),
            Generation.ToString(invariantCulture),
            BestFitness.ToString(invariantCulture),
            MeanFitness.ToString(invariantCulture),
            MedianFitness.ToString(invariantCulture),
            NodesEvaluated.ToString(invariantCulture),
            Seed.ToString(invariantCulture),
            ElapsedMs.ToString(invariantCulture),
            Csv(BestCheckpointPath ?? ""),
            AverageCompletedDepth.ToString(invariantCulture),
            AverageSearchMs.ToString(invariantCulture),
            TimeLosses.ToString(invariantCulture));
    }

    private static string Csv(string value)
    {
        if (value.Contains(',') ||
            value.Contains('"') ||
            value.Contains('\n') ||
            value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
