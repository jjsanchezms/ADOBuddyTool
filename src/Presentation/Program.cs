using ADOBuddyTool.Application.Commands;
using ADOBuddyTool.Application.ErrorHandling;
using ADOBuddyTool.Domain.Entities;
using ADOBuddyTool.Infrastructure.AzureDevOps.Interfaces;
using ADOBuddyTool.Infrastructure.HygieneChecks;
using ADOBuddyTool.Presentation.Configuration;
using ADOBuddyTool.Presentation.DependencyInjection;
using Microsoft.Extensions.Logging;

// Parse arguments early to configure logging appropriately
var earlyOptions = ParseEarlyArguments(args);

// Setup logging with appropriate level based on summary mode
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    // Use Warning level for summary mode to reduce noise
    var logLevel = earlyOptions.Quiet ? LogLevel.Warning :
                   earlyOptions.Verbose ? LogLevel.Debug : LogLevel.Information;
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
            case "--summary" or "-s" or "--quiet" or "-q":
                options.Quiet = true;
                break;
            case "--verbose" or "-v":
                options.Verbose = true;
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
    public bool Verbose { get; set; } = false;
    public bool Quiet { get; set; } = false;
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

            if (!options.Quiet)
            {
                _logger.LogInformation("Starting ADOBuddyTool application");
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
            if (!options.Quiet && options.CreateRoadmap)
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
            _logger.LogError("Area path is required"); Console.WriteLine("Error: Area path is required.");
            Console.WriteLine();
            HelpDisplay.ShowHelp();
            return false;
        }
        if (!options.RunHygieneChecks && !options.CreateRoadmap && !options.SwagUpdates)
        {
            _logger.LogError("At least one operation must be specified"); Console.WriteLine("Error: At least one operation must be specified (--ado-hygiene, --roadmap, or --swag-auto-generated).");
            Console.WriteLine();
            HelpDisplay.ShowHelp();
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
        if (options.Quiet) return;

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
        if (!options.Quiet)
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
                case "--summary" or "-s" or "--quiet" or "-q":
                    options.Quiet = true;
                    break;
                case "--verbose" or "-v":
                    options.Verbose = true;
                    break;
                case "--hygiene-checks" or "--ado-hygiene":
                    options.RunHygieneChecks = true;
                    break;
                case "--create-roadmap" or "--roadmap":
                    options.CreateRoadmap = true;
                    break;
                case "--swag-updates" or "--swag-auto-generated":
                    options.SwagUpdates = true;
                    // Check if the next argument is "all"
                    if (i + 1 < args.Length && args[i + 1].ToLowerInvariant() == "all")
                    {
                        options.SwagUpdatesAll = true;
                        i++; // Skip the "all" argument
                    }
                    break;
                case "--swag-all":
                    options.SwagUpdates = true;
                    options.SwagUpdatesAll = true;
                    break;
                case "--help" or "-h":
                    HelpDisplay.ShowHelp();
                    Environment.Exit(0);
                    break;
            }
        }
        return options;
    }
}
