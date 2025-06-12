namespace ADOBuddyTool.Presentation.Configuration;

/// <summary>
/// Application settings and options
/// </summary>
public class AppOptions
{
    /// <summary>
    /// Maximum number of Feature work items to retrieve
    /// </summary>
    public int MaxWorkItems { get; set; } = 1000;

    /// <summary>
    /// Default output format (json, csv, console)
    /// </summary>
    public string DefaultOutputFormat { get; set; } = "console";

    /// <summary>
    /// Default output directory for files
    /// </summary>
    public string OutputDirectory { get; set; } = "output";

    /// <summary>
    /// Timeout for HTTP requests in seconds
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 30;
}
