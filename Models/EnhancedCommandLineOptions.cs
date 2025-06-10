using System.ComponentModel.DataAnnotations;

namespace CreateRoadmapADO.Models;

/// <summary>
/// Enhanced command line options with validation
/// </summary>
public class EnhancedCommandLineOptions
{
    /// <summary>
    /// Azure DevOps area path to filter work items
    /// </summary>
    [Required(ErrorMessage = "Area path is required")]
    public string AreaPath { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of Feature work items to retrieve
    /// </summary>
    [Range(1, 10000, ErrorMessage = "Limit must be between 1 and 10,000")]
    public int Limit { get; set; } = 100;

    /// <summary>
    /// Output format for the results
    /// </summary>
    public OutputFormat OutputFormat { get; set; } = OutputFormat.Console;

    /// <summary>
    /// Output file path (optional)
    /// </summary>
    public string? OutputFile { get; set; }

    /// <summary>
    /// Output directory for generated files
    /// </summary>
    public string OutputDirectory { get; set; } = "output";

    /// <summary>
    /// Run hygiene checks in addition to roadmap generation
    /// </summary>
    public bool RunHygieneChecks { get; set; } = false;

    /// <summary>
    /// Run only hygiene checks (skip roadmap generation)
    /// </summary>
    public bool HygieneChecksOnly { get; set; } = false;

    /// <summary>
    /// Preview mode - show what would be done without making changes
    /// </summary>
    public bool DryRun { get; set; } = false;

    /// <summary>
    /// Configuration file path (optional)
    /// </summary>
    public string? ConfigFile { get; set; }

    /// <summary>
    /// Logging verbosity level
    /// </summary>
    public LogVerbosity LogLevel { get; set; } = LogVerbosity.Information;

    /// <summary>
    /// API timeout in seconds
    /// </summary>
    [Range(5, 300, ErrorMessage = "Timeout must be between 5 and 300 seconds")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Work item types to filter (optional)
    /// </summary>
    public string[]? WorkItemTypes { get; set; }

    /// <summary>
    /// Work item states to filter (optional)
    /// </summary>
    public string[]? WorkItemStates { get; set; }

    /// <summary>
    /// Filter items modified after this date (optional)
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Filter items modified before this date (optional)
    /// </summary>
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Validates the options and returns validation results
    /// </summary>
    public ParameterValidationResult Validate()
    {
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var context = new ValidationContext(this);
        
        // Run data annotation validation
        Validator.TryValidateObject(this, context, results, true);
        
        // Custom business logic validation
        ValidateBusinessRules(results);
        
        return new ParameterValidationResult(results);
    }

    private void ValidateBusinessRules(List<System.ComponentModel.DataAnnotations.ValidationResult> results)
    {
        // Validate area path format
        if (!string.IsNullOrEmpty(AreaPath) && !IsValidAreaPath(AreaPath))
        {
            results.Add(new System.ComponentModel.DataAnnotations.ValidationResult(
                "Area path must be in format 'Project\\Team' or 'Project\\Team\\SubTeam'",
                new[] { nameof(AreaPath) }));
        }

        // Validate output file path
        if (!string.IsNullOrEmpty(OutputFile) && !IsValidFilePath(OutputFile))
        {
            results.Add(new System.ComponentModel.DataAnnotations.ValidationResult(
                "Output file path is invalid or directory is not writable",
                new[] { nameof(OutputFile) }));
        }

        // Validate date range
        if (FromDate.HasValue && ToDate.HasValue && FromDate > ToDate)
        {
            results.Add(new System.ComponentModel.DataAnnotations.ValidationResult(
                "From date must be earlier than to date",
                new[] { nameof(FromDate), nameof(ToDate) }));
        }

        // Validate configuration file
        if (!string.IsNullOrEmpty(ConfigFile) && !File.Exists(ConfigFile))
        {
            results.Add(new System.ComponentModel.DataAnnotations.ValidationResult(
                "Configuration file does not exist",
                new[] { nameof(ConfigFile) }));
        }

        // Business rule: hygiene-only shouldn't have output format other than console/summary
        if (HygieneChecksOnly && OutputFormat == OutputFormat.Json)
        {
            results.Add(new System.ComponentModel.DataAnnotations.ValidationResult(
                "JSON output is not supported in hygiene-only mode",
                new[] { nameof(OutputFormat), nameof(HygieneChecksOnly) }));
        }
    }

    private static bool IsValidAreaPath(string areaPath)
    {
        // Basic validation for Azure DevOps area path format
        return areaPath.Contains('\\') && !areaPath.StartsWith('\\') && !areaPath.EndsWith('\\');
    }

    private static bool IsValidFilePath(string filePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            return string.IsNullOrEmpty(directory) || Directory.Exists(directory);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Available output formats
/// </summary>
public enum OutputFormat
{
    Console,
    Json,
    Csv,
    Summary
}

/// <summary>
/// Logging verbosity levels
/// </summary>
public enum LogVerbosity
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Parameter validation result container
/// </summary>
public class ParameterValidationResult
{
    public bool IsValid => !Errors.Any();
    public List<System.ComponentModel.DataAnnotations.ValidationResult> Errors { get; }

    public ParameterValidationResult(List<System.ComponentModel.DataAnnotations.ValidationResult> errors)
    {
        Errors = errors ?? new List<System.ComponentModel.DataAnnotations.ValidationResult>();
    }

    public void DisplayErrors()
    {
        if (!IsValid)
        {
            Console.WriteLine("❌ Parameter validation errors:");
            foreach (var error in Errors)
            {
                Console.WriteLine($"   • {error.ErrorMessage}");
            }
            Console.WriteLine();
        }
    }
}
