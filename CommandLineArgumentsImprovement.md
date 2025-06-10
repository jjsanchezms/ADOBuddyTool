# Command Line Arguments Improvement Plan

## Current Issues Analysis

### 1. **Error Handling & Validation**
- ❌ No input validation (negative numbers, invalid formats)
- ❌ Silent failures when parsing fails
- ❌ No descriptive error messages
- ❌ No validation against business rules

### 2. **Parameter Consistency**
- ❌ Inconsistent naming patterns (`--hygiene-checks` vs `--summary-only`)
- ❌ Missing short forms for some parameters
- ❌ No clear convention for boolean vs value parameters

### 3. **Missing Essential Features**
- ❌ No configuration file support via CLI
- ❌ No verbose/quiet logging options
- ❌ No dry-run mode
- ❌ No environment variable support
- ❌ No parameter validation feedback

### 4. **User Experience Issues**
- ❌ Help text could be more comprehensive
- ❌ No parameter auto-completion
- ❌ No progressive disclosure of advanced options

## Recommended Improvements

### 1. **Implement System.CommandLine Library**

**Benefits:**
- Automatic help generation
- Built-in validation
- Type-safe parsing
- Tab completion support
- Consistent error messages
- Subcommand support

**Implementation:**
```xml
<PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
```

### 2. **Enhanced Parameter Structure**

```csharp
// Proposed new parameter structure
public class CommandLineOptions
{
    // Core parameters
    public string AreaPath { get; set; } = string.Empty;
    public int Limit { get; set; } = 100;
    
    // Output options
    public OutputFormat Format { get; set; } = OutputFormat.Console;
    public string? OutputFile { get; set; }
    public string? OutputDirectory { get; set; }
    
    // Operation modes
    public bool HygieneChecks { get; set; } = false;
    public bool HygieneOnly { get; set; } = false;
    public bool DryRun { get; set; } = false;
    
    // Configuration
    public string? ConfigFile { get; set; }
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
    public int TimeoutSeconds { get; set; } = 30;
    
    // Filters and advanced options
    public string[]? WorkItemTypes { get; set; }
    public string[]? WorkItemStates { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public enum OutputFormat
{
    Console,
    Json,
    Csv,
    Summary,
    Excel  // Future enhancement
}
```

### 3. **Improved Parameter Names & Conventions**

```bash
# Core parameters (consistent short forms)
--area-path, -a           # Azure DevOps area path (required)
--limit, -l              # Max work items to retrieve
--config, -c             # Configuration file path

# Output options (grouped logically)
--output, -o             # Output format: console|json|csv|summary
--file, -f               # Output file path
--output-dir, -d         # Output directory

# Operation modes (clear boolean flags)
--hygiene                # Run hygiene checks with roadmap
--hygiene-only           # Run only hygiene checks (skip roadmap)
--dry-run                # Preview mode, no actual changes
--summary                # Summary output mode

# Configuration & behavior
--verbose, -v            # Verbose logging
--quiet, -q              # Quiet mode (errors only)
--timeout, -t            # API timeout in seconds
--log-level              # Logging level: trace|debug|info|warn|error

# Advanced filters
--work-item-types        # Filter by work item types
--work-item-states       # Filter by work item states  
--from-date              # Filter items modified after date
--to-date                # Filter items modified before date
```

### 4. **Enhanced Validation Rules**

```csharp
// Validation improvements needed:
- AreaPath: Required, non-empty, valid path format
- Limit: 1-10000 range with warning for large values
- OutputFile: Valid file path, writable directory
- TimeoutSeconds: 5-300 seconds range
- Dates: Valid format, logical date ranges
- ConfigFile: Must exist and be readable
```

### 5. **Configuration File Integration**

```json
// appsettings.json integration
{
  "App": {
    "DefaultAreaPath": "SPOOL\\Resource Provider",
    "DefaultLimit": 100,
    "DefaultOutputFormat": "console",
    "DefaultTimeoutSeconds": 30,
    "MaxWorkItems": 1000
  },
  "CommandLineDefaults": {
    "HygieneChecks": false,
    "LogLevel": "Information",
    "OutputDirectory": "output"
  }
}
```

### 6. **Subcommand Structure (Future Enhancement)**

```bash
# Organize functionality into subcommands
CreateRoadmapADO roadmap --area-path "..." [options]
CreateRoadmapADO hygiene --area-path "..." [options]
CreateRoadmapADO config --show|--set key=value
CreateRoadmapADO export --format json --area-path "..." [options]
```

## Implementation Priority

### Phase 1: Critical Fixes (High Priority)
1. ✅ Add comprehensive input validation
2. ✅ Implement proper error handling with descriptive messages
3. ✅ Standardize parameter naming conventions
4. ✅ Add missing essential parameters (--verbose, --dry-run, --timeout)

### Phase 2: Enhanced User Experience (Medium Priority)
1. ✅ Implement System.CommandLine library
2. ✅ Add configuration file parameter support
3. ✅ Improve help text and examples
4. ✅ Add environment variable support

### Phase 3: Advanced Features (Low Priority)
1. ✅ Add subcommand structure
2. ✅ Implement auto-completion
3. ✅ Add advanced filtering options
4. ✅ Progressive disclosure for advanced users

## Benefits of Improvements

### For Users:
- **Better Error Messages**: Clear feedback when something goes wrong
- **Consistent Interface**: Predictable parameter naming and behavior
- **Enhanced Productivity**: Dry-run mode, verbose logging, better defaults
- **Flexibility**: Configuration files, environment variables, advanced filters

### For Developers:
- **Type Safety**: Strongly-typed parameter parsing
- **Maintainability**: Clear separation of concerns, easier to extend
- **Testing**: Easier to unit test parameter parsing logic
- **Documentation**: Auto-generated help and validation messages

### For Operations:
- **Automation Friendly**: Environment variables, configuration files
- **Monitoring**: Better logging levels and diagnostic information
- **Error Handling**: Proper exit codes and error reporting
