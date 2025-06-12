namespace ADOBuddyTool.Presentation.Configuration;

/// <summary>
/// Handles displaying help information to users
/// </summary>
public static class HelpDisplay
{
    public static void ShowHelp()
    {
        ShowHeader();
        ShowUsage();
        ShowRequiredOptions();
        ShowOperations();
        ShowOptionalParameters();
        ShowExamples();
        ShowSwagDetails();
    }

    private static void ShowHeader()
    {
        Console.WriteLine("ADOBuddyTool - Generate roadmaps from Azure DevOps Feature work items");
        Console.WriteLine();
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage: ADOBuddyTool --area-path <path> (--ado-hygiene | --roadmap | --swag-auto-generated) [options]");
        Console.WriteLine();
    }

    private static void ShowRequiredOptions()
    {
        Console.WriteLine("Required:");
        Console.WriteLine("  -a, --area-path <path>    Azure DevOps area path to filter work items (e.g., \"SPOOL\\\\Resource Provider\")");
        Console.WriteLine();
    }

    private static void ShowOperations()
    {
        Console.WriteLine("Operations (at least one required):");
        Console.WriteLine("  --ado-hygiene             Run ADO hygiene checks on Release Trains and Features");
        Console.WriteLine("  --roadmap                 Generate roadmap and create Release Train work items from patterns");
        Console.WriteLine("  --swag-auto-generated     Review Release Trains and manage SWAG calculations (auto-generated only)");
        Console.WriteLine("  --swag-all                Update SWAG for ALL Release Trains (auto-generated and manual)");
        Console.WriteLine();
    }

    private static void ShowOptionalParameters()
    {
        Console.WriteLine("Options:");
        Console.WriteLine("  -l, --limit <number>      Maximum number of work items to retrieve (default: 100)");
        Console.WriteLine("  -v, --verbose             Enable verbose output (detailed logging and progress information)");
        Console.WriteLine("  -q, --quiet               Enable quiet mode (minimal output, errors only)");
        Console.WriteLine("  -h, --help                Show this help message");
        Console.WriteLine();
    }

    private static void ShowExamples()
    {
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Create roadmap only");
        Console.WriteLine("  dotnet run --area-path \"SPOOL\\\\Resource Provider\" --roadmap");
        Console.WriteLine();

        Console.WriteLine("  # Run hygiene checks only");
        Console.WriteLine("  dotnet run --area-path \"SPOOL\\\\Resource Provider\" --ado-hygiene");
        Console.WriteLine();

        Console.WriteLine("  # Update SWAG values for Release Trains (auto-generated only)");
        Console.WriteLine("  dotnet run --area-path \"SPOOL\\\\Resource Provider\" --swag-auto-generated");
        Console.WriteLine();

        Console.WriteLine("  # Update SWAG values for ALL Release Trains");
        Console.WriteLine("  dotnet run --area-path \"SPOOL\\\\Resource Provider\" --swag-all");
        Console.WriteLine();

        Console.WriteLine("  # Run multiple operations in quiet mode");
        Console.WriteLine("  dotnet run --area-path \"SPOOL\\\\Resource Provider\" --roadmap --ado-hygiene --quiet");
        Console.WriteLine();

        Console.WriteLine("  # Process more items with verbose output");
        Console.WriteLine("  dotnet run --area-path \"MyProject\\\\MyTeam\" --roadmap --limit 200 --verbose");
        Console.WriteLine();
    }

    private static void ShowSwagDetails()
    {
        Console.WriteLine("SWAG Updates Operation:");
        Console.WriteLine("  • Normal mode (--swag-auto-generated): Only updates auto-generated Release Trains, shows warnings for manual ones");
        Console.WriteLine("  • ALL mode (--swag-all): Updates ALL Release Trains regardless of auto-generated tag");
        Console.WriteLine("  • For manual Release Trains (normal mode): Shows warnings if SWAG doesn't match Feature sum");
        Console.WriteLine("  • Only processes Release Trains with related Feature work items");
        Console.WriteLine("  • SWAG values are stored as [SWAG: value] prefix in the status notes field");
    }
}
