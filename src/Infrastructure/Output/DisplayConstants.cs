namespace ADOBuddyTool.Infrastructure.Output;

/// <summary>
/// Constants used for console display formatting
/// </summary>
public static class DisplayConstants
{
    // Table layout constants
    public const int IdColumnWidth = 5;
    public const int StackRankColumnWidth = 12;
    public const int TypeColumnWidth = 10;
    public const int StatusColumnWidth = 12;
    public const int TitleColumnWidth = 40;

    // Content limits
    public const int TitleMaxLength = 40;
    public const int DescriptionMaxLength = 72;

    // Display formatting
    public const int TableWidth = 80;
    public const char SeparatorChar = '=';
    public const char MinorSeparatorChar = '-';
    public const string IndentSpaces = "      ";

    // Status indicators
    public const string MissingValueIndicator = "N/A (!)";
    public const string ZeroValueIndicator = "0.00 (!)";

    // Headers and labels
    public const string RoadmapHeader = "=== ROADMAP (Sorted by StackRank) ===";
    public const string NoItemsMessage = "No roadmap items found.";
    public const string SortingExplanation = "(Lower StackRank values appear first, items with N/A appear last)";
}
