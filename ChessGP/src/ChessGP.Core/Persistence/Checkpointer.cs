using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ChessGP.Core.Persistence;

/// <summary>
/// Saves and loads genome checkpoints as JSON files.
/// </summary>
public sealed class Checkpointer
{
    private readonly string _checkpointsDir;
    private readonly JsonSerializerOptions _jsonOptions;

    public Checkpointer(string checkpointsDir)
    {
        _checkpointsDir = checkpointsDir ?? throw new ArgumentNullException(nameof(checkpointsDir));
        Directory.CreateDirectory(_checkpointsDir);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    /// <summary>
    /// Saves the best genome for a generation and returns the created checkpoint path.
    /// </summary>
    public string SaveBest(
        string runId,
        string stage,
        int generation,
        string schemaId,
        string genomeBits)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new ArgumentException("runId required.", nameof(runId));
        }

        if (string.IsNullOrWhiteSpace(stage))
        {
            throw new ArgumentException("stage required.", nameof(stage));
        }

        if (string.IsNullOrWhiteSpace(schemaId))
        {
            throw new ArgumentException("schemaId required.", nameof(schemaId));
        }

        ArgumentNullException.ThrowIfNull(genomeBits);

        var checkpoint = new BestGenomeCheckpoint(
            RunId: runId,
            Stage: stage,
            Generation: generation,
            SchemaId: schemaId,
            GenomeBits: genomeBits,
            TimestampUtc: DateTimeOffset.UtcNow);

        var safeStage = stage
            .Replace(Path.DirectorySeparatorChar, '_')
            .Replace(Path.AltDirectorySeparatorChar, '_');

        var fileName = $"{runId}_{safeStage}_gen{generation:D4}_best.json";
        var path = Path.Combine(_checkpointsDir, fileName);

        var json = JsonSerializer.Serialize(checkpoint, _jsonOptions);
        File.WriteAllText(path, json);

        return path;
    }

    /// <summary>
    /// Loads the newest checkpoint, optionally filtered by run id and stage.
    /// </summary>
    public BestGenomeCheckpoint? LoadLatest(string? runId = null, string? stage = null)
    {
        if (!Directory.Exists(_checkpointsDir))
        {
            return null;
        }

        var files = Directory.EnumerateFiles(
            _checkpointsDir,
            "*.json",
            SearchOption.TopDirectoryOnly);

        if (!string.IsNullOrWhiteSpace(runId))
        {
            files = files.Where(file =>
                Path.GetFileName(file).StartsWith(runId + "_", StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(stage))
        {
            var stageToken = "_" + stage + "_";

            files = files.Where(file =>
                Path.GetFileName(file).Contains(stageToken, StringComparison.OrdinalIgnoreCase));
        }

        var latest = files
            .Select(file => new FileInfo(file))
            .OrderByDescending(fileInfo => fileInfo.LastWriteTimeUtc)
            .FirstOrDefault();

        if (latest is null)
        {
            return null;
        }

        var json = File.ReadAllText(latest.FullName);
        return JsonSerializer.Deserialize<BestGenomeCheckpoint>(json);
    }
}
