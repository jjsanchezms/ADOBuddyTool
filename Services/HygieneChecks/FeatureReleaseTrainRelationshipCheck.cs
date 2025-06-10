using CreateRoadmapADO.Interfaces;
using CreateRoadmapADO.Models;
using Microsoft.Extensions.Logging;

namespace CreateRoadmapADO.Services.HygieneChecks;

/// <summary>
/// Validates Features that have Related links to one or more Release Trains.
/// 
/// This check ensures proper relationship management by identifying Features that may be:
/// - Improperly linked to Release Trains that don't exist or are inactive
/// - Linked to Release Trains that are in completed states
/// 
/// Validation Rules:
/// - Info: Feature properly linked to one or more Release Trains
/// - Warning: Feature linked to Release Trains that are in completed states  
/// - Warning: Feature has Related links that don't point to valid Release Trains
/// 
/// This helps maintain accurate relationship tracking for Features.
/// </summary>
public class FeatureReleaseTrainRelationshipCheck : IHygieneCheck
{
    private readonly ILogger<FeatureReleaseTrainRelationshipCheck> _logger;

    public string CheckName => "Feature Release Train Links";
    public string CheckDescription => "Check Feature relationships with Release Trains via Related links";

    public FeatureReleaseTrainRelationshipCheck(ILogger<FeatureReleaseTrainRelationshipCheck> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IEnumerable<HygieneCheckResult>> PerformCheckAsync(HygieneCheckContext context, CancellationToken cancellationToken = default)
    {
        var feature = context.WorkItem;
        var allReleaseTrains = context.AllReleaseTrains;

        _logger.LogDebug("Checking Release Train relationships for Feature {Id}: {Title}", feature.Id, feature.Title);

        // Find Related links to Release Trains
        var relatedReleaseTrainIds = feature.Relations?
            .Where(r => r.Rel == "System.LinkTypes.Related")
            .Select(r => r.GetRelatedWorkItemId())
            .Where(id => id > 0)
            .ToList() ?? new List<int>();

        if (!relatedReleaseTrainIds.Any())
        {
            // Feature has no Related links to Release Trains - this might be expected behavior
            return Task.FromResult<IEnumerable<HygieneCheckResult>>(Array.Empty<HygieneCheckResult>());
        }

        // Validate the related Release Trains exist in our collection
        var validReleaseTrains = allReleaseTrains
            .Where(rt => relatedReleaseTrainIds.Contains(rt.Id))
            .ToList();

        var relatedCount = relatedReleaseTrainIds.Count;
        var validCount = validReleaseTrains.Count;        // Check if Feature has valid Release Train links
        var hasValidLinks = validCount > 0;

        var severity = HygieneCheckSeverity.Info;
        var passed = true;
        var details = "";
        var recommendation = "";        if (hasValidLinks)
        {
            if (relatedCount > 1)
            {
                details = $"Feature has Related links to {relatedCount} Release Trains: {string.Join(", ", validReleaseTrains.Select(rt => $"#{rt.Id} ({rt.Title})"))}";
                recommendation = "Multiple Release Train relationships are acceptable";
            }
            else
            {
                var releaseTrain = validReleaseTrains.First();
                details = $"Feature has Related link to Release Train #{releaseTrain.Id} ({releaseTrain.Title})";
                recommendation = "Release Train relationship looks appropriate";
            }
            
            // Additional check: verify Release Train state is reasonable for all linked Release Trains
            var completedReleaseTrains = validReleaseTrains
                .Where(rt => new[] { "done", "closed", "completed" }.Contains(rt.State?.ToLowerInvariant()))
                .ToList();
                
            if (completedReleaseTrains.Any())
            {
                severity = HygieneCheckSeverity.Warning;
                passed = false;
                var completedNames = string.Join(", ", completedReleaseTrains.Select(rt => $"#{rt.Id} ({rt.Title})"));
                details += $". Note: Related Release Train(s) in completed state: {completedNames}";
                recommendation = "Consider if this Feature should be linked to completed Release Train(s), or if the Release Train state(s) need updating";
            }
        }
        else
        {
            // Has links but none are valid Release Trains in our collection
            severity = HygieneCheckSeverity.Warning;
            passed = false;
            details = $"Feature has {relatedCount} Related links but none appear to be valid Release Trains";
            recommendation = "Verify the Related links point to valid Release Train work items";
        }

        var result = new HygieneCheckResult
        {
            CheckName = CheckName,
            Passed = passed,
            Severity = severity,
            Description = CheckDescription,
            Details = details,
            WorkItemId = feature.Id,
            WorkItemTitle = feature.Title,
            WorkItemUrl = HygieneCheckContext.GenerateWorkItemUrl(feature.Id),
            Recommendation = recommendation
        };

        return Task.FromResult<IEnumerable<HygieneCheckResult>>(new[] { result });
    }
}
