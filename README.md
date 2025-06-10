# CreateRoadmapADO

A C# .NET 8 console application that generates roadmaps from Azure DevOps Feature work items and automatically creates Release Train work items following SOLID principles.

## Overview

This application connects to Azure DevOps, retrieves Feature work items, processes special title patterns to create Release Trains, and generates roadmaps that can be exported to JSON, CSV, or displayed in the console.

## Architecture

The application follows clean architecture principles with a simplified structure:

- **Models/**: Domain models (WorkItem, RoadmapItem, ReleaseTrainSummary)
- **Interfaces/**: Service contracts (IAzureDevOpsService for testability)
- **Services/**: Service implementations with Release Train creation logic
- **Configuration/**: Configuration classes (AzureDevOpsOptions, AppOptions)

## Configuration

Update `appsettings.json` with your Azure DevOps details:

```json
{
  "AzureDevOps": {
    "Organization": "your-organization",
    "Project": "your-project",
    "PersonalAccessToken": "your-pat-token"
  }
}
```

### Getting a Personal Access Token

1. Go to Azure DevOps > User Settings > Personal Access Tokens
2. Create a new token with "Work Items (Read & Write)" permissions
3. Copy the token to your configuration

## Release Train Creation

The application automatically processes Feature work items with special title patterns to create Release Train work items:

### Pattern Format
- Use titles like: `----- Release Train Name -----rt`
- To update an existing Release Train: `----- Release Train Name -----rt:12345` (where 12345 is the existing work item ID)
- End the group with: `------------`

### Example
```
Feature 1: ----- Q1 2024 Release -----rt
Feature 2: User Authentication
Feature 3: Payment Gateway
Feature 4: Reporting Dashboard
Feature 5: ------------
```

This will create a Release Train called "Q1 2024 Release" with Features 2, 3, and 4 as child items.

## Usage

### Command Line Options

```bash
# Display help
CreateRoadmapADO --help

# Default usage (Features to console)
CreateRoadmapADO

# Export to JSON with limit
CreateRoadmapADO --limit 50 --output json

# Export to CSV with custom filename
CreateRoadmapADO --limit 200 --output csv --file my-roadmap.csv

# Show only Release Train creation summary
CreateRoadmapADO --output summary
```

### Options

- `-l, --limit <number>`: Maximum number of Feature work items to retrieve (default: 100)
- `-o, --output <format>`: Output format: console, json, csv, summary (default: console)
- `-f, --file <path>`: Output file path (auto-generated if not specified)
- `-h, --help`: Show help message

## Building and Running

### Prerequisites

- .NET 8 SDK
- Azure DevOps access with a Personal Access Token

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run

# Or with arguments
dotnet run -- --limit 50 --output json
```

## Features

- **Azure DevOps Integration**: Connects to Azure DevOps REST API
- **Release Train Creation**: Automatically creates Release Train work items from special title patterns
- **Pattern Processing**: Recognizes title patterns like `----- Title -----rt` to group features into release trains
- **Multiple Output Formats**: Console display, JSON export, CSV export, summary-only mode
- **Roadmap Generation**: Converts work items to roadmap items with timeline estimation
- **Operation Tracking**: Provides detailed summary of Release Train creation and update operations
- **Simplified Architecture**: Clean separation of concerns without over-engineering
- **Configuration Management**: Uses Microsoft.Extensions.Configuration
- **Logging**: Comprehensive logging with Microsoft.Extensions.Logging

## Sample Output

### Release Train Summary Output
```
============================================================
RELEASE TRAIN SUMMARY
============================================================
✅ Backlog read successfully (150 items processed)

🆕 CREATED (2):
   • Release Train #54321: "Q1 2024 Release" (15 work items)
   • Release Train #54322: "Security Improvements" (8 work items)

🔄 UPDATED (1):
   • Release Train #54320: "Performance Optimization" (12 total work items, +3 new relations)

============================================================
```

### Console Output
```
Total Items: 45
Not Started: 12
In Progress: 18
Completed: 10
Blocked: 3
Cancelled: 2
```

### JSON Output
```json
[  {
    "id": 12345,
    "title": "User Authentication Feature",
    "description": "Work Item: Feature - User Authentication Feature",
    "type": "Feature",
    "status": "InProgress",
    "assignedTo": "john.doe@company.com",
    "startDate": "2024-01-15T00:00:00",
    "endDate": "2024-02-15T00:00:00",
    "priority": 1,
    "stackRank": 100.5,
    "dependencies": [],
    "tags": ["Feature", "Security"]
  }
]
```

## Error Handling

The application includes comprehensive error handling:
- Configuration validation
- HTTP request timeouts
- API error responses for both reading and creating work items
- Release Train creation failures with rollback support
- File I/O errors
- Logging of all errors and warnings

## Dependencies

- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Hosting
- Microsoft.Extensions.Logging
- Microsoft.Extensions.Http
- System.Text.Json

## Contributing

This application follows clean architecture principles:
- **Separation of Concerns**: Each service has a focused responsibility
- **Maintainability**: Simple, straightforward code structure
- **Testability**: Core Azure DevOps service remains interfaced for testing
- **Simplicity**: Avoids over-engineering while maintaining code quality
