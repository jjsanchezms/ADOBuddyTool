using CreateRoadmapADO.Application.Commands;
using CreateRoadmapADO.Application.ErrorHandling;
using CreateRoadmapADO.Domain.Entities;
using CreateRoadmapADO.Infrastructure.AzureDevOps.Interfaces;
using CreateRoadmapADO.Infrastructure.HygieneChecks;
using CreateRoadmapADO.Presentation.Configuration;
using CreateRoadmapADO.Presentation.DependencyInjection;
using Microsoft.Extensions.Logging;

// Parse arguments early to configure logging appropriately
var earlyOptions = ParseEarlyArguments(args);

// Setup logging with appropriate level based on summary mode
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    // Use Warning level for summary mode to reduce noise
    var logLevel = earlyOptions.Summary ? LogLevel.Warning : LogLevel.Information;
    builder.SetMinimumLevel(logLevel);
});

try
{
    // Create logger for the application
    var logger = loggerFactory.CreateLogger<RoadmapApplication>();

    // Create services using simplified container
    var services = new ServiceContainer(loggerFactory);
    var app = new RoadmapApplication(services, logger);
    await app.RunAsync(args);

    // Cleanup
    services.AzureDevOps.Dispose();
}
catch (Exception ex)
{
    var logger = loggerFactory.CreateLogger<Program>();
    var errorHandler = new ErrorHandler(loggerFactory.CreateLogger<ErrorHandler>());

    var error = errorHandler.HandleException(ex, new Dictionary<string, object>
    {
        ["Operation"] = "Application Startup",
        ["Arguments"] = string.Join(" ", args)
    });

    logger.LogCritical(ex, "Application terminated unexpectedly: {ErrorCode}", error.Code);

    // Display user-friendly error message
    Console.WriteLine($"\n❌ {error.UserFriendlyMessage}");
    if (error.RecoveryActions.Any())
    {
        Console.WriteLine($"💡 {string.Join("\n💡 ", error.RecoveryActions)}");
    }

    Environment.Exit(1);
}

/// <summary>
/// Early parsing of arguments for logging configuration
/// </summary>
/// <param name="args">Command line arguments</param>
/// <returns>Basic command line options</returns>
static CommandLineOptions ParseEarlyArguments(string[] args)
{
    var options = new CommandLineOptions();

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i].ToLowerInvariant())
        {
            case "--summary" or "-s":
                options.Summary = true;
                break;
        }
    }

    return options;
}

/// <summary>
/// Command line options
/// </summary>
public class CommandLineOptions
{
    public int Limit { get; set; } = 100;
    public string? AreaPath { get; set; }
    public bool RunHygieneChecks { get; set; } = false;
    public bool CreateRoadmap { get; set; } = false;
    public bool Summary { get; set; } = false;
    public bool SwagUpdates { get; set; } = false;
    public bool SwagUpdatesAll { get; set; } = false;
}

/// <summary>
/// Main application class
/// </summary>
public class RoadmapApplication
{
    private readonly ServiceContainer _services;
    private readonly ILogger<RoadmapApplication> _logger;

    public RoadmapApplication(ServiceContainer services, ILogger<RoadmapApplication> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RunAsync(string[] args)
    {
        try
        {
            var options = ParseArguments(args);

            if (!ValidateOptions(options)) return;

            if (!options.Summary)
            {
                _logger.LogInformation("Starting CreateRoadmapADO application");
            }

            var workItems = await GetWorkItemsAsync(options);
            if (!workItems.Any())
            {
                HandleNoWorkItemsFound(options);
                return;
            }

            // Use the new command coordinator to handle all operations
            var coordinator = new CommandCoordinator(_services, LoggerFactory.Create(b => b.AddConsole()));
            var results = await coordinator.ExecuteCommandsAsync(workItems, options);

            // Display roadmap results if applicable and not in summary mode
            if (!options.Summary && options.CreateRoadmap)
            {
                var roadmapItems = coordinator.GetRoadmapItems(results);
                if (roadmapItems.Any())
                {
                    _services.Output.DisplayInConsole(roadmapItems);
                }
                _logger.LogInformation("Application completed successfully");
            }

            // Check for any failures
            var failures = results.Where(r => !r.Success).ToList();
            if (failures.Any())
            {
                foreach (var failure in failures)
                {
                    _logger.LogError("Operation failed: {Message}", failure.Message);
                }
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            var error = _services.ErrorHandler.HandleException(ex, new Dictionary<string, object>
            {
                ["Operation"] = "Application Execution",
                ["Arguments"] = string.Join(" ", args)
            });

            _logger.LogError(ex, "Error running application: {ErrorCode}", error.Code);

            // Display user-friendly error message
            Console.WriteLine($"\n❌ {error.UserFriendlyMessage}");
            if (error.RecoveryActions.Any())
            {
                Console.WriteLine($"💡 {string.Join("\n💡 ", error.RecoveryActions)}");
            }

            Environment.Exit(1);
        }
    }

    private bool ValidateOptions(CommandLineOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AreaPath))
        {
            _logger.LogError("Area path is required");
            Console.WriteLine("Error: Area path is required.");
            Console.WriteLine();
            ShowHelp();
            return false;
        }
        if (!options.RunHygieneChecks && !options.CreateRoadmap && !options.SwagUpdates)
        {
            _logger.LogError("At least one operation must be specified");
            Console.WriteLine("Error: At least one operation must be specified (--hygiene-checks, --create-roadmap, or --swag-updates).");
            Console.WriteLine();
            ShowHelp();
            return false;
        }

        return true;
    }

    private async Task<IEnumerable<WorkItem>> GetWorkItemsAsync(CommandLineOptions options)
    {
        // Log what we're retrieving based on the operation type
        LogWorkItemRetrievalOperation(options);

        // Select the appropriate retrieval method based on operation
        return options.SwagUpdates
            ? await _services.AzureDevOps.GetWorkItemsForSwagUpdatesAsync(options.Limit, options.AreaPath!)
            : options.RunHygieneChecks
                ? await _services.AzureDevOps.GetWorkItemsForHygieneChecksAsync(options.Limit, options.AreaPath!)
                : await _services.AzureDevOps.GetWorkItemsAsync(options.Limit, options.AreaPath!);
    }

    private void LogWorkItemRetrievalOperation(CommandLineOptions options)
    {
        if (options.Summary) return;

        if (options.RunHygieneChecks)
        {
            _logger.LogInformation("Retrieving Feature and Release Train work items for hygiene checks (limit: {Limit}, area path: {AreaPath})",
                options.Limit, options.AreaPath);
        }
        else if (options.CreateRoadmap)
        {
            _logger.LogInformation("Retrieving Feature work items for roadmap generation (limit: {Limit}, area path: {AreaPath})",
                options.Limit, options.AreaPath);
        }
        else if (options.SwagUpdates)
        {
            var modeText = options.SwagUpdatesAll ? " (ALL mode - updates all Release Trains)" : " (auto-generated only)";
            _logger.LogInformation("Retrieving Release Train and Feature work items for SWAG updates{ModeText} (including closed Features) (limit: {Limit}, area path: {AreaPath})",
                modeText, options.Limit, options.AreaPath);
        }
    }

    private void HandleNoWorkItemsFound(CommandLineOptions options)
    {
        if (!options.Summary)
        {
            _logger.LogInformation("No work items found in area path '{AreaPath}'.", options.AreaPath);
        }

        string workItemTypeDescription = options.RunHygieneChecks || options.SwagUpdates
            ? "Feature or Release Train work items"
            : "Feature work items";
        _logger.LogWarning("No {WorkItemTypeDescription} found in area path '{AreaPath}'", workItemTypeDescription, options.AreaPath);
        Console.WriteLine($"No {workItemTypeDescription} found in area path '{options.AreaPath}'.");
    }

    private static CommandLineOptions ParseArguments(string[] args)
    {
        var options = new CommandLineOptions();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--limit" or "-l":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var limit))
                        options.Limit = limit;
                    break;
                case "--area-path" or "-a":
                    if (i + 1 < args.Length)
                        options.AreaPath = args[++i];
                    break;
                case "--summary" or "-s":
                    options.Summary = true;
                    break;
                case "--hygiene-checks" or "--hygiene":
                    options.RunHygieneChecks = true;
                    break;
                case "--create-roadmap" or "--roadmap":
                    options.CreateRoadmap = true;
                    break;
                case "--swag-updates" or "--swag":
                    options.SwagUpdates = true;
                    // Check if the next argument is "all"
                    if (i + 1 < args.Length && args[i + 1].ToLowerInvariant() == "all")
                    {
                        options.SwagUpdatesAll = true;
                        i++; // Skip the "all" argument
                    }
                    break;
                case "--help" or "-h":
                    ShowHelp();
                    Environment.Exit(0);
                    break;
            }
        }

        return options;
    }

    private static void ShowHelp()
    {
        Console.WriteLine("CreateRoadmapADO - Generate roadmaps from Azure DevOps Feature work items");
        Console.WriteLine();
        Console.WriteLine("Usage: CreateRoadmapADO --area-path <path> (--hygiene-checks | --create-roadmap | --swag-updates) [options]");
        Console.WriteLine();
        Console.WriteLine("Required:");
        Console.WriteLine("  -a, --area-path <path>    Azure DevOps area path to filter work items (e.g., \"SPOOL\\\\Resource Provider\")");
        Console.WriteLine();
        Console.WriteLine("Operations (at least one required):");
        Console.WriteLine("  --hygiene-checks          Run ADO hygiene checks on Release Trains and Features");
        Console.WriteLine("  --create-roadmap          Generate roadmap and create Release Train work items from patterns");
        Console.WriteLine("  --swag-updates            Review Release Trains and manage SWAG calculations (auto-generated only)");
        Console.WriteLine("  --swag-updates all        Update SWAG for ALL Release Trains (ignores auto-generated tag)");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -l, --limit <number>      Maximum number of work items to retrieve (default: 100)");
        Console.WriteLine("  -s, --summary             Enable summary mode (reduced output for automation)");
        Console.WriteLine("  -h, --help                Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Create roadmap only");
        Console.WriteLine("  CreateRoadmapADO --area-path \"SPOOL\\\\Resource Provider\" --create-roadmap");
        Console.WriteLine();
        Console.WriteLine("  # Run hygiene checks only");
        Console.WriteLine("  CreateRoadmapADO --area-path \"SPOOL\\\\Resource Provider\" --hygiene-checks");
        Console.WriteLine();
        Console.WriteLine("  # Update SWAG values for Release Trains (auto-generated only)");
        Console.WriteLine("  CreateRoadmapADO --area-path \"SPOOL\\\\Resource Provider\" --swag-updates");
        Console.WriteLine();
        Console.WriteLine("  # Update SWAG values for ALL Release Trains");
        Console.WriteLine("  CreateRoadmapADO --area-path \"SPOOL\\\\Resource Provider\" --swag-updates all");
        Console.WriteLine();
        Console.WriteLine("  # Run multiple operations in summary mode");
        Console.WriteLine("  CreateRoadmapADO --area-path \"SPOOL\\\\Resource Provider\" --create-roadmap --hygiene-checks --summary");
        Console.WriteLine();
        Console.WriteLine("  # Process more items");
        Console.WriteLine("  CreateRoadmapADO --area-path \"MyProject\\\\MyTeam\" --create-roadmap --limit 200");
        Console.WriteLine();
        Console.WriteLine("SWAG Updates Operation:");
        Console.WriteLine("  • Normal mode: Only updates auto-generated Release Trains, shows warnings for manual ones");
        Console.WriteLine("  • ALL mode: Updates ALL Release Trains regardless of auto-generated tag");
        Console.WriteLine("  • For manual Release Trains (normal mode): Shows warnings if SWAG doesn't match Feature sum");
        Console.WriteLine("  • Only processes Release Trains with related Feature work items");
        Console.WriteLine("  • SWAG values are stored as [SWAG: value] prefix in the status notes field");
    }
}
