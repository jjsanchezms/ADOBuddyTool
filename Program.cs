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
    // Constants for better readability - replace magic numbers with meaningful names
    private const int ConsoleSeparatorWidth = 60;  // Width for console formatting separators

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

            await ProcessOperationsAsync(workItems, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running application");
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private bool ValidateOptions(CommandLineOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AreaPath))
        {
            Console.WriteLine("Error: Area path is required.");
            Console.WriteLine();
            ShowHelp();
            return false;
        }
        if (!options.RunHygieneChecks && !options.CreateRoadmap && !options.SwagUpdates)
        {
            Console.WriteLine("Error: At least one operation must be specified (--hygiene-checks, --create-roadmap, or --swag-updates).");
            Console.WriteLine();
            ShowHelp();
            return false;
        }

        return true;
    }
    private async Task<IEnumerable<WorkItem>> GetWorkItemsAsync(CommandLineOptions options)
    {
        if (!options.Summary)
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
            else if (options.SwagUpdates)
            {
                _logger.LogInformation("Retrieving Release Train and Feature work items for SWAG updates (limit: {Limit}, area path: {AreaPath})",
                    options.Limit, options.AreaPath);
            }
        }

        return options.RunHygieneChecks || options.SwagUpdates
            ? await _services.AzureDevOps.GetWorkItemsForHygieneChecksAsync(options.Limit, options.AreaPath!)
            : await _services.AzureDevOps.GetWorkItemsAsync(options.Limit, options.AreaPath!);
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
        Console.WriteLine($"No {workItemTypeDescription} found in area path '{options.AreaPath}'.");
    }
    private async Task ProcessOperationsAsync(IEnumerable<WorkItem> workItems, CommandLineOptions options)
    {
        IEnumerable<CreateRoadmapADO.Models.RoadmapItem> roadmapItems = Enumerable.Empty<CreateRoadmapADO.Models.RoadmapItem>();

        if (options.CreateRoadmap)
        {
            roadmapItems = await ProcessRoadmapGenerationAsync(workItems, options);
        }

        if (options.RunHygieneChecks)
        {
            await ProcessHygieneChecksAsync(workItems, options);
        }

        if (options.SwagUpdates)
        {
            await ProcessSwagUpdatesAsync(workItems, options);
        }

        if (!options.Summary && options.CreateRoadmap)
        {
            _services.Output.DisplayInConsole(roadmapItems);
            _logger.LogInformation("Application completed successfully");
        }
    }

    private async Task<IEnumerable<CreateRoadmapADO.Models.RoadmapItem>> ProcessRoadmapGenerationAsync(IEnumerable<WorkItem> workItems, CommandLineOptions options)
    {
        if (!options.Summary)
        {
            _logger.LogInformation("Generating roadmap from {Count} work items", workItems.Count());
            Console.WriteLine("\nProcessing work items for special title patterns (Release Trains)...\n");
        }

        var roadmapItems = await _services.Roadmap.GenerateRoadmapAsync(workItems);

        if (!options.Summary)
        {
            Console.WriteLine("\nFinished processing special title patterns\n");
        }

        DisplayReleaseTrainSummary(_services.Roadmap.OperationsSummary);
        return roadmapItems;
    }

    private async Task ProcessHygieneChecksAsync(IEnumerable<WorkItem> workItems, CommandLineOptions options)
    {
        if (!options.Summary)
        {
            Console.WriteLine("\n" + "=".PadRight(ConsoleSeparatorWidth, '='));
            Console.WriteLine("RUNNING ADO HYGIENE CHECKS");
            Console.WriteLine("=".PadRight(ConsoleSeparatorWidth, '='));
            _logger.LogInformation("Starting ADO hygiene checks on {Count} work items", workItems.Count());
        }

        var hygieneResults = await _services.Hygiene.PerformHygieneChecksAsync(workItems);
        DisplayHygieneCheckResults(hygieneResults, options);
    }

    private async Task ProcessSwagUpdatesAsync(IEnumerable<WorkItem> workItems, CommandLineOptions options)
    {
        if (!options.Summary)
        {
            Console.WriteLine("\n" + "=".PadRight(ConsoleSeparatorWidth, '='));
            Console.WriteLine("PROCESSING SWAG UPDATES");
            Console.WriteLine("=".PadRight(ConsoleSeparatorWidth, '='));
            _logger.LogInformation("Starting SWAG updates on Release Trains in area path: {AreaPath}", options.AreaPath);
        }

        var workItemsList = workItems.ToList();

        // Find all Release Trains in the area path
        var releaseTrains = workItemsList
            .Where(w => w.WorkItemType == "Release Train")
            .ToList();

        // Find all Features for SWAG calculation
        var features = workItemsList
            .Where(w => w.WorkItemType == "Feature")
            .ToList();

        if (!releaseTrains.Any())
        {
            Console.WriteLine("No Release Trains found in the specified area path.");
            return;
        }

        _logger.LogInformation("Found {ReleaseTrainCount} Release Trains and {FeatureCount} Features to process",
            releaseTrains.Count, features.Count);

        int updatedCount = 0;
        int warningCount = 0;

        foreach (var releaseTrain in releaseTrains)
        {
            try
            {
                // Get the Release Train with its relations
                var releaseTrainWithRelations = await _services.AzureDevOps.GetWorkItemWithRelationsAsync(releaseTrain.Id);

                if (releaseTrainWithRelations?.Relations == null)
                {
                    Console.WriteLine($"⚠️  Release Train #{releaseTrain.Id} has no relations - skipping");
                    continue;
                }

                // Get related feature IDs
                var relatedFeatureIds = releaseTrainWithRelations.Relations
                    .Where(r => r.Rel == "System.LinkTypes.Related" ||
                               r.Rel == "System.LinkTypes.Hierarchy-Forward" ||
                               r.Rel == "System.LinkTypes.Hierarchy-Reverse")
                    .Select(r => r.GetRelatedWorkItemId())
                    .Where(id => id > 0)
                    .ToList();

                if (!relatedFeatureIds.Any())
                {
                    Console.WriteLine($"⚠️  Release Train #{releaseTrain.Id} '{releaseTrain.Title}' has no related Features - skipping");
                    continue;
                }

                // Get the actual feature work items with SWAG values
                var relatedFeatures = new List<WorkItem>();
                foreach (var featureId in relatedFeatureIds)
                {
                    var feature = await _services.AzureDevOps.GetWorkItemByIdAsync(featureId);
                    if (feature != null && feature.WorkItemType == "Feature")
                    {
                        relatedFeatures.Add(feature);
                    }
                }

                if (!relatedFeatures.Any())
                {
                    Console.WriteLine($"⚠️  Release Train #{releaseTrain.Id} '{releaseTrain.Title}' has no valid Feature relations - skipping");
                    continue;
                }                // Calculate total SWAG from related features
                var totalSwag = relatedFeatures
                    .Where(f => f.Swag.HasValue)
                    .Sum(f => f.Swag!.Value);

                var featuresWithSwag = relatedFeatures.Count(f => f.Swag.HasValue);
                var featuresWithoutSwag = relatedFeatures.Count - featuresWithSwag;

                // Check if Release Train has auto-generated tag
                bool isAutoGenerated = releaseTrain.Tags.Contains("auto-generated");

                Console.WriteLine($"\n📊 Release Train #{releaseTrain.Id}: '{releaseTrain.Title}'");
                Console.WriteLine($"   Auto-generated: {(isAutoGenerated ? "Yes" : "No")}");
                Console.WriteLine($"   Related Features: {relatedFeatures.Count} ({featuresWithSwag} with SWAG, {featuresWithoutSwag} without)");
                Console.WriteLine($"   Current RT SWAG: {releaseTrain.Swag?.ToString() ?? "Not set"}");
                Console.WriteLine($"   Calculated SWAG: {totalSwag}");

                if (featuresWithoutSwag > 0)
                {
                    Console.WriteLine($"   ⚠️  Warning: {featuresWithoutSwag} Features have no SWAG value");
                }

                if (isAutoGenerated)
                {
                    // For auto-generated Release Trains: update SWAG to match sum of features
                    if (releaseTrain.Swag != totalSwag)
                    {
                        Console.WriteLine($"   🔄 Updating SWAG from {releaseTrain.Swag?.ToString() ?? "unset"} to {totalSwag}");
                        await _services.AzureDevOps.UpdateWorkItemSwagAsync(releaseTrain.Id, totalSwag);
                        updatedCount++;
                    }
                    else
                    {
                        Console.WriteLine($"   ✅ SWAG already matches calculated value");
                    }
                }
                else
                {
                    // For non-auto-generated Release Trains: verify SWAG matches, warn if mismatch
                    if (releaseTrain.Swag != totalSwag)
                    {
                        Console.WriteLine($"   ⚠️  WARNING: Release Train SWAG ({releaseTrain.Swag?.ToString() ?? "unset"}) does not match sum of Features ({totalSwag})");
                        warningCount++;
                    }
                    else
                    {
                        Console.WriteLine($"   ✅ SWAG matches calculated value");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SWAG updates for Release Train {Id}", releaseTrain.Id);
                Console.WriteLine($"❌ Error processing Release Train #{releaseTrain.Id}: {ex.Message}");
            }
        }

        // Display summary
        Console.WriteLine("\n" + "=".PadRight(ConsoleSeparatorWidth, '='));
        Console.WriteLine("SWAG UPDATES SUMMARY");
        Console.WriteLine("=".PadRight(ConsoleSeparatorWidth, '='));
        Console.WriteLine($"Release Trains processed: {releaseTrains.Count}");
        Console.WriteLine($"Auto-generated Release Trains updated: {updatedCount}");
        Console.WriteLine($"Manual Release Trains with SWAG mismatches: {warningCount}");
        Console.WriteLine("=".PadRight(ConsoleSeparatorWidth, '='));
        Console.WriteLine();
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
        Console.WriteLine("  --swag-updates            Review Release Trains and manage SWAG calculations");
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
        Console.WriteLine("  # Update SWAG values for Release Trains");
        Console.WriteLine("  CreateRoadmapADO --area-path \"SPOOL\\\\Resource Provider\" --swag-updates");
        Console.WriteLine();
        Console.WriteLine("  # Run multiple operations in summary mode");
        Console.WriteLine("  CreateRoadmapADO --area-path \"SPOOL\\\\Resource Provider\" --create-roadmap --hygiene-checks --summary");
        Console.WriteLine();
        Console.WriteLine("  # Process more items");
        Console.WriteLine("  CreateRoadmapADO --area-path \"MyProject\\\\MyTeam\" --create-roadmap --limit 200");
        Console.WriteLine();
        Console.WriteLine("SWAG Updates Operation:");
        Console.WriteLine("  • For auto-generated Release Trains: Updates SWAG to sum of related Features");
        Console.WriteLine("  • For manual Release Trains: Shows warnings if SWAG doesn't match Feature sum");
        Console.WriteLine("  • Only processes Release Trains with related Feature work items");
    }

    /// <summary>
    /// Displays a summary of Release Train operations
    /// </summary>
    /// <param name="summary">The operations summary to display</param>
    private static void DisplayReleaseTrainSummary(ReleaseTrainSummary summary)
    {
        Console.WriteLine("=".PadRight(ConsoleSeparatorWidth, '='));
        Console.WriteLine("RELEASE TRAIN SUMMARY");
        Console.WriteLine("=".PadRight(ConsoleSeparatorWidth, '='));

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

        Console.WriteLine("=".PadRight(ConsoleSeparatorWidth, '='));
        Console.WriteLine();
    }

    /// <summary>
    /// Displays hygiene check results in a formatted manner
    /// </summary>
    /// <param name="hygieneResults">The hygiene check results to display</param>
    /// <param name="options">Command line options for output formatting</param>
    private void DisplayHygieneCheckResults(HygieneCheckSummary hygieneResults, CommandLineOptions options)
    {
        // Display summary
        Console.WriteLine();
        Console.WriteLine("HYGIENE CHECK SUMMARY");
        Console.WriteLine("=".PadRight(ConsoleSeparatorWidth, '='));
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
            Console.WriteLine("-".PadRight(ConsoleSeparatorWidth, '-'));

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
            Console.WriteLine("-".PadRight(ConsoleSeparatorWidth, '-'));

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
    public bool SwagUpdates { get; set; } = false;
}
