using Microsoft.Extensions.Configuration;

namespace CreateRoadmapADO.Configuration;

/// <summary>
/// Simple configuration reader that loads settings from appsettings.json
/// </summary>
public static class ConfigurationReader
{
    private static IConfiguration? _configuration;

    public static IConfiguration GetConfiguration()
    {
        if (_configuration == null)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            _configuration = builder.Build();
        }

        return _configuration;
    }

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

    public static AppOptions GetAppOptions()
    {
        var config = GetConfiguration();
        var section = config.GetSection("App");
        
        return new AppOptions
        {
            MaxWorkItems = int.TryParse(section["MaxWorkItems"], out var max) ? max : 1000,
            DefaultOutputFormat = section["DefaultOutputFormat"] ?? "console",
            OutputDirectory = section["OutputDirectory"] ?? "output",
            HttpTimeoutSeconds = int.TryParse(section["HttpTimeoutSeconds"], out var timeout) ? timeout : 30
        };
    }
}
