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
public class StatusNotesDocumentationCheck : BaseHygieneCheck
{
    public override string CheckName => "Status Notes Currency";
    public override string CheckDescription => "Check if Release Train has adequate status notes/description";

    public StatusNotesDocumentationCheck(ILogger<StatusNotesDocumentationCheck> logger) : base(logger)
    {
    }
    public override Task<IEnumerable<HygieneCheckResult>> PerformCheckAsync(HygieneCheckContext context, CancellationToken cancellationToken = default)
    {
        var releaseTrain = context.WorkItem;

        _logger.LogDebug("Checking status documentation for Release Train {Id}: {Title}", releaseTrain.Id, releaseTrain.Title);

        // Simple validation logic
        var hasDescription = !string.IsNullOrWhiteSpace(releaseTrain.Description);
        var descriptionLength = releaseTrain.Description?.Trim().Length ?? 0;
        var isAdequate = hasDescription && descriptionLength > 20;

        var details = hasDescription
            ? $"Description present ({descriptionLength} characters)"
            : "No description provided";

        var recommendation = isAdequate
            ? "Status documentation looks adequate"
            : "Consider adding detailed status notes or description to provide context and current status";

        var result = CreateResult(
            releaseTrain,
            isAdequate,
            isAdequate ? HygieneCheckSeverity.Info : HygieneCheckSeverity.Warning,
            details,
            recommendation);

        return Task.FromResult<IEnumerable<HygieneCheckResult>>(new[] { result });
    }
}
