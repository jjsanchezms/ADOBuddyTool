using System.Net;

namespace ADOBuddyTool.Application.ErrorHandling;

/// <summary>
/// Represents different categories of application errors
/// </summary>
public enum ErrorCategory
{
    /// <summary>
    /// Configuration-related errors (missing settings, invalid values)
    /// </summary>
    Configuration,

    /// <summary>
    /// Network connectivity or Azure DevOps API errors
    /// </summary>
    Network,

    /// <summary>
    /// Authentication or authorization failures
    /// </summary>
    Authentication,

    /// <summary>
    /// Invalid user input or command line arguments
    /// </summary>
    UserInput,

    /// <summary>
    /// Business logic or validation errors
    /// </summary>
    BusinessLogic,

    /// <summary>
    /// File system or I/O related errors
    /// </summary>
    FileSystem,

    /// <summary>
    /// Unexpected system or application errors
    /// </summary>
    System
}

/// <summary>
/// Defines the severity level of an error
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// Informational message
    /// </summary>
    Info,

    /// <summary>
    /// Warning that doesn't prevent execution
    /// </summary>
    Warning,

    /// <summary>
    /// Error that stops current operation but allows others to continue
    /// </summary>
    Error,

    /// <summary>
    /// Critical error that stops the entire application
    /// </summary>
    Critical
}

/// <summary>
/// Represents a structured application error with context and recovery information
/// </summary>
public class ApplicationError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string UserFriendlyMessage { get; set; } = string.Empty;
    public ErrorCategory Category { get; set; }
    public ErrorSeverity Severity { get; set; }
    public Exception? InnerException { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
    public List<string> RecoveryActions { get; set; } = new();
    public string TechnicalDetails { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new ApplicationError
    /// </summary>
    public ApplicationError(string code, string message, ErrorCategory category, ErrorSeverity severity = ErrorSeverity.Error)
    {
        Code = code;
        Message = message;
        UserFriendlyMessage = message;
        Category = category;
        Severity = severity;
    }

    /// <summary>
    /// Creates an ApplicationError from an exception
    /// </summary>
    public static ApplicationError FromException(Exception ex, string? userFriendlyMessage = null)
    {
        var category = DetermineErrorCategory(ex);
        var severity = DetermineSeverity(ex);
        var code = GenerateErrorCode(ex, category);

        return new ApplicationError(code, ex.Message, category, severity)
        {
            UserFriendlyMessage = userFriendlyMessage ?? GenerateUserFriendlyMessage(ex, category),
            InnerException = ex,
            TechnicalDetails = ex.ToString()
        };
    }    /// <summary>
         /// Determines the error category based on the exception type
         /// </summary>
    private static ErrorCategory DetermineErrorCategory(Exception ex)
    {
        return ex switch
        {
            HttpRequestException => ErrorCategory.Network,
            UnauthorizedAccessException => ErrorCategory.Authentication,
            ArgumentNullException => ErrorCategory.UserInput,
            ArgumentException => ErrorCategory.UserInput,
            DirectoryNotFoundException => ErrorCategory.FileSystem,
            FileNotFoundException => ErrorCategory.FileSystem,
            IOException => ErrorCategory.FileSystem,
            InvalidOperationException when ex.Message.Contains("configuration") => ErrorCategory.Configuration,
            _ => ErrorCategory.System
        };
    }/// <summary>
     /// Determines the severity level based on the exception type
     /// </summary>
    private static ErrorSeverity DetermineSeverity(Exception ex)
    {
        return ex switch
        {
            HttpRequestException httpEx when httpEx.Message.Contains("401") => ErrorSeverity.Critical,
            HttpRequestException httpEx when httpEx.Message.Contains("403") => ErrorSeverity.Critical,
            HttpRequestException => ErrorSeverity.Error,
            UnauthorizedAccessException => ErrorSeverity.Critical,
            ArgumentNullException => ErrorSeverity.Error,
            ArgumentException => ErrorSeverity.Error,
            InvalidOperationException => ErrorSeverity.Error,
            FileNotFoundException => ErrorSeverity.Error,
            DirectoryNotFoundException => ErrorSeverity.Error,
            _ => ErrorSeverity.Critical
        };
    }

    /// <summary>
    /// Generates a user-friendly error code
    /// </summary>
    private static string GenerateErrorCode(Exception ex, ErrorCategory category)
    {
        var categoryPrefix = category switch
        {
            ErrorCategory.Configuration => "CFG",
            ErrorCategory.Network => "NET",
            ErrorCategory.Authentication => "AUTH",
            ErrorCategory.UserInput => "INPUT",
            ErrorCategory.BusinessLogic => "BIZ",
            ErrorCategory.FileSystem => "FILE",
            _ => "SYS"
        };

        var typeCode = ex.GetType().Name.Replace("Exception", "");
        return $"{categoryPrefix}_{typeCode}_{GetHashCode(ex.Message):X4}";
    }

    /// <summary>
    /// Generates a user-friendly error message
    /// </summary>
    private static string GenerateUserFriendlyMessage(Exception ex, ErrorCategory category)
    {
        return category switch
        {
            ErrorCategory.Network => "Unable to connect to Azure DevOps. Please check your network connection and try again.",
            ErrorCategory.Authentication => "Authentication failed. Please verify your Personal Access Token in the configuration.",
            ErrorCategory.Configuration => "Configuration error. Please check your settings in appsettings.json.",
            ErrorCategory.UserInput => "Invalid input provided. Please check your command line arguments.",
            ErrorCategory.FileSystem => "File system error occurred. Please check file permissions and available disk space.",
            _ => "An unexpected error occurred. Please try again or contact support if the problem persists."
        };
    }

    private static int GetHashCode(string input)
    {
        return input?.GetHashCode() ?? 0;
    }
}
