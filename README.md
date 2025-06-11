# CreateRoadmapADO

A C# .NET 8 console application that generates roadmaps from Azure DevOps Feature work items and automatically creates Release Train work items following SOLID principles.

## Overview

This application connects to Azure DevOps, retrieves Feature work items from a specified area path, processes special title patterns to create Release Trains, and generates roadmaps that can be exported to JSON, CSV, or displayed in the console.

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

### Automatic Error Recovery
**NEW FEATURE**: When a Feature references a non-existent Release Train ID (e.g., `----- GCCH -----rt:4160082` where Release Train #4160082 doesn't exist), the application will:

1. **Automatically create a new Release Train** instead of failing
2. **Update the Feature title** with the new Release Train ID (e.g., `----- GCCH -----rt:4170000`)
3. **Log the recovery operation** for transparency
4. **Continue processing** without interruption

This ensures data integrity and prevents broken references while maintaining the intended Release Train structure.

### Example
```
Feature 1: ----- Q1 2024 Release -----rt
Feature 2: User Authentication
Feature 3: Payment Gateway
Feature 4: Reporting Dashboard
Feature 5: ------------
```

This will create a Release Train called "Q1 2024 Release" with Features 2, 3, and 4 as child items.

### Error Recovery Example
```
Feature 1: ----- GCCH -----rt:4160082    # Non-existent Release Train ID
Feature 2: Data Migration
Feature 3: API Updates
Feature 4: ------------
```

**Result:**
- ❌ Release Train #4160082 doesn't exist
- 🔄 Creates new Release Train #4170000 instead
- ✅ Updates Feature 1 title to: `----- GCCH -----rt:4170000`
- ✅ Links Features 2 and 3 to the new Release Train

## Usage

### Command Line Options

```bash
# Display help
CreateRoadmapADO --help

# Basic roadmap generation (required area path parameter)
CreateRoadmapADO --area-path "SPOOL\\Resource Provider" --create-roadmap

# Run ADO hygiene checks
CreateRoadmapADO --area-path "SPOOL\\Resource Provider" --hygiene-checks

# Update SWAG values for Release Trains
CreateRoadmapADO --area-path "SPOOL\\Resource Provider" --swag-updates

# Combine multiple operations
CreateRoadmapADO --area-path "SPOOL\\Resource Provider" --create-roadmap --hygiene-checks

# Use summary mode for automation
CreateRoadmapADO --area-path "SPOOL\\Resource Provider" --swag-updates --summary

# Process more work items
CreateRoadmapADO --area-path "MyProject\\MyTeam" --swag-updates --limit 200
```

### Operations

- `--create-roadmap`: Generate roadmap and create Release Train work items from patterns
- `--hygiene-checks`: Run ADO hygiene checks on Release Trains and Features  
- `--swag-updates`: Review Release Trains and manage SWAG calculations
- `--summary`: Enable summary mode (reduced output for automation)

### SWAG Updates Operation

The `--swag-updates` operation provides intelligent SWAG (effort estimation) management for Release Trains:

#### For Auto-Generated Release Trains:
- **Automatically updates** the Release Train's SWAG value to match the sum of all related Features
- **Ensures consistency** between Release Train planning and actual Feature effort

#### For Manual Release Trains:
- **Validates** that the Release Train SWAG matches the sum of related Features
- **Shows warnings** when there are mismatches to help identify planning discrepancies
- **Preserves manual values** while providing visibility into inconsistencies

#### Example Output:
```
📊 Release Train #12345: 'Q1 2024 Platform Updates'
   Auto-generated: Yes
   Related Features: 5 (4 with SWAG, 1 without)
   Current RT SWAG: 8
   Calculated SWAG: 13
   🔄 Updating SWAG from 8 to 13

📊 Release Train #12346: 'Manual Planning Release'
   Auto-generated: No
   Related Features: 3 (3 with SWAG, 0 without)
   Current RT SWAG: 15
   Calculated SWAG: 12
   ⚠️  WARNING: Release Train SWAG (15) does not match sum of Features (12)
```

#### Benefits:
- **Automated effort tracking** for auto-generated Release Trains
- **Planning validation** for manual Release Trains
- **Visibility** into Features missing SWAG estimates
- **Data consistency** across Release Train hierarchies

### Options

- `-a, --area-path <path>`: **[REQUIRED]** Azure DevOps area path to filter work items (e.g., "SPOOL\\Resource Provider")
- `-l, --limit <number>`: Maximum number of work items to retrieve (default: 100)
- `-s, --summary`: Enable summary mode (reduced output for automation)
- `-h, --help`: Show help message

### Prerequisites

- .NET 8 SDK
- Azure DevOps access with a Personal Access Token

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run -- --area-path "SPOOL\\Resource Provider"

# Or with additional arguments
dotnet run -- --area-path "MyProject\\MyTeam" --limit 50 --output json
```

## Features

- **Azure DevOps Integration**: Connects to Azure DevOps REST API
- **Configurable Area Path**: Filter work items by specifying the Azure DevOps area path
- **Release Train Creation**: Automatically creates Release Train work items from special title patterns
- **Pattern Processing**: Recognizes title patterns like `----- Title -----rt` to group features into release trains
- **Automatic Error Recovery**: When Features reference non-existent Release Train IDs, automatically creates new Release Trains and updates Feature titles
- **SWAG Management**: Intelligent SWAG (effort estimation) calculation and validation for Release Trains
- **ADO Hygiene Checks**: Comprehensive assessment of Release Train and Feature work item health
- **Multiple Operations**: Support for roadmap generation, hygiene checks, and SWAG updates
- **Roadmap Generation**: Converts work items to roadmap items with timeline estimation
- **Operation Tracking**: Provides detailed summary of Release Train creation and update operations
- **Simplified Architecture**: Clean separation of concerns without over-engineering
- **Configuration Management**: Uses Microsoft.Extensions.Configuration
- **Logging**: Comprehensive logging with Microsoft.Extensions.Logging

## ADO Hygiene Checks

The application includes comprehensive hygiene checks to assess the quality and consistency of Azure DevOps work items:

### Hygiene Check Types

1. **Iteration Path Alignment**: Verifies that Release Train iteration paths align with related Feature work items
2. **Status Notes Currency**: Checks if Release Trains and Features have adequate descriptions and documentation
3. **Release Train Completeness**: Validates that Release Trains have sufficient related features and proper tagging
4. **Feature State Consistency**: Ensures Release Train states are consistent with the progress of related features

### Hygiene Check Severity Levels

- **🔴 Critical**: Issues that require immediate attention
- **🟠 Error**: Significant problems that should be addressed
- **🟡 Warning**: Recommendations for improvement
- **ℹ️ Info**: Informational status updates

### Hygiene Check Output

```
============================================================
RUNNING ADO HYGIENE CHECKS
============================================================

HYGIENE CHECK SUMMARY
============================================================
Total Checks: 24
Passed: 18 ✅
Failed: 6 ❌
Health Score: 75.0%
Error Issues: 2 🟠
Warning Issues: 4 🟡

FAILED CHECKS
------------------------------------------------------------
🟠 [ERROR] Iteration Path Alignment
   Work Item: #12345 - Q1 2024 Release
   Issue: Release Train iteration 'Q1 2024' does not match any feature iterations: Q2 2024, Q3 2024
   Recommendation: Consider aligning Release Train iteration path with related features or vice versa

🟡 [WARNING] Status Notes Currency
   Work Item: #12346 - User Authentication Feature
   Issue: Description present (15 characters)
   Recommendation: Consider adding detailed status notes or description to provide context and current status
```

## Sample Output

### Release Train Summary Output
```
============================================================
RELEASE TRAIN SUMMARY
============================================================
✅ Backlog read successfully (150 items processed)

🆕 CREATED (3):
   • Release Train #54321: "Q1 2024 Release" (15 work items)
   • Release Train #54322: "Security Improvements" (8 work items)
   • Release Train #54323: "GCCH" (5 work items) [Recovery: replaced non-existent #4160082]

🔄 UPDATED (1):
   • Release Train #54320: "Performance Optimization" (12 total work items, +3 new relations)

============================================================
```

### Console Output with Error Recovery
```
========================================================================================
❌ ERROR: Release Train #4160082 does not exist
🔄 RECOVERY: Creating new Release Train instead
✅ Created new Release Train #54323 instead of #4160082
🔄 Updating Feature title to reference new Release Train #54323
========================================================================================
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
- **Required Parameter Validation**: Ensures area path is provided before execution
- **Automatic Release Train Recovery**: When Features reference non-existent Release Train IDs, automatically creates new Release Trains and updates Feature titles
- **Data Integrity Protection**: Prevents broken references while maintaining workflow continuity
- Configuration validation
- HTTP request timeouts
- API error responses for both reading and creating work items
- Release Train creation failures with rollback support
- File I/O errors
- Logging of all errors, warnings, and recovery operations

## Dependencies

- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Configuration.Json
- Microsoft.Extensions.Logging
- Microsoft.Extensions.Logging.Console
- System.Text.Json

## Contributing

This application follows clean architecture principles:
- **Separation of Concerns**: Each service has a focused responsibility
- **Maintainability**: Simple, straightforward code structure
- **Testability**: Core Azure DevOps service remains interfaced for testing
- **Simplicity**: Avoids over-engineering while maintaining code quality

## Recent Improvements

### v2.0 - Simplified Architecture & Configurable Area Path
- **Removed Epic Functionality**: Simplified to focus only on Release Train creation
- **Interface Reduction**: Removed unnecessary interfaces (`IOutputService`, `IRoadmapService`) for simpler codebase
- **Configurable Area Path**: Added required `--area-path` parameter instead of hardcoded path
- **Simplified Dependency Injection**: Removed Microsoft.Extensions.Hosting dependency and implemented simple constructor injection
- **Enhanced Error Handling**: Added validation for required parameters with clear error messages
- **Updated Documentation**: Comprehensive help and usage examples with new parameter requirements

### v2.1 - Hygiene Check Features
- **ADO Hygiene Checks**: Added comprehensive hygiene assessment system for Release Trains and Features
- **Iteration Path Alignment**: Validates consistency between Release Train and Feature iteration paths
- **Status Documentation**: Checks for adequate descriptions and documentation coverage
- **Release Train Quality**: Validates feature count, tagging, and overall completeness
- **State Consistency**: Ensures Release Train states reflect actual Feature progress
- **Exportable Reports**: Export hygiene check results to JSON or CSV formats
- **Severity Classification**: Critical, Error, Warning, and Info level categorization
- **Flexible Output**: Multiple export formats with optional file specification
- **Better Error Handling**: Improved error messages and validation
- **Configurable Logging**: Context-aware logging levels based on output mode

### v2.2 - Automatic Error Recovery
- **Non-Existent Release Train Recovery**: Automatically creates new Release Trains when Features reference IDs that don't exist
- **Feature Title Auto-Update**: Updates Feature titles with new Release Train IDs after automatic recovery
- **Enhanced Logging**: Detailed logging of recovery operations and title updates
- **Data Integrity Protection**: Prevents broken references while maintaining intended Release Train structure
- **Seamless Operation**: Recovery happens transparently without interrupting the overall process
- **Error Recovery Tracking**: Operations summary includes information about created replacement Release Trains
