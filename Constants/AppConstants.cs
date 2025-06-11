namespace CreateRoadmapADO.Constants;

/// <summary>
/// Application constants for better readability and maintainability
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// Console formatting constants
    /// </summary>
    public static class Console
    {
        public const int SeparatorWidth = 60;
        public const string SuccessIcon = "✅";
        public const string ErrorIcon = "❌";
        public const string InfoIcon = "ℹ️";
        public const string CreatedIcon = "🆕";
        public const string UpdatedIcon = "🔄";
    }

    /// <summary>
    /// Severity icons for hygiene checks
    /// </summary>
    public static class SeverityIcons
    {
        public const string Critical = "🔴";
        public const string Error = "🟠";
        public const string Warning = "🟡";
        public const string Info = "ℹ️";
    }

    /// <summary>
    /// Work item type names
    /// </summary>
    public static class WorkItemTypes
    {
        public const string Feature = "Feature";
        public const string ReleaseTrain = "Release Train";
    }

    /// <summary>
    /// Application messages
    /// </summary>
    public static class Messages
    {
        public const string NoReleaseTrainPatternsFound = "No Release Train patterns found";
        public const string BacklogReadSuccessfully = "Backlog read successfully";
        public const string ErrorReadingBacklog = "Error reading backlog items";
    }
}
