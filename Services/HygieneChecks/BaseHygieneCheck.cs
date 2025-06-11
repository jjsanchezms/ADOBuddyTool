using CreateRoadmapADO.Interfaces;
using CreateRoadmapADO.Models;
using Microsoft.Extensions.Logging;

namespace CreateRoadmapADO.Services.HygieneChecks;

/// <summary>
/// Base class for hygiene checks to reduce boilerplate code
/// </summary>
public abstract class BaseHygieneCheck : IHygieneCheck
{
    protected readonly ILogger _logger;

    protected BaseHygieneCheck(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public abstract string CheckName { get; }
    public abstract string CheckDescription { get; }

    public abstract Task<IEnumerable<HygieneCheckResult>> PerformCheckAsync(HygieneCheckContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Helper method to create a standard hygiene check result
    /// </summary>
    /// <param name="workItem">The work item being checked</param>
    /// <param name="passed">Whether the check passed</param>
    /// <param name="severity">Severity level</param>
    /// <param name="details">Details about the check result</param>
    /// <param name="recommendation">Recommendation for fixing issues</param>
    /// <returns>Configured hygiene check result</returns>
    protected HygieneCheckResult CreateResult(WorkItem workItem, bool passed, HygieneCheckSeverity severity, string details, string recommendation)
    {
        return new HygieneCheckResult
        {
            CheckName = CheckName,
            Passed = passed,
            Severity = severity,
            Description = CheckDescription,
            Details = details,
            WorkItemId = workItem.Id,
            WorkItemTitle = workItem.Title,
            WorkItemUrl = HygieneCheckContext.GenerateWorkItemUrl(workItem.Id),
            Recommendation = recommendation
        };
    }
}
