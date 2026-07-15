using System.Globalization;
using System.Text.Json;
using ChessGP.Core.Config;
using ChessGP.Core.Training;

static string[] ExpandArenaUciSettingsIfPresent(string[] originalArgs)
{
    if (originalArgs.Length != 0)
    {
        return originalArgs;
    }

    var settingsPath = Path.Combine(AppContext.BaseDirectory, "uci_settings.json");
    if (!File.Exists(settingsPath))
    {
        return originalArgs;
    }

    using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
    var root = document.RootElement;

    var configPath = ReadRequiredString(root, settingsPath, "configPath");
    var checkpointPath = ReadRequiredString(root, settingsPath, "checkpointPath");

    var expandedArgs = new List<string>
    {
        "uci",
        configPath,
        checkpointPath
    };

    AddOptionalIntSetting(root, expandedArgs, "depth", "--depth");
    AddOptionalIntSetting(root, expandedArgs, "nodeCap", "--node-cap");
    AddOptionalIntSetting(root, expandedArgs, "maxSearchDepth", "--max-depth");
    AddOptionalIntSetting(root, expandedArgs, "moveOverheadMs", "--move-overhead-ms");
    AddOptionalIntSetting(root, expandedArgs, "estimatedMovesToGo", "--estimated-moves-to-go");
    AddOptionalIntSetting(root, expandedArgs, "minimumMoveTimeMs", "--minimum-move-time-ms");
    AddOptionalIntSetting(root, expandedArgs, "drawContemptCp", "--draw-contempt-cp");
    AddOptionalIntSetting(root, expandedArgs, "avoidDrawWhenAheadCp", "--avoid-draw-when-ahead-cp");

    return expandedArgs.ToArray();
}

static string ReadRequiredString(JsonElement root, string settingsPath, string propertyName)
{
    if (!root.TryGetProperty(propertyName, out var property) ||
        property.ValueKind != JsonValueKind.String)
    {
        throw new InvalidOperationException(
            $"Missing required UCI setting '{propertyName}' in {settingsPath}.");
    }

    var value = property.GetString();
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException(
            $"UCI setting '{propertyName}' cannot be empty in {settingsPath}.");
    }

    return value;
}

static void AddOptionalIntSetting(
    JsonElement root,
    List<string> expandedArgs,
    string propertyName,
    string argumentName)
{
    if (!root.TryGetProperty(propertyName, out var property) ||
        !property.TryGetInt32(out var value))
    {
        return;
    }

    expandedArgs.Add(argumentName);
    expandedArgs.Add(value.ToString(CultureInfo.InvariantCulture));
}

static string GetConfigPath(string[] args)
{
    if (args.Length == 0)
    {
        return Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    if (IsCommand(args[0]))
    {
        return args.Length >= 2
            ? args[1]
            : Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    return args[0];
}

static bool IsCommand(string value)
{
    return value.Equals("run-stage1", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("run-stage2", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("play", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("uci", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("eval-checkpoint", StringComparison.OrdinalIgnoreCase);
}

static void PrintConfigSummary(AppOptions options)
{
    Console.WriteLine("=== ChessGP ===");
    Console.WriteLine($"Data:       {options.Data.path} ({options.Data.format})");
    Console.WriteLine($"Stage1:     pop={options.Stage1.population} gens={options.Stage1.generations} batch={options.Stage1.batchSize}");
    Console.WriteLine($"Stage2:     pop={options.Stage2.population} gens={options.Stage2.generations} depth={options.Stage2.searchDepth} mode={options.Stage2.searchMode}");
    Console.WriteLine($"GA:         sel={options.GA.selectionType} k={options.GA.tournamentK} mut={options.GA.bitFlipRate} xover={options.GA.crossoverType} elitism={options.GA.elitism}");
    Console.WriteLine($"Genome:     schemaId={options.Genome.schemaId} genes={options.Genome.genes.Count}");
    Console.WriteLine($"Rng:        seed={options.Rng.seed}");
    Console.WriteLine($"Output:     ckptDir={options.Output.checkpointsDir} metrics={options.Output.metricsFile}");
    Console.WriteLine("================");
    Console.WriteLine("Config loaded successfully.");
}

static void PrintPlayUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  ChessGP.App play <config.json> <checkpoint.json> [--human white|black] [--depth N] [--node-cap N] [--fen FEN]");
}

static void PrintUciUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  ChessGP.App uci <config.json> <checkpoint.json> [--depth N] [--node-cap N]");
    Console.Error.WriteLine("                   [--max-depth N] [--move-overhead-ms N] [--estimated-moves-to-go N]");
    Console.Error.WriteLine("                   [--minimum-move-time-ms N] [--draw-contempt-cp N] [--avoid-draw-when-ahead-cp N]");
}

static void PrintEvaluateCheckpointUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  ChessGP.App eval-checkpoint <config.json> <checkpoint.json> [--test-path PATH] [--max N] [--depth N] [--node-cap N]");
}

try
{
    var expandedArgs = ExpandArenaUciSettingsIfPresent(args);
    var command = expandedArgs.Length > 0 ? expandedArgs[0] : string.Empty;
    var configPath = GetConfigPath(expandedArgs);

    var options = ConfigLoader.Load(configPath);
    var configBaseDir = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? AppContext.BaseDirectory;

    if (command.Equals("run-stage1", StringComparison.OrdinalIgnoreCase))
    {
        Stage1Driver.Run(options, configBaseDir);
        return;
    }

    if (command.Equals("run-stage2", StringComparison.OrdinalIgnoreCase))
    {
        Stage2Driver.Run(options, configBaseDir);
        return;
    }

    if (command.Equals("play", StringComparison.OrdinalIgnoreCase))
    {
        if (expandedArgs.Length < 3)
        {
            PrintPlayUsage();
            return;
        }

        InteractivePlayCommand.Run(options, configBaseDir, expandedArgs.Skip(2).ToArray());
        return;
    }

    if (command.Equals("uci", StringComparison.OrdinalIgnoreCase))
    {
        if (expandedArgs.Length < 3)
        {
            PrintUciUsage();
            return;
        }

        UciCommand.Run(options, configBaseDir, expandedArgs.Skip(2).ToArray());
        return;
    }

    if (command.Equals("eval-checkpoint", StringComparison.OrdinalIgnoreCase))
    {
        if (expandedArgs.Length < 3)
        {
            PrintEvaluateCheckpointUsage();
            return;
        }

        EvaluateCheckpointCommand.Run(options, configBaseDir, expandedArgs.Skip(2).ToArray());
        return;
    }

    PrintConfigSummary(options);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.ExitCode = 1;
}
