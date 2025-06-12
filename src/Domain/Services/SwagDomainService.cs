using ADOBuddyTool.Domain.Entities;
using ADOBuddyTool.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ADOBuddyTool.Domain.Services;

/// <summary>
/// Domain service for SWAG (estimation) business operations
/// Encapsulates SWAG-related business logic and calculations
/// </summary>
public class SwagDomainService : ISwagDomainService
{
    private readonly ILogger<SwagDomainService> _logger;
    private static readonly Regex SwagPrefixRegex = new(@"^\[SWAG:\s*(\d+(?:\.\d+)?)\]", RegexOptions.Compiled);
    private static readonly Regex SwagCleanupRegex = new(@"^\[SWAG:\s*\d+(?:\.\d+)?\]", RegexOptions.Compiled);

    public SwagDomainService(ILogger<SwagDomainService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public double? ExtractSwagValue(WorkItem workItem)
    {
        if (workItem == null)
        {
            return null;
        }

        // First priority: explicit SWAG field
        if (workItem.Swag.HasValue)
        {
            _logger.LogDebug("SWAG extracted from field for work item {WorkItemId}: {SwagValue}",
                workItem.Id, workItem.Swag.Value);
            return workItem.Swag.Value;
        }

        // Second priority: SWAG prefix in status notes
        var swagFromStatusNotes = ExtractSwagFromStatusNotes(workItem.StatusNotes);
        if (swagFromStatusNotes.HasValue)
        {
            _logger.LogDebug("SWAG extracted from status notes for work item {WorkItemId}: {SwagValue}",
                workItem.Id, swagFromStatusNotes.Value);
            return swagFromStatusNotes.Value;
        }

        _logger.LogDebug("No SWAG value found for work item {WorkItemId}", workItem.Id);
        return null;
    }

    public string FormatSwagValue(double swagValue)
    {
        // Format to remove unnecessary decimal places
        return swagValue % 1 == 0 ? swagValue.ToString("F0") : swagValue.ToString("F1");
    }

    public string CreateSwagPrefixedStatusNotes(double swagValue, string existingStatusNotes)
    {
        var cleanStatusNotes = RemoveSwagPrefix(existingStatusNotes ?? string.Empty);
        var formattedSwag = FormatSwagValue(swagValue);

        var prefixedNotes = $"[SWAG: {formattedSwag}]{cleanStatusNotes}";

        _logger.LogDebug("Created SWAG-prefixed status notes: {PrefixedNotes}", prefixedNotes);
        return prefixedNotes;
    }

    public string RemoveSwagPrefix(string statusNotes)
    {
        if (string.IsNullOrEmpty(statusNotes))
        {
            return statusNotes ?? string.Empty;
        }

        var cleaned = SwagCleanupRegex.Replace(statusNotes, "").TrimStart();

        if (cleaned != statusNotes)
        {
            _logger.LogDebug("Removed SWAG prefix from status notes");
        }

        return cleaned;
    }

    public SwagValidationResult ValidateSwagConsistency(WorkItem workItem)
    {
        var result = new SwagValidationResult
        {
            IsConsistent = true,
            FieldValue = workItem.Swag,
            StatusNotesValue = ExtractSwagFromStatusNotes(workItem.StatusNotes)
        };

        // Check if both sources have values
        if (result.FieldValue.HasValue && result.StatusNotesValue.HasValue)
        {
            var difference = Math.Abs(result.FieldValue.Value - result.StatusNotesValue.Value);
            if (difference > 0.1) // Allow small rounding differences
            {
                result.IsConsistent = false;
                result.Issues.Add($"SWAG field value ({result.FieldValue}) differs from status notes value ({result.StatusNotesValue})");
                result.Severity = SwagValidationSeverity.Warning;
            }
        }
        else if (result.FieldValue.HasValue && !result.StatusNotesValue.HasValue)
        {
            result.Issues.Add("SWAG field has value but status notes prefix is missing");
            result.Severity = SwagValidationSeverity.Info;
        }
        else if (!result.FieldValue.HasValue && result.StatusNotesValue.HasValue)
        {
            result.Issues.Add("Status notes has SWAG prefix but SWAG field is empty");
            result.Severity = SwagValidationSeverity.Warning;
        }
        else if (workItem.WorkItemType == "Feature" && workItem.State != "Closed")
        {
            result.Issues.Add("Active feature lacks SWAG value");
            result.Severity = SwagValidationSeverity.Warning;
        }

        return result;
    }

    public bool IsSwagUpdateNeeded(double? currentSwag, double calculatedSwag, double tolerance = 0.1)
    {
        if (!currentSwag.HasValue)
        {
            _logger.LogDebug("SWAG update needed: no current value, calculated: {CalculatedSwag}", calculatedSwag);
            return true;
        }

        var difference = Math.Abs(currentSwag.Value - calculatedSwag);
        var updateNeeded = difference > tolerance;

        if (updateNeeded)
        {
            _logger.LogDebug("SWAG update needed: current {CurrentSwag}, calculated {CalculatedSwag}, difference {Difference}",
                currentSwag.Value, calculatedSwag, difference);
        }
        else
        {
            _logger.LogDebug("SWAG update not needed: current {CurrentSwag}, calculated {CalculatedSwag}, difference {Difference}",
                currentSwag.Value, calculatedSwag, difference);
        }

        return updateNeeded;
    }

    private double? ExtractSwagFromStatusNotes(string statusNotes)
    {
        if (string.IsNullOrEmpty(statusNotes))
        {
            return null;
        }

        var match = SwagPrefixRegex.Match(statusNotes);
        if (match.Success && double.TryParse(match.Groups[1].Value, out var swagValue))
        {
            return swagValue;
        }

        return null;
    }
}
