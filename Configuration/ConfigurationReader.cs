using Microsoft.Extensions.Configuration;

namespace CreateRoadmapADO.Configuration;

/// <summary>
/// Simple configuration reader that loads settings from appsettings.json
/// </summary>
public static class ConfigurationReader
{
    private static IConfiguration? _configuration;

    /// <summary>
    /// Gets the configuration instance, creating it if needed
    /// </summary>
    /// <returns>Configuration instance</returns>
    public static IConfiguration GetConfiguration()
    {
        return _configuration ??= CreateConfiguration();
    }

    /// <summary>
    /// Creates a new configuration instance
    /// </summary>
    /// <returns>New configuration instance</returns>
    private static IConfiguration CreateConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        return builder.Build();
    }

    /// <summary>
    /// Gets Azure DevOps configuration options
    /// </summary>
    /// <returns>Azure DevOps options</returns>
    public static AzureDevOpsOptions GetAzureDevOpsOptions()
    {
        var config = GetConfiguration();
        var section = config.GetSection(AzureDevOpsOptions.SectionName);

        return new AzureDevOpsOptions
        {
            Organization = section["Organization"] ?? string.Empty,
            Project = section["Project"] ?? string.Empty,
            PersonalAccessToken = section["PersonalAccessToken"] ?? string.Empty
        };
    }

    /// <summary>
    /// Gets application configuration options
    /// </summary>
    /// <returns>Application options</returns>
    public static AppOptions GetAppOptions()
    {
        var config = GetConfiguration();
        var section = config.GetSection("App");

        return new AppOptions
        {
            MaxWorkItems = TryParseInt(section["MaxWorkItems"], 1000),
            DefaultOutputFormat = section["DefaultOutputFormat"] ?? "console",
            OutputDirectory = section["OutputDirectory"] ?? "output",
            HttpTimeoutSeconds = TryParseInt(section["HttpTimeoutSeconds"], 30)
        };
    }    /// <summary>
         /// Helper method to safely parse integer values with fallback
         /// </summary>
         /// <param name="value">String value to parse</param>
         /// <param name="defaultValue">Default value if parsing fails</param>
         /// <returns>Parsed integer or default value</returns>
    private static int TryParseInt(string? value, int defaultValue)
    {
        return int.TryParse(value, out var result) ? result : defaultValue;
    }
}
