# CreateRoadmapADO

A C# .NET 8 console application that generates roadmaps from Azure DevOps Feature work items following SOLID principles.

## Overview

This application connects to Azure DevOps, retrieves Feature work items, and generates roadmaps that can be exported to JSON, CSV, or displayed in the console.

## Architecture

The application follows SOLID principles with a clean architecture:

- **Models/**: Domain models (WorkItem, RoadmapItem)
- **Interfaces/**: Service contracts (IAzureDevOpsService, IRoadmapService, IOutputService)
- **Services/**: Service implementations
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
2. Create a new token with "Work Items (Read)" permissions
3. Copy the token to your configuration

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
```

### Options

- `-l, --limit <number>`: Maximum number of Feature work items to retrieve (default: 100)
- `-o, --output <format>`: Output format: console, json, csv (default: console)
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
dotnet run -- --type Feature --output json
```

## Features

- **Azure DevOps Integration**: Connects to Azure DevOps REST API
- **Flexible Work Item Queries**: Supports different work item types
- **Multiple Output Formats**: Console display, JSON export, CSV export
- **Roadmap Generation**: Converts work items to roadmap items with timeline estimation
- **Dependency Injection**: Uses Microsoft.Extensions.DependencyInjection
- **Configuration Management**: Uses Microsoft.Extensions.Configuration
- **Logging**: Comprehensive logging with Microsoft.Extensions.Logging

## Sample Output

### Console Output
```
=== ROADMAP ===

ID: 12345
Title: User Authentication Feature
Type: Feature
Status: InProgress
Assigned To: john.doe@company.com
Priority: 1
Start Date: 2024-01-15
End Date: 2024-02-15
Tags: Feature, Security
Description: Work Item: Feature - User Authentication Feature
```

### JSON Output
```json
[
  {
    "id": 12345,
    "title": "User Authentication Feature",
    "description": "Work Item: Feature - User Authentication Feature",
    "type": "Feature",
    "status": "InProgress",
    "assignedTo": "john.doe@company.com",
    "startDate": "2024-01-15T00:00:00",
    "endDate": "2024-02-15T00:00:00",
    "priority": 1,
    "dependencies": [],
    "tags": ["Feature", "Security"]
  }
]
```

## Error Handling

The application includes comprehensive error handling:
- Configuration validation
- HTTP request timeouts
- API error responses
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

This application follows SOLID principles:
- **S**ingle Responsibility: Each service has a single, well-defined purpose
- **O**pen/Closed: Services are open for extension but closed for modification
- **L**iskov Substitution: Implementations can be substituted via interfaces
- **I**nterface Segregation: Small, focused interfaces
- **D**ependency Inversion: Depends on abstractions, not concretions
