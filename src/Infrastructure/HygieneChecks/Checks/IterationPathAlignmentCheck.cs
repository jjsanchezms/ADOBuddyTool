using ADOBuddyTool.Infrastructure.AzureDevOps.Interfaces;
using ADOBuddyTool.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ADOBuddyTool.Infrastructure.HygieneChecks.Checks;

/// <summary>
/// Validates that Release Train iteration paths align with their related/child Feature iteration paths.
/// 
/// This check ensures project planning consistency by verifying that Release Trains and their
/// associated Features are planned for compatible time periods. The validation allows for:
/// - Exact iteration path matches
/// - Hierarchical path relationships (parent/child iterations)
/// - Case-insensitive comparison
/// 
/// Severity levels:
/// - Error: Release Train has no iteration path assigned
/// - Warning: Iteration paths don't align
/// - Info: Iteration paths are properly aligned
/// </summary>
public class IterationPathAlignmentCheck : IHygieneCheck
{
    private readonly ILogger<IterationPathAlignmentCheck> _logger;

    public string CheckName => "Iteration Path Alignment";
    public string CheckDescription => "Check if Release Train iteration path aligns with related features";

    public IterationPathAlignmentCheck(ILogger<IterationPathAlignmentCheck> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IEnumerable<HygieneCheckResult>> PerformCheckAsync(HygieneCheckContext context, CancellationToken cancellationToken = default)
    {
        var releaseTrain = context.WorkItem;
        var relatedFeatures = context.RelatedFeatures;

        _logger.LogDebug("Checking iteration path alignment for Release Train {Id}: {Title}", releaseTrain.Id, releaseTrain.Title);

        var releaseTrainIterationPath = releaseTrain.IterationPath?.Trim();
        var featureIterationPaths = relatedFeatures
            .Where(f => !string.IsNullOrWhiteSpace(f.IterationPath))
            .Select(f => f.IterationPath!.Trim())
            .Distinct()
            .ToList();

        // Check if Release Train has an iteration path assigned
        if (string.IsNullOrWhiteSpace(releaseTrainIterationPath))
        {
            var result = new HygieneCheckResult
            {
                CheckName = CheckName,
                Passed = false,
                Severity = HygieneCheckSeverity.Error,
                Description = "Check if Release Train has iteration path set",
                Details = "Release Train does not have an iteration path assigned",
                WorkItemId = releaseTrain.Id,
                WorkItemTitle = releaseTrain.Title,
                WorkItemUrl = HygieneCheckContext.GenerateWorkItemUrl(releaseTrain.Id),
                Recommendation = "Set an appropriate iteration path for this Release Train"
            };

            return Task.FromResult<IEnumerable<HygieneCheckResult>>(new[] { result });
        }

        // Check if iteration paths align
        var hasMatchingIteration = featureIterationPaths.Any(fp =>
            string.Equals(fp, releaseTrainIterationPath, StringComparison.OrdinalIgnoreCase) ||
            fp.StartsWith(releaseTrainIterationPath, StringComparison.OrdinalIgnoreCase) ||
            releaseTrainIterationPath.StartsWith(fp, StringComparison.OrdinalIgnoreCase));

        var alignmentResult = new HygieneCheckResult
        {
            CheckName = CheckName,
            Passed = hasMatchingIteration,
            Severity = hasMatchingIteration ? HygieneCheckSeverity.Info : HygieneCheckSeverity.Warning,
            Description = CheckDescription,
            Details = hasMatchingIteration
                ? $"Release Train iteration '{releaseTrainIterationPath}' aligns with feature iterations"
                : $"Release Train iteration '{releaseTrainIterationPath}' does not match any feature iterations: {string.Join(", ", featureIterationPaths)}",
            WorkItemId = releaseTrain.Id,
            WorkItemTitle = releaseTrain.Title,
            WorkItemUrl = HygieneCheckContext.GenerateWorkItemUrl(releaseTrain.Id),
            Recommendation = hasMatchingIteration
                ? "Iteration path alignment is good"
                : "Consider aligning Release Train iteration path with related features or vice versa"
        };

        return Task.FromResult<IEnumerable<HygieneCheckResult>>(new[] { alignmentResult });
    }
}

