using CreateRoadmapADO.Application.ErrorHandling;
using CreateRoadmapADO.Domain.Entities;
using CreateRoadmapADO.Presentation.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CreateRoadmapADO.Application.Commands;

/// <summary>
/// Handles roadmap generation operations
/// </summary>
public class RoadmapGenerationHandler : ICommandHandler
{
    private readonly ServiceContainer _services;
    private readonly ILogger<RoadmapGenerationHandler> _logger;

    public string CommandName => "Roadmap Generation";

    public RoadmapGenerationHandler(ServiceContainer services, ILogger<RoadmapGenerationHandler> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool ShouldExecute(CommandLineOptions options) => options.CreateRoadmap;

    public async Task<CommandResult> ExecuteAsync(IEnumerable<WorkItem> workItems, CommandLineOptions options)
    {
        try
        {
            if (!options.Quiet)
            {
                _logger.LogInformation("Generating roadmap from {Count} work items", workItems.Count());
                _logger.LogInformation("Processing work items for special title patterns (Release Trains)");
                Console.WriteLine("\nProcessing work items for special title patterns (Release Trains)...\n");
            }

            var roadmapItems = await _services.Roadmap.GenerateRoadmapAsync(workItems, options.AreaPath!);

            if (!options.Quiet)
            {
                _logger.LogInformation("Finished processing special title patterns");
                Console.WriteLine("\nFinished processing special title patterns\n");
            }

            DisplayReleaseTrainSummary(_services.Roadmap.OperationsSummary);

            return CommandResult.SuccessResult("Roadmap generation completed successfully", roadmapItems);
        }
        catch (Exception ex)
        {
            var error = _services.ErrorHandler.HandleException(ex, new Dictionary<string, object>
            {
                ["Operation"] = "Roadmap Generation",
                ["WorkItemCount"] = workItems.Count(),
                ["Options"] = options
            });

            _logger.LogError(ex, "Error during roadmap generation: {ErrorCode}", error.Code);

            // Display user-friendly error message
            Console.WriteLine($"\n❌ {error.UserFriendlyMessage}");
            if (error.RecoveryActions.Any())
            {
                Console.WriteLine($"💡 {string.Join("\n💡 ", error.RecoveryActions)}");
            }

            return CommandResult.FailureResult(error.UserFriendlyMessage);
        }
    }

    private static void DisplayReleaseTrainSummary(ReleaseTrainSummary summary)
    {
        const int separatorWidth = 60;

        Console.WriteLine("=".PadRight(separatorWidth, '='));
        Console.WriteLine("RELEASE TRAIN SUMMARY");
        Console.WriteLine("=".PadRight(separatorWidth, '='));

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

        Console.WriteLine("=".PadRight(separatorWidth, '='));
        Console.WriteLine();
    }
}

