using Microsoft.Extensions.Logging;

namespace ADOBuddyTool.Application.ErrorHandling;

/// <summary>
/// Centralized error handling service that provides consistent error management across the application
/// </summary>
public interface IErrorHandler
{
    /// <summary>
    /// Handles an exception and returns a structured error
    /// </summary>
    /// <param name="ex">Exception to handle</param>
    /// <param name="context">Additional context information</param>
    /// <param name="userFriendlyMessage">Override user-friendly message</param>
    /// <returns>Structured application error</returns>
    ApplicationError HandleException(Exception ex, Dictionary<string, object>? context = null, string? userFriendlyMessage = null);

    /// <summary>
    /// Creates a business logic error
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="context">Additional context</param>
    /// <param name="severity">Error severity</param>
    /// <returns>Structured application error</returns>
    ApplicationError CreateBusinessError(string message, Dictionary<string, object>? context = null, ErrorSeverity severity = ErrorSeverity.Error);

    /// <summary>
    /// Creates a configuration error
    /// </summary>
    /// <param name="setting">Configuration setting name</param>
    /// <param name="issue">Description of the issue</param>
    /// <param name="suggestion">Suggested fix</param>
    /// <returns>Structured application error</returns>
    ApplicationError CreateConfigurationError(string setting, string issue, string suggestion);

    /// <summary>
    /// Creates a user input validation error
    /// </summary>
    /// <param name="parameterName">Name of the invalid parameter</param>
    /// <param name="providedValue">Value that was provided</param>
    /// <param name="expectedFormat">Expected format description</param>
    /// <returns>Structured application error</returns>
    ApplicationError CreateValidationError(string parameterName, string providedValue, string expectedFormat);

    /// <summary>
    /// Logs an error and displays it to the user
    /// </summary>
    /// <param name="error">Application error to log and display</param>
    void LogAndDisplayError(ApplicationError error);

    /// <summary>
    /// Determines if an error is recoverable
    /// </summary>
    /// <param name="error">Error to evaluate</param>
    /// <returns>True if the error allows continued operation</returns>
    bool IsRecoverable(ApplicationError error);
}

/// <summary>
/// Implementation of centralized error handling
/// </summary>
public class ErrorHandler : IErrorHandler
{
    private readonly ILogger<ErrorHandler> _logger;

    public ErrorHandler(ILogger<ErrorHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ApplicationError HandleException(Exception ex, Dictionary<string, object>? context = null, string? userFriendlyMessage = null)
    {
        var error = ApplicationError.FromException(ex, userFriendlyMessage);

        if (context != null)
        {
            foreach (var kvp in context)
            {
                error.Context[kvp.Key] = kvp.Value;
            }
        }

        // Add common recovery actions based on error category
        AddRecoveryActions(error);

        return error;
    }

    public ApplicationError CreateBusinessError(string message, Dictionary<string, object>? context = null, ErrorSeverity severity = ErrorSeverity.Error)
    {
        var error = new ApplicationError("BIZ_LOGIC", message, ErrorCategory.BusinessLogic, severity)
        {
            UserFriendlyMessage = message
        };

        if (context != null)
        {
            foreach (var kvp in context)
            {
                error.Context[kvp.Key] = kvp.Value;
            }
        }

        AddRecoveryActions(error);
        return error;
    }

    public ApplicationError CreateConfigurationError(string setting, string issue, string suggestion)
    {
        var error = new ApplicationError("CFG_INVALID", $"Configuration issue with '{setting}': {issue}", ErrorCategory.Configuration, ErrorSeverity.Critical)
        {
            UserFriendlyMessage = $"Configuration problem: {issue}",
            Context = { ["Setting"] = setting, ["Issue"] = issue }
        };

        error.RecoveryActions.Add(suggestion);
        error.RecoveryActions.Add("Check the appsettings.json file for correct values");
        error.RecoveryActions.Add("Refer to the README.md for configuration examples");

        return error;
    }

    public ApplicationError CreateValidationError(string parameterName, string providedValue, string expectedFormat)
    {
        var error = new ApplicationError("INPUT_INVALID", $"Invalid value for parameter '{parameterName}'", ErrorCategory.UserInput, ErrorSeverity.Error)
        {
            UserFriendlyMessage = $"Invalid {parameterName}: expected {expectedFormat}, but got '{providedValue}'",
            Context =
            {
                ["Parameter"] = parameterName,
                ["ProvidedValue"] = providedValue,
                ["ExpectedFormat"] = expectedFormat
            }
        };

        error.RecoveryActions.Add($"Provide a valid {parameterName} in the format: {expectedFormat}");
        error.RecoveryActions.Add("Use --help to see usage examples");

        return error;
    }

    public void LogAndDisplayError(ApplicationError error)
    {
        // Log the error with appropriate level
        var logLevel = error.Severity switch
        {
            ErrorSeverity.Info => LogLevel.Information,
            ErrorSeverity.Warning => LogLevel.Warning,
            ErrorSeverity.Error => LogLevel.Error,
            ErrorSeverity.Critical => LogLevel.Critical,
            _ => LogLevel.Error
        };

        _logger.Log(logLevel, error.InnerException,
            "Error {ErrorCode}: {Message} (Category: {Category})",
            error.Code, error.Message, error.Category);

        // Display user-friendly error to console
        DisplayErrorToUser(error);
    }

    public bool IsRecoverable(ApplicationError error)
    {
        return error.Severity != ErrorSeverity.Critical &&
               error.Category != ErrorCategory.Authentication &&
               error.Category != ErrorCategory.Configuration;
    }

    private void AddRecoveryActions(ApplicationError error)
    {
        switch (error.Category)
        {
            case ErrorCategory.Network:
                error.RecoveryActions.Add("Check your internet connection");
                error.RecoveryActions.Add("Verify Azure DevOps service status");
                error.RecoveryActions.Add("Try again in a few minutes");
                break;

            case ErrorCategory.Authentication:
                error.RecoveryActions.Add("Verify your Personal Access Token is valid and not expired");
                error.RecoveryActions.Add("Check that the token has sufficient permissions");
                error.RecoveryActions.Add("Update the token in appsettings.json");
                break;

            case ErrorCategory.Configuration:
                error.RecoveryActions.Add("Review appsettings.json for missing or invalid values");
                error.RecoveryActions.Add("Ensure all required settings are provided");
                error.RecoveryActions.Add("Check the README.md for configuration examples");
                break;

            case ErrorCategory.UserInput:
                error.RecoveryActions.Add("Check your command line arguments");
                error.RecoveryActions.Add("Use --help to see valid options and examples");
                break;

            case ErrorCategory.FileSystem:
                error.RecoveryActions.Add("Check file and directory permissions");
                error.RecoveryActions.Add("Ensure sufficient disk space is available");
                error.RecoveryActions.Add("Verify the file path exists and is accessible");
                break;
        }
    }

    private void DisplayErrorToUser(ApplicationError error)
    {
        var severityIcon = error.Severity switch
        {
            ErrorSeverity.Info => "ℹ️",
            ErrorSeverity.Warning => "⚠️",
            ErrorSeverity.Error => "❌",
            ErrorSeverity.Critical => "🚨",
            _ => "❌"
        };

        Console.WriteLine();
        Console.WriteLine($"{severityIcon} {error.Severity.ToString().ToUpper()}: {error.UserFriendlyMessage}");

        if (!string.IsNullOrEmpty(error.Code))
        {
            Console.WriteLine($"   Error Code: {error.Code}");
        }

        if (error.Context.Any())
        {
            Console.WriteLine("   Context:");
            foreach (var kvp in error.Context)
            {
                Console.WriteLine($"     • {kvp.Key}: {kvp.Value}");
            }
        }

        if (error.RecoveryActions.Any())
        {
            Console.WriteLine("   Suggested actions:");
            foreach (var action in error.RecoveryActions)
            {
                Console.WriteLine($"     • {action}");
            }
        }

        // Show technical details for developers in verbose scenarios
        if (error.Severity == ErrorSeverity.Critical && !string.IsNullOrEmpty(error.TechnicalDetails))
        {
            Console.WriteLine("   Technical Details:");
            Console.WriteLine($"     {error.TechnicalDetails.Split('\n').FirstOrDefault()}");
        }

        Console.WriteLine();
    }
}
