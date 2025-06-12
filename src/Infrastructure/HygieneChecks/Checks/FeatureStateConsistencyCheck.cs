using CreateRoadmapADO.Infrastructure.AzureDevOps.Interfaces;
using CreateRoadmapADO.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CreateRoadmapADO.Infrastructure.HygieneChecks.Checks;

/// <summary>
/// Analyzes state consistency between Release Trains and their related/child Features.
/// 
/// This check identifies potential project management issues by detecting misaligned states:
/// 
/// State Analysis:
/// - Categorizes Features into New, Active, and Completed buckets
/// - Calculates completion rate across all related Features
/// - Identifies inconsistencies with Release Train state
/// 
/// Consistency Rules:
/// - Release Trains marked as "Done/Closed/Completed" should have ≥80% Features completed
/// - Release Trains in "New/Proposed" state shouldn't have active or completed Features
/// - Warns when Release Train state doesn't reflect actual Feature progress
/// 
/// This helps project managers identify:
/// - Release Trains marked complete prematurely
/// - Stale Release Train states that need updating
/// - Potential planning or tracking issues
/// </summary>
public class FeatureStateConsistencyCheck : IHygieneCheck
{
    private readonly ILogger<FeatureStateConsistencyCheck> _logger;

    public string CheckName => "Feature State Consistency";
    public string CheckDescription => "Check consistency between Release Train and feature states";

    public FeatureStateConsistencyCheck(ILogger<FeatureStateConsistencyCheck> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IEnumerable<HygieneCheckResult>> PerformCheckAsync(HygieneCheckContext context, CancellationToken cancellationToken = default)
    {
        var releaseTrain = context.WorkItem;
        var relatedFeatures = context.RelatedFeatures;

        _logger.LogDebug("Checking feature state consistency for Release Train {Id}: {Title}", releaseTrain.Id, releaseTrain.Title);

        // Skip if no features
        if (!relatedFeatures.Any())
        {
            return Task.FromResult<IEnumerable<HygieneCheckResult>>(Array.Empty<HygieneCheckResult>());
        }

        var releaseTrainState = releaseTrain.State?.ToLowerInvariant();
        var featureStates = relatedFeatures
            .Select(f => f.State?.ToLowerInvariant())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        // Check for inconsistent states
        var completedFeatures = featureStates.Count(s => s == "done" || s == "closed" || s == "completed");
        var activeFeatures = featureStates.Count(s => s == "active" || s == "committed" || s == "in progress");
        var newFeatures = featureStates.Count(s => s == "new" || s == "proposed" || s == "approved");

        var totalFeatures = featureStates.Count;
        var completionRate = totalFeatures > 0 ? (double)completedFeatures / totalFeatures * 100 : 0;

        // Determine if Release Train state is consistent with feature states
        var isConsistent = true;
        var inconsistencyDetails = new List<string>();

        if (releaseTrainState == "done" || releaseTrainState == "closed" || releaseTrainState == "completed")
        {
            if (completionRate < 80)
            {
                isConsistent = false;
                inconsistencyDetails.Add($"Release Train marked as complete but only {completionRate:F1}% of features are done");
            }
        }
        else if (releaseTrainState == "new" || releaseTrainState == "proposed")
        {
            if (activeFeatures > 0 || completedFeatures > 0)
            {
                isConsistent = false;
                inconsistencyDetails.Add($"Release Train is in '{releaseTrain.State}' state but has {activeFeatures + completedFeatures} features that are active or complete");
            }
        }

        var result = new HygieneCheckResult
        {
            CheckName = CheckName,
            Passed = isConsistent,
            Severity = isConsistent ? HygieneCheckSeverity.Info : HygieneCheckSeverity.Warning,
            Description = CheckDescription,
            Details = isConsistent
                ? $"State consistency is good. RT: '{releaseTrain.State}', Features: {completedFeatures} done, {activeFeatures} active, {newFeatures} new"
                : string.Join("; ", inconsistencyDetails),
            WorkItemId = releaseTrain.Id,
            WorkItemTitle = releaseTrain.Title,
            WorkItemUrl = HygieneCheckContext.GenerateWorkItemUrl(releaseTrain.Id),
            Recommendation = isConsistent
                ? "State consistency looks good"
                : "Review Release Train state to ensure it reflects the actual progress of related features"
        };

        return Task.FromResult<IEnumerable<HygieneCheckResult>>(new[] { result });
    }
}

