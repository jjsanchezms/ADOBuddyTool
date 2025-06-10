using CreateRoadmapADO.Interfaces;
using CreateRoadmapADO.Models;
using Microsoft.Extensions.Logging;

namespace CreateRoadmapADO.Services.HygieneChecks;

/// <summary>
/// Validates that Release Trains have an adequate number of related/child Features.
/// Empty Release Trains are flagged as warnings since they likely represent incomplete setup.
/// </summary>
public class ReleaseTrainFeatureCountCheck : IHygieneCheck
{
    private readonly ILogger<ReleaseTrainFeatureCountCheck> _logger;

    public string CheckName => "Release Train Feature Count";
    public string CheckDescription => "Check if Release Train has adequate number of related features";

    public ReleaseTrainFeatureCountCheck(ILogger<ReleaseTrainFeatureCountCheck> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IEnumerable<HygieneCheckResult>> PerformCheckAsync(HygieneCheckContext context, CancellationToken cancellationToken = default)
    {
        var releaseTrain = context.WorkItem;
        var relatedFeatures = context.RelatedFeatures;

        _logger.LogDebug("Checking feature count for Release Train {Id}: {Title}", releaseTrain.Id, releaseTrain.Title);

        var featureCount = relatedFeatures.Count;
        var hasAdequateFeatures = featureCount >= 1;

        var result = new HygieneCheckResult
        {
            CheckName = CheckName,
            Passed = hasAdequateFeatures,
            Severity = hasAdequateFeatures ? HygieneCheckSeverity.Info : HygieneCheckSeverity.Warning,
            Description = CheckDescription,
            Details = $"Release Train has {featureCount} related features",
            WorkItemId = releaseTrain.Id,
            WorkItemTitle = releaseTrain.Title,
            WorkItemUrl = HygieneCheckContext.GenerateWorkItemUrl(releaseTrain.Id),
            Recommendation = hasAdequateFeatures
                ? "Feature count looks appropriate"
                : "Release Train should have at least one related feature"
        };

        return Task.FromResult<IEnumerable<HygieneCheckResult>>(new[] { result });
    }
}
