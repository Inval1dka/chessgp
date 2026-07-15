using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChessGP.Core.Config;

/// <summary>
/// Loads and validates application configuration from a JSON file.
/// </summary>
public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Loads an application configuration file and validates the most important option constraints.
    /// </summary>
    /// <param name="path">Path to the JSON configuration file.</param>
    public static AppOptions Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Config file not found: {path}");
        }

        var json = File.ReadAllText(path);
        var options = JsonSerializer.Deserialize<AppOptions>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize AppOptions.");

        Validate(options);
        return options;
    }

    private static void Validate(AppOptions options)
    {
        var searchMode = options.Stage2.searchMode ?? "FixedDepth";

        var isTimePerMove =
            searchMode.Equals("TimePerMove", StringComparison.OrdinalIgnoreCase) ||
            searchMode.Equals("FixedTimePerMove", StringComparison.OrdinalIgnoreCase);

        var isTimePerGame =
            searchMode.Equals("TimePerGame", StringComparison.OrdinalIgnoreCase);

        if (isTimePerMove && options.Stage2.timePerMoveMs <= 0)
        {
            throw new ArgumentException("Stage2.timePerMoveMs must be > 0 when searchMode=TimePerMove.");
        }

        if (isTimePerGame && options.Stage2.initialTimeMs <= 0)
        {
            throw new ArgumentException("Stage2.initialTimeMs must be > 0 when searchMode=TimePerGame.");
        }

        if (options.Stage2.maxSearchDepth < 0)
        {
            throw new ArgumentException("Stage2.maxSearchDepth must be >= 0.");
        }

        if (options.Stage2.incrementMs < 0)
        {
            throw new ArgumentException("Stage2.incrementMs must be >= 0.");
        }

        if (options.Stage2.moveOverheadMs < 0)
        {
            throw new ArgumentException("Stage2.moveOverheadMs must be >= 0.");
        }

        if (options.Stage2.estimatedMovesToGo < 0)
        {
            throw new ArgumentException("Stage2.estimatedMovesToGo must be >= 0.");
        }

        if (options.Stage2.drawContemptCp < 0)
        {
            throw new ArgumentException("Stage2.drawContemptCp must be >= 0.");
        }

        if (options.Stage2.avoidDrawWhenAheadCp < 0)
        {
            throw new ArgumentException("Stage2.avoidDrawWhenAheadCp must be >= 0.");
        }

        if (options.GA.elitism < 0)
        {
            throw new ArgumentException("GA.elitism must be >= 0.");
        }

        if (options.Stage1.population < 2 || options.Stage2.population < 2)
        {
            throw new ArgumentException("Population must be >= 2 for both stages.");
        }
    }
}
