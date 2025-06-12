using CreateRoadmapADO.Application.ErrorHandling;
using CreateRoadmapADO.Domain.Entities;
using CreateRoadmapADO.Presentation.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CreateRoadmapADO.Application.Commands;

/// <summary>
/// Handles SWAG updates operations for Release Trains
/// </summary>
public class SwagUpdatesHandler : ICommandHandler
{
    private readonly ServiceContainer _services;
    private readonly ILogger<SwagUpdatesHandler> _logger;

    public string CommandName => "SWAG Updates";

    public SwagUpdatesHandler(ServiceContainer services, ILogger<SwagUpdatesHandler> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool ShouldExecute(CommandLineOptions options) => options.SwagUpdates;

    public async Task<CommandResult> ExecuteAsync(IEnumerable<WorkItem> workItems, CommandLineOptions options)
    {
        try
        {
            const int separatorWidth = 60;

            if (!options.Quiet)
            {
                _logger.LogInformation("Starting SWAG updates on Release Trains in area path: {AreaPath}", options.AreaPath);
                Console.WriteLine("\n" + "=".PadRight(separatorWidth, '='));
                Console.WriteLine("PROCESSING SWAG UPDATES");
                Console.WriteLine("=".PadRight(separatorWidth, '='));
            }

            var workItemsList = workItems.ToList();
            var releaseTrains = workItemsList.Where(w => w.WorkItemType == "Release Train").ToList();
            var features = workItemsList.Where(w => w.WorkItemType == "Feature").ToList();

            if (!releaseTrains.Any())
            {
                var message = "No Release Trains found in the specified area path";
                _logger.LogWarning(message);
                Console.WriteLine(message + ".");
                return CommandResult.FailureResult(message);
            }

            _logger.LogInformation("Found {ReleaseTrainCount} Release Trains and {FeatureCount} Features to process",
                releaseTrains.Count, features.Count);

            var (updatedCount, warningCount) = await ProcessReleaseTrains(releaseTrains, options);

            DisplaySwagUpdatesSummary(releaseTrains.Count, updatedCount, warningCount, options, separatorWidth); return CommandResult.SuccessResult($"SWAG updates completed - Updated: {updatedCount}, Warnings: {warningCount}");
        }
        catch (Exception ex)
        {
            var error = _services.ErrorHandler.HandleException(ex, new Dictionary<string, object>
            {
                ["Operation"] = "SWAG Updates",
                ["WorkItemCount"] = workItems.Count(),
                ["AreaPath"] = options.AreaPath ?? "Unknown",
                ["Options"] = options
            });

            _logger.LogError(ex, "Error during SWAG updates: {ErrorCode}", error.Code);

            // Display user-friendly error message
            Console.WriteLine($"\n❌ {error.UserFriendlyMessage}");
            if (error.RecoveryActions.Any())
            {
                Console.WriteLine($"💡 {string.Join("\n💡 ", error.RecoveryActions)}");
            }

            return CommandResult.FailureResult(error.UserFriendlyMessage);
        }
    }

    private async Task<(int updatedCount, int warningCount)> ProcessReleaseTrains(List<WorkItem> releaseTrains, CommandLineOptions options)
    {
        int updatedCount = 0;
        int warningCount = 0;

        foreach (var releaseTrain in releaseTrains)
        {
            try
            {
                var result = await ProcessSingleReleaseTrain(releaseTrain, options);
                if (result.Updated) updatedCount++;
                if (result.HasWarning) warningCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SWAG updates for Release Train {Id}", releaseTrain.Id);
                Console.WriteLine($"❌ Error processing Release Train #{releaseTrain.Id}: {ex.Message}");
            }
        }

        return (updatedCount, warningCount);
    }

    private async Task<ReleaseTrainProcessResult> ProcessSingleReleaseTrain(WorkItem releaseTrain, CommandLineOptions options)
    {
        // Get the Release Train with its relations
        var releaseTrainWithRelations = await _services.AzureDevOps.GetWorkItemWithRelationsAsync(releaseTrain.Id);

        if (releaseTrainWithRelations?.Relations == null)
        {
            _logger.LogWarning("Release Train #{Id} has no relations - skipping", releaseTrain.Id);
            Console.WriteLine($"⚠️  Release Train #{releaseTrain.Id} has no relations - skipping");
            return ReleaseTrainProcessResult.Skipped();
        }

        // Get related feature IDs
        var relatedFeatureIds = GetRelatedFeatureIds(releaseTrainWithRelations);

        if (!relatedFeatureIds.Any())
        {
            _logger.LogWarning("Release Train #{Id} '{Title}' has no related Features - skipping", releaseTrain.Id, releaseTrain.Title);
            Console.WriteLine($"⚠️  Release Train #{releaseTrain.Id} '{releaseTrain.Title}' has no related Features - skipping");
            return ReleaseTrainProcessResult.Skipped();
        }

        // Get the actual feature work items
        var relatedFeatures = await GetRelatedFeatures(relatedFeatureIds);

        if (!relatedFeatures.Any())
        {
            _logger.LogWarning("Release Train #{Id} '{Title}' has no valid Feature relations - skipping", releaseTrain.Id, releaseTrain.Title);
            Console.WriteLine($"⚠️  Release Train #{releaseTrain.Id} '{releaseTrain.Title}' has no valid Feature relations - skipping");
            return ReleaseTrainProcessResult.Skipped();
        }

        // Calculate SWAG and determine update action
        var swagInfo = CalculateSwagInfo(relatedFeatures, releaseTrain);
        DisplayReleaseTrainInfo(releaseTrain, swagInfo);

        return await UpdateReleaseTrainSwag(releaseTrain, swagInfo, options);
    }

    private List<int> GetRelatedFeatureIds(WorkItem releaseTrainWithRelations)
    {
        return releaseTrainWithRelations.Relations
            .Where(r => r.Rel == "System.LinkTypes.Related" ||
                       r.Rel == "System.LinkTypes.Hierarchy-Forward" ||
                       r.Rel == "System.LinkTypes.Hierarchy-Reverse")
            .Select(r => r.GetRelatedWorkItemId())
            .Where(id => id > 0)
            .ToList();
    }

    private async Task<List<WorkItem>> GetRelatedFeatures(List<int> featureIds)
    {
        var relatedFeatures = new List<WorkItem>();

        foreach (var featureId in featureIds)
        {
            var feature = await _services.AzureDevOps.GetWorkItemByIdAsync(featureId);
            if (feature != null && feature.WorkItemType == "Feature")
            {
                relatedFeatures.Add(feature);
            }
        }

        return relatedFeatures;
    }

    private SwagCalculationInfo CalculateSwagInfo(List<WorkItem> relatedFeatures, WorkItem releaseTrain)
    {
        var totalSwag = relatedFeatures.Where(f => f.Swag.HasValue).Sum(f => f.Swag!.Value);
        var featuresWithSwag = relatedFeatures.Count(f => f.Swag.HasValue);
        var featuresWithoutSwag = relatedFeatures.Count - featuresWithSwag;

        var isAutoGenerated = !string.IsNullOrEmpty(releaseTrain.Tags) &&
            releaseTrain.Tags.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Any(tag => tag.Trim().Equals("auto-generated", StringComparison.OrdinalIgnoreCase));

        var currentSwagFromStatusNotes = _services.SwagService.ExtractSwagFromDescription(releaseTrain.StatusNotes);

        return new SwagCalculationInfo
        {
            TotalSwag = totalSwag,
            FeaturesWithSwag = featuresWithSwag,
            FeaturesWithoutSwag = featuresWithoutSwag,
            IsAutoGenerated = isAutoGenerated,
            CurrentSwagFromStatusNotes = currentSwagFromStatusNotes,
            RelatedFeaturesCount = relatedFeatures.Count
        };
    }

    private void DisplayReleaseTrainInfo(WorkItem releaseTrain, SwagCalculationInfo swagInfo)
    {
        _logger.LogInformation("Processing Release Train #{Id}: '{Title}' - Auto-generated: {IsAutoGenerated}, Related Features: {RelatedCount} ({FeaturesWithSwag} with SWAG), Current RT SWAG: {CurrentSwag}, Calculated SWAG: {CalculatedSwag}",
            releaseTrain.Id, releaseTrain.Title, swagInfo.IsAutoGenerated, swagInfo.RelatedFeaturesCount, swagInfo.FeaturesWithSwag, swagInfo.CurrentSwagFromStatusNotes?.ToString() ?? "Not set", swagInfo.TotalSwag);

        Console.WriteLine($"\n📊 Release Train #{releaseTrain.Id}: '{releaseTrain.Title}'");
        Console.WriteLine($"   Auto-generated: {(swagInfo.IsAutoGenerated ? "Yes" : "No")}");
        Console.WriteLine($"   Related Features: {swagInfo.RelatedFeaturesCount} ({swagInfo.FeaturesWithSwag} with SWAG, {swagInfo.FeaturesWithoutSwag} without)");
        Console.WriteLine($"   Current RT SWAG (from status notes): {swagInfo.CurrentSwagFromStatusNotes?.ToString() ?? "Not set"}");
        Console.WriteLine($"   Calculated SWAG: {swagInfo.TotalSwag}");

        if (swagInfo.FeaturesWithoutSwag > 0)
        {
            _logger.LogWarning("Release Train #{Id} has {Count} Features with no SWAG value", releaseTrain.Id, swagInfo.FeaturesWithoutSwag);
            Console.WriteLine($"   ⚠️  Warning: {swagInfo.FeaturesWithoutSwag} Features have no SWAG value");
        }
    }

    private async Task<ReleaseTrainProcessResult> UpdateReleaseTrainSwag(WorkItem releaseTrain, SwagCalculationInfo swagInfo, CommandLineOptions options)
    {
        if (swagInfo.IsAutoGenerated || options.SwagUpdatesAll)
        {
            // Update SWAG in status notes
            var updateReason = options.SwagUpdatesAll ? "ALL mode" : "auto-generated";
            _logger.LogInformation("Updating Release Train #{Id} SWAG in status notes to {NewSwag} (was {OldSwag}) - {Reason}",
                releaseTrain.Id, swagInfo.TotalSwag, swagInfo.CurrentSwagFromStatusNotes?.ToString() ?? "unset", updateReason);
            Console.WriteLine($"   🔄 Updating SWAG in status notes to {swagInfo.TotalSwag} (was {swagInfo.CurrentSwagFromStatusNotes?.ToString() ?? "unset"}) - {updateReason}"); await _services.AzureDevOps.UpdateWorkItemStatusNotesWithSwagAsync(releaseTrain.Id, swagInfo.TotalSwag, releaseTrain.StatusNotes);
            return ReleaseTrainProcessResult.UpdatedResult();
        }
        else
        {
            // Check for mismatches and show warnings
            if (swagInfo.CurrentSwagFromStatusNotes != swagInfo.TotalSwag)
            {
                _logger.LogWarning("Release Train #{Id} SWAG mismatch: status notes ({CurrentSwag}) vs calculated ({CalculatedSwag})",
                    releaseTrain.Id, swagInfo.CurrentSwagFromStatusNotes?.ToString() ?? "unset", swagInfo.TotalSwag);
                Console.WriteLine($"   ⚠️  WARNING: Release Train SWAG in status notes ({swagInfo.CurrentSwagFromStatusNotes?.ToString() ?? "unset"}) does not match sum of Features ({swagInfo.TotalSwag})");
                return ReleaseTrainProcessResult.WarningResult();
            }
            else
            {
                _logger.LogInformation("Release Train #{Id} SWAG matches calculated value: {Swag}", releaseTrain.Id, swagInfo.TotalSwag);
                Console.WriteLine($"   ✅ SWAG matches calculated value");
                return ReleaseTrainProcessResult.NoAction();
            }
        }
    }

    private void DisplaySwagUpdatesSummary(int totalProcessed, int updatedCount, int warningCount, CommandLineOptions options, int separatorWidth)
    {
        _logger.LogInformation("SWAG updates completed - Processed: {ProcessedCount}, Updated: {UpdatedCount}, Warnings: {WarningCount}",
            totalProcessed, updatedCount, warningCount);

        Console.WriteLine("\n" + "=".PadRight(separatorWidth, '='));
        Console.WriteLine("SWAG UPDATES SUMMARY");
        Console.WriteLine("=".PadRight(separatorWidth, '='));
        Console.WriteLine($"Release Trains processed: {totalProcessed}");

        if (options.SwagUpdatesAll)
        {
            Console.WriteLine($"Release Trains updated (ALL mode): {updatedCount}");
        }
        else
        {
            Console.WriteLine($"Auto-generated Release Trains updated: {updatedCount}");
            Console.WriteLine($"Manual Release Trains with SWAG mismatches: {warningCount}");
        }

        Console.WriteLine("=".PadRight(separatorWidth, '='));
        Console.WriteLine();
    }

    private class SwagCalculationInfo
    {
        public double TotalSwag { get; set; }
        public int FeaturesWithSwag { get; set; }
        public int FeaturesWithoutSwag { get; set; }
        public bool IsAutoGenerated { get; set; }
        public double? CurrentSwagFromStatusNotes { get; set; }
        public int RelatedFeaturesCount { get; set; }
    }
    private class ReleaseTrainProcessResult
    {
        public bool Updated { get; set; }
        public bool HasWarning { get; set; }

        public static ReleaseTrainProcessResult UpdatedResult() => new() { Updated = true };
        public static ReleaseTrainProcessResult WarningResult() => new() { HasWarning = true };
        public static ReleaseTrainProcessResult NoAction() => new();
        public static ReleaseTrainProcessResult Skipped() => new();
    }
}

