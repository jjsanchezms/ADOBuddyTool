using CreateRoadmapADO.Configuration;
using CreateRoadmapADO.Interfaces;
using CreateRoadmapADO.Models;
using CreateRoadmapADO.Services;
using CreateRoadmapADO.Services.HygieneChecks;
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
    var logger = loggerFactory.CreateLogger<RoadmapApplication>();    // Create services with simple constructor injection
    var azureDevOpsLogger = loggerFactory.CreateLogger<AzureDevOpsService>();
    var azureDevOpsService = new AzureDevOpsService(azureDevOpsLogger);

    var roadmapLogger = loggerFactory.CreateLogger<RoadmapService>();
    var roadmapService = new RoadmapService(roadmapLogger, azureDevOpsService);

    var outputLogger = loggerFactory.CreateLogger<OutputService>();
    var outputService = new OutputService(outputLogger);
    // Create individual hygiene check instances with their loggers
    var iterationPathAlignmentCheck = new IterationPathAlignmentCheck(
        loggerFactory.CreateLogger<IterationPathAlignmentCheck>());
    var releaseTrainCompletenessCheck = new ReleaseTrainCompletenessCheck(
        iterationPathAlignmentCheck,
        loggerFactory.CreateLogger<ReleaseTrainCompletenessCheck>()); var statusNotesDocumentationCheck = new StatusNotesDocumentationCheck(
        loggerFactory.CreateLogger<StatusNotesDocumentationCheck>());
    var featureStateConsistencyCheck = new FeatureStateConsistencyCheck(
        loggerFactory.CreateLogger<FeatureStateConsistencyCheck>());

    var hygieneLogger = loggerFactory.CreateLogger<HygieneCheckService>();
    var hygieneService = new HygieneCheckService(
        azureDevOpsService,
        hygieneLogger,
        releaseTrainCompletenessCheck,
        statusNotesDocumentationCheck,
        featureStateConsistencyCheck);
    // Create and run the application
    var app = new RoadmapApplication(azureDevOpsService, roadmapService, outputService, hygieneService, logger);
    await app.RunAsync(args);

    // Cleanup
    azureDevOpsService.Dispose();
}
catch (Exception ex)
{
    var logger = loggerFactory.CreateLogger<Program>();
    logger.LogCritical(ex, "Application terminated unexpectedly");
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
/// Main application class
/// </summary>
public class RoadmapApplication
{
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly RoadmapService _roadmapService;
    private readonly OutputService _outputService;
    private readonly HygieneCheckService _hygieneService;
    private readonly ILogger<RoadmapApplication> _logger;

    public RoadmapApplication(
        IAzureDevOpsService azureDevOpsService,
        RoadmapService roadmapService,
        OutputService outputService,
        HygieneCheckService hygieneService,
        ILogger<RoadmapApplication> logger)
    {
        _azureDevOpsService = azureDevOpsService ?? throw new ArgumentNullException(nameof(azureDevOpsService));
        _roadmapService = roadmapService ?? throw new ArgumentNullException(nameof(roadmapService));
        _outputService = outputService ?? throw new ArgumentNullException(nameof(outputService));
        _hygieneService = hygieneService ?? throw new ArgumentNullException(nameof(hygieneService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    public async Task RunAsync(string[] args)
    {
        try
        {
            // Parse command line arguments
            var options = ParseArguments(args);            // Validate required parameters
            if (string.IsNullOrWhiteSpace(options.AreaPath))
            {
                Console.WriteLine("Error: Area path is required.");
                Console.WriteLine();
                ShowHelp();
                return;
            }

            // Validate that at least one operation is requested
            if (!options.RunHygieneChecks && !options.CreateRoadmap)
            {
                Console.WriteLine("Error: At least one operation must be specified (--hygiene-checks or --create-roadmap).");
                Console.WriteLine();
                ShowHelp();
                return;
            }            // Only show startup message if not in summary mode
            if (!options.Summary)
            {
                _logger.LogInformation("Starting CreateRoadmapADO application");
            }// Retrieve work items from Azure DevOps
            IEnumerable<WorkItem> workItems; if (!options.Summary)
            {
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
            }

            if (options.RunHygieneChecks)
            {
                // For hygiene checks, get both Feature and Release Train work items
                workItems = await _azureDevOpsService.GetWorkItemsForHygieneChecksAsync(options.Limit, options.AreaPath!);
            }
            else
            {
                // For roadmap generation, get only Feature work items
                workItems = await _azureDevOpsService.GetWorkItemsAsync(options.Limit, options.AreaPath!);
            }
            if (!workItems.Any())
            {
                if (!options.Summary)
                {
                    _logger.LogInformation("No work items found in area path '{AreaPath}'.", options.AreaPath);
                }
                string workItemTypeDescription = options.RunHygieneChecks
                  ? "Feature or Release Train work items"
                  : "Feature work items";
                Console.WriteLine($"No {workItemTypeDescription} found in area path '{options.AreaPath}'.");
                return;
            }

            // Only show processing messages if not in summary mode and roadmap is requested
            if (!options.Summary && options.CreateRoadmap)
            {
                _logger.LogInformation("Generating roadmap from {Count} work items", workItems.Count());
                Console.WriteLine("\nProcessing work items for special title patterns (Release Trains)...\n");
            }

            // Run roadmap generation if requested
            IEnumerable<CreateRoadmapADO.Models.RoadmapItem> roadmapItems = Enumerable.Empty<CreateRoadmapADO.Models.RoadmapItem>();
            if (options.CreateRoadmap)
            {
                roadmapItems = await _roadmapService.GenerateRoadmapAsync(workItems);

                if (!options.Summary)
                {
                    Console.WriteLine("\nFinished processing special title patterns\n");
                }

                // Always display Release Train Summary (this is the main output for summary mode)
                DisplayReleaseTrainSummary(_roadmapService.OperationsSummary);
            }            // Run hygiene checks if requested
            if (options.RunHygieneChecks)
            {
                if (!options.Summary)
                {
                    Console.WriteLine("\n" + "=".PadRight(60, '='));
                    Console.WriteLine("RUNNING ADO HYGIENE CHECKS");
                    Console.WriteLine("=".PadRight(60, '='));
                    _logger.LogInformation("Starting ADO hygiene checks on {Count} work items", workItems.Count());
                }
                var hygieneResults = await _hygieneService.PerformHygieneChecksAsync(workItems);
                DisplayHygieneCheckResults(hygieneResults, options);
            }            // Output roadmap if requested and not in summary mode
            if (!options.Summary && options.CreateRoadmap)
            {
                _outputService.DisplayInConsole(roadmapItems);
                _logger.LogInformation("Application completed successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running application");
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
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
        Console.WriteLine("Usage: CreateRoadmapADO --area-path <path> (--hygiene-checks | --create-roadmap) [options]");
        Console.WriteLine();
        Console.WriteLine("Required:");
        Console.WriteLine("  -a, --area-path <path>    Azure DevOps area path to filter work items (e.g., \"SPOOL\\\\Resource Provider\")");
        Console.WriteLine();
        Console.WriteLine("Operations (at least one required):");
        Console.WriteLine("  --hygiene-checks          Run ADO hygiene checks on Release Trains and Features");
        Console.WriteLine("  --create-roadmap          Generate roadmap and create Release Train work items from patterns");
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
        Console.WriteLine("  # Run both operations in summary mode");
        Console.WriteLine("  CreateRoadmapADO --area-path \"SPOOL\\\\Resource Provider\" --create-roadmap --hygiene-checks --summary");
        Console.WriteLine();
        Console.WriteLine("  # Process more items");
        Console.WriteLine("  CreateRoadmapADO --area-path \"MyProject\\\\MyTeam\" --create-roadmap --limit 200");
    }

    /// <summary>
    /// Displays a summary of Release Train operations
    /// </summary>
    /// <param name="summary">The operations summary to display</param>
    private static void DisplayReleaseTrainSummary(ReleaseTrainSummary summary)
    {
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine("RELEASE TRAIN SUMMARY");
        Console.WriteLine("=".PadRight(60, '='));

        if (!summary.BacklogReadSuccessfully)
        {
            Console.WriteLine("❌ Error reading backlog items");
            return;
        }

        Console.WriteLine($"✅ Backlog read successfully ({summary.TotalBacklogItemsProcessed} items processed)");
        Console.WriteLine();

        if (!summary.Operations.Any())
        {
            Console.WriteLine("ℹ️  No Release Train patterns found");
            Console.WriteLine();
            return;
        }

        // Group operations by type
        var createdOps = summary.Operations.Where(op => op.Operation == OperationType.Created).ToList();
        var updatedOps = summary.Operations.Where(op => op.Operation == OperationType.Updated).ToList();

        if (createdOps.Any())
        {
            Console.WriteLine($"🆕 CREATED ({createdOps.Count}):");
            foreach (var op in createdOps)
            {
                Console.WriteLine($"   • Release Train #{op.Id}: \"{op.Title}\" ({op.TotalWorkItems} work items)");
            }
            Console.WriteLine();
        }

        if (updatedOps.Any())
        {
            Console.WriteLine($"🔄 UPDATED ({updatedOps.Count}):");
            foreach (var op in updatedOps)
            {
                var newRelationsText = op.NewRelationsAdded > 0
                    ? $", +{op.NewRelationsAdded} new relations"
                    : ", no new relations needed";
                Console.WriteLine($"   • Release Train #{op.Id}: \"{op.Title}\" ({op.TotalWorkItems} total work items{newRelationsText})");
            }
            Console.WriteLine();
        }

        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine();
    }    /// <summary>
         /// Displays hygiene check results in a formatted manner
         /// </summary>
         /// <param name="hygieneResults">The hygiene check results to display</param>
         /// <param name="options">Command line options for output formatting</param>
    private void DisplayHygieneCheckResults(HygieneCheckSummary hygieneResults, CommandLineOptions options)
    {        // Display summary
        Console.WriteLine();
        Console.WriteLine("HYGIENE CHECK SUMMARY");
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine($"Total Checks: {hygieneResults.TotalChecks}");
        Console.WriteLine($"Passed: {hygieneResults.PassedChecks} ✅");
        Console.WriteLine($"Failed: {hygieneResults.FailedChecks} ❌");
        Console.WriteLine($"Health Score: {hygieneResults.HealthScore:F1}%");

        if (hygieneResults.CriticalIssues > 0)
            Console.WriteLine($"Critical Issues: {hygieneResults.CriticalIssues} 🔴");
        if (hygieneResults.ErrorIssues > 0)
            Console.WriteLine($"Error Issues: {hygieneResults.ErrorIssues} 🟠");
        if (hygieneResults.WarningIssues > 0)
            Console.WriteLine($"Warning Issues: {hygieneResults.WarningIssues} 🟡");

        // Display breakdown by check type for failed checks
        var failedChecksByType = hygieneResults.CheckResults
            .Where(r => !r.Passed)
            .GroupBy(r => r.CheckName)
            .Where(g => g.Any())
            .ToList();

        if (failedChecksByType.Any())
        {
            Console.WriteLine();
            Console.WriteLine("ISSUES BY CHECK TYPE");
            Console.WriteLine("-".PadRight(60, '-'));

            foreach (var checkGroup in failedChecksByType.OrderByDescending(g => g.Count()))
            {
                var severityIcon = GetMostSevereIcon(checkGroup);
                Console.WriteLine($"{severityIcon} {checkGroup.Key}: {checkGroup.Count()} issues");
            }
        }

        Console.WriteLine();

        // Display failed checks in detail
        var failedChecks = hygieneResults.CheckResults.Where(r => !r.Passed).ToList();
        if (failedChecks.Any())
        {
            Console.WriteLine("FAILED CHECKS");
            Console.WriteLine("-".PadRight(60, '-'));

            foreach (var check in failedChecks.OrderByDescending(c => c.Severity))
            {
                var severityIcon = check.Severity switch
                {
                    HygieneCheckSeverity.Critical => "🔴",
                    HygieneCheckSeverity.Error => "🟠",
                    HygieneCheckSeverity.Warning => "🟡",
                    _ => "ℹ️"
                };
                Console.WriteLine($"{severityIcon} [{check.Severity.ToString().ToUpper()}] {check.CheckName}");
                Console.WriteLine($"   Work Item: #{check.WorkItemId} - {check.WorkItemTitle}");
                Console.WriteLine($"   URL: {check.WorkItemUrl}");
                Console.WriteLine($"   Issue: {check.Details}");
                Console.WriteLine($"   Recommendation: {check.Recommendation}");
                Console.WriteLine();
            }
        }
    }

    /// <summary>
    /// Gets the most severe icon for a group of hygiene check results
    /// </summary>
    /// <param name="checkGroup">Group of hygiene check results</param>
    /// <returns>Icon representing the most severe issue in the group</returns>
    private static string GetMostSevereIcon(IGrouping<string, HygieneCheckResult> checkGroup)
    {
        var mostSevere = checkGroup.Max(c => c.Severity);
        return mostSevere switch
        {
            HygieneCheckSeverity.Critical => "🔴",
            HygieneCheckSeverity.Error => "🟠",
            HygieneCheckSeverity.Warning => "🟡",
            _ => "ℹ️"
        };
    }
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
}
