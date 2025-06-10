using CreateRoadmapADO.Models;
using Microsoft.Extensions.Logging;

namespace CreateRoadmapADO.Services;

/// <summary>
/// Enhanced command line argument parser with comprehensive validation and error handling
/// </summary>
public class CommandLineParser
{
    private readonly ILogger<CommandLineParser> _logger;

    public CommandLineParser(ILogger<CommandLineParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Parses command line arguments with comprehensive validation
    /// </summary>
    public ParseResult Parse(string[] args)
    {
        try
        {
            var options = new EnhancedCommandLineOptions();
            var parseErrors = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                try
                {
                    switch (args[i].ToLowerInvariant())
                    {
                        case "--area-path" or "-a":
                            options.AreaPath = GetRequiredValue(args, i, "area-path");
                            i++; // Skip the value
                            break;

                        case "--limit" or "-l":
                            var limitValue = GetRequiredValue(args, i, "limit");
                            if (int.TryParse(limitValue, out var limit))
                            {
                                options.Limit = limit;
                            }
                            else
                            {
                                parseErrors.Add($"Invalid limit value '{limitValue}'. Must be a number.");
                            }
                            i++; // Skip the value
                            break;

                        case "--output" or "-o":
                            var outputValue = GetRequiredValue(args, i, "output");
                            if (Enum.TryParse<OutputFormat>(outputValue, true, out var format))
                            {
                                options.OutputFormat = format;
                            }
                            else
                            {
                                parseErrors.Add($"Invalid output format '{outputValue}'. Valid options: {GetValidOutputFormats()}");
                            }
                            i++; // Skip the value
                            break;

                        case "--file" or "-f":
                            options.OutputFile = GetRequiredValue(args, i, "file");
                            i++; // Skip the value
                            break;

                        case "--output-dir" or "-d":
                            options.OutputDirectory = GetRequiredValue(args, i, "output-dir");
                            i++; // Skip the value
                            break;

                        case "--config" or "-c":
                            options.ConfigFile = GetRequiredValue(args, i, "config");
                            i++; // Skip the value
                            break;

                        case "--timeout" or "-t":
                            var timeoutValue = GetRequiredValue(args, i, "timeout");
                            if (int.TryParse(timeoutValue, out var timeout))
                            {
                                options.TimeoutSeconds = timeout;
                            }
                            else
                            {
                                parseErrors.Add($"Invalid timeout value '{timeoutValue}'. Must be a number.");
                            }
                            i++; // Skip the value
                            break;

                        case "--from-date":
                            var fromDateValue = GetRequiredValue(args, i, "from-date");
                            if (DateTime.TryParse(fromDateValue, out var fromDate))
                            {
                                options.FromDate = fromDate;
                            }
                            else
                            {
                                parseErrors.Add($"Invalid from-date value '{fromDateValue}'. Use format: yyyy-MM-dd");
                            }
                            i++; // Skip the value
                            break;

                        case "--to-date":
                            var toDateValue = GetRequiredValue(args, i, "to-date");
                            if (DateTime.TryParse(toDateValue, out var toDate))
                            {
                                options.ToDate = toDate;
                            }
                            else
                            {
                                parseErrors.Add($"Invalid to-date value '{toDateValue}'. Use format: yyyy-MM-dd");
                            }
                            i++; // Skip the value
                            break;

                        case "--work-item-types":
                            var typesValue = GetRequiredValue(args, i, "work-item-types");
                            options.WorkItemTypes = typesValue.Split(',', StringSplitOptions.RemoveEmptyEntries);
                            i++; // Skip the value
                            break;

                        case "--work-item-states":
                            var statesValue = GetRequiredValue(args, i, "work-item-states");
                            options.WorkItemStates = statesValue.Split(',', StringSplitOptions.RemoveEmptyEntries);
                            i++; // Skip the value
                            break;

                        case "--log-level":
                            var logLevelValue = GetRequiredValue(args, i, "log-level");
                            if (Enum.TryParse<LogVerbosity>(logLevelValue, true, out var logLevel))
                            {
                                options.LogLevel = logLevel;
                            }
                            else
                            {
                                parseErrors.Add($"Invalid log level '{logLevelValue}'. Valid options: {GetValidLogLevels()}");
                            }
                            i++; // Skip the value
                            break;

                        // Boolean flags
                        case "--hygiene":
                            options.RunHygieneChecks = true;
                            break;

                        case "--hygiene-only":
                            options.HygieneChecksOnly = true;
                            options.RunHygieneChecks = true;
                            break;

                        case "--dry-run":
                            options.DryRun = true;
                            break;

                        case "--summary":
                            options.OutputFormat = OutputFormat.Summary;
                            break;

                        case "--verbose" or "-v":
                            options.LogLevel = LogVerbosity.Debug;
                            break;

                        case "--quiet" or "-q":
                            options.LogLevel = LogVerbosity.Error;
                            break;

                        case "--help" or "-h":
                            return ParseResult.Help();

                        default:
                            parseErrors.Add($"Unknown parameter: {args[i]}");
                            break;
                    }
                }
                catch (ArgumentException ex)
                {
                    parseErrors.Add(ex.Message);
                }
            }

            // If there were parse errors, return them immediately
            if (parseErrors.Any())
            {
                return ParseResult.Error(parseErrors, options);
            }            // Validate the parsed options
            var validationResult = options.Validate();
            if (!validationResult.IsValid)
            {
                var validationErrors = validationResult.Errors.Select(e => e.ErrorMessage ?? "Unknown validation error").ToList();
                return ParseResult.Error(validationErrors, options);
            }

            _logger.LogDebug("Successfully parsed command line arguments");
            return ParseResult.Success(options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing command line arguments");
            return ParseResult.Error(new[] { $"Unexpected error: {ex.Message}" }, new EnhancedCommandLineOptions());
        }
    }

    private static string GetRequiredValue(string[] args, int currentIndex, string parameterName)
    {
        if (currentIndex + 1 >= args.Length)
        {
            throw new ArgumentException($"Parameter --{parameterName} requires a value");
        }

        var value = args[currentIndex + 1];
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("-"))
        {
            throw new ArgumentException($"Parameter --{parameterName} requires a valid value");
        }

        return value;
    }

    private static string GetValidOutputFormats()
    {
        return string.Join(", ", Enum.GetNames<OutputFormat>().Select(n => n.ToLowerInvariant()));
    }

    private static string GetValidLogLevels()
    {
        return string.Join(", ", Enum.GetNames<LogVerbosity>().Select(n => n.ToLowerInvariant()));
    }

    /// <summary>
    /// Displays comprehensive help information
    /// </summary>
    public static void ShowHelp()
    {
        Console.WriteLine("CreateRoadmapADO - Generate roadmaps from Azure DevOps Feature work items");
        Console.WriteLine();
        Console.WriteLine("Usage: CreateRoadmapADO --area-path <path> [options]");
        Console.WriteLine();
        
        Console.WriteLine("Required Parameters:");
        Console.WriteLine("  -a, --area-path <path>       Azure DevOps area path to filter work items");
        Console.WriteLine("                               Example: \"SPOOL\\\\Resource Provider\"");
        Console.WriteLine();
        
        Console.WriteLine("Output Options:");
        Console.WriteLine("  -o, --output <format>        Output format (default: console)");
        Console.WriteLine("                               Options: console, json, csv, summary");
        Console.WriteLine("  -f, --file <path>            Output file path (auto-generated if not specified)");
        Console.WriteLine("  -d, --output-dir <path>      Output directory for generated files (default: output)");
        Console.WriteLine();
        
        Console.WriteLine("Operation Modes:");
        Console.WriteLine("      --hygiene                Run hygiene checks with roadmap generation");
        Console.WriteLine("      --hygiene-only           Run only hygiene checks (skip roadmap)");
        Console.WriteLine("      --summary                Use summary output format");
        Console.WriteLine("      --dry-run                Preview mode - show what would be done");
        Console.WriteLine();
        
        Console.WriteLine("Filtering Options:");
        Console.WriteLine("  -l, --limit <number>         Maximum work items to retrieve (default: 100, max: 10000)");
        Console.WriteLine("      --work-item-types <list> Filter by work item types (comma-separated)");
        Console.WriteLine("      --work-item-states <list> Filter by work item states (comma-separated)");
        Console.WriteLine("      --from-date <date>       Filter items modified after date (yyyy-MM-dd)");
        Console.WriteLine("      --to-date <date>         Filter items modified before date (yyyy-MM-dd)");
        Console.WriteLine();
        
        Console.WriteLine("Configuration & Debugging:");
        Console.WriteLine("  -c, --config <path>          Configuration file path");
        Console.WriteLine("  -t, --timeout <seconds>      API timeout in seconds (default: 30, range: 5-300)");
        Console.WriteLine("  -v, --verbose                Enable verbose logging");
        Console.WriteLine("  -q, --quiet                  Quiet mode (errors only)");
        Console.WriteLine("      --log-level <level>      Set logging level: trace|debug|info|warn|error|critical");
        Console.WriteLine();
        
        Console.WriteLine("Help:");
        Console.WriteLine("  -h, --help                   Show this help message");
        Console.WriteLine();
        
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Basic roadmap generation");
        Console.WriteLine("  CreateRoadmapADO --area-path \"SPOOL\\\\Resource Provider\"");
        Console.WriteLine();
        Console.WriteLine("  # Generate JSON output with increased limit");
        Console.WriteLine("  CreateRoadmapADO -a \"MyProject\\\\MyTeam\" -l 200 -o json -f roadmap.json");
        Console.WriteLine();
        Console.WriteLine("  # Run hygiene checks only");
        Console.WriteLine("  CreateRoadmapADO -a \"SPOOL\\\\Resource Provider\" --hygiene-only");
        Console.WriteLine();
        Console.WriteLine("  # Filter by date range with verbose logging");
        Console.WriteLine("  CreateRoadmapADO -a \"SPOOL\\\\Resource Provider\" --from-date 2024-01-01 --to-date 2024-12-31 -v");
        Console.WriteLine();
        Console.WriteLine("  # Preview mode with custom configuration");
        Console.WriteLine("  CreateRoadmapADO -a \"SPOOL\\\\Resource Provider\" --dry-run -c custom-config.json");
    }
}

/// <summary>
/// Result of command line parsing operation
/// </summary>
public class ParseResult
{
    public bool IsSuccess { get; private set; }
    public bool IsHelp { get; private set; }
    public EnhancedCommandLineOptions? Options { get; private set; }
    public List<string> Errors { get; private set; } = new();

    private ParseResult() { }

    public static ParseResult Success(EnhancedCommandLineOptions options)
    {
        return new ParseResult
        {
            IsSuccess = true,
            Options = options
        };
    }

    public static ParseResult Error(IEnumerable<string> errors, EnhancedCommandLineOptions options)
    {
        return new ParseResult
        {
            IsSuccess = false,
            Errors = errors.ToList(),
            Options = options
        };
    }

    public static ParseResult Help()
    {
        return new ParseResult
        {
            IsHelp = true
        };
    }

    public void DisplayErrors()
    {
        if (Errors.Any())
        {
            Console.WriteLine("❌ Command line errors:");
            foreach (var error in Errors)
            {
                Console.WriteLine($"   • {error}");
            }
            Console.WriteLine();
            Console.WriteLine("Use --help for usage information.");
        }
    }
}
