using CreateRoadmapADO.Configuration;
using CreateRoadmapADO.Interfaces;
using CreateRoadmapADO.Models;
using CreateRoadmapADO.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Build configuration
var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

var configuration = builder.Build();

// Build host with dependency injection
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Configuration
        services.Configure<AzureDevOpsOptions>(configuration.GetSection(AzureDevOpsOptions.SectionName));
        services.Configure<AppOptions>(configuration.GetSection("App"));

        // HttpClient
        services.AddHttpClient<IAzureDevOpsService, AzureDevOpsService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Services
        services.AddScoped<IAzureDevOpsService, AzureDevOpsService>();
        services.AddScoped<IRoadmapService, RoadmapService>();
        services.AddScoped<IOutputService, OutputService>();
        services.AddScoped<RoadmapApplication>();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddConfiguration(configuration.GetSection("Logging"));
        });
    })
    .Build();

try
{
    // Run the application
    var app = host.Services.GetRequiredService<RoadmapApplication>();
    await app.RunAsync(args);
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogCritical(ex, "Application terminated unexpectedly");
    Environment.Exit(1);
}

/// <summary>
/// Main application class
/// </summary>
public class RoadmapApplication
{
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly IRoadmapService _roadmapService;
    private readonly IOutputService _outputService;
    private readonly ILogger<RoadmapApplication> _logger;

    public RoadmapApplication(
        IAzureDevOpsService azureDevOpsService,
        IRoadmapService roadmapService,
        IOutputService outputService,
        ILogger<RoadmapApplication> logger)
    {
        _azureDevOpsService = azureDevOpsService ?? throw new ArgumentNullException(nameof(azureDevOpsService));
        _roadmapService = roadmapService ?? throw new ArgumentNullException(nameof(roadmapService));
        _outputService = outputService ?? throw new ArgumentNullException(nameof(outputService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RunAsync(string[] args)
    {
        try
        {
            _logger.LogInformation("Starting CreateRoadmapADO application");

            // Parse command line arguments
            var options = ParseArguments(args);            // Retrieve work items from Azure DevOps
            _logger.LogInformation("Retrieving Feature work items from Azure DevOps (limit: {Limit})", options.Limit);
            var workItems = await _azureDevOpsService.GetWorkItemsAsync(options.Limit);

            if (!workItems.Any())
            {
                _logger.LogInformation("No work items found.");
                return;
            }            _logger.LogInformation("Generating roadmap from {Count} work items", workItems.Count());
            Console.WriteLine("\nProcessing work items for special title patterns (Release Trains)...\n");
            var roadmapItems = await _roadmapService.GenerateRoadmapAsync(workItems);
            Console.WriteLine("\nFinished processing special title patterns\n");

            // Display Release Train Summary
            DisplayReleaseTrainSummary(_roadmapService.OperationsSummary);

            // Output roadmap (only if requested)
            if (options.OutputFormat != "summary")
            {
                await OutputRoadmapAsync(roadmapItems, options);
            }

            _logger.LogInformation("Application completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running application");
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }    private static CommandLineOptions ParseArguments(string[] args)
    {
        var options = new CommandLineOptions();        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {                case "--output" or "-o":
                    if (i + 1 < args.Length)
                        options.OutputFormat = args[++i];
                    break;
                case "--file" or "-f":
                    if (i + 1 < args.Length)
                        options.OutputFile = args[++i];
                    break;
                case "--limit" or "-l":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var limit))
                        options.Limit = limit;
                    break;
                case "--summary-only" or "-s":
                    options.OutputFormat = "summary";
                    break;
                case "--help" or "-h":
                    ShowHelp();
                    Environment.Exit(0);
                    break;
            }
        }

        return options;
    }

    private async Task OutputRoadmapAsync(IEnumerable<CreateRoadmapADO.Models.RoadmapItem> roadmapItems, CommandLineOptions options)
    {
        switch (options.OutputFormat.ToLowerInvariant())
        {
            case "json":
                var jsonFile = options.OutputFile ?? $"roadmap_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                await _outputService.ExportToJsonAsync(roadmapItems, jsonFile);
                Console.WriteLine($"Roadmap exported to: {jsonFile}");
                break;

            case "csv":
                var csvFile = options.OutputFile ?? $"roadmap_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                await _outputService.ExportToCsvAsync(roadmapItems, csvFile);
                Console.WriteLine($"Roadmap exported to: {csvFile}");
                break;

            case "console":
            default:
                _outputService.DisplayInConsole(roadmapItems);
                break;
        }
    }    private static void ShowHelp()
    {
        Console.WriteLine("CreateRoadmapADO - Generate roadmaps from Azure DevOps Feature work items");
        Console.WriteLine();
        Console.WriteLine("Usage: CreateRoadmapADO [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -l, --limit <number>  Maximum number of Feature work items to retrieve (default: 100)");
        Console.WriteLine("  -o, --output <format> Output format: console, json, csv (default: console)");
        Console.WriteLine("  -f, --file <path>     Output file path (auto-generated if not specified)");
        Console.WriteLine("  -h, --help            Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  CreateRoadmapADO");
        Console.WriteLine("  CreateRoadmapADO --limit 50 --output json");
        Console.WriteLine("  CreateRoadmapADO --limit 200 --output csv --file roadmap.csv");
    }    /// <summary>
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
    }
}

/// <summary>
/// Command line options
/// </summary>
public class CommandLineOptions
{
    public int Limit { get; set; } = 100;
    public string OutputFormat { get; set; } = "console";
    public string? OutputFile { get; set; }
}
