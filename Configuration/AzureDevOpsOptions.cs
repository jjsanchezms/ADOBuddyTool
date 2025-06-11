namespace CreateRoadmapADO.Configuration;

/// <summary>
/// Configuration settings for Azure DevOps connection
/// </summary>
public class AzureDevOpsOptions
{
    public const string SectionName = "AzureDevOps";

    /// <summary>
    /// Azure DevOps organization name
    /// </summary>
    public string Organization { get; set; } = string.Empty;

    /// <summary>
    /// Azure DevOps project name
    /// </summary>
    public string Project { get; set; } = string.Empty;

    /// <summary>
    /// Personal Access Token for authentication
    /// </summary>
    public string PersonalAccessToken { get; set; } = string.Empty;    /// <summary>
                                                                       /// Base URL for Azure DevOps API
                                                                       /// </summary>
    public string BaseUrl => $"https://{Organization}.visualstudio.com";

    /// <summary>
    /// Validates the configuration
    /// </summary>
    /// <returns>True if configuration is valid</returns>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Organization) &&
               !string.IsNullOrWhiteSpace(Project) &&
               !string.IsNullOrWhiteSpace(PersonalAccessToken);
    }
}
