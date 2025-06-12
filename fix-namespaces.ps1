# Comprehensive namespace fix script
$patterns = @{
    # Domain entities (Models -> Domain.Entities)
    "using CreateRoadmapADO\.Models;"                  = "using CreateRoadmapADO.Domain.Entities;"
    
    # Configuration (Configuration -> Presentation.Configuration)
    "using CreateRoadmapADO\.Configuration;"           = "using CreateRoadmapADO.Presentation.Configuration;"
    
    # Error Handling (ErrorHandling -> Application.ErrorHandling)
    "using CreateRoadmapADO\.ErrorHandling;"           = "using CreateRoadmapADO.Application.ErrorHandling;"
    
    # Azure DevOps Services
    "using CreateRoadmapADO\.Services\.AzureDevOps;"   = "using CreateRoadmapADO.Infrastructure.AzureDevOps.Services;"
    
    # Interfaces
    "using CreateRoadmapADO\.Interfaces;"              = "using CreateRoadmapADO.Infrastructure.AzureDevOps.Interfaces;"
    
    # Services (general)
    "using CreateRoadmapADO\.Services;"                = "using CreateRoadmapADO.Presentation.DependencyInjection;"
    
    # Commands
    "using CreateRoadmapADO\.Commands;"                = "using CreateRoadmapADO.Application.Commands;"
    
    # Hygiene Checks
    "using CreateRoadmapADO\.Services\.HygieneChecks;" = "using CreateRoadmapADO.Infrastructure.HygieneChecks.Checks;"
}

# Get all C# files in src directory
$files = Get-ChildItem "src" -Recurse -Filter "*.cs"

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    
    # Apply all pattern replacements
    foreach ($pattern in $patterns.Keys) {
        $replacement = $patterns[$pattern]
        $content = $content -replace $pattern, $replacement
    }
    
    # Only write if content changed
    if ($content -ne $originalContent) {
        Set-Content $file.FullName $content
        Write-Host "Updated: $($file.FullName)"
    }
}

Write-Host "Namespace fix completed!"
