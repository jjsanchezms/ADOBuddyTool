using CreateRoadmapADO.Configuration;

namespace CreateRoadmapADO.Helpers;

/// <summary>
/// Helper class for Azure DevOps authentication and token management
/// </summary>
public static class AuthenticationHelper
{
    /// <summary>
    /// Displays helpful instructions for fixing authentication issues
    /// </summary>
    public static void DisplayTokenInstructions()
    {
        var config = ConfigurationReader.GetAzureDevOpsOptions();

        Console.WriteLine();
        Console.WriteLine("🔧 HOW TO FIX YOUR PERSONAL ACCESS TOKEN");
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine();
        Console.WriteLine("Step 1: Generate a new Personal Access Token");
        Console.WriteLine($"   → Visit: https://dev.azure.com/{config.Organization}/_usersSettings/tokens");
        Console.WriteLine("   → Click 'New Token'");
        Console.WriteLine("   → Set expiration date (30, 60, or 90 days)");
        Console.WriteLine("   → Under 'Scopes', select 'Work Items (Read & Write)'");
        Console.WriteLine("   → Click 'Create'");
        Console.WriteLine();
        Console.WriteLine("Step 2: Update your configuration");
        Console.WriteLine("   → Copy the generated token");
        Console.WriteLine("   → Open appsettings.json in your project");
        Console.WriteLine("   → Replace the 'PersonalAccessToken' value with your new token");
        Console.WriteLine();
        Console.WriteLine("Step 3: Test the connection");
        Console.WriteLine("   → Run your application again");
        Console.WriteLine();
        Console.WriteLine("💡 Pro Tips:");
        Console.WriteLine("   → Tokens expire! Set a calendar reminder before expiration");
        Console.WriteLine("   → Use the shortest expiration time that works for your needs");
        Console.WriteLine("   → Never commit tokens to source control");
        Console.WriteLine();
    }

    /// <summary>
    /// Checks if a token appears to be expired based on common error patterns
    /// </summary>
    /// <param name="errorMessage">The error message from Azure DevOps API</param>
    /// <returns>True if the error suggests an expired token</returns>
    public static bool IsTokenExpiredError(string errorMessage)
    {
        return errorMessage.Contains("expired", StringComparison.OrdinalIgnoreCase) ||
               errorMessage.Contains("Access Denied", StringComparison.OrdinalIgnoreCase) ||
               errorMessage.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
               errorMessage.Contains("401", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates basic token format (not comprehensive, just basic checks)
    /// </summary>
    /// <param name="token">The token to validate</param>
    /// <returns>True if token appears to have valid format</returns>
    public static bool IsTokenFormatValid(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        // Azure DevOps PATs are typically 52 characters long and alphanumeric
        return token.Length >= 40 && token.All(char.IsLetterOrDigit);
    }
}
