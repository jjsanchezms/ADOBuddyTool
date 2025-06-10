using CreateRoadmapADO.Interfaces;
using CreateRoadmapADO.Models;
using Microsoft.Extensions.Logging;

namespace CreateRoadmapADO.Services.HygieneChecks;

/// <summary>
/// Evaluates the quality and currency of status documentation for Release Trains.
/// 
/// This check validates Release Train Description Quality by ensuring the Release Train has adequate status notes:
/// - Checks for presence of description
/// - Validates minimum length (>20 characters) for meaningful content
/// 
/// This helps ensure project stakeholders have sufficient information for decision-making
/// and status reporting.
/// </summary>
public class StatusNotesDocumentationCheck : IHygieneCheck
{
    private readonly ILogger<StatusNotesDocumentationCheck> _logger;

    public string CheckName => "Status Notes Currency";
    public string CheckDescription => "Check if Release Train has adequate status notes/description";

    public StatusNotesDocumentationCheck(ILogger<StatusNotesDocumentationCheck> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }    public Task<IEnumerable<HygieneCheckResult>> PerformCheckAsync(HygieneCheckContext context, CancellationToken cancellationToken = default)
    {
        var releaseTrain = context.WorkItem;

        _logger.LogDebug("Checking status documentation for Release Train {Id}: {Title}", releaseTrain.Id, releaseTrain.Title);

        // Check Release Train description
        var hasDescription = !string.IsNullOrWhiteSpace(releaseTrain.Description);
        var descriptionLength = releaseTrain.Description?.Trim().Length ?? 0;

        var releaseTrainResult = new HygieneCheckResult
        {
            CheckName = CheckName,
            Passed = hasDescription && descriptionLength > 20,
            Severity = hasDescription && descriptionLength > 20 ? HygieneCheckSeverity.Info : HygieneCheckSeverity.Warning,
            Description = CheckDescription,
            Details = hasDescription 
                ? $"Description present ({descriptionLength} characters)"
                : "No description provided",
            WorkItemId = releaseTrain.Id,
            WorkItemTitle = releaseTrain.Title,
            WorkItemUrl = HygieneCheckContext.GenerateWorkItemUrl(releaseTrain.Id),
            Recommendation = hasDescription && descriptionLength > 20
                ? "Status documentation looks adequate"
                : "Consider adding detailed status notes or description to provide context and current status"
        };

        return Task.FromResult<IEnumerable<HygieneCheckResult>>(new[] { releaseTrainResult });
    }
}
